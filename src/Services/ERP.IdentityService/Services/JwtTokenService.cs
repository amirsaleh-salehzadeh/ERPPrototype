using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ERP.IdentityService.Services;

/// <summary>
/// Service interface for JWT token generation and validation operations.
/// This service provides the core JWT functionality for the ERP system's
/// authentication and authorization infrastructure.
///
/// Key Responsibilities:
/// - Generate cryptographically signed JWT tokens for users and services
/// - Validate JWT token signatures and claims
/// - Provide public key access for distributed token verification
/// - Support different token types with appropriate lifetimes
///
/// Security Features:
/// - RSA-256 digital signatures for token integrity
/// - Claims-based authorization with fine-grained permissions
/// - Configurable token expiration for different use cases
/// - Stateless token validation without database lookups
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token specifically for inter-service communication.
    /// Service tokens are designed for machine-to-machine authentication
    /// with shorter lifetimes and service-specific claims.
    ///
    /// Token Characteristics:
    /// - Default expiration: 1 hour (configurable)
    /// - Claims: service_name, token_type, permissions, jti
    /// - Purpose: Authenticate service requests to other services
    /// - Security: Signed with RSA private key
    /// </summary>
    /// <param name="serviceName">Name of the service requesting the token</param>
    /// <param name="permissions">Array of permissions granted to the service</param>
    /// <param name="expiration">Optional custom expiration time (defaults to 1 hour)</param>
    /// <returns>Base64-encoded JWT token string</returns>
    string GenerateServiceToken(string serviceName, string[] permissions, TimeSpan? expiration = null);

    /// <summary>
    /// Generates a JWT token for user authentication and authorization.
    /// User tokens are designed for client applications with longer lifetimes
    /// and user-specific claims for personalized access control.
    ///
    /// Token Characteristics:
    /// - Default expiration: 8 hours (configurable)
    /// - Claims: user_id, user_name, token_type, permissions, jti
    /// - Purpose: Authenticate user requests from client applications
    /// - Security: Signed with RSA private key
    /// </summary>
    /// <param name="userId">Unique identifier for the user</param>
    /// <param name="userName">Human-readable username</param>
    /// <param name="permissions">Array of permissions granted to the user</param>
    /// <param name="expiration">Optional custom expiration time (defaults to 8 hours)</param>
    /// <returns>Base64-encoded JWT token string</returns>
    string GenerateUserToken(string userId, string userName, string[] permissions, TimeSpan? expiration = null);

    /// <summary>
    /// Validates a JWT token's signature and extracts claims for authorization.
    /// This method performs cryptographic verification using the RSA public key
    /// and returns a ClaimsPrincipal for .NET authorization integration.
    ///
    /// Validation Process:
    /// 1. Verify RSA signature using public key
    /// 2. Check token expiration (nbf/exp claims)
    /// 3. Validate issuer and audience claims
    /// 4. Extract and return user/service claims
    ///
    /// Security: Returns null for invalid/expired/tampered tokens
    /// </summary>
    /// <param name="token">Base64-encoded JWT token to validate</param>
    /// <returns>ClaimsPrincipal with token claims, or null if invalid</returns>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Retrieves the RSA public key in PEM format for distribution to other services.
    /// This allows other services (like BFF Gateway) to independently validate
    /// JWT tokens without communicating with the Identity Service.
    ///
    /// Usage: Called by other services to obtain the verification key
    /// Format: PEM-encoded RSA public key (PKCS#1 SubjectPublicKeyInfo)
    /// Security: Public key can be safely transmitted over HTTP
    /// </summary>
    /// <returns>RSA public key in PEM format</returns>
    string GetPublicKeyPem();
}

/// <summary>
/// Concrete implementation of JWT token service using RSA digital signatures.
/// This service provides enterprise-grade JWT token generation and validation
/// with strong cryptographic security and comprehensive claims management.
///
/// Architecture Features:
/// - RSA-256 digital signatures for token integrity
/// - Configurable token lifetimes for different use cases
/// - Claims-based authorization with custom permissions
/// - Integration with Microsoft.IdentityModel.Tokens
/// - Comprehensive logging for security auditing
///
/// Security Implementation:
/// - Private key signing ensures token authenticity
/// - Public key validation enables distributed verification
/// - Token expiration prevents replay attacks
/// - Issuer/Audience validation prevents token misuse
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    #region Private Fields

    /// <summary>Cryptographic key service for RSA operations</summary>
    private readonly ICryptographicKeyService _keyService;

    /// <summary>Logger for JWT operations and security events</summary>
    private readonly ILogger<JwtTokenService> _logger;

    /// <summary>Configuration provider for JWT settings</summary>
    private readonly IConfiguration _configuration;

    /// <summary>Microsoft JWT token handler for token operations</summary>
    private readonly JwtSecurityTokenHandler _tokenHandler;

    #endregion

    /// <summary>
    /// Initializes a new instance of the JWT token service.
    /// Sets up all dependencies required for JWT token operations
    /// including cryptographic keys, logging, and configuration.
    /// </summary>
    /// <param name="keyService">Service providing RSA keys for signing/validation</param>
    /// <param name="logger">Logger for security and operational events</param>
    /// <param name="configuration">Configuration containing JWT settings</param>
    public JwtTokenService(
        ICryptographicKeyService keyService,
        ILogger<JwtTokenService> logger,
        IConfiguration configuration)
    {
        _keyService = keyService;
        _logger = logger;
        _configuration = configuration;

        // Initialize Microsoft's JWT security token handler
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <summary>
    /// Generates a JWT token specifically designed for inter-service communication.
    /// Service tokens have shorter lifetimes and service-specific claims optimized
    /// for machine-to-machine authentication scenarios.
    ///
    /// Token Generation Process:
    /// 1. Set default expiration (1 hour) or use custom value
    /// 2. Create service-specific claims (service_name, token_type)
    /// 3. Add standard JWT claims (jti, iat) for tracking
    /// 4. Include permission claims for authorization
    /// 5. Sign token with RSA private key
    /// 6. Return Base64-encoded token string
    ///
    /// Security Features:
    /// - RSA-256 digital signature for authenticity
    /// - Unique token ID (jti) for tracking and revocation
    /// - Issued-at timestamp (iat) for audit trails
    /// - Short expiration to limit exposure window
    /// </summary>
    /// <param name="serviceName">Name of the service requesting authentication</param>
    /// <param name="permissions">Array of permissions granted to the service</param>
    /// <param name="expiration">Optional custom expiration (defaults to 1 hour)</param>
    /// <returns>Cryptographically signed JWT token string</returns>
    public string GenerateServiceToken(string serviceName, string[] permissions, TimeSpan? expiration = null)
    {
        try
        {
            // Use provided expiration or default to 1 hour for service tokens
            var exp = expiration ?? TimeSpan.FromHours(1);

            // Build claims collection for service authentication
            var claims = new List<Claim>
            {
                // Standard name claim for compatibility
                new(ClaimTypes.Name, serviceName),

                // Custom service name claim for service identification
                new("service_name", serviceName),

                // Token type identifier for authorization logic
                new("token_type", "service"),

                // Unique token identifier for tracking and potential revocation
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Issued-at timestamp for audit trails and token age verification
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add each permission as a separate claim for fine-grained authorization
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            // Create token descriptor with all security parameters
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                // Set the claims identity for the token
                Subject = new ClaimsIdentity(claims),

                // Set token expiration time
                Expires = DateTime.UtcNow.Add(exp),

                // Set token issuer (this Identity Service)
                Issuer = GetIssuer(),

                // Set intended audience (ERP services)
                Audience = GetAudience(),

                // Configure RSA-256 signing with private key
                SigningCredentials = new SigningCredentials(_keyService.GetSigningKey(), SecurityAlgorithms.RsaSha256)
            };

            // Generate the actual JWT token
            var token = _tokenHandler.CreateToken(tokenDescriptor);

            // Convert token to Base64-encoded string
            var tokenString = _tokenHandler.WriteToken(token);

            // Log successful token generation for audit trail
            _logger.LogInformation("🎫 Generated service JWT token for: {ServiceName}, expires: {Expiration}",
                serviceName, tokenDescriptor.Expires);

            return tokenString;
        }
        catch (Exception ex)
        {
            // Log token generation failure for security monitoring
            _logger.LogError(ex, "❌ Failed to generate service token for: {ServiceName}", serviceName);
            throw;
        }
    }

    /// <summary>
    /// Generates a JWT token specifically designed for user authentication and authorization.
    /// User tokens have longer lifetimes and user-specific claims optimized for
    /// client application scenarios with personalized access control.
    ///
    /// Token Generation Process:
    /// 1. Set default expiration (8 hours) or use custom value
    /// 2. Create user-specific claims (user_id, user_name, token_type)
    /// 3. Add standard JWT claims (jti, iat) for tracking
    /// 4. Include permission claims for fine-grained authorization
    /// 5. Sign token with RSA private key
    /// 6. Return Base64-encoded token string
    ///
    /// Security Features:
    /// - RSA-256 digital signature for authenticity
    /// - Unique token ID (jti) for tracking and potential revocation
    /// - Issued-at timestamp (iat) for audit trails
    /// - Longer expiration suitable for user sessions
    /// - Standard .NET claims for framework integration
    /// </summary>
    /// <param name="userId">Unique identifier for the user</param>
    /// <param name="userName">Human-readable username for display</param>
    /// <param name="permissions">Array of permissions granted to the user</param>
    /// <param name="expiration">Optional custom expiration (defaults to 8 hours)</param>
    /// <returns>Cryptographically signed JWT token string</returns>
    public string GenerateUserToken(string userId, string userName, string[] permissions, TimeSpan? expiration = null)
    {
        try
        {
            // Use provided expiration or default to 8 hours for user tokens
            var exp = expiration ?? TimeSpan.FromHours(8);

            // Build claims collection for user authentication
            var claims = new List<Claim>
            {
                // Standard .NET user identifier claim
                new(ClaimTypes.NameIdentifier, userId),

                // Standard .NET name claim for display purposes
                new(ClaimTypes.Name, userName),

                // Custom user ID claim for explicit user identification
                new("user_id", userId),

                // Custom user name claim for service-to-service communication
                new("user_name", userName),

                // Token type identifier for authorization logic
                new("token_type", "user"),

                // Unique token identifier for tracking and potential revocation
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Issued-at timestamp for audit trails and token age verification
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            // Add each permission as a separate claim for fine-grained authorization
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            // Create token descriptor with all security parameters
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                // Set the claims identity for the token
                Subject = new ClaimsIdentity(claims),

                // Set token expiration time
                Expires = DateTime.UtcNow.Add(exp),

                // Set token issuer (this Identity Service)
                Issuer = GetIssuer(),

                // Set intended audience (ERP services)
                Audience = GetAudience(),

                // Configure RSA-256 signing with private key
                SigningCredentials = new SigningCredentials(_keyService.GetSigningKey(), SecurityAlgorithms.RsaSha256)
            };

            // Generate the actual JWT token
            var token = _tokenHandler.CreateToken(tokenDescriptor);

            // Convert token to Base64-encoded string
            var tokenString = _tokenHandler.WriteToken(token);

            // Log successful token generation for audit trail
            _logger.LogInformation("🎫 Generated user JWT token for: {UserName} (ID: {UserId}), expires: {Expiration}",
                userName, userId, tokenDescriptor.Expires);

            return tokenString;
        }
        catch (Exception ex)
        {
            // Log token generation failure for security monitoring
            _logger.LogError(ex, "❌ Failed to generate user token for: {UserName} (ID: {UserId})", userName, userId);
            throw;
        }
    }

    /// <summary>
    /// Validates a JWT token's cryptographic signature and claims.
    /// This method performs comprehensive token validation including signature
    /// verification, expiration checking, and issuer/audience validation.
    ///
    /// Validation Process:
    /// 1. Configure validation parameters (keys, issuer, audience, lifetime)
    /// 2. Verify RSA signature using public key
    /// 3. Check token expiration with clock skew tolerance
    /// 4. Validate issuer matches this Identity Service
    /// 5. Validate audience matches ERP services
    /// 6. Extract and return claims as ClaimsPrincipal
    ///
    /// Security Features:
    /// - Cryptographic signature verification prevents tampering
    /// - Expiration checking prevents replay attacks
    /// - Issuer/audience validation prevents token misuse
    /// - Clock skew tolerance handles minor time differences
    ///
    /// Error Handling:
    /// - Returns null for any validation failure
    /// - Logs specific failure reasons for security monitoring
    /// - Handles expired, invalid signature, and malformed tokens
    /// </summary>
    /// <param name="token">Base64-encoded JWT token to validate</param>
    /// <returns>ClaimsPrincipal with validated claims, or null if invalid</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            // Configure comprehensive token validation parameters
            var validationParameters = new TokenValidationParameters
            {
                // Enable signature validation using RSA public key
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _keyService.GetValidationKey(),

                // Enable issuer validation to prevent token misuse
                ValidateIssuer = true,
                ValidIssuer = GetIssuer(),

                // Enable audience validation to ensure token is for ERP services
                ValidateAudience = true,
                ValidAudience = GetAudience(),

                // Enable lifetime validation to prevent expired token use
                ValidateLifetime = true,

                // Allow 5 minutes clock skew to handle minor time differences
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            // Perform token validation and extract claims
            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            // Log successful validation for audit trail
            _logger.LogDebug("✅ JWT token validated successfully");
            return principal;
        }
        catch (SecurityTokenExpiredException)
        {
            // Handle expired tokens - common in normal operation
            _logger.LogWarning("🚫 JWT token has expired");
            return null;
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            // Handle signature validation failures - potential security issue
            _logger.LogWarning("🚫 JWT token has invalid signature");
            return null;
        }
        catch (Exception ex)
        {
            // Handle any other validation failures (malformed tokens, etc.)
            _logger.LogWarning(ex, "🚫 JWT token validation failed");
            return null;
        }
    }

    /// <summary>
    /// Retrieves the RSA public key in PEM format for distribution to other services.
    /// This method delegates to the cryptographic key service to export the public
    /// key in a standardized format that can be transmitted and used by other services.
    ///
    /// Usage Scenarios:
    /// - BFF Gateway fetches this key to validate JWT tokens locally
    /// - Other microservices can use this key for token verification
    /// - Load balancers or API gateways can cache this key
    ///
    /// Security: Public key can be safely transmitted over HTTP/HTTPS
    /// Format: PEM-encoded RSA public key (PKCS#1 SubjectPublicKeyInfo)
    /// </summary>
    /// <returns>RSA public key in PEM format</returns>
    public string GetPublicKeyPem()
    {
        // Delegate to cryptographic key service for PEM export
        return _keyService.ExportPublicKeyPem();
    }

    #region Private Configuration Helpers

    /// <summary>
    /// Gets the JWT issuer identifier from configuration.
    /// The issuer identifies this Identity Service as the token creator
    /// and is validated by token consumers to prevent token misuse.
    ///
    /// Configuration: JWT:Issuer
    /// Default: "ERP.IdentityService"
    /// Purpose: Prevents tokens from other systems being accepted
    /// </summary>
    /// <returns>JWT issuer identifier string</returns>
    private string GetIssuer()
    {
        return _configuration.GetValue<string>("JWT:Issuer") ?? "ERP.IdentityService";
    }

    /// <summary>
    /// Gets the JWT audience identifier from configuration.
    /// The audience identifies the intended consumers of the tokens
    /// and is validated by token consumers to ensure tokens are intended for them.
    ///
    /// Configuration: JWT:Audience
    /// Default: "ERP.Services"
    /// Purpose: Prevents tokens intended for other systems being accepted
    /// </summary>
    /// <returns>JWT audience identifier string</returns>
    private string GetAudience()
    {
        return _configuration.GetValue<string>("JWT:Audience") ?? "ERP.Services";
    }

    #endregion
}
