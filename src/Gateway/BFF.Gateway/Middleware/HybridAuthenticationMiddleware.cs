using BFF.Gateway.Services;
using ERP.Contracts.Identity;
using System.Security.Claims;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Hybrid authentication middleware that supports both API keys and JWT tokens
/// </summary>
public class HybridAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HybridAuthenticationMiddleware> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private readonly IJwtValidationService _jwtValidationService;

    public HybridAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<HybridAuthenticationMiddleware> logger,
        IGrpcClientService grpcClientService,
        IJwtValidationService jwtValidationService)
    {
        _next = next;
        _logger = logger;
        _grpcClientService = grpcClientService;
        _jwtValidationService = jwtValidationService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for certain paths
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Try JWT authentication first
        var jwtResult = await TryJwtAuthentication(context);
        if (jwtResult.IsAuthenticated)
        {
            _logger.LogInformation("✅ Request authenticated via JWT token");
            await _next(context);
            return;
        }

        // Fall back to API key authentication
        var apiKeyResult = await TryApiKeyAuthentication(context);
        if (apiKeyResult.IsAuthenticated)
        {
            _logger.LogInformation("✅ Request authenticated via API key");
            await _next(context);
            return;
        }

        // No valid authentication found
        _logger.LogWarning("🚫 No valid authentication found for path: {Path}", context.Request.Path);
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Authentication required. Provide either a valid JWT token or API key.");
    }

    private async Task<AuthenticationResult> TryJwtAuthentication(HttpContext context)
    {
        try
        {
            // Check if JWT validation service is ready, try to load key if not
            if (!_jwtValidationService.IsPublicKeyLoaded)
            {
                _logger.LogDebug("JWT validation service not ready - attempting to load public key");
                await _jwtValidationService.UpdatePublicKeyAsync();

                if (!_jwtValidationService.IsPublicKeyLoaded)
                {
                    _logger.LogDebug("JWT validation service still not ready after retry");
                    return AuthenticationResult.Failed();
                }
            }

            // Extract JWT token from Authorization header
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            string? token = null;

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }

            // Also check for JWT token in X-JWT-Token header (for service-to-service communication)
            if (string.IsNullOrEmpty(token))
            {
                token = context.Request.Headers["X-JWT-Token"].FirstOrDefault();
            }

            if (string.IsNullOrEmpty(token))
            {
                return AuthenticationResult.Failed();
            }

            // Validate JWT token
            _logger.LogInformation("🔍 Attempting JWT authentication for path: {Path}", context.Request.Path);

            var principal = _jwtValidationService.ValidateToken(token);

            if (principal == null)
            {
                _logger.LogDebug("JWT token validation failed");
                return AuthenticationResult.Failed();
            }

            // Extract claims and add to request headers for downstream services
            var tokenType = principal.FindFirst("token_type")?.Value;
            
            if (tokenType == "service")
            {
                // Service token
                var serviceName = principal.FindFirst("service_name")?.Value;
                context.Request.Headers["X-Service-Name"] = serviceName ?? string.Empty;
                context.Request.Headers["X-Token-Type"] = "service";
                context.Request.Headers["X-Auth-Method"] = "JWT";
                
                _logger.LogInformation("✅ Service JWT token validated for service: {ServiceName}, path: {Path}", 
                    serviceName, context.Request.Path);
            }
            else if (tokenType == "user")
            {
                // User token
                var userId = principal.FindFirst("user_id")?.Value;
                var userName = principal.FindFirst("user_name")?.Value;
                context.Request.Headers["X-User-Id"] = userId ?? string.Empty;
                context.Request.Headers["X-User-Name"] = userName ?? string.Empty;
                context.Request.Headers["X-Token-Type"] = "user";
                context.Request.Headers["X-Auth-Method"] = "JWT";
                
                _logger.LogInformation("✅ User JWT token validated for user: {UserName} (ID: {UserId}), path: {Path}", 
                    userName, userId, context.Request.Path);
            }

            // Add permissions to headers
            var permissions = principal.FindAll("permission").Select(c => c.Value).ToArray();
            context.Request.Headers["X-User-Permissions"] = string.Join(",", permissions);

            // Add JWT token ID for tracking
            var jti = principal.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                context.Request.Headers["X-Token-Id"] = jti;
            }

            // Set the user principal for the request
            context.User = principal;

            return AuthenticationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JWT authentication failed");
            return AuthenticationResult.Failed();
        }
    }

    private async Task<AuthenticationResult> TryApiKeyAuthentication(HttpContext context)
    {
        try
        {
            // Extract API key from header
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(apiKey))
            {
                return AuthenticationResult.Failed();
            }

            // Validate API key with Identity service via gRPC
            _logger.LogInformation("🔍 Attempting API key authentication for path: {Path}", context.Request.Path);

            var request = new ValidateApiKeyRequest
            {
                ApiKey = apiKey,
                ServiceName = ExtractServiceName(context.Request.Path),
                Endpoint = context.Request.Path.ToString()
            };

            var response = await _grpcClientService.ValidateApiKeyAsync(request);

            if (!response.IsValid)
            {
                var errorMessage = !string.IsNullOrEmpty(response.ErrorMessage) ? response.ErrorMessage : "Unknown error";
                _logger.LogDebug("API key validation failed: {Error}", errorMessage);
                return AuthenticationResult.Failed();
            }

            // Add user information to request headers for downstream services
            context.Request.Headers["X-User-Id"] = response.UserId ?? string.Empty;
            context.Request.Headers["X-User-Name"] = response.UserName ?? string.Empty;
            context.Request.Headers["X-User-Permissions"] = string.Join(",", response.Permissions);
            context.Request.Headers["X-Token-Type"] = "api-key";
            context.Request.Headers["X-Auth-Method"] = "API-Key";

            _logger.LogInformation("✅ API key validated for user: {UserName}, path: {Path}", response.UserName, context.Request.Path);

            return AuthenticationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "API key authentication failed");
            return AuthenticationResult.Failed();
        }
    }

    private static string ExtractServiceName(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        
        return pathValue switch
        {
            var p when p?.StartsWith("/api/weather") == true => "WeatherService",
            var p when p?.StartsWith("/api/docs") == true => "DocumentationService",
            var p when p?.StartsWith("/api/identity") == true => "IdentityService",
            _ => "UnknownService"
        };
    }

    private static bool ShouldSkipValidation(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();
        
        // Skip validation for health checks, documentation, and public endpoints
        return pathValue switch
        {
            "/health" => true,
            "/ready" => true,
            "/metrics" => true,
            var p when p?.StartsWith("/api/docs") == true => true,
            var p when p?.StartsWith("/swagger") == true => true,
            var p when p?.StartsWith("/scalar") == true => true,
            var p when p?.Contains("/public-key") == true => true,
            _ => false
        };
    }

    private record AuthenticationResult(bool IsAuthenticated)
    {
        public static AuthenticationResult Success() => new(true);
        public static AuthenticationResult Failed() => new(false);
    }
}
