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
                _logger.LogWarning("🚫 Invalid API key for path: {Path} - {Error}", context.Request.Path, response.ErrorMessage);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync($"Invalid API key: {response.ErrorMessage}");
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

    private bool ShouldSkipValidation(string path)
    {
        var skipPaths = new[]
        {
            "/health",
            "/api/gateway/services",
            "/swagger",
            "/scalar",
            "/api/docs"  // Allow all documentation endpoints through gateway
            // Documentation endpoints are public - users can view APIs freely
            // But actual API requests from Scalar will require authentication
        };

        return skipPaths.Any(skipPath => path.StartsWith(skipPath, StringComparison.OrdinalIgnoreCase));
    }

    private string ExtractServiceName(string path)
    {
        if (path.StartsWith("/api/weather")) return "WeatherService";
        if (path.StartsWith("/api/docs")) return "DocumentationService";

        return "Unknown";
    }

}
