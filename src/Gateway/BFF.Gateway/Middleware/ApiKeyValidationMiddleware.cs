using System.Text;
using System.Text.Json;

namespace BFF.Gateway.Middleware;

public class ApiKeyValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyValidationMiddleware> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _identityServiceUrl;

    public ApiKeyValidationMiddleware(RequestDelegate next, ILogger<ApiKeyValidationMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _httpClient = new HttpClient();

        // Use REST API for Identity service (simplified for demo)
        _identityServiceUrl = configuration.GetValue<string>("IdentityService:RestUrl") ?? "http://localhost:5007";
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
            // Validate API key with Identity service via REST API
            var requestPayload = new
            {
                ApiKey = apiKey,
                ServiceName = ExtractServiceName(context.Request.Path),
                Endpoint = context.Request.Path.ToString()
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_identityServiceUrl}/validate", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("🚫 Identity service error for path: {Path} - Status: {Status}", context.Request.Path, response.StatusCode);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Authentication service unavailable");
                return;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var validationResult = JsonSerializer.Deserialize<ValidationResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (validationResult == null || !validationResult.IsValid)
            {
                _logger.LogWarning("🚫 Invalid API key for path: {Path} - {Error}", context.Request.Path, validationResult?.ErrorMessage ?? "Unknown error");
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync($"Invalid API key: {validationResult?.ErrorMessage ?? "Unknown error"}");
                return;
            }

            // Add user information to request headers for downstream services
            context.Request.Headers["X-User-Id"] = validationResult.UserId;
            context.Request.Headers["X-User-Name"] = validationResult.UserName;
            context.Request.Headers["X-User-Permissions"] = string.Join(",", validationResult.Permissions ?? Array.Empty<string>());

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
            "/api/gateway/services"
            // Removed /swagger and /scalar - they should be protected too
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

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Response model for validation
public class ValidationResponse
{
    public bool IsValid { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
