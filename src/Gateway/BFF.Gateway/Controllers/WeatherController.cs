using Microsoft.AspNetCore.Mvc;
using BFF.Gateway.Services;
using ERP.Contracts.Weather;
using Google.Protobuf.WellKnownTypes;

namespace BFF.Gateway.Controllers;

[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly IGrpcClientService _grpcClientService;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(IGrpcClientService grpcClientService, ILogger<WeatherController> logger)
    {
        _grpcClientService = grpcClientService;
        _logger = logger;
    }

    [HttpGet("hello")]
    public async Task<IActionResult> GetHello()
    {
        try
        {
            var request = new HelloRequest
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "",
            };
            
            // Add permissions
            var permissions = Request.Headers["X-User-Permissions"].FirstOrDefault()?.Split(',') ?? Array.Empty<string>();
            request.Permissions.AddRange(permissions);

            var response = await _grpcClientService.GetWeatherHelloAsync(request);
            
            return Ok(new
            {
                message = response.Message,
                service = response.Service,
                timestamp = response.Timestamp?.ToDateTime()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Weather service GetHello");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("forecast")]
    public async Task<IActionResult> GetWeatherForecast([FromQuery] int days = 5)
    {
        try
        {
            var request = new WeatherForecastRequest
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "",
                Days = days
            };
            
            // Add permissions
            var permissions = Request.Headers["X-User-Permissions"].FirstOrDefault()?.Split(',') ?? Array.Empty<string>();
            request.Permissions.AddRange(permissions);

            var response = await _grpcClientService.GetWeatherForecastAsync(request);
            
            return Ok(new
            {
                forecasts = response.Forecasts.Select(f => new
                {
                    date = f.Date?.ToDateTime(),
                    temperatureC = f.TemperatureC,
                    temperatureF = f.TemperatureF,
                    summary = f.Summary,
                    humidity = f.Humidity,
                    windSpeed = f.WindSpeed,
                    windDirection = f.WindDirection
                }),
                generatedBy = response.GeneratedBy,
                generatedAt = response.GeneratedAt?.ToDateTime()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Weather service GetWeatherForecast");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var request = new ERP.Contracts.Weather.HealthRequest();
            var response = await _grpcClientService.GetWeatherHealthAsync(request);
            
            return Ok(new
            {
                status = response.Status,
                service = response.Service,
                timestamp = response.Timestamp?.ToDateTime()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Weather service GetHealth");
            return StatusCode(500, "Internal server error");
        }
    }
}
