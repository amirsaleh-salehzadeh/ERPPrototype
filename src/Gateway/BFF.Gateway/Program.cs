using Yarp.ReverseProxy.Configuration;
using BFF.Gateway.Services;
using BFF.Gateway.Models;
using BFF.Gateway.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Load service mappings from JSON file
builder.Configuration.AddJsonFile("servicemapping.json", optional: false, reloadOnChange: true);

// Add YARP services
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add logging
builder.Services.AddLogging();

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

var app = builder.Build();

// Enable CORS
app.UseCors("AllowDocumentation");

// Add API key validation middleware
app.UseMiddleware<ApiKeyValidationMiddleware>();

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

    // Remove service headers from response (clean up after gateway)
    if (context.Response.Headers.ContainsKey("X-Service-Name"))
    {
        context.Response.Headers.Remove("X-Service-Name");
        context.Response.Headers.Remove("X-Service-Display-Name");
        context.Response.Headers.Remove("X-User-Id");
        context.Response.Headers.Remove("X-User-Name");
        context.Response.Headers.Remove("X-User-Permissions");
        logger.LogInformation("🧹 Service headers removed after gateway processing");
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

// Configure YARP
app.MapReverseProxy();

app.Run();
