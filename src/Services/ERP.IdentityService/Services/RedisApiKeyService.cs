using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text.Json;

namespace ERP.IdentityService.Services;

public class RedisApiKeyService : IApiKeyService
{
    private readonly IDatabase _database;
    private readonly ILogger<RedisApiKeyService> _logger;
    private const string API_KEY_PREFIX = "apikey:";
    private const string API_KEY_LIST = "apikeys:all";

    public RedisApiKeyService(IConnectionMultiplexer redis, ILogger<RedisApiKeyService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
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

            // Store in Redis
            var json = JsonSerializer.Serialize(apiKeyData);
            var key = API_KEY_PREFIX + apiKey;
            
            // Set with expiration
            var expiry = TimeSpan.FromDays(expiresInDays);
            _database.StringSet(key, json, expiry);
            
            // Add to the list of all API keys
            _database.SetAdd(API_KEY_LIST, apiKey);
            
            _logger.LogInformation("✅ API key created and stored in Redis for user: {UserName}, KeyId: {KeyId}", userName, keyId);
            
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
            var key = API_KEY_PREFIX + apiKey;
            var json = _database.StringGet(key);
            
            if (!json.HasValue)
            {
                _logger.LogWarning("⚠️ API key not found in Redis for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "Invalid API key", DateTime.MinValue);
            }

            var keyData = JsonSerializer.Deserialize<ApiKeyData>(json!);
            
            if (keyData == null)
            {
                _logger.LogWarning("⚠️ Failed to deserialize API key data for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "Invalid API key data", DateTime.MinValue);
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
            var updatedJson = JsonSerializer.Serialize(keyData);
            var expiry = keyData.ExpiresAt - DateTime.UtcNow;
            _database.StringSet(key, updatedJson, expiry);
            
            _logger.LogInformation("✅ API key validated from Redis for user: {UserName}, service: {ServiceName}", keyData.UserName, serviceName);
            
            return new ValidateApiKeyResult(true, keyData.KeyId, keyData.UserName, keyData.Permissions, string.Empty, keyData.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error validating API key from Redis for service: {ServiceName}", serviceName);
            return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), ex.Message, DateTime.MinValue);
        }
    }

    public RevokeApiKeyResult RevokeApiKey(string apiKey)
    {
        try
        {
            var key = API_KEY_PREFIX + apiKey;
            var json = _database.StringGet(key);
            
            if (!json.HasValue)
            {
                return new RevokeApiKeyResult(false, "API key not found");
            }

            var keyData = JsonSerializer.Deserialize<ApiKeyData>(json!);
            if (keyData != null)
            {
                keyData.IsActive = false;
                var updatedJson = JsonSerializer.Serialize(keyData);
                var expiry = keyData.ExpiresAt - DateTime.UtcNow;
                _database.StringSet(key, updatedJson, expiry);
                
                _logger.LogInformation("✅ API key revoked in Redis for user: {UserName}", keyData.UserName);
                return new RevokeApiKeyResult(true, "API key revoked successfully");
            }

            return new RevokeApiKeyResult(false, "Failed to revoke API key");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error revoking API key in Redis");
            return new RevokeApiKeyResult(false, ex.Message);
        }
    }

    public GetApiKeyInfoResult GetApiKeyInfo(string apiKey)
    {
        try
        {
            var key = API_KEY_PREFIX + apiKey;
            var json = _database.StringGet(key);
            
            if (!json.HasValue)
            {
                return new GetApiKeyInfoResult(string.Empty, string.Empty, string.Empty, Array.Empty<string>(), DateTime.MinValue, DateTime.MinValue, false, 0);
            }

            var keyData = JsonSerializer.Deserialize<ApiKeyData>(json!);
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
            _logger.LogError(ex, "❌ Error getting API key info from Redis");
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
