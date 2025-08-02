using System.Security.Cryptography;

namespace ERP.IdentityService.Services;

public class ApiKeySeederService
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<ApiKeySeederService> _logger;

    public ApiKeySeederService(IApiKeyService apiKeyService, ILogger<ApiKeySeederService> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    public async Task SeedRandomApiKeysAsync(int count = 20)
    {
        _logger.LogInformation("🌱 Starting to seed {Count} random API keys into Redis", count);

        var userTypes = new[]
        {
            new { Type = "admin", Permissions = new[] { "read", "write", "delete", "admin" } },
            new { Type = "developer", Permissions = new[] { "read", "write" } },
            new { Type = "analyst", Permissions = new[] { "read", "analytics" } },
            new { Type = "readonly", Permissions = new[] { "read" } },
            new { Type = "service", Permissions = new[] { "read", "write", "service" } }
        };

        var companies = new[] { "TechCorp", "DataSys", "CloudInc", "DevOps", "Analytics", "Systems", "Solutions", "Digital" };
        var departments = new[] { "Engineering", "Sales", "Marketing", "Finance", "Operations", "Support", "Research" };

        var random = new Random();
        var createdKeys = new List<string>();

        for (int i = 0; i < count; i++)
        {
            try
            {
                var userType = userTypes[random.Next(userTypes.Length)];
                var company = companies[random.Next(companies.Length)];
                var department = departments[random.Next(departments.Length)];
                
                var userName = $"{userType.Type}_{company}_{department}_{i + 1:D3}";
                var description = $"{userType.Type.ToUpper()} API Key for {company} {department} Department";
                var expiresInDays = random.Next(30, 365); // Random expiration between 30-365 days

                var result = _apiKeyService.CreateApiKey(userName, description, userType.Permissions, expiresInDays);
                
                if (result.Success)
                {
                    createdKeys.Add(result.ApiKey);
                    _logger.LogInformation("✅ Created API key {Index}/{Total}: {UserName} - {ApiKey}", 
                        i + 1, count, userName, result.ApiKey[..8] + "...");
                }
                else
                {
                    _logger.LogWarning("⚠️ Failed to create API key {Index}/{Total}: {Error}", 
                        i + 1, count, result.ErrorMessage);
                }

                // Small delay to avoid overwhelming Redis
                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating API key {Index}/{Total}", i + 1, count);
            }
        }

        _logger.LogInformation("🎉 Completed seeding! Created {CreatedCount}/{TotalCount} API keys", 
            createdKeys.Count, count);

        // Log some sample keys for testing
        if (createdKeys.Count > 0)
        {
            _logger.LogInformation("📋 Sample API keys for testing:");
            var sampleCount = Math.Min(5, createdKeys.Count);
            for (int i = 0; i < sampleCount; i++)
            {
                _logger.LogInformation("   🔑 {Index}: {ApiKey}", i + 1, createdKeys[i]);
            }
        }
    }

    public async Task CreatePredefinedApiKeysAsync()
    {
        _logger.LogInformation("🔧 Creating predefined API keys for testing");

        var predefinedKeys = new[]
        {
            new { UserName = "admin_master", Description = "Master Admin Key", Permissions = new[] { "read", "write", "delete", "admin" }, Days = 365 },
            new { UserName = "dev_team_lead", Description = "Development Team Lead", Permissions = new[] { "read", "write", "deploy" }, Days = 180 },
            new { UserName = "qa_automation", Description = "QA Automation Service", Permissions = new[] { "read", "write", "test" }, Days = 90 },
            new { UserName = "monitoring_service", Description = "System Monitoring", Permissions = new[] { "read", "health" }, Days = 365 },
            new { UserName = "analytics_dashboard", Description = "Analytics Dashboard", Permissions = new[] { "read", "analytics" }, Days = 180 }
        };

        var createdKeys = new List<(string UserName, string ApiKey)>();

        foreach (var keyDef in predefinedKeys)
        {
            try
            {
                var result = _apiKeyService.CreateApiKey(keyDef.UserName, keyDef.Description, keyDef.Permissions, keyDef.Days);
                
                if (result.Success)
                {
                    createdKeys.Add((keyDef.UserName, result.ApiKey));
                    _logger.LogInformation("✅ Created predefined key: {UserName} - {ApiKey}", 
                        keyDef.UserName, result.ApiKey[..8] + "...");
                }
                else
                {
                    _logger.LogWarning("⚠️ Failed to create predefined key {UserName}: {Error}", 
                        keyDef.UserName, result.ErrorMessage);
                }

                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating predefined key: {UserName}", keyDef.UserName);
            }
        }

        if (createdKeys.Count > 0)
        {
            _logger.LogInformation("📋 Predefined API keys created:");
            foreach (var (userName, apiKey) in createdKeys)
            {
                _logger.LogInformation("   🔑 {UserName}: {ApiKey}", userName, apiKey);
            }
        }
    }
}
