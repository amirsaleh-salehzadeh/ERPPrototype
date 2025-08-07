using Microsoft.AspNetCore.Mvc;

namespace BFF.Gateway.Controllers;

[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(ILogger<WeatherController> logger)
    {
        _logger = logger;
    }

    [HttpGet("forecast")]
    public async Task<IActionResult> GetWeatherForecast([FromQuery] int days = 5)
    {
        try
        {
            // Print request headers
            _logger.LogInformation("=== GetWeatherForecast Request Headers ===");
            foreach (var header in Request.Headers)
            {
                _logger.LogInformation("Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value.ToArray()));
            }

            // Print request cookies
            _logger.LogInformation("=== GetWeatherForecast Request Cookies ===");
            foreach (var cookie in Request.Cookies)
            {
                _logger.LogInformation("Cookie: {Key} = {Value}", cookie.Key, cookie.Value);
            }

            // Print query parameters
            _logger.LogInformation("=== GetWeatherForecast Query Parameters ===");
            foreach (var query in Request.Query)
            {
                _logger.LogInformation("Query: {Key} = {Value}", query.Key, string.Join(", ", query.Value.ToArray()));
            }

            // Print request body info
            _logger.LogInformation("=== GetWeatherForecast Request Info ===");
            _logger.LogInformation("Method: {Method}", Request.Method);
            _logger.LogInformation("Path: {Path}", Request.Path);
            _logger.LogInformation("QueryString: {QueryString}", Request.QueryString);
            _logger.LogInformation("ContentType: {ContentType}", Request.ContentType);
            _logger.LogInformation("ContentLength: {ContentLength}", Request.ContentLength);
            _logger.LogInformation("Host: {Host}", Request.Host);
            _logger.LogInformation("Scheme: {Scheme}", Request.Scheme);

            // Extract user information from headers
            var userId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "anonymous";
            var userName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "unknown";
            var permissions = Request.Headers["X-User-Permissions"].FirstOrDefault()?.Split(',') ?? Array.Empty<string>();

            _logger.LogInformation("=== Extracted User Information ===");
            _logger.LogInformation("UserId: {UserId}", userId);
            _logger.LogInformation("UserName: {UserName}", userName);
            _logger.LogInformation("Permissions: {Permissions}", string.Join(", ", permissions));

            // For now, return a mock response since the gRPC service isn't set up
            var mockResponse = new
            {
                message = "Headers and cookies logged successfully",
                userId = userId,
                userName = userName,
                permissions = permissions,
                requestedDays = days,
                timestamp = DateTime.UtcNow,
                note = "This is a mock response - check logs for header/cookie information"
            };

            return Ok(mockResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GetWeatherForecast request");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("test")]
    public IActionResult TestEndpoint()
    {
        _logger.LogInformation("=== Test Endpoint Called ===");
        _logger.LogInformation("This endpoint is working and will log all request information");
        
        return Ok(new { 
            message = "Test endpoint working", 
            timestamp = DateTime.UtcNow,
            note = "Check the logs for header and cookie information when calling /api/weather/forecast"
        });
    }
}
