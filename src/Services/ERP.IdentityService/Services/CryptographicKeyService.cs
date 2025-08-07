using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;

namespace ERP.IdentityService.Services;

/// <summary>
/// Service interface for managing RSA key pairs used in JWT token signing and verification.
/// This service handles the cryptographic foundation of the JWT security system by:
/// - Generating and storing RSA public/private key pairs
/// - Providing keys for JWT token signing (private key) and verification (public key)
/// - Supporting key rotation for enhanced security
/// - Exporting public keys for distribution to other services
/// </summary>
public interface ICryptographicKeyService
{
    /// <summary>
    /// Gets the RSA private key used for JWT token signing.
    /// The private key is kept secure within the Identity Service and never shared.
    /// Used by the JWT token service to create cryptographically signed tokens.
    /// </summary>
    /// <returns>RSA private key instance</returns>
    RSA GetPrivateKey();

    /// <summary>
    /// Gets the RSA public key used for JWT token verification.
    /// This key can be safely shared with other services for token validation.
    /// </summary>
    /// <returns>RSA public key instance</returns>
    RSA GetPublicKey();

    /// <summary>
    /// Gets the RSA security key wrapper for JWT signing operations.
    /// This provides the Microsoft.IdentityModel.Tokens compatible key format
    /// required by the JWT token handler for signing operations.
    /// </summary>
    /// <returns>RsaSecurityKey configured for signing</returns>
    RsaSecurityKey GetSigningKey();

    /// <summary>
    /// Gets the RSA security key wrapper for JWT verification operations.
    /// This provides the Microsoft.IdentityModel.Tokens compatible key format
    /// required by the JWT token handler for validation operations.
    /// </summary>
    /// <returns>RsaSecurityKey configured for validation</returns>
    RsaSecurityKey GetValidationKey();

    /// <summary>
    /// Exports the public key in PEM (Privacy-Enhanced Mail) format.
    /// This standardized format allows the public key to be easily shared
    /// with other services (like the BFF Gateway) for JWT token verification.
    /// Uses PKCS#1 SubjectPublicKeyInfo format for maximum compatibility.
    /// </summary>
    /// <returns>Public key in PEM format as string</returns>
    string ExportPublicKeyPem();

    /// <summary>
    /// Rotates the RSA key pair by generating new keys and replacing the old ones.
    /// This is a critical security operation that should be performed periodically
    /// to maintain cryptographic security. After rotation, all existing JWT tokens
    /// signed with the old key will become invalid.
    /// </summary>
    /// <returns>Task representing the asynchronous key rotation operation</returns>
    Task RotateKeysAsync();
}

/// <summary>
/// Concrete implementation of the cryptographic key service using RSA key pairs.
/// This service manages the complete lifecycle of RSA keys used for JWT token security:
///
/// Key Management Features:
/// - Automatic key generation on first startup
/// - Persistent key storage using JSON serialization
/// - Thread-safe key operations using locking
/// - Secure key disposal to prevent memory leaks
/// - Support for key rotation without service restart
///
/// Security Considerations:
/// - Private keys are stored locally and never transmitted
/// - Public keys can be safely shared with other services
/// - Keys are generated with 2048-bit strength (industry standard)
/// - Memory cleanup ensures keys don't persist in memory after disposal
/// </summary>
public class CryptographicKeyService : ICryptographicKeyService, IDisposable
{
    #region Private Fields

    /// <summary>Logger for cryptographic operations and security events</summary>
    private readonly ILogger<CryptographicKeyService> _logger;

    /// <summary>Configuration provider for JWT and key storage settings</summary>
    private readonly IConfiguration _configuration;

    /// <summary>RSA private key instance used for JWT token signing</summary>
    private RSA? _privateKey;

    /// <summary>RSA public key instance used for JWT token verification</summary>
    private RSA? _publicKey;

    /// <summary>File system path where RSA keys are persisted</summary>
    private readonly string _keyStoragePath;

    /// <summary>Thread synchronization lock for key operations</summary>
    private readonly object _keyLock = new object();

    #endregion

    /// <summary>
    /// Initializes a new instance of the CryptographicKeyService.
    /// This constructor performs the following operations:
    /// 1. Configures logging and settings
    /// 2. Sets up key storage directory
    /// 3. Loads existing keys or generates new ones
    /// 4. Ensures the service is ready for JWT operations
    /// </summary>
    /// <param name="logger">Logger for security and operational events</param>
    /// <param name="configuration">Configuration containing JWT settings</param>
    public CryptographicKeyService(ILogger<CryptographicKeyService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Get key storage path from configuration, default to "keys" directory
        _keyStoragePath = _configuration.GetValue<string>("JWT:KeyStoragePath") ?? "keys";

        // Ensure the key storage directory exists for persistent key storage
        Directory.CreateDirectory(_keyStoragePath);

        // Initialize the RSA key pair (load existing or generate new)
        InitializeKeys();
    }

    /// <summary>
    /// Initializes the RSA key pair by either loading existing keys from storage
    /// or generating a new pair if none exist. This method ensures the service
    /// always has valid keys available for JWT operations.
    ///
    /// Process Flow:
    /// 1. Check if key files exist in storage
    /// 2. If exists: Load and deserialize keys from JSON files
    /// 3. If not exists: Generate new 2048-bit RSA key pair
    /// 4. Save new keys to persistent storage
    /// 5. Log the initialization result for security auditing
    ///
    /// Thread Safety: Uses lock to ensure atomic key initialization
    /// </summary>
    private void InitializeKeys()
    {
        // Use lock to ensure thread-safe key initialization
        lock (_keyLock)
        {
            try
            {
                // Define file paths for persistent key storage
                var privateKeyPath = Path.Combine(_keyStoragePath, "private_key.json");
                var publicKeyPath = Path.Combine(_keyStoragePath, "public_key.json");

                // Check if both key files exist (complete key pair required)
                if (File.Exists(privateKeyPath) && File.Exists(publicKeyPath))
                {
                    // Load existing keys from JSON storage
                    LoadKeysFromStorage();
                    _logger.LogInformation("🔑 Loaded existing RSA key pair from storage");
                }
                else
                {
                    // Generate new RSA key pair (first startup or missing keys)
                    GenerateNewKeyPair();

                    // Persist the new keys to storage for future use
                    SaveKeysToStorage();

                    _logger.LogInformation("🔑 Generated new RSA key pair and saved to storage");
                }
            }
            catch (Exception ex)
            {
                // Critical error - service cannot function without keys
                _logger.LogError(ex, "❌ Failed to initialize cryptographic keys");
                throw;
            }
        }
    }

    /// <summary>
    /// Generates a new RSA key pair with 2048-bit key strength.
    /// This method creates both private and public keys that are mathematically
    /// related but cryptographically secure. The private key can sign tokens,
    /// while the public key can verify those signatures.
    ///
    /// Security Notes:
    /// - Uses 2048-bit key size (industry standard for RSA)
    /// - Private key contains all parameters (p, q, d, etc.)
    /// - Public key contains only modulus (n) and exponent (e)
    /// </summary>
    private void GenerateNewKeyPair()
    {
        // Create RSA instance with 2048-bit key size for strong security
        _privateKey = RSA.Create(2048);

        // Create separate RSA instance for public key
        _publicKey = RSA.Create();

        // Extract public key parameters from private key (n, e only)
        var publicKeyParameters = _privateKey.ExportParameters(false);

        // Import public parameters into the public key instance
        _publicKey.ImportParameters(publicKeyParameters);
    }

    /// <summary>
    /// Loads RSA key pair from persistent JSON storage.
    /// This method deserializes previously saved keys and reconstructs
    /// the RSA instances for use in JWT operations.
    ///
    /// Storage Format:
    /// - Keys are stored as JSON files with Base64-encoded parameters
    /// - Private key file contains all RSA parameters
    /// - Public key file contains only public parameters
    ///
    /// Security: Private key parameters are never exposed outside this service
    /// </summary>
    private void LoadKeysFromStorage()
    {
        // Define file paths for key storage
        var privateKeyPath = Path.Combine(_keyStoragePath, "private_key.json");
        var publicKeyPath = Path.Combine(_keyStoragePath, "public_key.json");

        // Load and deserialize private key from JSON storage
        var privateKeyJson = File.ReadAllText(privateKeyPath);
        var privateKeyData = JsonSerializer.Deserialize<RSAKeyData>(privateKeyJson);

        // Create new RSA instance and import private key parameters
        _privateKey = RSA.Create();
        _privateKey.ImportParameters(privateKeyData!.ToRSAParameters(true)); // true = include private parameters

        // Load and deserialize public key from JSON storage
        var publicKeyJson = File.ReadAllText(publicKeyPath);
        var publicKeyData = JsonSerializer.Deserialize<RSAKeyData>(publicKeyJson);

        // Create new RSA instance and import public key parameters
        _publicKey = RSA.Create();
        _publicKey.ImportParameters(publicKeyData!.ToRSAParameters(false)); // false = public parameters only
    }

    /// <summary>
    /// Saves the current RSA key pair to persistent JSON storage.
    /// This method serializes the RSA parameters to Base64-encoded JSON
    /// for secure storage and future loading.
    ///
    /// Storage Strategy:
    /// - Private key: Stored with all parameters (p, q, d, dp, dq, inverseQ)
    /// - Public key: Stored with only public parameters (n, e)
    /// - JSON format: Human-readable with indentation for debugging
    /// - File permissions: Should be restricted to service account only
    ///
    /// Security: Private key file should have restricted file system permissions
    /// </summary>
    private void SaveKeysToStorage()
    {
        // Define file paths for persistent storage
        var privateKeyPath = Path.Combine(_keyStoragePath, "private_key.json");
        var publicKeyPath = Path.Combine(_keyStoragePath, "public_key.json");

        // Export and save private key with all parameters
        var privateKeyParameters = _privateKey!.ExportParameters(true); // true = include private parameters
        var privateKeyData = RSAKeyData.FromRSAParameters(privateKeyParameters, true);
        var privateKeyJson = JsonSerializer.Serialize(privateKeyData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(privateKeyPath, privateKeyJson);

        // Export and save public key with only public parameters
        var publicKeyParameters = _privateKey.ExportParameters(false); // false = public parameters only
        var publicKeyData = RSAKeyData.FromRSAParameters(publicKeyParameters, false);
        var publicKeyJson = JsonSerializer.Serialize(publicKeyData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(publicKeyPath, publicKeyJson);
    }

    #region Public Key Access Methods

    /// <summary>
    /// Retrieves the RSA private key used for JWT token signing.
    /// This key must be kept secure and never transmitted outside the Identity Service.
    ///
    /// Usage: Used by JWT token service to create cryptographic signatures
    /// Security: Private key access should be logged for audit purposes
    /// Thread Safety: Uses lock to ensure thread-safe access
    /// </summary>
    /// <returns>RSA private key instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if private key is not initialized</exception>
    public RSA GetPrivateKey()
    {
        lock (_keyLock)
        {
            // Ensure private key is available before returning
            if (_privateKey == null)
                throw new InvalidOperationException("Private key not initialized");
            return _privateKey;
        }
    }

    /// <summary>
    /// Retrieves the RSA public key used for JWT token verification.
    /// This key can be safely shared with other services for token validation.
    ///
    /// Usage: Used by other services (via PEM export) to verify JWT signatures
    /// Security: Public key can be freely distributed
    /// Thread Safety: Uses lock to ensure thread-safe access
    /// </summary>
    /// <returns>RSA public key instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if public key is not initialized</exception>
    public RSA GetPublicKey()
    {
        lock (_keyLock)
        {
            // Ensure public key is available before returning
            if (_publicKey == null)
                throw new InvalidOperationException("Public key not initialized");
            return _publicKey;
        }
    }

    /// <summary>
    /// Creates an RsaSecurityKey wrapper for JWT token signing operations.
    /// This provides the Microsoft.IdentityModel.Tokens compatible format
    /// required by the JWT security token handler for signing operations.
    ///
    /// Key Features:
    /// - Wraps the private RSA key for signing operations
    /// - Includes a unique KeyId for key identification
    /// - Compatible with Microsoft JWT libraries
    /// </summary>
    /// <returns>RsaSecurityKey configured for JWT signing</returns>
    public RsaSecurityKey GetSigningKey()
    {
        return new RsaSecurityKey(GetPrivateKey())
        {
            KeyId = "erp-identity-signing-key" // Unique identifier for this key
        };
    }

    /// <summary>
    /// Creates an RsaSecurityKey wrapper for JWT token validation operations.
    /// This provides the Microsoft.IdentityModel.Tokens compatible format
    /// required by the JWT security token handler for validation operations.
    ///
    /// Key Features:
    /// - Wraps the public RSA key for validation operations
    /// - Includes a unique KeyId for key identification
    /// - Compatible with Microsoft JWT libraries
    /// - Can be used by other services for token verification
    /// </summary>
    /// <returns>RsaSecurityKey configured for JWT validation</returns>
    public RsaSecurityKey GetValidationKey()
    {
        return new RsaSecurityKey(GetPublicKey())
        {
            KeyId = "erp-identity-validation-key" // Unique identifier for this key
        };
    }

    #endregion

    /// <summary>
    /// Exports the public key in PEM (Privacy-Enhanced Mail) format.
    /// This standardized format allows the public key to be transmitted
    /// to other services for JWT token verification.
    ///
    /// PEM Format Details:
    /// - Uses PKCS#1 SubjectPublicKeyInfo format
    /// - Base64-encoded with standard PEM headers/footers
    /// - Compatible with most cryptographic libraries
    /// - Safe to transmit over HTTP/HTTPS
    ///
    /// Usage: Called by REST API to provide public key to BFF Gateway
    /// </summary>
    /// <returns>Public key in PEM format as string</returns>
    /// <exception cref="InvalidOperationException">Thrown if public key is not initialized</exception>
    public string ExportPublicKeyPem()
    {
        lock (_keyLock)
        {
            // Ensure public key is available before export
            if (_publicKey == null)
                throw new InvalidOperationException("Public key not initialized");

            // Export in SubjectPublicKeyInfo format (PKCS#1 standard)
            return _publicKey.ExportSubjectPublicKeyInfoPem();
        }
    }

    /// <summary>
    /// Rotates the RSA key pair by generating new keys and replacing the old ones.
    /// This is a critical security operation that invalidates all existing JWT tokens.
    ///
    /// Rotation Process:
    /// 1. Log the start of rotation for audit trail
    /// 2. Safely dispose of old RSA key instances
    /// 3. Generate new 2048-bit RSA key pair
    /// 4. Save new keys to persistent storage
    /// 5. Log successful completion
    ///
    /// Impact: All existing JWT tokens become invalid after rotation
    /// Frequency: Should be performed periodically for security
    /// </summary>
    /// <returns>Task representing the asynchronous key rotation operation</returns>
    public async Task RotateKeysAsync()
    {
        lock (_keyLock)
        {
            try
            {
                // Log rotation start for security audit trail
                _logger.LogInformation("🔄 Starting key rotation...");

                // Safely dispose of old RSA key instances to free memory
                _privateKey?.Dispose();
                _publicKey?.Dispose();

                // Generate new RSA key pair with fresh cryptographic material
                GenerateNewKeyPair();

                // Persist new keys to storage for service restart persistence
                SaveKeysToStorage();

                // Log successful completion for audit trail
                _logger.LogInformation("✅ Key rotation completed successfully");
            }
            catch (Exception ex)
            {
                // Log rotation failure for security monitoring
                _logger.LogError(ex, "❌ Failed to rotate keys");
                throw;
            }
        }
    }

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
        // Dispose RSA instances to clear cryptographic material from memory
        _privateKey?.Dispose();
        _publicKey?.Dispose();
    }
}

/// <summary>
/// Data transfer object for serializing RSA key parameters to JSON storage.
/// This class provides a secure way to persist RSA keys by converting
/// binary cryptographic parameters to Base64-encoded strings.
///
/// RSA Parameter Explanation:
/// - Modulus (n): Public parameter, product of two large primes
/// - Exponent (e): Public parameter, typically 65537
/// - D: Private exponent, used for decryption/signing
/// - P, Q: Private prime factors of the modulus
/// - DP, DQ: Private exponents for Chinese Remainder Theorem optimization
/// - InverseQ: Private parameter for CRT optimization
///
/// Security: Private parameters (D, P, Q, DP, DQ, InverseQ) are only
/// included when serializing private keys, never for public keys.
/// </summary>
public class RSAKeyData
{
    #region RSA Parameters (Base64-encoded for JSON serialization)

    /// <summary>RSA modulus (n) - Public parameter, product of two large primes</summary>
    public string? Modulus { get; set; }

    /// <summary>RSA public exponent (e) - Public parameter, typically 65537</summary>
    public string? Exponent { get; set; }

    /// <summary>RSA private exponent (d) - Private parameter for signing/decryption</summary>
    public string? D { get; set; }

    /// <summary>First prime factor (p) - Private parameter</summary>
    public string? P { get; set; }

    /// <summary>Second prime factor (q) - Private parameter</summary>
    public string? Q { get; set; }

    /// <summary>d mod (p-1) - Private parameter for CRT optimization</summary>
    public string? DP { get; set; }

    /// <summary>d mod (q-1) - Private parameter for CRT optimization</summary>
    public string? DQ { get; set; }

    /// <summary>q^(-1) mod p - Private parameter for CRT optimization</summary>
    public string? InverseQ { get; set; }

    #endregion

    /// <summary>
    /// Creates an RSAKeyData instance from .NET RSAParameters structure.
    /// This method converts binary RSA parameters to Base64-encoded strings
    /// suitable for JSON serialization and persistent storage.
    ///
    /// Conversion Process:
    /// 1. Always include public parameters (Modulus, Exponent)
    /// 2. Conditionally include private parameters based on flag
    /// 3. Convert byte arrays to Base64 strings for JSON compatibility
    /// 4. Handle null parameters gracefully
    ///
    /// Security: Private parameters are only included when explicitly requested
    /// </summary>
    /// <param name="parameters">RSA parameters from .NET RSA instance</param>
    /// <param name="includePrivateParameters">Whether to include private key parameters</param>
    /// <returns>RSAKeyData instance ready for JSON serialization</returns>
    public static RSAKeyData FromRSAParameters(RSAParameters parameters, bool includePrivateParameters)
    {
        // Create base key data with public parameters (always included)
        var keyData = new RSAKeyData
        {
            // Convert modulus (n) to Base64 - public parameter
            Modulus = parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,

            // Convert exponent (e) to Base64 - public parameter
            Exponent = parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null
        };

        // Include private parameters only if requested (for private key storage)
        if (includePrivateParameters)
        {
            // Private exponent (d) - core private parameter
            keyData.D = parameters.D != null ? Convert.ToBase64String(parameters.D) : null;

            // Prime factors (p, q) - fundamental private parameters
            keyData.P = parameters.P != null ? Convert.ToBase64String(parameters.P) : null;
            keyData.Q = parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null;

            // Chinese Remainder Theorem optimization parameters
            keyData.DP = parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null;
            keyData.DQ = parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null;
            keyData.InverseQ = parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null;
        }

        return keyData;
    }

    /// <summary>
    /// Converts this RSAKeyData instance back to .NET RSAParameters structure.
    /// This method deserializes Base64-encoded strings back to binary format
    /// required by .NET RSA cryptographic operations.
    ///
    /// Conversion Process:
    /// 1. Always convert public parameters (Modulus, Exponent)
    /// 2. Conditionally convert private parameters based on flag
    /// 3. Convert Base64 strings back to byte arrays
    /// 4. Handle null values gracefully
    ///
    /// Usage: Called when loading keys from storage for cryptographic operations
    /// </summary>
    /// <param name="includePrivateParameters">Whether to include private key parameters</param>
    /// <returns>RSAParameters structure ready for RSA instance import</returns>
    public RSAParameters ToRSAParameters(bool includePrivateParameters)
    {
        // Create RSA parameters with public components (always included)
        var parameters = new RSAParameters
        {
            // Convert modulus from Base64 to byte array
            Modulus = Modulus != null ? Convert.FromBase64String(Modulus) : null,

            // Convert exponent from Base64 to byte array
            Exponent = Exponent != null ? Convert.FromBase64String(Exponent) : null
        };

        // Include private parameters only if requested (for private key operations)
        if (includePrivateParameters)
        {
            // Private exponent - required for signing/decryption
            parameters.D = D != null ? Convert.FromBase64String(D) : null;

            // Prime factors - required for private key operations
            parameters.P = P != null ? Convert.FromBase64String(P) : null;
            parameters.Q = Q != null ? Convert.FromBase64String(Q) : null;

            // CRT optimization parameters - improve performance
            parameters.DP = DP != null ? Convert.FromBase64String(DP) : null;
            parameters.DQ = DQ != null ? Convert.FromBase64String(DQ) : null;
            parameters.InverseQ = InverseQ != null ? Convert.FromBase64String(InverseQ) : null;
        }

        return parameters;
    }
}
