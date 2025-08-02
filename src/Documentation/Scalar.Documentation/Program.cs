using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ERP Prototype API Documentation",
        Version = "v1",
        Description = "Centralized API documentation for the ERP Prototype microservices architecture",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Development Team",
            Email = "dev@erpprototype.com"
        }
    });
});
builder.Services.AddLogging();

// Add HTTP client for fetching OpenAPI specs from other services
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Configure Swagger and Scalar
app.UseSwagger();

// Configure Scalar with proper OpenAPI spec
app.MapScalarApiReference(options =>
{
    options.Title = "ERP Prototype API Documentation - All Services";
    options.Theme = ScalarTheme.Purple;
    options.ShowSidebar = true;
    options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
});

// Add endpoint to serve aggregated OpenAPI spec
app.MapGet("/swagger/v1/swagger-aggregated.json", async (HttpClient httpClient, ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Generating aggregated OpenAPI specification");

    var aggregatedSpec = new
    {
        openapi = "3.0.1",
        info = new
        {
            title = "ERP Prototype - All Services API",
            version = "v1",
            description = "Aggregated API documentation for all ERP microservices"
        },
        servers = new[]
        {
            new { url = "http://localhost:5000", description = "API Gateway" }
        },
        paths = new Dictionary<string, object>(),
        components = new
        {
            schemas = new Dictionary<string, object>()
        }
    };

    var services = new[]
    {
        new { Name = "WeatherService", Url = "http://localhost:5001/swagger/v1/swagger.json", Prefix = "/api/weather" },
        new { Name = "OrderService", Url = "http://localhost:5003/swagger/v1/swagger.json", Prefix = "/api/orders" },
        new { Name = "InventoryService", Url = "http://localhost:5004/swagger/v1/swagger.json", Prefix = "/api/inventory" },
        new { Name = "CustomerService", Url = "http://localhost:5005/swagger/v1/swagger.json", Prefix = "/api/customers" },
        new { Name = "FinanceService", Url = "http://localhost:5006/swagger/v1/swagger.json", Prefix = "/api/finance" },
        new { Name = "DocumentationService", Url = "http://localhost:5002/swagger/v1/swagger.json", Prefix = "/api/docs" }
    };

    var pathsDict = (Dictionary<string, object>)aggregatedSpec.paths;
    var schemasDict = (Dictionary<string, object>)aggregatedSpec.components.schemas;

    foreach (var service in services)
    {
        try
        {
            var specJson = await httpClient.GetStringAsync(service.Url);
            var spec = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(specJson);

            // Add paths with service prefix
            if (spec.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    var newPath = service.Prefix + path.Name;
                    pathsDict[newPath] = System.Text.Json.JsonSerializer.Deserialize<object>(path.Value.GetRawText())!;
                }
            }

            // Add schemas
            if (spec.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas))
            {
                foreach (var schema in schemas.EnumerateObject())
                {
                    var schemaKey = $"{service.Name}_{schema.Name}";
                    schemasDict[schemaKey] = System.Text.Json.JsonSerializer.Deserialize<object>(schema.Value.GetRawText())!;
                }
            }

            logger.LogInformation("✅ {ServiceName} spec aggregated successfully", service.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning("⚠️ Failed to aggregate {ServiceName} spec: {Error}", service.Name, ex.Message);
        }
    }

    return Results.Json(aggregatedSpec);
})
.WithName("GetAggregatedOpenApiSpec")
.ExcludeFromDescription();

// Middleware to log incoming requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("📚 Documentation service accessed - Path: {Path}", context.Request.Path);
    await next();
});

// Configure aggregated Scalar documentation
app.MapScalarApiReference("/scalar/all", options =>
{
    options.Title = "🚀 ERP Prototype - All Services Combined";
    options.Theme = ScalarTheme.Purple;
    options.ShowSidebar = true;
    options.OpenApiRoutePattern = "/swagger/v1/swagger-aggregated.json";
});

// API endpoints for documentation service
app.MapGet("/", () => Results.Redirect("/scalar/all"))
.WithName("RedirectToScalar")
.ExcludeFromDescription();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Documentation Service!", Service = "DocumentationService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Documentation Service")
.WithOpenApi();

app.MapGet("/health", () => new { Status = "Healthy", Service = "DocumentationService", Timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Health check endpoint for the Documentation service")
.WithOpenApi();

// Endpoint to aggregate OpenAPI specs from other services
// Sample API endpoints for demonstration
app.MapGet("/api/services", () => new
{
    Services = new[]
    {
        new { Name = "WeatherService", Status = "Running", Port = 5001, Description = "Weather forecast API" },
        new { Name = "DocumentationService", Status = "Running", Port = 5002, Description = "API documentation service" },
        new { Name = "Gateway", Status = "Running", Port = 5000, Description = "BFF Gateway with YARP" }
    },
    Timestamp = DateTime.UtcNow
})
.WithName("GetServices")
.WithTags("Services")
.WithSummary("Get list of all microservices in the ERP system")
.WithDescription("Returns information about all microservices including their status and endpoints")
.WithOpenApi();

app.MapGet("/api/architecture", () => new
{
    Architecture = "Microservices",
    Gateway = "YARP (Yet Another Reverse Proxy)",
    Documentation = "Scalar API Documentation",
    Framework = ".NET 8",
    Containerization = "Docker",
    Orchestration = "Kubernetes Ready",
    Features = new[]
    {
        "Service Discovery",
        "Load Balancing",
        "Health Checks",
        "Centralized Logging",
        "API Gateway Pattern",
        "Scalable Architecture"
    }
})
.WithName("GetArchitecture")
.WithTags("System")
.WithSummary("Get system architecture information")
.WithDescription("Returns detailed information about the ERP prototype architecture and technologies used")
.WithOpenApi();

app.MapGet("/api/specs", async (HttpClient httpClient, ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Fetching API specifications from services");

    var specs = new Dictionary<string, object>();

    try
    {
        // Fetch WeatherService OpenAPI spec
        var weatherSpec = await httpClient.GetStringAsync("http://localhost:5001/swagger/v1/swagger.json");
        specs.Add("WeatherService", System.Text.Json.JsonSerializer.Deserialize<object>(weatherSpec));
        logger.LogInformation("✅ WeatherService spec fetched successfully");
    }
    catch (Exception ex)
    {
        logger.LogWarning("⚠️ Failed to fetch WeatherService spec: {Error}", ex.Message);
        specs.Add("WeatherService", new { error = "Service unavailable" });
    }

    return specs;
})
.WithName("GetApiSpecs")
.WithTags("Integration")
.WithSummary("Aggregate OpenAPI specs from other services")
.WithDescription("Fetches and aggregates OpenAPI specifications from all microservices")
.WithOpenApi();

app.Run();
