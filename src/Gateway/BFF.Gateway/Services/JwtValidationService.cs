using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BFF.Gateway.Services;

/// <summary>
/// Service interface for JWT token validation in the BFF Gateway.
/// This service enables distributed JWT token verification by fetching
/// the RSA public key from the Identity Service and performing local validation.
///
/// Key Responsibilities:
/// - Fetch RSA public key from Identity Service via HTTP
/// - Perform local JWT token validation using the public key
/// - Handle key updates and rotation scenarios
/// - Provide validation status for middleware decision-making
///
/// Architecture Benefits:
/// - Reduces load on Identity Service (no validation requests)
/// - Enables offline token validation (after initial key fetch)
/// - Supports high-throughput scenarios with local validation
/// - Handles key rotation gracefully with automatic updates
/// </summary>
public interface IJwtValidationService
{
    /// <summary>
    /// Validates a JWT token using the locally cached RSA public key.
    /// This method performs cryptographic signature verification and
    /// claims validation without communicating with the Identity Service.
    ///
    /// Validation Process:
    /// 1. Check if public key is loaded and available
    /// 2. Verify RSA signature using cached public key
    /// 3. Validate token expiration and standard claims
    /// 4. Return ClaimsPrincipal for .NET authorization integration
    ///
    /// Performance: Local validation is much faster than remote calls
    /// Security: Same cryptographic security as Identity Service validation
    /// </summary>
    /// <param name="token">Base64-encoded JWT token to validate</param>
    /// <returns>ClaimsPrincipal with validated claims, or null if invalid</returns>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// Fetches the latest RSA public key from the Identity Service.
    /// This method makes an HTTP request to retrieve the current public key
    /// and updates the local cache for token validation operations.
    ///
    /// Update Scenarios:
    /// - Initial service startup (bootstrap key loading)
    /// - Key rotation events (periodic security updates)
    /// - Validation failures (potential key mismatch)
    /// - Manual refresh operations
    ///
    /// Network: Makes HTTP GET request to Identity Service
    /// Caching: Updates local RSA key instance for validation
    /// </summary>
    /// <returns>Task representing the asynchronous key update operation</returns>
    Task UpdatePublicKeyAsync();

    /// <summary>
    /// Indicates whether the RSA public key has been successfully loaded
    /// and is available for JWT token validation operations.
    ///
    /// Usage: Middleware checks this before attempting token validation
    /// States: true = ready for validation, false = key not loaded
    /// Fallback: When false, middleware may attempt key refresh or use API key auth
    /// </summary>
    bool IsPublicKeyLoaded { get; }
}

/// <summary>
/// Concrete implementation of JWT validation service for the BFF Gateway.
/// This service provides distributed JWT token validation by fetching the
/// RSA public key from the Identity Service and performing local cryptographic verification.
///
/// Architecture Features:
/// - HTTP-based public key retrieval from Identity Service
/// - Local RSA public key caching for performance
/// - Thread-safe key operations with locking
/// - Automatic key loading on service startup
/// - Graceful handling of key update failures
///
/// Performance Benefits:
/// - Local validation eliminates network calls per request
/// - Cached public key enables high-throughput validation
/// - Asynchronous key updates don't block validation operations
///
/// Security Features:
/// - Same cryptographic validation as Identity Service
/// - Automatic key rotation support
/// - Secure memory management for RSA keys
/// </summary>
public class JwtValidationService : IJwtValidationService, IDisposable
{
    #region Private Fields

    /// <summary>Logger for JWT validation operations and security events</summary>
    private readonly ILogger<JwtValidationService> _logger;

    /// <summary>Configuration provider for Identity Service URLs and JWT settings</summary>
    private readonly IConfiguration _configuration;

    /// <summary>HTTP client for fetching public key from Identity Service</summary>
    private readonly HttpClient _httpClient;

    /// <summary>Microsoft JWT token handler for token validation operations</summary>
    private readonly JwtSecurityTokenHandler _tokenHandler;

    /// <summary>Cached RSA public key instance for token validation</summary>
    private RSA? _publicKey;

    /// <summary>Microsoft.IdentityModel.Tokens wrapper for the RSA public key</summary>
    private RsaSecurityKey? _validationKey;

    /// <summary>Thread synchronization lock for key operations</summary>
    private readonly object _keyLock = new object();

    #endregion

    /// <summary>
    /// Initializes a new instance of the JWT validation service.
    /// Sets up all dependencies and initiates background public key loading
    /// to ensure the service is ready for token validation operations.
    /// </summary>
    /// <param name="logger">Logger for validation operations and security events</param>
    /// <param name="configuration">Configuration containing Identity Service URLs</param>
    /// <param name="httpClient">HTTP client for public key retrieval</param>
    public JwtValidationService(
        ILogger<JwtValidationService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;

        // Initialize Microsoft's JWT security token handler
        _tokenHandler = new JwtSecurityTokenHandler();

        // Start background task to load public key from Identity Service
        // Using Task.Run to avoid blocking the constructor
        _ = Task.Run(UpdatePublicKeyAsync);
    }

    /// <summary>
    /// Indicates whether the RSA public key has been successfully loaded and is ready for use.
    /// This property is checked by middleware to determine if JWT validation is available.
    ///
    /// Implementation: Returns true only when both RSA key and security key wrapper are loaded
    /// Thread Safety: Reads are atomic for reference types in .NET
    /// </summary>
    public bool IsPublicKeyLoaded => _publicKey != null && _validationKey != null;

    /// <summary>
    /// Fetches the RSA public key from the Identity Service and updates the local cache.
    /// This method makes an HTTP request to retrieve the current public key in PEM format
    /// and configures it for local JWT token validation operations.
    ///
    /// Update Process:
    /// 1. Construct Identity Service public key endpoint URL
    /// 2. Make HTTP GET request to fetch public key
    /// 3. Parse JSON response to extract PEM-encoded key
    /// 4. Import PEM key into RSA instance
    /// 5. Create Microsoft.IdentityModel.Tokens wrapper
    /// 6. Update cached keys with thread safety
    ///
    /// Error Handling:
    /// - Logs HTTP failures for monitoring
    /// - Handles malformed JSON responses
    /// - Gracefully handles PEM import errors
    /// - Preserves existing key on update failure
    /// </summary>
    /// <returns>Task representing the asynchronous key update operation</returns>
    public async Task UpdatePublicKeyAsync()
    {
        try
        {
            // Get Identity Service URL from configuration
            var identityServiceUrl = _configuration.GetValue<string>("IdentityService:RestUrl") ?? "http://localhost:5007";
            var publicKeyEndpoint = $"{identityServiceUrl}/jwt/public-key";

            // Log key fetch attempt for audit trail
            _logger.LogInformation("🔑 Fetching public key from Identity service: {Endpoint}", publicKeyEndpoint);

            // Make HTTP request to Identity Service public key endpoint
            var response = await _httpClient.GetAsync(publicKeyEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ Failed to fetch public key from Identity service. Status: {StatusCode}", response.StatusCode);
                return;
            }

            // Read response content as JSON string
            var responseContent = await response.Content.ReadAsStringAsync();

            // Parse JSON response to extract the PEM-encoded public key
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseContent);
            if (!jsonDoc.RootElement.TryGetProperty("publicKeyPem", out var pemElement))
            {
                _logger.LogError("❌ Public key response does not contain 'publicKeyPem' property");
                return;
            }

            // Extract PEM string from JSON property
            var publicKeyPem = pemElement.GetString();
            if (string.IsNullOrEmpty(publicKeyPem))
            {
                _logger.LogError("❌ Public key PEM is null or empty");
                return;
            }

            // Thread-safe key update operation
            lock (_keyLock)
            {
                // Dispose old RSA key to prevent memory leaks
                _publicKey?.Dispose();

                // Create new RSA instance and import PEM key
                _publicKey = RSA.Create();
                _publicKey.ImportFromPem(publicKeyPem);

                // Create Microsoft.IdentityModel.Tokens wrapper for JWT validation
                _validationKey = new RsaSecurityKey(_publicKey)
                {
                    KeyId = "erp-identity-validation-key" // Match Identity Service key ID
                };
            }

            // Log successful key update for audit trail
            _logger.LogInformation("✅ Public key updated successfully from Identity service");
        }
        catch (Exception ex)
        {
            // Log key update failure for monitoring and troubleshooting
            _logger.LogError(ex, "❌ Failed to update public key from Identity service");
        }
    }

    /// <summary>
    /// Validates a JWT token using the locally cached RSA public key.
    /// This method performs the same cryptographic validation as the Identity Service
    /// but uses the cached public key for high-performance local validation.
    ///
    /// Validation Process:
    /// 1. Check if public key is loaded and available
    /// 2. Configure validation parameters (signature, issuer, audience, lifetime)
    /// 3. Verify RSA signature using cached public key
    /// 4. Validate token expiration with clock skew tolerance
    /// 5. Validate issuer and audience claims
    /// 6. Extract and return claims as ClaimsPrincipal
    ///
    /// Performance Benefits:
    /// - No network calls to Identity Service
    /// - Local cryptographic operations are very fast
    /// - Suitable for high-throughput scenarios
    ///
    /// Security Features:
    /// - Same cryptographic security as Identity Service validation
    /// - Comprehensive validation of all security claims
    /// - Proper error handling for different failure scenarios
    ///
    /// Thread Safety: Uses lock to ensure thread-safe access to validation key
    /// </summary>
    /// <param name="token">Base64-encoded JWT token to validate</param>
    /// <returns>ClaimsPrincipal with validated claims, or null if invalid</returns>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        // Thread-safe access to validation key
        lock (_keyLock)
        {
            // Ensure public key is loaded before attempting validation
            if (_validationKey == null)
            {
                _logger.LogWarning("🚫 Cannot validate token: public key not loaded");
                return null;
            }

            try
            {
                // Configure comprehensive token validation parameters
                var validationParameters = new TokenValidationParameters
                {
                    // Enable signature validation using cached RSA public key
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _validationKey,

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

                // Perform comprehensive token validation
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
    }

    #region Private Configuration Helpers

    /// <summary>
    /// Gets the expected JWT issuer identifier from configuration.
    /// This must match the issuer set by the Identity Service to ensure
    /// tokens are only accepted from the trusted Identity Service.
    ///
    /// Configuration: JWT:Issuer
    /// Default: "ERP.IdentityService"
    /// Security: Prevents tokens from unauthorized issuers
    /// </summary>
    /// <returns>Expected JWT issuer identifier string</returns>
    private string GetIssuer()
    {
        return _configuration.GetValue<string>("JWT:Issuer") ?? "ERP.IdentityService";
    }

    /// <summary>
    /// Gets the expected JWT audience identifier from configuration.
    /// This must match the audience set by the Identity Service to ensure
    /// tokens are intended for this ERP system's services.
    ///
    /// Configuration: JWT:Audience
    /// Default: "ERP.Services"
    /// Security: Prevents tokens intended for other systems
    /// </summary>
    /// <returns>Expected JWT audience identifier string</returns>
    private string GetAudience()
    {
        return _configuration.GetValue<string>("JWT:Audience") ?? "ERP.Services";
    }

    #endregion

    /// <summary>
    /// Disposes of RSA key instances to prevent memory leaks.
    /// This method ensures that cryptographic material is properly
    /// cleared from memory when the service is disposed.
    ///
    /// Security: Prevents RSA keys from remaining in memory
    /// Memory Management: Frees unmanaged cryptographic resources
    /// </summary>
    public void Dispose()
    {
        // Dispose RSA instance to clear cryptographic material from memory
        _publicKey?.Dispose();
    }
}
