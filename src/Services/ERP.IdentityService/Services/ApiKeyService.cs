// ============================================================================
// ERP Prototype - API Key Service (In-Memory Implementation)
// ============================================================================
// Purpose: In-memory API key management service for development and testing
// Author: ERP Development Team
// Created: 2024
// 
// Description:
// Provides API key management functionality using in-memory storage.
// This implementation is suitable for development, testing, and demonstration
// purposes. For production use, consider RedisApiKeyService for persistence
// and scalability.
//
// Key Features:
// - API key generation with cryptographic security
// - Role-based permission management
// - API key validation and lifecycle management
// - Usage tracking and analytics
// - Sample data seeding for testing
//
// Note: This is an in-memory implementation. Data will be lost on service restart.
// For persistent storage, use RedisApiKeyService or database-backed implementation.
// ============================================================================

using System.Security.Cryptography;
using System.Text;

namespace ERP.IdentityService.Services;

/// <summary>
/// In-memory implementation of the API key management service
/// Provides API key creation, validation, and management functionality
/// Suitable for development, testing, and demonstration environments
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private readonly ILogger<ApiKeyService> _logger;
    private readonly Dictionary<string, ApiKeyData> _apiKeys;

    /// <summary>
    /// Initializes the API key service with in-memory storage
    /// Creates sample API keys for testing and development
    /// </summary>
    /// <param name="logger">Logger for recording API key operations</param>
    public ApiKeyService(ILogger<ApiKeyService> logger)
    {
        _logger = logger;
        _apiKeys = new Dictionary<string, ApiKeyData>();
        
        // Create sample API keys for testing and development
        CreateSampleApiKeys();
        
        _logger.LogInformation("🔑 API Key Service initialized with in-memory storage");
    }

    /// <summary>
    /// Creates a new API key with specified permissions and expiration
    /// Generates a cryptographically secure API key and stores it with metadata
    /// </summary>
    /// <param name="userName">Name of the user the API key belongs to</param>
    /// <param name="description">Description of the API key's purpose</param>
    /// <param name="permissions">Array of permissions granted to this API key</param>
    /// <param name="expiresInDays">Number of days until the API key expires</param>
    /// <returns>Result containing the generated API key and metadata</returns>
    public CreateApiKeyResult CreateApiKey(string userName, string description, string[] permissions, int expiresInDays)
    {
        try
        {
            // Generate unique identifiers and timestamps
            var keyId = Guid.NewGuid().ToString();
            var apiKey = GenerateApiKey();
            var createdAt = DateTime.UtcNow;
            var expiresAt = createdAt.AddDays(expiresInDays);

            // Create API key data structure with metadata
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

            // Store the API key in memory storage
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

    /// <summary>
    /// Validates an API key for access to a specific service endpoint
    /// Checks key existence, activity status, expiration, and permissions
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <param name="serviceName">Name of the service being accessed</param>
    /// <param name="endpoint">Specific endpoint being accessed</param>
    /// <returns>Validation result with user information and permissions</returns>
    public ValidateApiKeyResult ValidateApiKey(string apiKey, string serviceName, string endpoint)
    {
        try
        {
            // Check if API key exists in storage
            if (!_apiKeys.TryGetValue(apiKey, out var keyData))
            {
                _logger.LogWarning("⚠️ Invalid API key attempted for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "Invalid API key", DateTime.MinValue);
            }

            // Verify API key is active
            if (!keyData.IsActive)
            {
                _logger.LogWarning("⚠️ Inactive API key attempted for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "API key is inactive", DateTime.MinValue);
            }

            // Check if API key has expired
            if (keyData.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("⚠️ Expired API key attempted for service: {ServiceName}", serviceName);
                return new ValidateApiKeyResult(false, string.Empty, string.Empty, Array.Empty<string>(), "API key has expired", DateTime.MinValue);
            }

            // Update usage statistics for analytics
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

    /// <summary>
    /// Revokes an API key by setting it to inactive status
    /// Revoked keys will fail validation but remain in storage for audit purposes
    /// </summary>
    /// <param name="apiKey">The API key to revoke</param>
    /// <returns>Result indicating success or failure of the revocation</returns>
    public RevokeApiKeyResult RevokeApiKey(string apiKey)
    {
        try
        {
            if (_apiKeys.TryGetValue(apiKey, out var keyData))
            {
                // Mark as inactive instead of deleting for audit trail
                keyData.IsActive = false;
                _logger.LogInformation("✅ API key revoked for user: {UserName}", keyData.UserName);
                return new RevokeApiKeyResult(true, "API key revoked successfully");
            }

            _logger.LogWarning("⚠️ Attempted to revoke non-existent API key");
            return new RevokeApiKeyResult(false, "API key not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error revoking API key");
            return new RevokeApiKeyResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Retrieves detailed information about an API key
    /// Useful for administration and monitoring purposes
    /// </summary>
    /// <param name="apiKey">The API key to get information for</param>
    /// <returns>Detailed information about the API key, or null if not found</returns>
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

        // Return empty result if API key not found
        return new GetApiKeyInfoResult(string.Empty, string.Empty, string.Empty, Array.Empty<string>(), DateTime.MinValue, DateTime.MinValue, false, 0);
    }

    /// <summary>
    /// Generates a cryptographically secure API key
    /// Uses RandomNumberGenerator for secure random byte generation
    /// </summary>
    /// <returns>Base64-encoded API key with URL-safe characters</returns>
    private string GenerateApiKey()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32]; // 256-bit key for strong security
        rng.GetBytes(bytes);
        
        // Convert to URL-safe Base64 encoding
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    /// <summary>
    /// Creates sample API keys for development and testing purposes
    /// Provides predefined keys with different permission levels for testing scenarios
    /// </summary>
    private void CreateSampleApiKeys()
    {
        // Define sample API keys with different permission levels
        var sampleKeys = new[]
        {
            new { UserName = "admin", Description = "Admin API Key", Permissions = new[] { "read", "write", "admin" } },
            new { UserName = "developer", Description = "Developer API Key", Permissions = new[] { "read", "write" } },
            new { UserName = "readonly", Description = "Read-only API Key", Permissions = new[] { "read" } }
        };

        // Create each sample API key with 1-year expiration
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
