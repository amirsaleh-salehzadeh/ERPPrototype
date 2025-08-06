using BFF.Gateway.Middleware;

namespace BFF.Gateway.Extensions;

/// <summary>
/// Extension methods for configuring header sanitization middleware
/// </summary>
public static class HeaderSanitizationExtensions
{
    /// <summary>
    /// Adds header sanitization services to the DI container
    /// </summary>
    public static IServiceCollection AddHeaderSanitization(
        this IServiceCollection services, 
        Action<HeaderSanitizationOptions>? configureOptions = null)
    {
        var options = HeaderSanitizationOptions.CreateDefault();
        configureOptions?.Invoke(options);
        
        services.AddSingleton(options);
        
        return services;
    }

    /// <summary>
    /// Adds header sanitization services with custom options
    /// </summary>
    public static IServiceCollection AddHeaderSanitization(
        this IServiceCollection services, 
        HeaderSanitizationOptions options)
    {
        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// Uses header sanitization middleware in the request pipeline
    /// </summary>
    public static IApplicationBuilder UseHeaderSanitization(this IApplicationBuilder app)
    {
        return app.UseMiddleware<HeaderSanitizationMiddleware>();
    }

    /// <summary>
    /// Uses header sanitization middleware with custom options
    /// </summary>
    public static IApplicationBuilder UseHeaderSanitization(
        this IApplicationBuilder app, 
        HeaderSanitizationOptions options)
    {
        return app.UseMiddleware<HeaderSanitizationMiddleware>(options);
    }
}
