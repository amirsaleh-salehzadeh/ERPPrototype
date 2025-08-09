// ============================================================================
// SERVICE MAPPING MODELS
// ============================================================================
// Data models for configuring API Gateway routing and service discovery.
// These classes define how external API paths are mapped to internal
// microservices in the ERP system.
//
// Usage:
// - ServiceMappingConfig: Root configuration containing all service mappings
// - ServiceMapping: Individual service routing configuration
//
// Configuration Source: servicemapping.json
// ============================================================================

namespace BFF.Gateway.Models;

/// <summary>
/// Root configuration model for service mappings in the API Gateway.
/// Contains a collection of all microservice routing configurations.
/// </summary>
/// <remarks>
/// This model is used to deserialize the servicemapping.json configuration file
/// that defines how external API paths are routed to internal microservices.
/// </remarks>
public class ServiceMappingConfig
{
    /// <summary>
    /// Collection of service mapping configurations for API routing.
    /// Each mapping defines how a specific URL path prefix maps to a microservice.
    /// </summary>
    /// <example>
    /// Example configuration:
    /// {
    ///   "ServiceMappings": [
    ///     {
    ///       "PathPrefix": "/api/weather",
    ///       "ServiceName": "WeatherService",
    ///       "DisplayName": "Weather API",
    ///       "Description": "Provides weather forecast data"
    ///     }
    ///   ]
    /// }
    /// </example>
    public List<ServiceMapping> ServiceMappings { get; set; } = new();
}

/// <summary>
/// Configuration model for mapping an API path prefix to a specific microservice.
/// Defines the routing rules used by YARP (Yet Another Reverse Proxy) in the BFF Gateway.
/// </summary>
/// <remarks>
/// Each ServiceMapping instance represents one microservice endpoint configuration,
/// including the URL path pattern, target service, and descriptive metadata.
/// </remarks>
public class ServiceMapping
{
    /// <summary>
    /// URL path prefix that triggers routing to this service.
    /// Requests matching this prefix will be forwarded to the associated microservice.
    /// </summary>
    /// <example>
    /// "/api/weather" - Routes all requests starting with /api/weather to the weather service
    /// "/api/identity" - Routes all requests starting with /api/identity to the identity service
    /// </example>
    public string PathPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Internal name of the microservice that handles requests for this path prefix.
    /// This should match the service name used in service discovery and configuration.
    /// </summary>
    /// <example>
    /// "WeatherService" - Maps to the weather forecast microservice
    /// "IdentityService" - Maps to the authentication and authorization service
    /// </example>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the service, used in documentation and UI.
    /// Provides a friendly name for the service that appears in API documentation.
    /// </summary>
    /// <example>
    /// "Weather Forecast API" - Descriptive name for weather service
    /// "Identity & Authentication" - Descriptive name for identity service
    /// </example>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the service functionality and purpose.
    /// Used in API documentation to explain what the service provides.
    /// </summary>
    /// <example>
    /// "Provides 5-day weather forecasts with temperature and conditions"
    /// "Handles user authentication, authorization, and API key management"
    /// </example>
    public string Description { get; set; } = string.Empty;
}
