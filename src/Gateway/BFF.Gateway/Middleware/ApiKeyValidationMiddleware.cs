using BFF.Gateway.Services;
using ERP.Contracts.Identity;

namespace BFF.Gateway.Middleware;

public class ApiKeyValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyValidationMiddleware> _logger;
    private readonly IGrpcClientService _grpcClientService;

    public ApiKeyValidationMiddleware(RequestDelegate next, ILogger<ApiKeyValidationMiddleware> logger, IGrpcClientService grpcClientService)
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
            var request = new ValidateApiKeyRequest
            {
                ApiKey = apiKey,
                ServiceName = ExtractServiceName(context.Request.Path),
                Endpoint = context.Request.Path.ToString()
            };

            var validationResult = await _grpcClientService.ValidateApiKeyAsync(request);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("🚫 Invalid API key for path: {Path} - {Error}", context.Request.Path, validationResult.ErrorMessage);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync($"Invalid API key: {validationResult.ErrorMessage}");
                return;
            }

            // Add user information to request headers for downstream services
            context.Request.Headers["X-User-Id"] = validationResult.UserId;
            context.Request.Headers["X-User-Name"] = validationResult.UserName;
            context.Request.Headers["X-User-Permissions"] = string.Join(",", validationResult.Permissions);

            _logger.LogInformation("✅ API key validated for user: {UserName}, path: {Path}", validationResult.UserName, context.Request.Path);

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
