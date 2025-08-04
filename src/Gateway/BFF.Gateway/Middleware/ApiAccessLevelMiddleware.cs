using System.Diagnostics;
using BFF.Gateway.Models;
using BFF.Gateway.Services;
using ERP.Contracts.Identity;
using Serilog;
using Serilog.Context;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Middleware to verify API access level for specific endpoints
/// This runs AFTER API key validation but BEFORE user authentication
/// </summary>
public class ApiAccessLevelMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Microsoft.Extensions.Logging.ILogger<ApiAccessLevelMiddleware> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private static readonly Serilog.ILogger _serilogLogger = Log.ForContext<ApiAccessLevelMiddleware>();

    public ApiAccessLevelMiddleware(
        RequestDelegate next,
        Microsoft.Extensions.Logging.ILogger<ApiAccessLevelMiddleware> logger,
        IGrpcClientService grpcClientService)
    {
        _next = next;
        _logger = logger;
        _grpcClientService = grpcClientService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Skip API access check for public paths
        if (ShouldSkipApiAccessCheck(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Get security context from previous middleware
        var securityContext = context.Items["SecurityContext"] as SecurityContext;
        if (securityContext == null)
        {
            _serilogLogger.Warning("🚫 API Access Check: No security context found - API key validation may have failed");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Authentication required");
            return;
        }

        // Check if API key validation passed
        if (securityContext.ApiKey?.IsValid != true)
        {
            _serilogLogger.Warning("🚫 API Access Check: Invalid API key in security context");
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Invalid API key");
            return;
        }

        try
        {
            // Extract API key from security context
            var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey))
            {
                _serilogLogger.Warning("🚫 API Access Check: Missing API key header");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API key required");
                return;
            }

            // Check API access level
            var request = new CheckApiAccessRequest
            {
                ApiKey = apiKey,
                ServiceName = securityContext.ServiceName,
                Endpoint = securityContext.Path,
                Method = securityContext.Method
            };

            using (LogContext.PushProperty("Stage", "ApiAccessCheck"))
            using (LogContext.PushProperty("ApiKeyId", securityContext.ApiKey.KeyId))
            using (LogContext.PushProperty("ServiceName", securityContext.ServiceName))
            using (LogContext.PushProperty("Endpoint", securityContext.Path))
            {
                _serilogLogger.Information("🎯 Checking API access level for endpoint: {Method} {Path} - Service: {ServiceName}",
                    request.Method, request.Endpoint, request.ServiceName);

                var response = await _grpcClientService.CheckApiAccessAsync(request);
                stopwatch.Stop();

                // Create security decision
                var decision = new SecurityDecision
                {
                    Stage = "ApiAccessCheck",
                    IsAllowed = response.HasAccess,
                    Reason = response.Reason,
                    Details = $"Required: {response.RequiredLevel}, Current: {response.CurrentLevel}",
                    Duration = stopwatch.Elapsed
                };

                securityContext.Decisions.Add(decision);

                if (!response.HasAccess)
                {
                    using (LogContext.PushProperty("RequiredLevel", response.RequiredLevel.ToString()))
                    using (LogContext.PushProperty("CurrentLevel", response.CurrentLevel.ToString()))
                    using (LogContext.PushProperty("Reason", response.Reason))
                    {
                        _serilogLogger.Warning("🚫 API Access Denied: {Reason} - Required: {RequiredLevel}, Current: {CurrentLevel}",
                            response.Reason, response.RequiredLevel, response.CurrentLevel);
                    }

                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync($"API access denied: {response.Reason}");
                    return;
                }

                // Update security context with API access info
                if (securityContext.ApiKey != null)
                {
                    securityContext.ApiKey.AccessLevel = ConvertApiAccessLevel(response.CurrentLevel);
                }

                using (LogContext.PushProperty("AccessLevel", response.CurrentLevel.ToString()))
                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    _serilogLogger.Information("✅ API Access Granted: Level {AccessLevel} - Duration: {Duration}ms",
                        response.CurrentLevel, stopwatch.ElapsedMilliseconds);
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
                Stage = "ApiAccessCheck",
                IsAllowed = false,
                Reason = "Internal error during API access check",
                Details = ex.Message,
                Duration = stopwatch.Elapsed
            };

            securityContext.Decisions.Add(decision);

            _serilogLogger.Error(ex, "❌ Error during API access check for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during API access verification");
        }
    }

    private bool ShouldSkipApiAccessCheck(string path)
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

    private Models.ApiAccessLevel ConvertApiAccessLevel(ERP.Contracts.Identity.ApiAccessLevel grpcLevel)
    {
        return grpcLevel switch
        {
            ERP.Contracts.Identity.ApiAccessLevel.ApiNone => Models.ApiAccessLevel.None,
            ERP.Contracts.Identity.ApiAccessLevel.ApiReadOnly => Models.ApiAccessLevel.ReadOnly,
            ERP.Contracts.Identity.ApiAccessLevel.ApiLimited => Models.ApiAccessLevel.Limited,
            ERP.Contracts.Identity.ApiAccessLevel.ApiStandard => Models.ApiAccessLevel.Standard,
            ERP.Contracts.Identity.ApiAccessLevel.ApiPremium => Models.ApiAccessLevel.Premium,
            ERP.Contracts.Identity.ApiAccessLevel.ApiAdmin => Models.ApiAccessLevel.Admin,
            _ => Models.ApiAccessLevel.None
        };
    }
}
