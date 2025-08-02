namespace ERP.IdentityService.Services;

public interface IApiKeyService
{
    CreateApiKeyResult CreateApiKey(string userName, string description, string[] permissions, int expiresInDays);
    ValidateApiKeyResult ValidateApiKey(string apiKey, string serviceName, string endpoint);
    RevokeApiKeyResult RevokeApiKey(string apiKey);
    GetApiKeyInfoResult GetApiKeyInfo(string apiKey);
}

public record CreateApiKeyResult(bool Success, string ApiKey, string KeyId, DateTime CreatedAt, DateTime ExpiresAt, string ErrorMessage);

public record ValidateApiKeyResult(bool IsValid, string UserId, string UserName, string[] Permissions, string ErrorMessage, DateTime ExpiresAt);

public record RevokeApiKeyResult(bool Success, string Message);

public record GetApiKeyInfoResult(string KeyId, string UserName, string Description, string[] Permissions, DateTime CreatedAt, DateTime ExpiresAt, bool IsActive, int UsageCount);
