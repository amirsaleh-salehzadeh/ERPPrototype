using Grpc.Core;
using Playground.WeatherService.Contracts;
using Google.Protobuf.WellKnownTypes;

namespace Playground.WeatherService.Services;

/// <summary>
/// gRPC service implementation for Weather Service
/// Handles weather-related requests via gRPC
/// </summary>
public class WeatherGrpcService : Playground.WeatherService.Contracts.WeatherService.WeatherServiceBase
{
    private readonly ILogger<WeatherGrpcService> _logger;
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public WeatherGrpcService(ILogger<WeatherGrpcService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns a hello message via gRPC
    /// </summary>
    public override Task<HelloResponse> GetHello(HelloRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🌤️ gRPC GetHello called for user: {UserName}", request.UserName);

            var response = new HelloResponse
            {
                Message = $"Hello {request.UserName} from Weather Service via gRPC! 🌤️",
                Service = "WeatherService",
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            _logger.LogInformation("✅ gRPC Hello response sent to user: {UserName}", request.UserName);
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC GetHello");
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }

    /// <summary>
    /// Returns weather forecast via gRPC
    /// </summary>
    public override Task<WeatherForecastResponse> GetWeatherForecast(WeatherForecastRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🌦️ gRPC GetWeatherForecast called for user: {UserName}, days: {Days}", 
                request.UserName, request.Days);

            var days = request.Days > 0 ? request.Days : 5; // Default to 5 days
            var forecasts = Enumerable.Range(1, days).Select(index =>
            {
                var date = DateTime.Today.AddDays(index);
                var temperatureC = Random.Shared.Next(-20, 55);

                return new Playground.WeatherService.Contracts.WeatherForecast
                {
                    Date = Timestamp.FromDateTime(date.ToUniversalTime()),
                    TemperatureC = temperatureC,
                    TemperatureF = 32 + (int)(temperatureC / 0.5556),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)],
                    Humidity = Random.Shared.Next(30, 90),
                    WindSpeed = Random.Shared.NextDouble() * 20,
                    WindDirection = GetRandomWindDirection()
                };
            }).ToArray();

            var response = new WeatherForecastResponse
            {
                GeneratedBy = $"WeatherService gRPC for {request.UserName}",
                GeneratedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            response.Forecasts.AddRange(forecasts);

            _logger.LogInformation("✅ gRPC Weather forecast generated for user: {UserName}, {Count} days", 
                request.UserName, forecasts.Length);

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC GetWeatherForecast");
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }

    /// <summary>
    /// Returns health status via gRPC
    /// </summary>
    public override Task<HealthResponse> GetHealth(HealthRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🏥 gRPC GetHealth called");

            var response = new HealthResponse
            {
                Status = "Healthy",
                Service = "WeatherService",
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            _logger.LogInformation("✅ gRPC Health check completed");
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC GetHealth");
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }

    private static string GetRandomWindDirection()
    {
        var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return directions[Random.Shared.Next(directions.Length)];
    }
}
