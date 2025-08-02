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
    options.Title = "ERP Prototype API Documentation";
    options.Theme = ScalarTheme.Purple;
    options.ShowSidebar = true;
    options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
});

// Middleware to log incoming requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("📚 Documentation service accessed - Path: {Path}", context.Request.Path);
    await next();
});

// API endpoints for documentation service
app.MapGet("/", () => Results.Redirect("/scalar/v1"))
.WithName("RedirectToScalar")
.ExcludeFromDescription();

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
