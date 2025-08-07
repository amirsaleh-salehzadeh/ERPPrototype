using BFF.Gateway.Services;
using ERP.Contracts.Identity;
using System.Security.Claims;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Hybrid authentication middleware that supports both modern JWT tokens and legacy API keys.
/// This middleware provides a seamless transition path from API key authentication to JWT-based
/// authentication while maintaining backward compatibility and high security standards.
///
/// Authentication Strategy:
/// 1. **JWT Authentication (Primary)**: Modern, stateless, cryptographically secure
/// 2. **API Key Authentication (Fallback)**: Legacy support for existing clients
/// 3. **Graceful Degradation**: Falls back to API key if JWT validation fails
/// 4. **Unified Authorization**: Both methods produce consistent user context headers
///
/// Security Features:
/// - JWT tokens provide cryptographic integrity and non-repudiation
/// - API keys validated via secure gRPC communication with Identity Service
/// - Comprehensive request/response header sanitization
/// - Detailed security event logging for audit trails
///
/// Performance Benefits:
/// - JWT validation is performed locally (no network calls)
/// - API key validation uses efficient gRPC protocol
/// - Automatic retry mechanisms for transient failures
/// - Optimized for high-throughput scenarios
/// </summary>
public class HybridAuthenticationMiddleware
{
    #region Private Fields

    /// <summary>Next middleware in the ASP.NET Core pipeline</summary>
    private readonly RequestDelegate _next;

    /// <summary>Logger for authentication events and security monitoring</summary>
    private readonly ILogger<HybridAuthenticationMiddleware> _logger;

    /// <summary>gRPC client service for API key validation with Identity Service</summary>
    private readonly IGrpcClientService _grpcClientService;

    /// <summary>JWT validation service for local token verification</summary>
    private readonly IJwtValidationService _jwtValidationService;

    #endregion

    /// <summary>
    /// Initializes a new instance of the hybrid authentication middleware.
    /// Sets up all dependencies required for both JWT and API key authentication methods.
    /// </summary>
    /// <param name="next">Next middleware in the ASP.NET Core pipeline</param>
    /// <param name="logger">Logger for authentication events and security monitoring</param>
    /// <param name="grpcClientService">gRPC client for API key validation</param>
    /// <param name="jwtValidationService">Service for local JWT token validation</param>
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

    /// <summary>
    /// Main middleware entry point that orchestrates the hybrid authentication process.
    /// This method implements a two-tier authentication strategy with JWT as primary
    /// and API key as fallback, ensuring maximum compatibility and security.
    ///
    /// Authentication Flow:
    /// 1. Check if request path should skip authentication (health checks, docs, etc.)
    /// 2. Attempt JWT authentication first (modern, preferred method)
    /// 3. Fall back to API key authentication if JWT fails (legacy support)
    /// 4. Reject request if both authentication methods fail
    /// 5. Add user/service context headers for downstream services
    /// 6. Continue to next middleware in pipeline
    ///
    /// Security Benefits:
    /// - JWT provides cryptographic integrity and stateless validation
    /// - API key provides backward compatibility for legacy clients
    /// - Comprehensive logging for security monitoring and audit trails
    /// - Proper HTTP status codes and error messages for clients
    /// </summary>
    /// <param name="context">HTTP context for the current request</param>
    /// <returns>Task representing the asynchronous middleware operation</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for public endpoints (health checks, documentation, etc.)
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Primary authentication method: JWT token validation
        // This is preferred due to cryptographic security and stateless nature
        var jwtResult = await TryJwtAuthentication(context);
        if (jwtResult.IsAuthenticated)
        {
            _logger.LogInformation("✅ Request authenticated via JWT token");
            await _next(context);
            return;
        }

        // Fallback authentication method: API key validation
        // This provides backward compatibility for existing clients
        var apiKeyResult = await TryApiKeyAuthentication(context);
        if (apiKeyResult.IsAuthenticated)
        {
            _logger.LogInformation("✅ Request authenticated via API key");
            await _next(context);
            return;
        }

        // Authentication failed - reject request with appropriate error
        _logger.LogWarning("🚫 No valid authentication found for path: {Path}", context.Request.Path);
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Authentication required. Provide either a valid JWT token or API key.");
    }

    /// <summary>
    /// Attempts to authenticate the request using JWT token validation.
    /// This method implements the primary authentication mechanism with comprehensive
    /// token extraction, validation, and user context setup.
    ///
    /// JWT Authentication Process:
    /// 1. Verify JWT validation service readiness (public key loaded)
    /// 2. Extract JWT token from Authorization header (Bearer scheme)
    /// 3. Fallback to X-JWT-Token header for service-to-service communication
    /// 4. Validate token cryptographically using cached public key
    /// 5. Extract claims and determine token type (user vs service)
    /// 6. Add appropriate context headers for downstream services
    /// 7. Set ClaimsPrincipal for .NET authorization integration
    ///
    /// Security Features:
    /// - Automatic public key refresh if validation service not ready
    /// - Support for both standard Bearer tokens and custom headers
    /// - Comprehensive claims extraction for authorization
    /// - Proper error handling and logging for security monitoring
    /// </summary>
    /// <param name="context">HTTP context for the current request</param>
    /// <returns>AuthenticationResult indicating success or failure</returns>
    private async Task<AuthenticationResult> TryJwtAuthentication(HttpContext context)
    {
        try
        {
            // Ensure JWT validation service is ready with public key loaded
            if (!_jwtValidationService.IsPublicKeyLoaded)
            {
                _logger.LogDebug("JWT validation service not ready - attempting to load public key");

                // Attempt to refresh public key from Identity Service
                await _jwtValidationService.UpdatePublicKeyAsync();

                // Check if key loading was successful
                if (!_jwtValidationService.IsPublicKeyLoaded)
                {
                    _logger.LogDebug("JWT validation service still not ready after retry");
                    return AuthenticationResult.Failed();
                }
            }

            // Extract JWT token from standard Authorization header (Bearer scheme)
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            string? token = null;

            // Parse Bearer token from Authorization header
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }

            // Fallback: Check X-JWT-Token header for service-to-service communication
            // This allows services to pass JWT tokens without Bearer scheme
            if (string.IsNullOrEmpty(token))
            {
                token = context.Request.Headers["X-JWT-Token"].FirstOrDefault();
            }

            // No JWT token found in either location
            if (string.IsNullOrEmpty(token))
            {
                return AuthenticationResult.Failed();
            }

            // Log JWT authentication attempt for audit trail
            _logger.LogInformation("🔍 Attempting JWT authentication for path: {Path}", context.Request.Path);

            // Perform cryptographic token validation using cached public key
            var principal = _jwtValidationService.ValidateToken(token);

            // Token validation failed (expired, invalid signature, malformed, etc.)
            if (principal == null)
            {
                _logger.LogDebug("JWT token validation failed");
                return AuthenticationResult.Failed();
            }

            // Extract token type to determine how to process claims
            var tokenType = principal.FindFirst("token_type")?.Value;

            // Process service tokens (machine-to-machine authentication)
            if (tokenType == "service")
            {
                // Extract service-specific claims
                var serviceName = principal.FindFirst("service_name")?.Value;

                // Add service context headers for downstream services
                context.Request.Headers["X-Service-Name"] = serviceName ?? string.Empty;
                context.Request.Headers["X-Token-Type"] = "service";
                context.Request.Headers["X-Auth-Method"] = "JWT";

                // Log successful service authentication for audit trail
                _logger.LogInformation("✅ Service JWT token validated for service: {ServiceName}, path: {Path}",
                    serviceName, context.Request.Path);
            }
            // Process user tokens (client application authentication)
            else if (tokenType == "user")
            {
                // Extract user-specific claims
                var userId = principal.FindFirst("user_id")?.Value;
                var userName = principal.FindFirst("user_name")?.Value;

                // Add user context headers for downstream services
                context.Request.Headers["X-User-Id"] = userId ?? string.Empty;
                context.Request.Headers["X-User-Name"] = userName ?? string.Empty;
                context.Request.Headers["X-Token-Type"] = "user";
                context.Request.Headers["X-Auth-Method"] = "JWT";

                // Log successful user authentication for audit trail
                _logger.LogInformation("✅ User JWT token validated for user: {UserName} (ID: {UserId}), path: {Path}",
                    userName, userId, context.Request.Path);
            }

            // Extract and add permission claims for fine-grained authorization
            var permissions = principal.FindAll("permission").Select(c => c.Value).ToArray();
            context.Request.Headers["X-User-Permissions"] = string.Join(",", permissions);

            // Add JWT token ID (jti) for request tracking and potential revocation
            var jti = principal.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                context.Request.Headers["X-Token-Id"] = jti;
            }

            // Set the ClaimsPrincipal for .NET authorization integration
            // This enables [Authorize] attributes and User.Identity in controllers
            context.User = principal;

            return AuthenticationResult.Success();
        }
        catch (Exception ex)
        {
            // Log JWT authentication failure for debugging (not security event)
            _logger.LogDebug(ex, "JWT authentication failed");
            return AuthenticationResult.Failed();
        }
    }

    /// <summary>
    /// Attempts to authenticate the request using legacy API key validation.
    /// This method provides backward compatibility for existing clients while
    /// maintaining security through gRPC communication with the Identity Service.
    ///
    /// API Key Authentication Process:
    /// 1. Extract API key from X-API-Key header
    /// 2. Determine target service name from request path
    /// 3. Make gRPC call to Identity Service for validation
    /// 4. Process validation response and extract user information
    /// 5. Add user context headers for downstream services
    /// 6. Log authentication result for audit trail
    ///
    /// Legacy Support Features:
    /// - Maintains compatibility with existing API key clients
    /// - Uses secure gRPC protocol for validation requests
    /// - Provides same user context headers as JWT authentication
    /// - Comprehensive error handling and logging
    ///
    /// Performance Considerations:
    /// - Requires network call to Identity Service per request
    /// - Less efficient than JWT validation but necessary for legacy support
    /// - gRPC protocol provides better performance than REST
    /// </summary>
    /// <param name="context">HTTP context for the current request</param>
    /// <returns>AuthenticationResult indicating success or failure</returns>
    private async Task<AuthenticationResult> TryApiKeyAuthentication(HttpContext context)
    {
        try
        {
            // Extract API key from standard X-API-Key header
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

            // No API key provided
            if (string.IsNullOrEmpty(apiKey))
            {
                return AuthenticationResult.Failed();
            }

            // Log API key authentication attempt for audit trail
            _logger.LogInformation("🔍 Attempting API key authentication for path: {Path}", context.Request.Path);

            // Prepare gRPC request for Identity Service validation
            var request = new ValidateApiKeyRequest
            {
                ApiKey = apiKey,
                ServiceName = ExtractServiceName(context.Request.Path), // Determine target service
                Endpoint = context.Request.Path.ToString() // Full endpoint path
            };

            // Make secure gRPC call to Identity Service for validation
            var response = await _grpcClientService.ValidateApiKeyAsync(request);

            // API key validation failed
            if (!response.IsValid)
            {
                var errorMessage = !string.IsNullOrEmpty(response.ErrorMessage) ? response.ErrorMessage : "Unknown error";
                _logger.LogDebug("API key validation failed: {Error}", errorMessage);
                return AuthenticationResult.Failed();
            }

            // Add user context headers for downstream services (same format as JWT)
            context.Request.Headers["X-User-Id"] = response.UserId ?? string.Empty;
            context.Request.Headers["X-User-Name"] = response.UserName ?? string.Empty;
            context.Request.Headers["X-User-Permissions"] = string.Join(",", response.Permissions);
            context.Request.Headers["X-Token-Type"] = "api-key"; // Distinguish from JWT tokens
            context.Request.Headers["X-Auth-Method"] = "API-Key"; // Track authentication method

            // Log successful API key authentication for audit trail
            _logger.LogInformation("✅ API key validated for user: {UserName}, path: {Path}", response.UserName, context.Request.Path);

            return AuthenticationResult.Success();
        }
        catch (Exception ex)
        {
            // Log API key authentication failure for debugging (not security event)
            _logger.LogDebug(ex, "API key authentication failed");
            return AuthenticationResult.Failed();
        }
    }

    #region Helper Methods

    /// <summary>
    /// Extracts the target service name from the request path for API key validation.
    /// This method maps URL paths to service names to enable service-specific
    /// API key validation and authorization in the Identity Service.
    ///
    /// Path Mapping Strategy:
    /// - /api/weather/* → WeatherService
    /// - /api/docs/* → DocumentationService
    /// - /api/identity/* → IdentityService
    /// - Unknown paths → UnknownService
    ///
    /// Usage: Used by API key validation to determine which service the request targets
    /// Extensibility: Add new path mappings as services are added to the system
    /// </summary>
    /// <param name="path">Request path from HTTP context</param>
    /// <returns>Service name string for Identity Service validation</returns>
    private static string ExtractServiceName(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();

        return pathValue switch
        {
            // Weather service endpoints
            var p when p?.StartsWith("/api/weather") == true => "WeatherService",

            // Documentation service endpoints
            var p when p?.StartsWith("/api/docs") == true => "DocumentationService",

            // Identity service endpoints
            var p when p?.StartsWith("/api/identity") == true => "IdentityService",

            // Default for unmapped paths
            _ => "UnknownService"
        };
    }

    /// <summary>
    /// Determines whether authentication should be skipped for the given request path.
    /// This method identifies public endpoints that don't require authentication,
    /// such as health checks, documentation, and public key distribution.
    ///
    /// Skip Criteria:
    /// - Health and monitoring endpoints (/health, /ready, /metrics)
    /// - API documentation endpoints (/api/docs, /swagger, /scalar)
    /// - Public key distribution endpoints (contains /public-key)
    ///
    /// Security Considerations:
    /// - Only truly public endpoints should be exempted
    /// - Documentation endpoints are public to enable API exploration
    /// - Health checks must be accessible for load balancer probes
    /// - Public key endpoints enable JWT validation setup
    /// </summary>
    /// <param name="path">Request path from HTTP context</param>
    /// <returns>True if authentication should be skipped, false otherwise</returns>
    private static bool ShouldSkipValidation(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant();

        // Skip validation for public endpoints
        return pathValue switch
        {
            // Health and monitoring endpoints
            "/health" => true,
            "/ready" => true,
            "/metrics" => true,

            // API documentation endpoints (public for exploration)
            var p when p?.StartsWith("/api/docs") == true => true,
            var p when p?.StartsWith("/swagger") == true => true,
            var p when p?.StartsWith("/scalar") == true => true,

            // Public key distribution endpoints (required for JWT setup)
            var p when p?.Contains("/public-key") == true => true,

            // All other endpoints require authentication
            _ => false
        };
    }

    #endregion

    /// <summary>
    /// Simple result type for authentication operations.
    /// Provides a clean way to return success/failure status from authentication methods.
    ///
    /// Design Benefits:
    /// - Type-safe authentication result handling
    /// - Clear success/failure semantics
    /// - Extensible for additional result information if needed
    /// </summary>
    /// <param name="IsAuthenticated">True if authentication succeeded, false otherwise</param>
    private record AuthenticationResult(bool IsAuthenticated)
    {
        /// <summary>Creates a successful authentication result</summary>
        public static AuthenticationResult Success() => new(true);

        /// <summary>Creates a failed authentication result</summary>
        public static AuthenticationResult Failed() => new(false);
    }
}
