using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text.Json;

namespace ERP.IdentityService.Services;

/// <summary>
/// Hybrid API Key service that uses Redis if available, otherwise falls back to in-memory storage
/// </summary>
public class HybridApiKeyService : IApiKeyService
{
    private readonly ILogger<HybridApiKeyService> _logger;
    private readonly bool _useRedis;
    private readonly IDatabase? _database;
    private readonly Dictionary<string, ApiKeyData> _inMemoryStorage;
    private const string API_KEY_PREFIX = "apikey:";
    private const string API_KEY_LIST = "apikeys:all";

    public HybridApiKeyService(ILogger<HybridApiKeyService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _inMemoryStorage = new Dictionary<string, ApiKeyData>();

        // Try to get Redis connection
        try
        {
            var multiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
            if (multiplexer != null && multiplexer.IsConnected)
            {
                _database = multiplexer.GetDatabase();
                _useRedis = true;
                _logger.LogInformation("✅ Using Redis for API key storage");
            }
            else
            {
                _useRedis = false;
                _logger.LogInformation("🧠 Using in-memory storage for API keys (Redis not available)");
            }
        }
        catch (Exception ex)
        {
            _useRedis = false;
            _logger.LogWarning(ex, "⚠️ Redis not available, using in-memory storage for API keys");
        }

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

            if (_useRedis && _database != null)
            {
                // Store in Redis
                var json = JsonSerializer.Serialize(apiKeyData);
                var key = API_KEY_PREFIX + apiKey;
                var expiry = TimeSpan.FromDays(expiresInDays);
                _database.StringSet(key, json, expiry);
                _database.SetAdd(API_KEY_LIST, apiKey);
                _logger.LogInformation("✅ API key created and stored in Redis for user: {UserName}", userName);
            }
            else
            {
                // Store in memory
                _inMemoryStorage[apiKey] = apiKeyData;
                _logger.LogInformation("✅ API key created and stored in memory for user: {UserName}", userName);
            }

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
            ApiKeyData? keyData = null;

            if (_useRedis && _database != null)
            {
                // Get from Redis
                var key = API_KEY_PREFIX + apiKey;
                var json = _database.StringGet(key);
                if (json.HasValue)
                {
                    keyData = JsonSerializer.Deserialize<ApiKeyData>(json!);
                }
            }
            else
            {
                // Get from memory
                _inMemoryStorage.TryGetValue(apiKey, out keyData);
            }

            if (keyData == null)
            {
                _logger.LogWarning("⚠️ API key not found for service: {ServiceName}", serviceName);
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

            if (_useRedis && _database != null)
            {
                var updatedJson = JsonSerializer.Serialize(keyData);
                var key = API_KEY_PREFIX + apiKey;
                var expiry = keyData.ExpiresAt - DateTime.UtcNow;
                _database.StringSet(key, updatedJson, expiry);
            }
            // In-memory storage is updated by reference

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
            ApiKeyData? keyData = null;

            if (_useRedis && _database != null)
            {
                var key = API_KEY_PREFIX + apiKey;
                var json = _database.StringGet(key);
                if (json.HasValue)
                {
                    keyData = JsonSerializer.Deserialize<ApiKeyData>(json!);
                    if (keyData != null)
                    {
                        keyData.IsActive = false;
                        var updatedJson = JsonSerializer.Serialize(keyData);
                        var expiry = keyData.ExpiresAt - DateTime.UtcNow;
                        _database.StringSet(key, updatedJson, expiry);
                    }
                }
            }
            else
            {
                if (_inMemoryStorage.TryGetValue(apiKey, out keyData))
                {
                    keyData.IsActive = false;
                }
            }

            if (keyData != null)
            {
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
        try
        {
            ApiKeyData? keyData = null;

            if (_useRedis && _database != null)
            {
                var key = API_KEY_PREFIX + apiKey;
                var json = _database.StringGet(key);
                if (json.HasValue)
                {
                    keyData = JsonSerializer.Deserialize<ApiKeyData>(json!);
                }
            }
            else
            {
                _inMemoryStorage.TryGetValue(apiKey, out keyData);
            }

            if (keyData != null)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting API key info");
            return new GetApiKeyInfoResult(string.Empty, string.Empty, string.Empty, Array.Empty<string>(), DateTime.MinValue, DateTime.MinValue, false, 0);
        }
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
