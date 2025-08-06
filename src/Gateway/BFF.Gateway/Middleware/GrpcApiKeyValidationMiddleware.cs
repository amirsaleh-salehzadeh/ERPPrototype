using BFF.Gateway.Services;
using ERP.Contracts.Identity;

namespace BFF.Gateway.Middleware;

public class GrpcApiKeyValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GrpcApiKeyValidationMiddleware> _logger;
    private readonly IGrpcClientService _grpcClientService;

    public GrpcApiKeyValidationMiddleware(RequestDelegate next, ILogger<GrpcApiKeyValidationMiddleware> logger, IGrpcClientService grpcClientService)
    {
        _next = next;
        _logger = logger;
        _grpcClientService = grpcClientService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for certain paths
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Extract API key from header
        var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("🚫 Missing API key for path: {Path}", context.Request.Path);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API key is required");
            return;
        }

        try
        {
            // Validate API key with Identity service via gRPC
            _logger.LogInformation("🔍 Validating API key via gRPC for service: {ServiceName}", ExtractServiceName(context.Request.Path));

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
                _logger.LogWarning("🚫 Invalid API key for path: {Path} - {Error}", context.Request.Path, errorMessage);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync($"Invalid API key: {errorMessage}");
                return;
            }

            // Add user information to request headers for downstream services
            context.Request.Headers["X-User-Id"] = response.UserId ?? string.Empty;
            context.Request.Headers["X-User-Name"] = response.UserName ?? string.Empty;
            context.Request.Headers["X-User-Permissions"] = string.Join(",", response.Permissions);

            _logger.LogInformation("✅ API key validated via gRPC for user: {UserName}, path: {Path}", response.UserName, context.Request.Path);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Identity service via gRPC");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Internal server error during authentication");
        }
    }

    private static bool ShouldSkipValidation(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        
        // Skip validation for health checks, documentation, and gateway endpoints
        return pathValue.Contains("/health") ||
               pathValue.Contains("/swagger") ||
               pathValue.Contains("/scalar") ||
               pathValue.Contains("/openapi") ||
               pathValue.StartsWith("/service-mappings") ||
               pathValue == "/" ||
               pathValue == "";
    }

    private static string ExtractServiceName(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;
        
        // Extract service name from path like /api/weather/... -> WeatherService
        if (pathValue.StartsWith("/api/weather"))
            return "WeatherService";
        if (pathValue.StartsWith("/api/identity"))
            return "IdentityService";
        if (pathValue.StartsWith("/api/orders"))
            return "OrderService";
        if (pathValue.StartsWith("/api/inventory"))
            return "InventoryService";
        if (pathValue.StartsWith("/api/customers"))
            return "CustomerService";
        if (pathValue.StartsWith("/api/finance"))
            return "FinanceService";
            
        return "UnknownService";
    }
}
