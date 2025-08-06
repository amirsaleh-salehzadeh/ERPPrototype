using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BFF.Gateway.Services;

/// <summary>
/// Service for validating JWT tokens in the BFF Gateway
/// </summary>
public interface IJwtValidationService
{
    /// <summary>
    /// Validates a JWT token and returns the claims principal
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
    
    /// <summary>
    /// Updates the public key from the Identity service
    /// </summary>
    Task UpdatePublicKeyAsync();
    
    /// <summary>
    /// Gets the current public key status
    /// </summary>
    bool IsPublicKeyLoaded { get; }
}

/// <summary>
/// Implementation of JWT validation service using RSA public key verification
/// </summary>
public class JwtValidationService : IJwtValidationService, IDisposable
{
    private readonly ILogger<JwtValidationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private RSA? _publicKey;
    private RsaSecurityKey? _validationKey;
    private readonly object _keyLock = new object();

    public JwtValidationService(
        ILogger<JwtValidationService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
        _tokenHandler = new JwtSecurityTokenHandler();
        
        // Initialize public key
        _ = Task.Run(UpdatePublicKeyAsync);
    }

    public bool IsPublicKeyLoaded => _publicKey != null && _validationKey != null;

    public async Task UpdatePublicKeyAsync()
    {
        try
        {
            var identityServiceUrl = _configuration.GetValue<string>("IdentityService:RestUrl") ?? "http://localhost:5007";
            var publicKeyEndpoint = $"{identityServiceUrl}/jwt/public-key";

            _logger.LogInformation("🔑 Fetching public key from Identity service: {Endpoint}", publicKeyEndpoint);

            var response = await _httpClient.GetAsync(publicKeyEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Failed to fetch public key from Identity service. Status: {StatusCode}", response.StatusCode);
                return;
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            // Parse JSON response to extract the PEM key
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
            if (!jsonDoc.RootElement.TryGetProperty("publicKeyPem", out var pemElement))
            {
                _logger.LogError("❌ Public key response does not contain 'publicKeyPem' property");
                return;
            }

            var publicKeyPem = pemElement.GetString();
            if (string.IsNullOrEmpty(publicKeyPem))
            {
                _logger.LogError("❌ Public key PEM is null or empty");
                return;
            }
            
            lock (_keyLock)
            {
                // Dispose old key
                _publicKey?.Dispose();
                
                // Load new public key
                _publicKey = RSA.Create();
                _publicKey.ImportFromPem(publicKeyPem);
                
                _validationKey = new RsaSecurityKey(_publicKey)
                {
                    KeyId = "erp-identity-validation-key"
                };
            }

            _logger.LogInformation("✅ Public key updated successfully from Identity service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to update public key from Identity service");
        }
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        lock (_keyLock)
        {
            if (_validationKey == null)
            {
                _logger.LogWarning("🚫 Cannot validate token: public key not loaded");
                return null;
            }

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _validationKey,
                    ValidateIssuer = true,
                    ValidIssuer = GetIssuer(),
                    ValidateAudience = true,
                    ValidAudience = GetAudience(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
                };

                var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                
                _logger.LogDebug("✅ JWT token validated successfully");
                return principal;
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogWarning("🚫 JWT token has expired");
                return null;
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                _logger.LogWarning("🚫 JWT token has invalid signature");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🚫 JWT token validation failed");
                return null;
            }
        }
    }

    private string GetIssuer()
    {
        return _configuration.GetValue<string>("JWT:Issuer") ?? "ERP.IdentityService";
    }

    private string GetAudience()
    {
        return _configuration.GetValue<string>("JWT:Audience") ?? "ERP.Services";
    }

    public void Dispose()
    {
        _publicKey?.Dispose();
    }
}
