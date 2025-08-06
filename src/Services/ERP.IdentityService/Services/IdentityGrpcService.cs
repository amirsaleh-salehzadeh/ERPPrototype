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
    private readonly IJwtTokenService _jwtTokenService;

    public IdentityGrpcService(IApiKeyService apiKeyService, ILogger<IdentityGrpcService> logger, IJwtTokenService jwtTokenService)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
        _jwtTokenService = jwtTokenService;
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

    /// <summary>
    /// Generates a JWT token for service-to-service communication
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.GenerateServiceTokenResponse> GenerateServiceToken(ERP.IdentityService.Contracts.GenerateServiceTokenRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🎫 gRPC GenerateServiceToken called for service: {ServiceName}", request.ServiceName);

            var expiration = request.ExpirationHours > 0 ? TimeSpan.FromHours(request.ExpirationHours) : TimeSpan.FromHours(1);
            var token = _jwtTokenService.GenerateServiceToken(request.ServiceName, request.Permissions.ToArray(), expiration);
            var expiresAt = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();

            var response = new ERP.IdentityService.Contracts.GenerateServiceTokenResponse
            {
                JwtToken = token,
                ExpiresAt = expiresAt,
                Success = true,
                ErrorMessage = string.Empty
            };

            _logger.LogInformation("✅ gRPC Service JWT token generated successfully for service: {ServiceName}", request.ServiceName);
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC GenerateServiceToken");

            var errorResponse = new ERP.IdentityService.Contracts.GenerateServiceTokenResponse
            {
                JwtToken = string.Empty,
                ExpiresAt = 0,
                Success = false,
                ErrorMessage = ex.Message
            };

            return Task.FromResult(errorResponse);
        }
    }

    /// <summary>
    /// Generates a JWT token for user authentication
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.GenerateUserTokenResponse> GenerateUserToken(ERP.IdentityService.Contracts.GenerateUserTokenRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🎫 gRPC GenerateUserToken called for user: {UserName} (ID: {UserId})", request.UserName, request.UserId);

            var expiration = request.ExpirationHours > 0 ? TimeSpan.FromHours(request.ExpirationHours) : TimeSpan.FromHours(8);
            var token = _jwtTokenService.GenerateUserToken(request.UserId, request.UserName, request.Permissions.ToArray(), expiration);
            var expiresAt = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();

            var response = new ERP.IdentityService.Contracts.GenerateUserTokenResponse
            {
                JwtToken = token,
                ExpiresAt = expiresAt,
                Success = true,
                ErrorMessage = string.Empty
            };

            _logger.LogInformation("✅ gRPC User JWT token generated successfully for user: {UserName} (ID: {UserId})", request.UserName, request.UserId);
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC GenerateUserToken");

            var errorResponse = new ERP.IdentityService.Contracts.GenerateUserTokenResponse
            {
                JwtToken = string.Empty,
                ExpiresAt = 0,
                Success = false,
                ErrorMessage = ex.Message
            };

            return Task.FromResult(errorResponse);
        }
    }

    /// <summary>
    /// Validates a JWT token
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.ValidateJwtTokenResponse> ValidateJwtToken(ERP.IdentityService.Contracts.ValidateJwtTokenRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🔍 gRPC ValidateJwtToken called");

            var principal = _jwtTokenService.ValidateToken(request.JwtToken);

            if (principal == null)
            {
                var invalidResponse = new ERP.IdentityService.Contracts.ValidateJwtTokenResponse
                {
                    IsValid = false,
                    ErrorMessage = "Invalid or expired JWT token"
                };
                return Task.FromResult(invalidResponse);
            }

            var tokenType = principal.FindFirst("token_type")?.Value ?? string.Empty;
            var userId = principal.FindFirst("user_id")?.Value ?? string.Empty;
            var userName = principal.FindFirst("user_name")?.Value ?? string.Empty;
            var serviceName = principal.FindFirst("service_name")?.Value ?? string.Empty;
            var permissions = principal.FindAll("permission").Select(c => c.Value).ToArray();
            var tokenId = principal.FindFirst("jti")?.Value ?? string.Empty;

            var response = new ERP.IdentityService.Contracts.ValidateJwtTokenResponse
            {
                IsValid = true,
                UserId = userId,
                UserName = userName,
                ServiceName = serviceName,
                TokenType = tokenType,
                TokenId = tokenId,
                ErrorMessage = string.Empty
            };

            response.Permissions.AddRange(permissions);

            _logger.LogInformation("✅ gRPC JWT token validated successfully, type: {TokenType}", tokenType);
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC ValidateJwtToken");

            var errorResponse = new ERP.IdentityService.Contracts.ValidateJwtTokenResponse
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };

            return Task.FromResult(errorResponse);
        }
    }

    /// <summary>
    /// Gets the public key for JWT validation
    /// </summary>
    public override Task<ERP.IdentityService.Contracts.GetPublicKeyResponse> GetPublicKey(ERP.IdentityService.Contracts.GetPublicKeyRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("🔑 gRPC GetPublicKey called");

            var publicKeyPem = _jwtTokenService.GetPublicKeyPem();

            var response = new ERP.IdentityService.Contracts.GetPublicKeyResponse
            {
                PublicKeyPem = publicKeyPem,
                KeyId = "erp-identity-validation-key",
                Success = true,
                ErrorMessage = string.Empty
            };

            _logger.LogInformation("✅ gRPC Public key retrieved successfully");
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in gRPC GetPublicKey");

            var errorResponse = new ERP.IdentityService.Contracts.GetPublicKeyResponse
            {
                PublicKeyPem = string.Empty,
                KeyId = string.Empty,
                Success = false,
                ErrorMessage = ex.Message
            };

            return Task.FromResult(errorResponse);
        }
    }
}
