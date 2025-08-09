using ERP.IdentityService.Services;
using StackExchange.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/2 support (required for gRPC)
builder.WebHost.ConfigureKestrel(options =>
{
    // Listen on HTTP/1.1 for REST API
    options.ListenLocalhost(5007, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1;
    });

    // Listen on HTTP/2 for gRPC (separate port)
    options.ListenLocalhost(5008, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddLogging();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Identity Service is running"))
    .AddCheck("redis", () =>
    {
        try
        {
            var redis = builder.Services.BuildServiceProvider().GetService<IConnectionMultiplexer>();
            if (redis?.IsConnected == true)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Redis is connected");
            }
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Redis is not connected");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Redis check failed", ex);
        }
    });

// Add Redis connection (optional)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("🔗 Attempting to connect to Redis at: {ConnectionString}", redisConnectionString);
        var connection = ConnectionMultiplexer.Connect(redisConnectionString);
        logger.LogInformation("✅ Successfully connected to Redis");
        return connection;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "⚠️ Failed to connect to Redis, will use in-memory storage for demo");
        return null!; // Return null, the HybridApiKeyService will handle this
    }
});

// Add gRPC services
builder.Services.AddGrpc();

// Add Identity services - Use Hybrid service that works with or without Redis
builder.Services.AddSingleton<IApiKeyService, HybridApiKeyService>();

// Add gRPC service implementation
builder.Services.AddScoped<IdentityGrpcService>();
builder.Services.AddSingleton<ApiKeySeederService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
// Using OpenAPI without Swagger UI

app.UseHttpsRedirection();

// Middleware to log incoming requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Check if request came through gateway
    var serviceName = context.Request.Headers["X-Service-Name"].FirstOrDefault();
    if (!string.IsNullOrEmpty(serviceName))
    {
        logger.LogInformation("🔐 IdentityService received request from gateway - Service: {ServiceName}", serviceName);
    }
    else
    {
        logger.LogInformation("🔐 IdentityService received direct request");
    }

    await next();
});

// Configure gRPC endpoint
app.MapGrpcService<IdentityGrpcService>();

// Health Check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            service = "IdentityService",
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || check.Name == "self",
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            service = "IdentityService",
            ready = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
            timestamp = DateTime.UtcNow
        });
        await context.Response.WriteAsync(result);
    }
});

// Metrics endpoint for Prometheus
app.MapGet("/metrics", () =>
{
    var metrics = new List<string>
    {
        "# HELP identity_service_health Health status of the Identity Service",
        "# TYPE identity_service_health gauge",
        "identity_service_health 1",
        "",
        "# HELP identity_service_uptime_seconds Uptime of the Identity Service in seconds",
        "# TYPE identity_service_uptime_seconds counter",
        $"identity_service_uptime_seconds {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
        "",
        "# HELP identity_service_requests_total Total number of requests",
        "# TYPE identity_service_requests_total counter",
        "identity_service_requests_total 0"
    };
    
    return Results.Text(string.Join("\n", metrics), "text/plain");
})
.WithName("Metrics")
.WithTags("Monitoring")
.ExcludeFromDescription();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Identity Service!", Service = "IdentityService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Identity Service")
.WithOpenApi();

// REST API endpoints for API Key management
app.MapPost("/api-keys", (CreateApiKeyRequest request, IApiKeyService apiKeyService, ILogger<Program> logger) =>
{
    logger.LogInformation("🔑 Creating new API key for user: {UserName}", request.UserName);
    var result = apiKeyService.CreateApiKey(request.UserName, request.Description, request.Permissions, request.ExpiresInDays);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
.WithName("CreateApiKey")
.WithTags("API Keys")
.WithSummary("Create a new API key")
.WithDescription("Creates a new API key for authentication")
.WithOpenApi();

app.MapPost("/validate", (ValidateApiKeyRequest request, IApiKeyService apiKeyService, ILogger<Program> logger) =>
{
    logger.LogInformation("🔍 Validating API key for service: {ServiceName}", request.ServiceName);
    var result = apiKeyService.ValidateApiKey(request.ApiKey, request.ServiceName, request.Endpoint);
    return Results.Ok(result);
})
.WithName("ValidateApiKey")
.WithTags("API Keys")
.WithSummary("Validate an API key")
.WithDescription("Validates an API key and returns user information and permissions")
.WithOpenApi();

app.MapGet("/api-keys/{apiKey}/info", (string apiKey, IApiKeyService apiKeyService, ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Getting API key info");
    var result = apiKeyService.GetApiKeyInfo(apiKey);
    return result.IsActive ? Results.Ok(result) : Results.NotFound("API key not found or inactive");
})
.WithName("GetApiKeyInfo")
.WithTags("API Keys")
.WithSummary("Get API key information")
.WithDescription("Returns information about an API key")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Service = "IdentityService", Timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Health check endpoint")
.WithDescription("Returns the health status of the Identity Service")
.WithOpenApi();

// Seed API keys endpoints
app.MapPost("/seed/random/{count:int?}", async (int? count, ApiKeySeederService seeder, ILogger<Program> logger) =>
{
    var keyCount = count ?? 20;
    logger.LogInformation("🌱 Seeding {Count} random API keys", keyCount);
    await seeder.SeedRandomApiKeysAsync(keyCount);
    return Results.Ok(new { Message = $"Successfully seeded {keyCount} random API keys", Count = keyCount });
})
.WithName("SeedRandomApiKeys")
.WithTags("Seeding")
.WithSummary("Seed random API keys")
.WithDescription("Creates random API keys for testing purposes")
.WithOpenApi();

app.MapPost("/seed/predefined", async (ApiKeySeederService seeder, ILogger<Program> logger) =>
{
    logger.LogInformation("🔧 Seeding predefined API keys");
    await seeder.CreatePredefinedApiKeysAsync();
    return Results.Ok(new { Message = "Successfully created predefined API keys" });
})
.WithName("SeedPredefinedApiKeys")
.WithTags("Seeding")
.WithSummary("Seed predefined API keys")
.WithDescription("Creates predefined API keys for testing")
.WithOpenApi();

// OpenAPI specification endpoint
app.MapGet("/swagger/v1/swagger.json", () =>
{
    var openApiJson = """
    {
        "openapi": "3.0.1",
        "info": {
            "title": "Identity Service API",
            "version": "v1",
            "description": "API key management and validation service for ERP system"
        },
        "servers": [
            {
                "url": "http://localhost:5007",
                "description": "Identity Service"
            }
        ],
        "paths": {
            "/hello": {
                "get": {
                    "tags": ["General"],
                    "summary": "Hello World endpoint",
                    "description": "Returns a hello message from the Identity Service",
                    "responses": {
                        "200": {
                            "description": "Success",
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
            "/api-keys": {
                "post": {
                    "tags": ["API Keys"],
                    "summary": "Create a new API key",
                    "description": "Creates a new API key for authentication",
                    "requestBody": {
                        "required": true,
                        "content": {
                            "application/json": {
                                "schema": {
                                    "$ref": "#/components/schemas/CreateApiKeyRequest"
                                }
                            }
                        }
                    },
                    "responses": {
                        "200": {
                            "description": "API key created successfully"
                        }
                    }
                }
            },
            "/validate": {
                "post": {
                    "tags": ["API Keys"],
                    "summary": "Validate an API key",
                    "description": "Validates an API key and returns user information and permissions",
                    "requestBody": {
                        "required": true,
                        "content": {
                            "application/json": {
                                "schema": {
                                    "$ref": "#/components/schemas/ValidateApiKeyRequest"
                                }
                            }
                        }
                    },
                    "responses": {
                        "200": {
                            "description": "Validation result"
                        }
                    }
                }
            },
            "/health": {
                "get": {
                    "tags": ["Health"],
                    "summary": "Health check endpoint",
                    "description": "Returns the health status of the Identity Service",
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
                "CreateApiKeyRequest": {
                    "type": "object",
                    "required": ["UserName", "Description", "Permissions", "ExpiresInDays"],
                    "properties": {
                        "UserName": {"type": "string", "example": "john.doe"},
                        "Description": {"type": "string", "example": "API key for testing"},
                        "Permissions": {"type": "array", "items": {"type": "string"}, "example": ["read", "write"]},
                        "ExpiresInDays": {"type": "integer", "example": 30}
                    }
                },
                "ValidateApiKeyRequest": {
                    "type": "object",
                    "required": ["ApiKey", "ServiceName", "Endpoint"],
                    "properties": {
                        "ApiKey": {"type": "string", "example": "your-api-key-here"},
                        "ServiceName": {"type": "string", "example": "WeatherService"},
                        "Endpoint": {"type": "string", "example": "/weatherforecast"}
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

// Startup seeding
var seederService = app.Services.GetRequiredService<ApiKeySeederService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("🚀 Starting Identity Service with Redis backend");

// Seed some initial API keys on startup
try
{
    await seederService.CreatePredefinedApiKeysAsync();
    await seederService.SeedRandomApiKeysAsync(10); // Create 10 random keys on startup
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Failed to seed initial API keys");
}

app.Run();

// Request/Response models
record CreateApiKeyRequest(string UserName, string Description, string[] Permissions, int ExpiresInDays);
record ValidateApiKeyRequest(string ApiKey, string ServiceName, string Endpoint);
