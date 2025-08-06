using Yarp.ReverseProxy.Configuration;
using BFF.Gateway.Services;
using BFF.Gateway.Models;
using BFF.Gateway.Middleware;
using BFF.Gateway.Extensions;

var builder = WebApplication.CreateBuilder(args);

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

// Add JWT validation service
builder.Services.AddSingleton<IJwtValidationService, JwtValidationService>();

// Add HttpClient for API key validation middleware
builder.Services.AddHttpClient();

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Enable CORS
app.UseCors("AllowDocumentation");

// Add hybrid authentication middleware (supports both JWT and API keys)
app.UseMiddleware<HybridAuthenticationMiddleware>();

// Add header sanitization middleware (after authentication)
app.UseHeaderSanitization();

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

app.Run();
