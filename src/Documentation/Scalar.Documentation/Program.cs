using System.Text.Json;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();

// Add HTTP client for fetching OpenAPI specs
builder.Services.AddHttpClient();

// Add CORS for cross-origin requests
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("AllowAll");

// Add health check endpoint
app.MapGet("/health", () => new {
    status = "Healthy",
    service = "Scalar.Documentation",
    timestamp = DateTime.UtcNow
});

// Using OpenAPI without Swagger

// Configure Scalar UI with aggregated OpenAPI spec
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("ERP Microservices API Documentation")
        .WithTheme(ScalarTheme.Purple)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
        .WithSidebar(true)
        .WithOpenApiRoutePattern("/swagger/v1/swagger-aggregated.json");
});

// Aggregated OpenAPI endpoint
app.MapGet("/swagger/v1/swagger-aggregated.json", async (HttpClient httpClient) =>
{
    var services = new[]
    {
        new { Name = "Weather Service", Url = "http://localhost:5001/swagger/v1/swagger.json" },
        new { Name = "Identity Service", Url = "http://localhost:5007/swagger/v1/swagger.json" }
    };

    var aggregatedSpec = new
    {
        openapi = "3.0.1",
        info = new
        {
            title = "ERP Weather Service - Aggregated API",
            version = "v1",
            description = "Complete API documentation for the ERP Weather Service system"
        },
        servers = new[]
        {
            new { url = "http://localhost:5000", description = "BFF Gateway (Recommended)" },
            new { url = "http://localhost:5001", description = "Weather Service (Direct)" },
            new { url = "http://localhost:5007", description = "Identity Service (Direct)" }
        },
        paths = new Dictionary<string, object>(),
        components = new { schemas = new Dictionary<string, object>() }
    };

    var allPaths = new Dictionary<string, object>();
    var allSchemas = new Dictionary<string, object>();

    foreach (var service in services)
    {
        try
        {
            var response = await httpClient.GetStringAsync(service.Url);
            var spec = JsonSerializer.Deserialize<JsonElement>(response);

            if (spec.TryGetProperty("paths", out var paths))
            {
                foreach (var path in paths.EnumerateObject())
                {
                    var pathKey = service.Name == "Weather Service" ? $"/api/weather{path.Name}" : path.Name;

                    // Fix schema references in the path
                    var pathJson = path.Value.GetRawText();
                    pathJson = pathJson.Replace("#/components/schemas/", $"#/components/schemas/{service.Name.Replace(" ", "_")}_");

                    allPaths[pathKey] = JsonSerializer.Deserialize<JsonElement>(pathJson);
                }
            }

            if (spec.TryGetProperty("components", out var components) &&
                components.TryGetProperty("schemas", out var schemas))
            {
                foreach (var schema in schemas.EnumerateObject())
                {
                    allSchemas[$"{service.Name.Replace(" ", "_")}_{schema.Name}"] = schema.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not fetch OpenAPI spec from {service.Name}: {ex.Message}");
        }
    }

    return Results.Json(new
    {
        openapi = aggregatedSpec.openapi,
        info = aggregatedSpec.info,
        servers = aggregatedSpec.servers,
        paths = allPaths,
        components = new { schemas = allSchemas }
    });
});

// Redirect root to Scalar UI
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

Console.WriteLine("📚 Scalar Documentation Service starting...");
Console.WriteLine("🌐 Available at: http://localhost:5002");
Console.WriteLine("📖 Scalar UI: http://localhost:5002/scalar/v1");
Console.WriteLine("📋 Aggregated API: http://localhost:5002/swagger/v1/swagger-aggregated.json");

app.Run();
