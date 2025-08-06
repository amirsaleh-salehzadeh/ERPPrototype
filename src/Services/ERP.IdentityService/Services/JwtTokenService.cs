using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ERP.IdentityService.Services;

/// <summary>
/// Service for generating and validating JWT tokens
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for inter-service communication
    /// </summary>
    string GenerateServiceToken(string serviceName, string[] permissions, TimeSpan? expiration = null);
    
    /// <summary>
    /// Generates a JWT token for user authentication
    /// </summary>
    string GenerateUserToken(string userId, string userName, string[] permissions, TimeSpan? expiration = null);
    
    /// <summary>
    /// Validates a JWT token and returns the claims
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
    
    /// <summary>
    /// Gets the public key for token validation (for sharing with other services)
    /// </summary>
    string GetPublicKeyPem();
}

/// <summary>
/// Implementation of JWT token service using RSA signing
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly ICryptographicKeyService _keyService;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly IConfiguration _configuration;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(
        ICryptographicKeyService keyService,
        ILogger<JwtTokenService> logger,
        IConfiguration configuration)
    {
        _keyService = keyService;
        _logger = logger;
        _configuration = configuration;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public string GenerateServiceToken(string serviceName, string[] permissions, TimeSpan? expiration = null)
    {
        try
        {
            var exp = expiration ?? TimeSpan.FromHours(1); // Default 1 hour for service tokens
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, serviceName),
                new("service_name", serviceName),
                new("token_type", "service"),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add permissions as claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(exp),
                Issuer = GetIssuer(),
                Audience = GetAudience(),
                SigningCredentials = new SigningCredentials(_keyService.GetSigningKey(), SecurityAlgorithms.RsaSha256)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(token);

            _logger.LogInformation("🎫 Generated service JWT token for: {ServiceName}, expires: {Expiration}", 
                serviceName, tokenDescriptor.Expires);

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate service token for: {ServiceName}", serviceName);
            throw;
        }
    }

    public string GenerateUserToken(string userId, string userName, string[] permissions, TimeSpan? expiration = null)
    {
        try
        {
            var exp = expiration ?? TimeSpan.FromHours(8); // Default 8 hours for user tokens
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Name, userName),
                new("user_id", userId),
                new("user_name", userName),
                new("token_type", "user"),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add permissions as claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.Add(exp),
                Issuer = GetIssuer(),
                Audience = GetAudience(),
                SigningCredentials = new SigningCredentials(_keyService.GetSigningKey(), SecurityAlgorithms.RsaSha256)
            };

            var token = _tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = _tokenHandler.WriteToken(token);

            _logger.LogInformation("🎫 Generated user JWT token for: {UserName} (ID: {UserId}), expires: {Expiration}", 
                userName, userId, tokenDescriptor.Expires);

            return tokenString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate user token for: {UserName} (ID: {UserId})", userName, userId);
            throw;
        }
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _keyService.GetValidationKey(),
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

    public string GetPublicKeyPem()
    {
        return _keyService.ExportPublicKeyPem();
    }

    private string GetIssuer()
    {
        return _configuration.GetValue<string>("JWT:Issuer") ?? "ERP.IdentityService";
    }

    private string GetAudience()
    {
        return _configuration.GetValue<string>("JWT:Audience") ?? "ERP.Services";
    }
}
