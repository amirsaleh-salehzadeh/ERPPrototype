using Grpc.Core;
using ERP.IdentityService.Contracts;

namespace ERP.IdentityService.Services;

/// <summary>
/// gRPC service implementation for Identity Service
/// Handles API key validation and management via gRPC
/// </summary>
public class IdentityGrpcService : ERP.IdentityService.Contracts.IdentityService.IdentityServiceBase
{
    private readonly IApiKeyService _apiKeyService;
    private readonly ILogger<IdentityGrpcService> _logger;

    public IdentityGrpcService(IApiKeyService apiKeyService, ILogger<IdentityGrpcService> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Validates an API key via gRPC
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.ValidateApiKeyResponse> ValidateApiKey(ERP.IdentityService.Contracts.ValidateApiKeyRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🔍 gRPC ValidateApiKey called for service: {ServiceName}, endpoint: {Endpoint}", 
                request.ServiceName, request.Endpoint);

            var result = _apiKeyService.ValidateApiKey(request.ApiKey, request.ServiceName, request.Endpoint);

            var response = new ERP.IdentityService.Contracts.ValidateApiKeyResponse
            {
                IsValid = result.IsValid,
                UserId = result.UserId,
                UserName = result.UserName,
                ErrorMessage = result.ErrorMessage,
                ExpiresAt = ((DateTimeOffset)result.ExpiresAt).ToUnixTimeSeconds()
            };

            // Add permissions to the response
            response.Permissions.AddRange(result.Permissions);

            if (result.IsValid)
            {
                _logger.LogInformation("✅ gRPC API key validation successful for user: {UserName}", result.UserName);
            }
            else
            {
                _logger.LogWarning("🚫 gRPC API key validation failed: {Error}", result.ErrorMessage);
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC ValidateApiKey");
            
            var errorResponse = new ERP.IdentityService.Contracts.ValidateApiKeyResponse
            {
                IsValid = false,
                ErrorMessage = "Internal server error during validation"
            };
            
            return Task.FromResult(errorResponse);
        }
    }

    /// <summary>
    /// Creates a new API key via gRPC
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.CreateApiKeyResponse> CreateApiKey(ERP.IdentityService.Contracts.CreateApiKeyRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🔑 gRPC CreateApiKey called for user: {UserName}", request.UserName);

            var permissions = request.Permissions.ToArray();
            var result = _apiKeyService.CreateApiKey(request.UserName, request.Description, permissions, request.ExpiresInDays);

            var response = new ERP.IdentityService.Contracts.CreateApiKeyResponse
            {
                ApiKey = result.ApiKey,
                KeyId = result.KeyId,
                CreatedAt = ((DateTimeOffset)result.CreatedAt).ToUnixTimeSeconds(),
                ExpiresAt = ((DateTimeOffset)result.ExpiresAt).ToUnixTimeSeconds()
            };

            if (result.Success)
            {
                _logger.LogInformation("✅ gRPC API key created successfully for user: {UserName}, KeyId: {KeyId}", 
                    request.UserName, result.KeyId);
            }
            else
            {
                _logger.LogWarning("🚫 gRPC API key creation failed: {Error}", result.ErrorMessage);
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC CreateApiKey");
            
            var errorResponse = new ERP.IdentityService.Contracts.CreateApiKeyResponse
            {
                ApiKey = string.Empty,
                KeyId = string.Empty,
                CreatedAt = 0,
                ExpiresAt = 0
            };
            
            return Task.FromResult(errorResponse);
        }
    }

    /// <summary>
    /// Revokes an API key via gRPC
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.RevokeApiKeyResponse> RevokeApiKey(ERP.IdentityService.Contracts.RevokeApiKeyRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🗑️ gRPC RevokeApiKey called for key: {ApiKey}", request.ApiKey);

            var result = _apiKeyService.RevokeApiKey(request.ApiKey);

            var response = new ERP.IdentityService.Contracts.RevokeApiKeyResponse
            {
                Success = result.Success,
                Message = result.Message
            };

            if (result.Success)
            {
                _logger.LogInformation("✅ gRPC API key revoked successfully");
            }
            else
            {
                _logger.LogWarning("🚫 gRPC API key revocation failed: {Error}", result.Message);
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC RevokeApiKey");
            
            var errorResponse = new ERP.IdentityService.Contracts.RevokeApiKeyResponse
            {
                Success = false,
                Message = "Internal server error during API key revocation"
            };
            
            return Task.FromResult(errorResponse);
        }
    }

    /// <summary>
    /// Gets API key information via gRPC
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.GetApiKeyInfoResponse> GetApiKeyInfo(ERP.IdentityService.Contracts.GetApiKeyInfoRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("ℹ️ gRPC GetApiKeyInfo called for key: {ApiKey}", request.ApiKey);

            var result = _apiKeyService.GetApiKeyInfo(request.ApiKey);

            var response = new ERP.IdentityService.Contracts.GetApiKeyInfoResponse
            {
                KeyId = result.KeyId,
                UserName = result.UserName,
                Description = result.Description,
                CreatedAt = ((DateTimeOffset)result.CreatedAt).ToUnixTimeSeconds(),
                ExpiresAt = ((DateTimeOffset)result.ExpiresAt).ToUnixTimeSeconds(),
                IsActive = result.IsActive,
                UsageCount = result.UsageCount
            };

            // Add permissions to the response
            response.Permissions.AddRange(result.Permissions);

            _logger.LogInformation("✅ gRPC API key info retrieved for user: {UserName}", result.UserName);

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC GetApiKeyInfo");
            
            var errorResponse = new ERP.IdentityService.Contracts.GetApiKeyInfoResponse
            {
                KeyId = string.Empty,
                UserName = string.Empty,
                Description = "Error retrieving API key information"
            };
            
            return Task.FromResult(errorResponse);
        }
    }
}
