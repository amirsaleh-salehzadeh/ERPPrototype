using BFF.Gateway.Models;
using System.Text.Json;

namespace BFF.Gateway.Services;

public interface IServiceMappingService
{
    ServiceMapping? GetServiceMapping(string path);
    IEnumerable<ServiceMapping> GetAllMappings();
}

public class ServiceMappingService : IServiceMappingService
{
    private readonly List<ServiceMapping> _serviceMappings;
    private readonly ILogger<ServiceMappingService> _logger;

    public ServiceMappingService(IConfiguration configuration, ILogger<ServiceMappingService> logger)
    {
        _logger = logger;
        _serviceMappings = new List<ServiceMapping>();
        LoadServiceMappings(configuration);
    }

    private void LoadServiceMappings(IConfiguration configuration)
    {
        try
        {
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

    public ServiceMapping? GetServiceMapping(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // Find the service mapping that matches the path prefix
        var mapping = _serviceMappings
            .Where(m => path.StartsWith(m.PathPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.PathPrefix.Length) // Longest match first
            .FirstOrDefault();

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

    public IEnumerable<ServiceMapping> GetAllMappings()
    {
        return _serviceMappings.AsReadOnly();
    }
}
