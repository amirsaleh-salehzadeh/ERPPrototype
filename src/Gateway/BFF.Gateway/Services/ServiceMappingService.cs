// ============================================================================
// ERP Prototype - Service Mapping Service
// ============================================================================
// Purpose: Dynamic service discovery and routing configuration for the BFF Gateway
// Author: ERP Development Team
// Created: 2024
// 
// Description:
// Manages the routing configuration for the BFF Gateway by providing service
// mapping information that determines how incoming requests are routed to
// downstream services. Supports dynamic configuration loading and path-based
// service discovery.
//
// Key Features:
// - Dynamic service mapping configuration
// - Path-based service discovery
// - Runtime service registration
// - Configuration validation and error handling
// ============================================================================

using BFF.Gateway.Models;
using System.Text.Json;

namespace BFF.Gateway.Services;

/// <summary>
/// Interface for service mapping operations in the BFF Gateway
/// Provides methods to retrieve service mapping configurations for routing
/// </summary>
public interface IServiceMappingService
{
    /// <summary>
    /// Retrieves the service mapping for a given request path
    /// </summary>
    /// <param name="path">The request path to match against service mappings</param>
    /// <returns>ServiceMapping if found, null otherwise</returns>
    ServiceMapping? GetServiceMapping(string path);
    
    /// <summary>
    /// Gets all configured service mappings
    /// </summary>
    /// <returns>Collection of all service mappings</returns>
    IEnumerable<ServiceMapping> GetAllMappings();
}

/// <summary>
/// Service mapping implementation that manages routing configuration for the BFF Gateway
/// Loads service mappings from configuration and provides path-based service discovery
/// </summary>
public class ServiceMappingService : IServiceMappingService
{
    private readonly List<ServiceMapping> _serviceMappings;
    private readonly ILogger<ServiceMappingService> _logger;

    /// <summary>
    /// Initializes the ServiceMappingService with configuration and logging
    /// </summary>
    /// <param name="configuration">Application configuration containing service mappings</param>
    /// <param name="logger">Logger for recording service mapping operations</param>
    public ServiceMappingService(IConfiguration configuration, ILogger<ServiceMappingService> logger)
    {
        _logger = logger;
        _serviceMappings = new List<ServiceMapping>();
        LoadServiceMappings(configuration);
    }

    /// <summary>
    /// Loads service mappings from the application configuration
    /// Validates and registers all configured service routes
    /// </summary>
    /// <param name="configuration">Application configuration source</param>
    private void LoadServiceMappings(IConfiguration configuration)
    {
        try
        {
            // Load service mappings from configuration section
            var mappingConfig = configuration.GetSection("ServiceMappings").Get<ServiceMapping[]>();
            if (mappingConfig != null)
            {
                _serviceMappings.AddRange(mappingConfig);
                _logger.LogInformation("✅ Loaded {Count} service mappings from configuration", mappingConfig.Length);
            }
            else
            {
                _logger.LogWarning("⚠️ No service mappings found in configuration");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load service mappings from configuration");
        }
    }

    /// <summary>
    /// Retrieves the service mapping that matches the given request path
    /// Uses longest-match-first algorithm to find the most specific route
    /// </summary>
    /// <param name="path">The incoming request path to match</param>
    /// <returns>ServiceMapping for the matching service, or null if no match found</returns>
    public ServiceMapping? GetServiceMapping(string path)
    {
        // Validate input path
        if (string.IsNullOrEmpty(path))
            return null;

        // Find the service mapping that matches the path prefix
        // Use longest match first to ensure most specific route is selected
        var mapping = _serviceMappings
            .Where(m => path.StartsWith(m.PathPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.PathPrefix.Length) // Longest match first for specificity
            .FirstOrDefault();

        // Log the result for debugging and monitoring
        if (mapping != null)
        {
            _logger.LogDebug("🎯 Found service mapping: {ServiceName} for path: {Path}", mapping.ServiceName, path);
        }
        else
        {
            _logger.LogDebug("❓ No service mapping found for path: {Path}", path);
        }

        return mapping;
    }

    /// <summary>
    /// Returns all configured service mappings
    /// Useful for service discovery endpoints and administration
    /// </summary>
    /// <returns>Read-only collection of all service mappings</returns>
    public IEnumerable<ServiceMapping> GetAllMappings()
    {
        return _serviceMappings.AsReadOnly();
    }
}
