using BFF.Gateway.Services;
using System.Security.Claims;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Middleware for validating JWT tokens in inter-service communication
/// </summary>
public class JwtValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtValidationMiddleware> _logger;
    private readonly IJwtValidationService _jwtValidationService;

    public JwtValidationMiddleware(
        RequestDelegate next,
        ILogger<JwtValidationMiddleware> logger,
        IJwtValidationService jwtValidationService)
    {
        _next = next;
        _logger = logger;
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

        // Check if JWT validation service is ready
        if (!_jwtValidationService.IsPublicKeyLoaded)
        {
            _logger.LogWarning("🚫 JWT validation service not ready - public key not loaded");
            context.Response.StatusCode = 503;
            await context.Response.WriteAsync("Authentication service temporarily unavailable");
            return;
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
            _logger.LogWarning("🚫 Missing JWT token for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("JWT token is required");
            return;
        }

        try
        {
            // Validate JWT token
            _logger.LogInformation("🔍 Validating JWT token for path: {Path}", context.Request.Path);

            var principal = _jwtValidationService.ValidateToken(token);

            if (principal == null)
            {
                _logger.LogWarning("🚫 Invalid JWT token for path: {Path}", context.Request.Path);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid JWT token");
                return;
            }

            // Extract claims and add to request headers for downstream services
            var tokenType = principal.FindFirst("token_type")?.Value;
            
            if (tokenType == "service")
            {
                // Service token
                var serviceName = principal.FindFirst("service_name")?.Value;
                context.Request.Headers["X-Service-Name"] = serviceName ?? string.Empty;
                context.Request.Headers["X-Token-Type"] = "service";
                
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

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error validating JWT token for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during JWT validation");
        }
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
}
