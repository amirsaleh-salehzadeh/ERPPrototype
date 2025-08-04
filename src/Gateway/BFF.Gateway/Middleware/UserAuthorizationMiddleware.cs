using System.Diagnostics;
using BFF.Gateway.Models;
using BFF.Gateway.Services;
using ERP.Contracts.Identity;
using Serilog;
using Serilog.Context;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Middleware to verify user authorization (roles/permissions) for specific endpoints
/// This runs LAST in the security pipeline, after user authentication
/// </summary>
public class UserAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Microsoft.Extensions.Logging.ILogger<UserAuthorizationMiddleware> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private static readonly Serilog.ILogger _serilogLogger = Log.ForContext<UserAuthorizationMiddleware>();

    public UserAuthorizationMiddleware(
        RequestDelegate next,
        Microsoft.Extensions.Logging.ILogger<UserAuthorizationMiddleware> logger,
        IGrpcClientService grpcClientService)
    {
        _next = next;
        _logger = logger;
        _grpcClientService = grpcClientService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Skip user authorization for public or API-key-only endpoints
        if (ShouldSkipUserAuthorization(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Get security context from previous middleware
        var securityContext = context.Items["SecurityContext"] as SecurityContext;
        if (securityContext == null)
        {
            _serilogLogger.Warning("🚫 User Authorization: No security context found");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Authentication required");
            return;
        }

        // Check if user is authenticated (if required for this endpoint)
        if (RequiresUserAuthorization(context.Request.Path) && securityContext.User?.IsAuthenticated != true)
        {
            _serilogLogger.Warning("🚫 User Authorization: User not authenticated for protected endpoint: {Path}", 
                context.Request.Path);
            
            var decision = new SecurityDecision
            {
                Stage = "UserAuthorization",
                IsAllowed = false,
                Reason = "User not authenticated",
                Details = "Endpoint requires user authorization but user is not authenticated",
                Duration = stopwatch.Elapsed
            };
            securityContext.Decisions.Add(decision);

            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("User authentication required for authorization");
            return;
        }

        // If user is not authenticated but endpoint doesn't require it, skip authorization
        if (securityContext.User?.IsAuthenticated != true)
        {
            _serilogLogger.Information("ℹ️ User Authorization: Skipped for non-authenticated user on endpoint: {Path}", 
                context.Request.Path);
            await _next(context);
            return;
        }

        try
        {
            // Check user authorization
            var request = new CheckUserAuthorizationRequest
            {
                UserId = securityContext.User.UserId,
                ServiceName = securityContext.ServiceName,
                Endpoint = securityContext.Path,
                Method = securityContext.Method
            };

            // Add user roles and permissions
            request.UserRoles.AddRange(securityContext.User.Roles);
            request.UserPermissions.AddRange(securityContext.User.Permissions);

            using (LogContext.PushProperty("Stage", "UserAuthorization"))
            using (LogContext.PushProperty("UserId", securityContext.User.UserId))
            using (LogContext.PushProperty("UserName", securityContext.User.UserName))
            using (LogContext.PushProperty("ServiceName", securityContext.ServiceName))
            using (LogContext.PushProperty("Endpoint", securityContext.Path))
            {
                _serilogLogger.Information("🛡️ Checking user authorization: {UserName} for endpoint: {Method} {Path} - Service: {ServiceName}",
                    securityContext.User.UserName, securityContext.Method, securityContext.Path, securityContext.ServiceName);

                var response = await _grpcClientService.CheckUserAuthorizationAsync(request);
                stopwatch.Stop();

                // Create security decision
                var decision = new SecurityDecision
                {
                    Stage = "UserAuthorization",
                    IsAllowed = response.IsAuthorized,
                    Reason = response.Reason,
                    Details = response.IsAuthorized 
                        ? $"User authorized with level {response.CurrentLevel}" 
                        : $"Required: {response.RequiredLevel}, Current: {response.CurrentLevel}. Missing roles: [{string.Join(", ", response.RequiredRoles)}], permissions: [{string.Join(", ", response.RequiredPermissions)}]",
                    Duration = stopwatch.Elapsed
                };

                securityContext.Decisions.Add(decision);

                if (!response.IsAuthorized)
                {
                    using (LogContext.PushProperty("RequiredLevel", response.RequiredLevel.ToString()))
                    using (LogContext.PushProperty("CurrentLevel", response.CurrentLevel.ToString()))
                    using (LogContext.PushProperty("RequiredRoles", string.Join(", ", response.RequiredRoles)))
                    using (LogContext.PushProperty("RequiredPermissions", string.Join(", ", response.RequiredPermissions)))
                    using (LogContext.PushProperty("Reason", response.Reason))
                    {
                        _serilogLogger.Warning("🚫 User Authorization Denied: {Reason} - Required Level: {RequiredLevel}, Current: {CurrentLevel}",
                            response.Reason, response.RequiredLevel, response.CurrentLevel);
                    }

                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync($"Access denied: {response.Reason}");
                    return;
                }

                using (LogContext.PushProperty("UserAccessLevel", response.CurrentLevel.ToString()))
                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    _serilogLogger.Information("✅ User Authorization Granted: {UserName} - Level: {UserAccessLevel} - Duration: {Duration}ms",
                        securityContext.User.UserName, response.CurrentLevel, stopwatch.ElapsedMilliseconds);
                }

                // Add final security context to request headers for downstream services
                AddSecurityHeaders(context, securityContext);

                // Log complete security pipeline success
                LogSecurityPipelineSuccess(securityContext);

                // Continue to request processing
                await _next(context);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            var decision = new SecurityDecision
            {
                Stage = "UserAuthorization",
                IsAllowed = false,
                Reason = "Internal error during user authorization",
                Details = ex.Message,
                Duration = stopwatch.Elapsed
            };

            securityContext.Decisions.Add(decision);

            _serilogLogger.Error(ex, "❌ Error during user authorization for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during user authorization");
        }
    }

    private void AddSecurityHeaders(HttpContext context, SecurityContext securityContext)
    {
        // Add API key info
        if (securityContext.ApiKey != null)
        {
            context.Request.Headers["X-API-Key-Id"] = securityContext.ApiKey.KeyId;
            context.Request.Headers["X-API-Client-Name"] = securityContext.ApiKey.ClientName;
            context.Request.Headers["X-API-Access-Level"] = securityContext.ApiKey.AccessLevel.ToString();
        }

        // Add user info
        if (securityContext.User != null)
        {
            context.Request.Headers["X-User-Id"] = securityContext.User.UserId;
            context.Request.Headers["X-User-Name"] = securityContext.User.UserName;
            context.Request.Headers["X-User-Email"] = securityContext.User.Email;
            context.Request.Headers["X-User-Roles"] = string.Join(",", securityContext.User.Roles);
            context.Request.Headers["X-User-Permissions"] = string.Join(",", securityContext.User.Permissions);
            context.Request.Headers["X-User-Access-Level"] = securityContext.User.AccessLevel.ToString();
        }

        // Add security context
        context.Request.Headers["X-Security-Context"] = "Verified";
        context.Request.Headers["X-Security-Pipeline"] = "Complete";
    }

    private void LogSecurityPipelineSuccess(SecurityContext securityContext)
    {
        var totalDuration = securityContext.Decisions.Sum(d => d.Duration.TotalMilliseconds);
        var stagesSummary = string.Join(" → ", securityContext.Decisions.Select(d => $"{d.Stage}({d.Duration.TotalMilliseconds:F1}ms)"));

        using (LogContext.PushProperty("SecurityPipelineStages", stagesSummary))
        using (LogContext.PushProperty("TotalSecurityDuration", totalDuration))
        using (LogContext.PushProperty("ApiKeyId", securityContext.ApiKey?.KeyId))
        using (LogContext.PushProperty("UserId", securityContext.User?.UserId))
        using (LogContext.PushProperty("UserName", securityContext.User?.UserName))
        {
            _serilogLogger.Information("🔐 Security Pipeline Complete: {SecurityPipelineStages} - Total: {TotalSecurityDuration:F1}ms - User: {UserName} - API: {ApiKeyId}",
                stagesSummary, totalDuration, securityContext.User?.UserName, securityContext.ApiKey?.KeyId);
        }
    }

    private bool ShouldSkipUserAuthorization(string path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/api/gateway/services",
            "/swagger",
            "/scalar",
            "/api/docs/scalar",
            "/api/weather" // Weather service might be API-key-only
        };

        return skipPaths.Any(skipPath => path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase));
    }

    private bool RequiresUserAuthorization(string path)
    {
        // Define endpoints that require user authorization
        var authPaths = new[]
        {
            "/api/orders",
            "/api/customers",
            "/api/finance",
            "/api/inventory" // Some inventory operations might require user auth
        };

        return authPaths.Any(authPath => path.StartsWith(authPath, StringComparison.OrdinalIgnoreCase));
    }
}
