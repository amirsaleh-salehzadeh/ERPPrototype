using Microsoft.AspNetCore.Mvc;
using BFF.Gateway.Services.Security;
using System.Text.Json;

namespace BFF.Gateway.Controllers;

[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly ISecureHttpClientService _secureHttpClient;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(ISecureHttpClientService secureHttpClient, ILogger<WeatherController> logger)
    {
        _secureHttpClient = secureHttpClient;
        _logger = logger;
    }

    [HttpGet("forecast")]
    public async Task<IActionResult> GetWeatherForecast([FromQuery] int days = 5)
    {
        try
        {
            // Log request details for debugging
            LogRequestDetails();

            // Create request payload
            var request = new
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "Unknown User",
                Days = Math.Max(1, Math.Min(days, 30)), // Ensure reasonable range
                RequestId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("🌤️ Requesting weather forecast for {Days} days (User: {UserName})", 
                request.Days, request.UserName);

            // Make secure request to weather service
            var response = await _secureHttpClient.GetAsync("weather", 
                $"api/WeatherForecast?days={request.Days}");

            if (string.IsNullOrEmpty(response))
            {
                _logger.LogWarning("Empty response received from Weather service");
                return StatusCode(503, new { 
                    error = "Weather service unavailable", 
                    message = "The weather service did not return any data" 
                });
            }

            // Parse and return the response
            var weatherData = JsonSerializer.Deserialize<JsonElement>(response);
            
            return Ok(new
            {
                data = weatherData,
                meta = new
                {
                    requestId = request.RequestId,
                    requestedBy = request.UserName,
                    requestedAt = request.Timestamp,
                    daysRequested = request.Days,
                    service = "weather",
                    gateway = "BFF.Gateway"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Weather service for forecast");
            return StatusCode(500, new { 
                error = "Internal server error", 
                message = "Failed to retrieve weather forecast" 
            });
        }
    }

    [HttpGet("hello")]
    public async Task<IActionResult> GetHello()
    {
        try
        {
            LogRequestDetails();

            var request = new
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "Unknown User",
                RequestId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("👋 Hello request from user: {UserName}", request.UserName);

            // Make secure request to weather service
            var response = await _secureHttpClient.GetAsync("weather", "api/WeatherForecast/hello");

            if (string.IsNullOrEmpty(response))
            {
                return StatusCode(503, new { 
                    error = "Weather service unavailable",
                    message = "The weather service did not respond"
                });
            }

            var helloData = JsonSerializer.Deserialize<JsonElement>(response);
            
            return Ok(new
            {
                data = helloData,
                meta = new
                {
                    requestId = request.RequestId,
                    requestedBy = request.UserName,
                    requestedAt = request.Timestamp,
                    service = "weather",
                    gateway = "BFF.Gateway"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Weather service for hello");
            return StatusCode(500, new { 
                error = "Internal server error", 
                message = "Failed to get hello response" 
            });
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            LogRequestDetails();

            _logger.LogInformation("🏥 Health check request for Weather service");

            var response = await _secureHttpClient.GetAsync("weather", "health");

            if (string.IsNullOrEmpty(response))
            {
                return StatusCode(503, new { 
                    status = "Unhealthy",
                    service = "weather",
                    message = "Weather service is not responding" 
                });
            }

            var healthData = JsonSerializer.Deserialize<JsonElement>(response);
            
            return Ok(new
            {
                data = healthData,
                meta = new
                {
                    checkedAt = DateTime.UtcNow,
                    service = "weather",
                    gateway = "BFF.Gateway",
                    secure = true
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Weather service health");
            return StatusCode(500, new { 
                status = "Error",
                service = "weather",
                error = "Failed to check health status" 
            });
        }
    }

    private void LogRequestDetails()
    {
        _logger.LogInformation("📋 === REQUEST DETAILS ===");
        _logger.LogInformation("🔗 Request URL: {Method} {Path}{QueryString}", 
            Request.Method, Request.Path, Request.QueryString);
        
        _logger.LogInformation("📄 Headers ({Count}):", Request.Headers.Count);
        foreach (var header in Request.Headers)
        {
            var value = header.Key.ToLowerInvariant().Contains("authorization") || 
                       header.Key.ToLowerInvariant().Contains("cookie") ||
                       header.Key.ToLowerInvariant().Contains("token")
                       ? "[REDACTED]" 
                       : string.Join(", ", header.Value.ToArray());
            _logger.LogInformation("  • {HeaderName}: {HeaderValue}", header.Key, value);
        }

        _logger.LogInformation("🍪 Cookies ({Count}):", Request.Cookies.Count);
        foreach (var cookie in Request.Cookies)
        {
            _logger.LogInformation("  • {CookieName}: [REDACTED]", cookie.Key);
        }

        _logger.LogInformation("🌐 Remote IP: {RemoteIp}", Request.HttpContext.Connection.RemoteIpAddress);
        _logger.LogInformation("🔒 Is HTTPS: {IsHttps}", Request.IsHttps);
        _logger.LogInformation("📋 === END REQUEST DETAILS ===");
    }
}
