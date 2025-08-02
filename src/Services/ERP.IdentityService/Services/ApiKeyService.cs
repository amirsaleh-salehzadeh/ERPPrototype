using System.Security.Cryptography;
using System.Text;

namespace ERP.IdentityService.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly ILogger<ApiKeyService> _logger;
    private readonly Dictionary<string, ApiKeyData> _apiKeys;

    public ApiKeyService(ILogger<ApiKeyService> logger)
    {
        _logger = logger;
        _apiKeys = new Dictionary<string, ApiKeyData>();
        
        // Create some sample API keys for testing
        CreateSampleApiKeys();
    }

    public CreateApiKeyResult CreateApiKey(string userName, string description, string[] permissions, int expiresInDays)
    {
        try
        {
            var keyId = Guid.NewGuid().ToString();
            var apiKey = GenerateApiKey();
            var createdAt = DateTime.UtcNow;
            var expiresAt = createdAt.AddDays(expiresInDays);

            var apiKeyData = new ApiKeyData
            {
                KeyId = keyId,
                ApiKey = apiKey,
                UserName = userName,
                Description = description,
                Permissions = permissions,
                CreatedAt = createdAt,
                ExpiresAt = expiresAt,
                IsActive = true,
                UsageCount = 0
            };

            _apiKeys[apiKey] = apiKeyData;
            
            _logger.LogInformation("✅ API key created for user: {UserName}, KeyId: {KeyId}", userName, keyId);
            
            return new CreateApiKeyResult(true, apiKey, keyId, createdAt, expiresAt, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create API key for user: {UserName}", userName);
            return new CreateApiKeyResult(false, string.Empty, string.Empty, DateTime.MinValue, DateTime.MinValue, ex.Message);
        }
    }

    public ValidateApiKeyResult ValidateApiKey(string apiKey, string serviceName, string endpoint)
    {
        try
        {
            if (!_apiKeys.TryGetValue(apiKey, out var keyData))
            {
                _logger.LogWarning("⚠️ Invalid API key attempted for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "Invalid API key", DateTime.MinValue);
            }

            if (!keyData.IsActive)
            {
                _logger.LogWarning("⚠️ Inactive API key attempted for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "API key is inactive", DateTime.MinValue);
            }

            if (keyData.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("⚠️ Expired API key attempted for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "API key has expired", DateTime.MinValue);
            }

            // Update usage count
            keyData.UsageCount++;
            
            _logger.LogInformation("✅ API key validated for user: {UserName}, service: {ServiceName}", keyData.UserName, serviceName);
            
            return new ValidateApiKeyResult(true, keyData.KeyId, keyData.UserName, keyData.Permissions, string.Empty, keyData.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error validating API key for service: {ServiceName}", serviceName);
            return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), ex.Message, DateTime.MinValue);
        }
    }

    public RevokeApiKeyResult RevokeApiKey(string apiKey)
    {
        try
        {
            if (_apiKeys.TryGetValue(apiKey, out var keyData))
            {
                keyData.IsActive = false;
                _logger.LogInformation("✅ API key revoked for user: {UserName}", keyData.UserName);
                return new RevokeApiKeyResult(true, "API key revoked successfully");
            }

            return new RevokeApiKeyResult(false, "API key not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error revoking API key");
            return new RevokeApiKeyResult(false, ex.Message);
        }
    }

    public GetApiKeyInfoResult GetApiKeyInfo(string apiKey)
    {
        if (_apiKeys.TryGetValue(apiKey, out var keyData))
        {
            return new GetApiKeyInfoResult(
                keyData.KeyId,
                keyData.UserName,
                keyData.Description,
                keyData.Permissions,
                keyData.CreatedAt,
                keyData.ExpiresAt,
                keyData.IsActive,
                keyData.UsageCount
            );
        }

        return new GetApiKeyInfoResult(string.Empty, string.Empty, string.Empty, Array.Empty<string>(), DateTime.MinValue, DateTime.MinValue, false, 0);
    }

    private string GenerateApiKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private void CreateSampleApiKeys()
    {
        // Create sample API keys for testing
        var sampleKeys = new[]
        {
            new { UserName = "admin", Description = "Admin API Key", Permissions = new[] { "read", "write", "admin" } },
            new { UserName = "developer", Description = "Developer API Key", Permissions = new[] { "read", "write" } },
            new { UserName = "readonly", Description = "Read-only API Key", Permissions = new[] { "read" } }
        };

        foreach (var sample in sampleKeys)
        {
            CreateApiKey(sample.UserName, sample.Description, sample.Permissions, 365);
        }

        _logger.LogInformation("🔑 Created {Count} sample API keys", sampleKeys.Length);
    }

    private class ApiKeyData
    {
        public string KeyId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Permissions { get; set; } = Array.Empty<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
        public int UsageCount { get; set; }
    }
}
