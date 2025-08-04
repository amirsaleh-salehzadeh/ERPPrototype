using System.Diagnostics;
using BFF.Gateway.Models;
using BFF.Gateway.Services;
using ERP.Contracts.Identity;
using Serilog;
using Serilog.Context;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Middleware to authenticate users via JWT tokens, sessions, etc.
/// This runs AFTER API access level check but BEFORE user authorization
/// </summary>
public class UserAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Microsoft.Extensions.Logging.ILogger<UserAuthenticationMiddleware> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private static readonly Serilog.ILogger _serilogLogger = Log.ForContext<UserAuthenticationMiddleware>();

    public UserAuthenticationMiddleware(
        RequestDelegate next,
        Microsoft.Extensions.Logging.ILogger<UserAuthenticationMiddleware> logger,
        IGrpcClientService grpcClientService)
    {
        _next = next;
        _logger = logger;
        _grpcClientService = grpcClientService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Skip user authentication for API-key-only endpoints
        if (ShouldSkipUserAuthentication(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Get security context from previous middleware
        var securityContext = context.Items["SecurityContext"] as SecurityContext;
        if (securityContext == null)
        {
            _serilogLogger.Warning("🚫 User Authentication: No security context found");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Authentication required");
            return;
        }

        try
        {
            // Extract user token from various possible headers
            var token = ExtractUserToken(context.Request);
            var tokenType = DetermineTokenType(context.Request);

            if (string.IsNullOrEmpty(token))
            {
                // Check if endpoint requires user authentication
                if (RequiresUserAuthentication(context.Request.Path))
                {
                    _serilogLogger.Warning("🚫 User Authentication: Missing user token for protected endpoint: {Path}", 
                        context.Request.Path);
                    
                    var decision = new SecurityDecision
                    {
                        Stage = "UserAuthentication",
                        IsAllowed = false,
                        Reason = "Missing user authentication token",
                        Details = "Endpoint requires user authentication but no token provided",
                        Duration = stopwatch.Elapsed
                    };
                    securityContext.Decisions.Add(decision);

                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("User authentication required");
                    return;
                }
                else
                {
                    // Endpoint doesn't require user auth, continue with API-key-only access
                    _serilogLogger.Information("ℹ️ User Authentication: Skipped for API-key-only endpoint: {Path}", 
                        context.Request.Path);
                    await _next(context);
                    return;
                }
            }

            // Authenticate user
            var request = new AuthenticateUserRequest
            {
                Token = token,
                TokenType = tokenType,
                ServiceName = securityContext.ServiceName,
                Endpoint = securityContext.Path
            };

            using (LogContext.PushProperty("Stage", "UserAuthentication"))
            using (LogContext.PushProperty("TokenType", tokenType))
            using (LogContext.PushProperty("ServiceName", securityContext.ServiceName))
            using (LogContext.PushProperty("Endpoint", securityContext.Path))
            {
                _serilogLogger.Information("👤 Authenticating user token: {TokenType} for endpoint: {Method} {Path}",
                    tokenType, securityContext.Method, securityContext.Path);

                var response = await _grpcClientService.AuthenticateUserAsync(request);
                stopwatch.Stop();

                // Create security decision
                var decision = new SecurityDecision
                {
                    Stage = "UserAuthentication",
                    IsAllowed = response.IsAuthenticated,
                    Reason = response.IsAuthenticated ? "User authenticated successfully" : response.ErrorMessage,
                    Details = response.IsAuthenticated ? $"User: {response.UserName} ({response.UserId})" : response.ErrorMessage,
                    Duration = stopwatch.Elapsed
                };

                securityContext.Decisions.Add(decision);

                if (!response.IsAuthenticated)
                {
                    using (LogContext.PushProperty("ErrorMessage", response.ErrorMessage))
                    {
                        _serilogLogger.Warning("🚫 User Authentication Failed: {ErrorMessage}", response.ErrorMessage);
                    }

                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync($"User authentication failed: {response.ErrorMessage}");
                    return;
                }

                // Update security context with user info
                securityContext.User = new UserInfo
                {
                    UserId = response.UserId,
                    UserName = response.UserName,
                    Email = response.Email,
                    IsAuthenticated = response.IsAuthenticated,
                    Roles = response.Roles.ToList(),
                    Permissions = response.Permissions.ToList(),
                    AccessLevel = ConvertUserAccessLevel(response.AccessLevel),
                    TokenType = tokenType,
                    TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(response.TokenExpiresAt).DateTime
                };

                using (LogContext.PushProperty("UserId", response.UserId))
                using (LogContext.PushProperty("UserName", response.UserName))
                using (LogContext.PushProperty("UserAccessLevel", response.AccessLevel.ToString()))
                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    _serilogLogger.Information("✅ User Authenticated: {UserName} ({UserId}) - Level: {UserAccessLevel} - Duration: {Duration}ms",
                        response.UserName, response.UserId, response.AccessLevel, stopwatch.ElapsedMilliseconds);
                }

                // Continue to next middleware
                await _next(context);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            var decision = new SecurityDecision
            {
                Stage = "UserAuthentication",
                IsAllowed = false,
                Reason = "Internal error during user authentication",
                Details = ex.Message,
                Duration = stopwatch.Elapsed
            };

            securityContext.Decisions.Add(decision);

            _serilogLogger.Error(ex, "❌ Error during user authentication for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during user authentication");
        }
    }

    private string? ExtractUserToken(HttpRequest request)
    {
        // Try Authorization header first (Bearer token)
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }

        // Try custom headers
        var customToken = request.Headers["X-User-Token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(customToken))
        {
            return customToken;
        }

        // Try session cookie
        var sessionCookie = request.Cookies["SessionToken"];
        if (!string.IsNullOrEmpty(sessionCookie))
        {
            return sessionCookie;
        }

        return null;
    }

    private string DetermineTokenType(HttpRequest request)
    {
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return "JWT";
        }

        if (request.Headers.ContainsKey("X-User-Token"))
        {
            return "Custom";
        }

        if (request.Cookies.ContainsKey("SessionToken"))
        {
            return "Session";
        }

        return "Unknown";
    }

    private bool ShouldSkipUserAuthentication(string path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/api/gateway/services",
            "/swagger",
            "/scalar",
            "/api/docs/scalar"
        };

        return skipPaths.Any(skipPath => path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase));
    }

    private bool RequiresUserAuthentication(string path)
    {
        // Define endpoints that require user authentication
        var userAuthPaths = new[]
        {
            "/api/orders",
            "/api/customers",
            "/api/finance"
        };

        return userAuthPaths.Any(authPath => path.StartsWith(authPath, StringComparison.OrdinalIgnoreCase));
    }

    private Models.UserAccessLevel ConvertUserAccessLevel(ERP.Contracts.Identity.UserAccessLevel grpcLevel)
    {
        return grpcLevel switch
        {
            ERP.Contracts.Identity.UserAccessLevel.UserNone => Models.UserAccessLevel.None,
            ERP.Contracts.Identity.UserAccessLevel.UserGuest => Models.UserAccessLevel.Guest,
            ERP.Contracts.Identity.UserAccessLevel.UserUser => Models.UserAccessLevel.User,
            ERP.Contracts.Identity.UserAccessLevel.UserPowerUser => Models.UserAccessLevel.PowerUser,
            ERP.Contracts.Identity.UserAccessLevel.UserManager => Models.UserAccessLevel.Manager,
            ERP.Contracts.Identity.UserAccessLevel.UserAdmin => Models.UserAccessLevel.Admin,
            ERP.Contracts.Identity.UserAccessLevel.UserSuperAdmin => Models.UserAccessLevel.SuperAdmin,
            _ => Models.UserAccessLevel.None
        };
    }
}
