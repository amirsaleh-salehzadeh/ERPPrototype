using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();

// Add HTTP client for fetching OpenAPI specs from other services
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

// Configure Scalar instead of Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
}

app.MapScalarApiReference(options =>
{
    options.Title = "ERP Prototype API Documentation";
    options.Theme = ScalarTheme.Purple;
    options.ShowSidebar = true;
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
.WithOpenApi();

// Endpoint to aggregate OpenAPI specs from other services
app.MapGet("/api/specs", async (HttpClient httpClient, ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Fetching API specifications from services");

    var specs = new Dictionary<string, object>();

    try
    {
        // Fetch WeatherService OpenAPI spec
        var weatherSpec = await httpClient.GetStringAsync("https://localhost:7001/openapi/v1.json");
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
.WithOpenApi();

app.Run();
