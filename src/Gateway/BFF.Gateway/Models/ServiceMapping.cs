namespace BFF.Gateway.Models;

public class ServiceMappingConfig
{
    public List<ServiceMapping> ServiceMappings { get; set; } = new();
}

public class ServiceMapping
{
    public string PathPrefix { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
