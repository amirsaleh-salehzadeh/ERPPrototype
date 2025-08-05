using BFF.Gateway.Services;
using ERP.Contracts.Identity;
using System.Text.Json;
using System.Text;

namespace BFF.Gateway.Middleware;

public class ApiKeyValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyValidationMiddleware> _logger;
    private readonly HttpClient _httpClient;

    public ApiKeyValidationMiddleware(RequestDelegate next, ILogger<ApiKeyValidationMiddleware> logger, IHttpClientFactory httpClientFactory)
    {
        _next = next;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
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
            // Validate API key with Identity service via HTTP REST
            _logger.LogInformation("🔍 Validating API key via HTTP for service: {ServiceName}", ExtractServiceName(context.Request.Path));

            var requestPayload = new
            {
                ApiKey = apiKey,
                ServiceName = ExtractServiceName(context.Request.Path),
                Endpoint = context.Request.Path.ToString()
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.PostAsync("http://localhost:5007/validate", content);
            var responseContent = await httpResponse.Content.ReadAsStringAsync();

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("🚫 Identity service returned error: {StatusCode} - {Content}", httpResponse.StatusCode, responseContent);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }

            var validationResult = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (!validationResult.GetProperty("isValid").GetBoolean())
            {
                var errorMessage = validationResult.TryGetProperty("errorMessage", out var errorProp) ? errorProp.GetString() : "Unknown error";
                _logger.LogWarning("🚫 Invalid API key for path: {Path} - {Error}", context.Request.Path, errorMessage);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync($"Invalid API key: {errorMessage}");
                return;
            }

            // Add user information to request headers for downstream services
            var userId = validationResult.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetString() : "";
            var userName = validationResult.TryGetProperty("userName", out var userNameProp) ? userNameProp.GetString() : "";
            var permissions = validationResult.TryGetProperty("permissions", out var permissionsProp) ?
                string.Join(",", permissionsProp.EnumerateArray().Select(p => p.GetString())) : "";

            context.Request.Headers["X-User-Id"] = userId ?? string.Empty;
            context.Request.Headers["X-User-Name"] = userName ?? string.Empty;
            context.Request.Headers["X-User-Permissions"] = permissions;

            _logger.LogInformation("✅ API key validated via HTTP for user: {UserName}, path: {Path}", userName, context.Request.Path);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Identity service via HTTP");
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
