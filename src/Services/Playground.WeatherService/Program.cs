using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddLogging();

// Add gRPC services
builder.Services.AddGrpc();

// Add CORS for Documentation service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDocumentation", policy =>
    {
        policy.WithOrigins("http://localhost:5002", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Add OpenAPI and Scalar documentation
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Weather Service API";
        options.Theme = Scalar.AspNetCore.ScalarTheme.BluePlanet;
        options.ShowSidebar = true;
    });
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowDocumentation");

// Configure gRPC endpoint
app.MapGrpcService<Playground.WeatherService.Services.WeatherGrpcService>();

// Middleware to log incoming requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Check if request came through gateway
    var serviceName = context.Request.Headers["X-Service-Name"].FirstOrDefault();
    if (!string.IsNullOrEmpty(serviceName))
    {
        logger.LogInformation("🎯 WeatherService received request from gateway - Service: {ServiceName}", serviceName);
    }
    else
    {
        logger.LogInformation("🎯 WeatherService received direct request");
    }

    await next();
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (ILogger<Program> logger) =>
{
    logger.LogInformation("🌤️ Generating weather forecast data");

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();

    logger.LogInformation("🌤️ Weather forecast generated with {Count} entries", forecast.Length);
    return forecast;
})
.WithName("GetWeatherForecast")
.WithTags("Weather")
.WithSummary("Get weather forecast")
.WithDescription("Returns a 5-day weather forecast with temperature and weather conditions")
.WithOpenApi();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Weather Service!", Service = "WeatherService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Weather Service")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Service = "WeatherService", Timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Health check endpoint")
.WithDescription("Returns the health status of the Weather Service")
.WithOpenApi();

// OpenAPI specification endpoint
app.MapGet("/swagger/v1/swagger.json", () =>
{
    var openApiJson = """
    {
        "openapi": "3.0.1",
        "info": {
            "title": "Weather Service API",
            "version": "v1",
            "description": "Weather forecast service providing 5-day weather predictions"
        },
        "servers": [
            {
                "url": "http://localhost:5001",
                "description": "Weather Service"
            }
        ],
        "paths": {
            "/weatherforecast": {
                "get": {
                    "tags": ["Weather"],
                    "summary": "Get weather forecast",
                    "description": "Returns a 5-day weather forecast with temperature and weather conditions",
                    "responses": {
                        "200": {
                            "description": "Weather forecast data",
                            "content": {
                                "application/json": {
                                    "schema": {
                                        "type": "array",
                                        "items": {
                                            "$ref": "#/components/schemas/WeatherForecast"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            "/hello": {
                "get": {
                    "tags": ["General"],
                    "summary": "Hello World endpoint",
                    "description": "Returns a hello message from the Weather Service",
                    "responses": {
                        "200": {
                            "description": "Hello message",
                            "content": {
                                "application/json": {
                                    "schema": {
                                        "type": "object",
                                        "properties": {
                                            "Message": {"type": "string"},
                                            "Service": {"type": "string"},
                                            "Timestamp": {"type": "string", "format": "date-time"}
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            "/health": {
                "get": {
                    "tags": ["Health"],
                    "summary": "Health check endpoint",
                    "description": "Returns the health status of the Weather Service",
                    "responses": {
                        "200": {
                            "description": "Service health status",
                            "content": {
                                "application/json": {
                                    "schema": {
                                        "type": "object",
                                        "properties": {
                                            "Status": {"type": "string"},
                                            "Service": {"type": "string"},
                                            "Timestamp": {"type": "string", "format": "date-time"}
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        },
        "components": {
            "schemas": {
                "WeatherForecast": {
                    "type": "object",
                    "properties": {
                        "Date": {"type": "string", "format": "date", "example": "2024-08-05"},
                        "TemperatureC": {"type": "integer", "format": "int32", "example": 25},
                        "TemperatureF": {"type": "integer", "format": "int32", "example": 77},
                        "Summary": {"type": "string", "nullable": true, "example": "Warm"}
                    }
                }
            }
        }
    }
    """;

    return Results.Content(openApiJson, "application/json");
})
.WithName("GetOpenApiSpec")
.WithTags("OpenAPI");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
