using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add YARP services
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Custom middleware to log service names and remove them from headers
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Extract service name from the path
    var path = context.Request.Path.Value;
    string? serviceName = null;

    if (path?.StartsWith("/api/weather") == true)
    {
        serviceName = "WeatherService";
    }
    else if (path?.StartsWith("/api/docs") == true)
    {
        serviceName = "DocumentationService";
    }

    if (!string.IsNullOrEmpty(serviceName))
    {
        logger.LogInformation("🚀 Request routed to service: {ServiceName} - Path: {Path}", serviceName, path);

        // Add service name to request headers for internal tracking
        context.Request.Headers.Add("X-Service-Name", serviceName);
    }

    await next();

    // Remove service name from response headers (clean up after gateway)
    if (context.Response.Headers.ContainsKey("X-Service-Name"))
    {
        context.Response.Headers.Remove("X-Service-Name");
        logger.LogInformation("🧹 Service name header removed after gateway processing");
    }
});

// Configure YARP
app.MapReverseProxy();

app.Run();
