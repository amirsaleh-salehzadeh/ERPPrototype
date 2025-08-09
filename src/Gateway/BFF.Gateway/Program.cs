using Yarp.ReverseProxy.Configuration;
using BFF.Gateway.Services;
using BFF.Gateway.Models;
using BFF.Gateway.Middleware;
using BFF.Gateway.Extensions;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using System.Reflection;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "BFF.Gateway")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("🚀 Starting BFF Gateway application");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog((context, services, configuration) => 
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "BFF.Gateway")
            .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
            .WriteTo.File("logs/bff-gateway-.log", 
                rollingInterval: RollingInterval.Day, 
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}");

        // Try to add Elasticsearch sink, but don't fail if Elasticsearch is not available
        try
        {
            var elasticsearchUri = context.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
            var indexFormat = context.Configuration["Elasticsearch:IndexFormat"] ?? "bff-gateway-logs-{0:yyyy.MM.dd}";
            
            configuration.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticsearchUri))
            {
                IndexFormat = indexFormat,
                AutoRegisterTemplate = bool.Parse(context.Configuration["Elasticsearch:AutoRegisterTemplate"] ?? "true"),
                NumberOfShards = int.Parse(context.Configuration["Elasticsearch:NumberOfShards"] ?? "1"),
                NumberOfReplicas = int.Parse(context.Configuration["Elasticsearch:NumberOfReplicas"] ?? "0"),
                TemplateName = "bff-gateway-template",
                FailureCallback = (logEvent, exception) => Log.Error("Failed to emit event to Elasticsearch: {Error}", exception?.Message ?? "Unknown error"),
                EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog
            });
            
            Log.Information("✅ Elasticsearch logging configured for {Uri}", elasticsearchUri);
        }
        catch (Exception ex)
        {
            Log.Warning("⚠️ Elasticsearch not available, using file/console logging only: {Error}", ex.Message);
        }
    });

    // Load service mappings from JSON file
    builder.Configuration.AddJsonFile("servicemapping.json", optional: false, reloadOnChange: true);

    // Load header sanitization configuration
    builder.Configuration.AddJsonFile("headersanitization.json", optional: true, reloadOnChange: true);

// Add controllers for REST API endpoints
builder.Services.AddControllers();

// Add logging
builder.Services.AddLogging();

// Add HTTP client for API key validation
builder.Services.AddHttpClient();

// Add CORS for Documentation service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDocumentation", policy =>
    {
        policy.WithOrigins("http://localhost:5002", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add service mapping service
builder.Services.AddSingleton<IServiceMappingService, ServiceMappingService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("BFF Gateway is running"))
    .AddCheck("identity-service", async () =>
    {
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync("http://localhost:5007/health");
            if (response.IsSuccessStatusCode)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Identity Service is healthy");
            }
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Identity Service is not responding");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Identity Service check failed", ex);
        }
    })
    .AddCheck("weather-service", async () =>
    {
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync("http://localhost:5001/health");
            if (response.IsSuccessStatusCode)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Weather Service is healthy");
            }
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Weather Service is not responding");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Weather Service check failed", ex);
        }
    });

// Add header sanitization with configuration from JSON file
builder.Services.AddHeaderSanitization(options =>
{
    // Load configuration from appsettings or headersanitization.json
    var config = builder.Configuration.GetSection("HeaderSanitization");

    if (config.Exists())
    {
        // Load from configuration file
        options.HeadersToRemove = config.GetSection("HeadersToRemove").Get<List<string>>() ?? options.HeadersToRemove;
        options.HeadersToMask = config.GetSection("HeadersToMask").Get<List<string>>() ?? options.HeadersToMask;
        options.ResponseHeadersToRemove = config.GetSection("ResponseHeadersToRemove").Get<List<string>>() ?? options.ResponseHeadersToRemove;
        options.ResponseHeadersToMask = config.GetSection("ResponseHeadersToMask").Get<List<string>>() ?? options.ResponseHeadersToMask;
        options.SensitivePatterns = config.GetSection("SensitivePatterns").Get<List<string>>() ?? options.SensitivePatterns;
    }
    else
    {
        // Fallback to default configuration with custom additions
        options.HeadersToRemove.AddRange(new[]
        {
            "x-internal-gateway-*",    // Remove internal gateway headers
            "x-upstream-*",            // Remove upstream service headers
            "x-backend-*"              // Remove backend service headers
        });

        options.HeadersToMask.AddRange(new[]
        {
            "x-client-id",             // Mask client identifiers
            "x-tenant-id"              // Mask tenant identifiers
        });

        options.ResponseHeadersToRemove.AddRange(new[]
        {
            "x-gateway-*",             // Remove gateway processing headers
            "x-processing-time",       // Remove processing time info
            "x-cache-*"                // Remove cache headers
        });
    }
});

// Add gRPC client service for microservice communication
builder.Services.AddSingleton<IGrpcClientService, GrpcClientService>();

// Add HttpClient for API key validation middleware
builder.Services.AddHttpClient();

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    var app = builder.Build();

    // Add request logging middleware first to capture all requests
    app.UseMiddleware<RequestLoggingMiddleware>();

    // Enable CORS
    app.UseCors("AllowDocumentation");

    // Add gRPC-based API key validation middleware (BEFORE header sanitization)
    app.UseMiddleware<GrpcApiKeyValidationMiddleware>();

    // Add header sanitization middleware (after API key validation)
    app.UseHeaderSanitization();

// Health Check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            service = "BFF.Gateway",
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

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || check.Name == "self",
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            service = "BFF.Gateway",
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
        "# HELP bff_gateway_health Health status of the BFF Gateway",
        "# TYPE bff_gateway_health gauge",
        "bff_gateway_health 1",
        "",
        "# HELP bff_gateway_uptime_seconds Uptime of the BFF Gateway in seconds",
        "# TYPE bff_gateway_uptime_seconds counter",
        $"bff_gateway_uptime_seconds {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
        "",
        "# HELP bff_gateway_requests_total Total number of requests",
        "# TYPE bff_gateway_requests_total counter",
        "bff_gateway_requests_total 0"
    };
    
    return Results.Text(string.Join("\n", metrics), "text/plain");
})
.WithName("Metrics")
.WithTags("Monitoring")
.ExcludeFromDescription();

// Custom middleware to log service names and remove them from headers
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var serviceMappingService = context.RequestServices.GetRequiredService<IServiceMappingService>();

    // Extract service name from the path using service mapping
    var path = context.Request.Path.Value;
    var serviceMapping = serviceMappingService.GetServiceMapping(path ?? string.Empty);

    if (serviceMapping != null)
    {
        logger.LogInformation("🚀 Request routed to service: {ServiceName} ({DisplayName}) - Path: {Path}",
            serviceMapping.ServiceName, serviceMapping.DisplayName, path);

        // Add service information to request headers for internal tracking
        context.Request.Headers["X-Service-Name"] = serviceMapping.ServiceName;
        context.Request.Headers["X-Service-Display-Name"] = serviceMapping.DisplayName;
    }
    else
    {
        logger.LogInformation("🔍 Request to unmapped path: {Path}", path);
    }

    await next();

    // Note: Service headers are automatically removed by HeaderSanitizationMiddleware
    // This manual cleanup is kept for backwards compatibility and explicit logging
    if (context.Response.Headers.ContainsKey("X-Service-Name"))
    {
        logger.LogInformation("🧹 Service headers will be sanitized by HeaderSanitizationMiddleware");
    }
});

// API endpoint to expose service mappings (useful for Kubernetes service discovery)
app.MapGet("/api/gateway/services", (IServiceMappingService serviceMappingService) =>
{
    var mappings = serviceMappingService.GetAllMappings().Select(m => new
    {
        pathPrefix = m.PathPrefix,
        serviceName = m.ServiceName,
        displayName = m.DisplayName,
        description = m.Description
    });

    return Results.Ok(new { services = mappings, timestamp = DateTime.UtcNow });
})
.WithName("GetServiceMappings")
.WithTags("Gateway")
.WithSummary("Get all service mappings configured in the gateway");

    // Health check endpoint
    app.MapGet("/health", () => new { Status = "Healthy", Service = "BFF.Gateway", Timestamp = DateTime.UtcNow })
    .WithName("GatewayHealthCheck")
    .WithTags("Health");

    // Map controllers (temporarily disabled to use YARP routing)
    // app.MapControllers();

    // Map YARP reverse proxy
    app.MapReverseProxy();

    Log.Information("🎯 BFF Gateway started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 BFF Gateway failed to start");
}
finally
{
    Log.CloseAndFlush();
}
