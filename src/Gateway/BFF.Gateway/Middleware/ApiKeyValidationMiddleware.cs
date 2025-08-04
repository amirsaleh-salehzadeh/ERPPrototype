using BFF.Gateway.Services;
using BFF.Gateway.Models;
using ERP.Contracts.Identity;
using System.Diagnostics;
using Serilog;
using Serilog.Context;

namespace BFF.Gateway.Middleware;

/// <summary>
/// First middleware in the security pipeline - validates API keys and initializes security context
/// </summary>
public class ApiKeyValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyValidationMiddleware> _logger;
    private readonly IGrpcClientService _grpcClientService;
    private readonly IServiceMappingService _serviceMappingService;
    private static readonly Serilog.ILogger _serilogLogger = Log.ForContext<ApiKeyValidationMiddleware>();

    public ApiKeyValidationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyValidationMiddleware> logger,
        IGrpcClientService grpcClientService,
        IServiceMappingService serviceMappingService)
    {
        _next = next;
        _logger = logger;
        _grpcClientService = grpcClientService;
        _serviceMappingService = serviceMappingService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        // Initialize security context
        var serviceMapping = _serviceMappingService.GetServiceMapping(context.Request.Path.Value ?? string.Empty);
        var securityContext = new SecurityContext
        {
            RequestId = Guid.NewGuid().ToString(),
            Path = context.Request.Path.Value ?? string.Empty,
            Method = context.Request.Method,
            ServiceName = serviceMapping?.ServiceName ?? "Unknown"
        };

        // Add security context to request items for other middleware
        context.Items["SecurityContext"] = securityContext;

        // Skip validation for certain paths
        if (ShouldSkipValidation(context.Request.Path))
        {
            _serilogLogger.Information("ℹ️ API Key Validation: Skipped for public path: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Extract API key from header
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            var decision = new SecurityDecision
            {
                Stage = "ApiKeyValidation",
                IsAllowed = false,
                Reason = "Missing API key",
                Details = "API key header not provided",
                Duration = stopwatch.Elapsed
            };
            securityContext.Decisions.Add(decision);

            _serilogLogger.Warning("🚫 API Key Validation: Missing API key for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API key is required");
            return;
        }

        try
        {
            // Validate API key with Identity service via gRPC
            var request = new ValidateApiKeyRequest
            {
                ApiKey = apiKey,
                ServiceName = ExtractServiceName(context.Request.Path),
                Endpoint = context.Request.Path.ToString()
            };

            using (LogContext.PushProperty("Stage", "ApiKeyValidation"))
            using (LogContext.PushProperty("ServiceName", securityContext.ServiceName))
            using (LogContext.PushProperty("Endpoint", securityContext.Path))
            {
                _serilogLogger.Information("🔑 Validating API key for endpoint: {Method} {Path} - Service: {ServiceName}",
                    securityContext.Method, request.Endpoint, request.ServiceName);

                var validationResult = await _grpcClientService.ValidateApiKeyAsync(request);
                stopwatch.Stop();

                // Create security decision
                var decision = new SecurityDecision
                {
                    Stage = "ApiKeyValidation",
                    IsAllowed = validationResult.IsValid,
                    Reason = validationResult.IsValid ? "API key validated successfully" : validationResult.ErrorMessage,
                    Details = validationResult.IsValid ? $"Client: {validationResult.ClientName}" : validationResult.ErrorMessage,
                    Duration = stopwatch.Elapsed
                };
                securityContext.Decisions.Add(decision);

                if (!validationResult.IsValid)
                {
                    using (LogContext.PushProperty("ErrorMessage", validationResult.ErrorMessage))
                    {
                        _serilogLogger.Warning("🚫 API Key Validation Failed: {ErrorMessage}", validationResult.ErrorMessage);
                    }

                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync($"Invalid API key: {validationResult.ErrorMessage}");
                    return;
                }

                // Update security context with API key info
                securityContext.ApiKey = new ApiKeyInfo
                {
                    KeyId = validationResult.KeyId,
                    MaskedKey = MaskApiKey(apiKey),
                    IsValid = validationResult.IsValid,
                    ClientName = validationResult.ClientName,
                    AccessLevel = ConvertApiAccessLevel(validationResult.AccessLevel),
                    AllowedServices = validationResult.AllowedServices.ToList(),
                    AllowedEndpoints = validationResult.AllowedEndpoints.ToList(),
                    ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(validationResult.ExpiresAt).DateTime
                };

                using (LogContext.PushProperty("ApiKeyId", validationResult.KeyId))
                using (LogContext.PushProperty("ClientName", validationResult.ClientName))
                using (LogContext.PushProperty("AccessLevel", validationResult.AccessLevel.ToString()))
                using (LogContext.PushProperty("Duration", stopwatch.ElapsedMilliseconds))
                {
                    _serilogLogger.Information("✅ API Key Validated: {ClientName} ({KeyId}) - Level: {AccessLevel} - Duration: {Duration}ms",
                        validationResult.ClientName, validationResult.KeyId, validationResult.AccessLevel, stopwatch.ElapsedMilliseconds);
                }
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error validating API key for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during authentication");
        }
    }

    private bool ShouldSkipValidation(string path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/api/gateway/services",
            "/swagger",
            "/scalar",
            "/api/docs/scalar"  // Allow Scalar documentation through gateway
            // Documentation endpoints are public - users can view APIs freely
            // But actual API requests from Scalar will require authentication
        };

        return skipPaths.Any(skipPath => path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase));
    }

    private string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length <= 8)
            return "***";

        return apiKey.Substring(0, Math.Min(8, apiKey.Length)) + "***";
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

    private string ExtractServiceName(string path)
    {
        if (path.StartsWith("/api/weather")) return "WeatherService";
        if (path.StartsWith("/api/orders")) return "OrderService";
        if (path.StartsWith("/api/inventory")) return "InventoryService";
        if (path.StartsWith("/api/customers")) return "CustomerService";
        if (path.StartsWith("/api/finance")) return "FinanceService";
        if (path.StartsWith("/api/docs")) return "DocumentationService";

        return "Unknown";
    }

}
