using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;

namespace ERP.IdentityService.Services;

/// <summary>
/// Service for managing RSA key pairs for JWT signing and verification
/// </summary>
public interface ICryptographicKeyService
{
    /// <summary>
    /// Gets the RSA private key for JWT signing
    /// </summary>
    RSA GetPrivateKey();
    
    /// <summary>
    /// Gets the RSA public key for JWT verification
    /// </summary>
    RSA GetPublicKey();
    
    /// <summary>
    /// Gets the RSA security key for JWT signing
    /// </summary>
    RsaSecurityKey GetSigningKey();
    
    /// <summary>
    /// Gets the RSA security key for JWT verification
    /// </summary>
    RsaSecurityKey GetValidationKey();
    
    /// <summary>
    /// Exports the public key in PEM format for sharing with other services
    /// </summary>
    string ExportPublicKeyPem();
    
    /// <summary>
    /// Rotates the key pair (generates new keys)
    /// </summary>
    Task RotateKeysAsync();
}

/// <summary>
/// Implementation of cryptographic key service using RSA key pairs
/// </summary>
public class CryptographicKeyService : ICryptographicKeyService, IDisposable
{
    private readonly ILogger<CryptographicKeyService> _logger;
    private readonly IConfiguration _configuration;
    private RSA? _privateKey;
    private RSA? _publicKey;
    private readonly string _keyStoragePath;
    private readonly object _keyLock = new object();

    public CryptographicKeyService(ILogger<CryptographicKeyService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _keyStoragePath = _configuration.GetValue<string>("JWT:KeyStoragePath") ?? "keys";
        
        // Ensure key storage directory exists
        Directory.CreateDirectory(_keyStoragePath);
        
        // Initialize or load keys
        InitializeKeys();
    }

    private void InitializeKeys()
    {
        lock (_keyLock)
        {
            try
            {
                var privateKeyPath = Path.Combine(_keyStoragePath, "private_key.json");
                var publicKeyPath = Path.Combine(_keyStoragePath, "public_key.json");

                if (File.Exists(privateKeyPath) && File.Exists(publicKeyPath))
                {
                    // Load existing keys
                    LoadKeysFromStorage();
                    _logger.LogInformation("🔑 Loaded existing RSA key pair from storage");
                }
                else
                {
                    // Generate new keys
                    GenerateNewKeyPair();
                    SaveKeysToStorage();
                    _logger.LogInformation("🔑 Generated new RSA key pair and saved to storage");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize cryptographic keys");
                throw;
            }
        }
    }

    private void GenerateNewKeyPair()
    {
        // Generate RSA key pair with 2048-bit key size
        _privateKey = RSA.Create(2048);
        _publicKey = RSA.Create();
        
        // Import public key from private key
        var publicKeyParameters = _privateKey.ExportParameters(false);
        _publicKey.ImportParameters(publicKeyParameters);
    }

    private void LoadKeysFromStorage()
    {
        var privateKeyPath = Path.Combine(_keyStoragePath, "private_key.json");
        var publicKeyPath = Path.Combine(_keyStoragePath, "public_key.json");

        // Load private key
        var privateKeyJson = File.ReadAllText(privateKeyPath);
        var privateKeyData = JsonSerializer.Deserialize<RSAKeyData>(privateKeyJson);
        _privateKey = RSA.Create();
        _privateKey.ImportParameters(privateKeyData!.ToRSAParameters(true));

        // Load public key
        var publicKeyJson = File.ReadAllText(publicKeyPath);
        var publicKeyData = JsonSerializer.Deserialize<RSAKeyData>(publicKeyJson);
        _publicKey = RSA.Create();
        _publicKey.ImportParameters(publicKeyData!.ToRSAParameters(false));
    }

    private void SaveKeysToStorage()
    {
        var privateKeyPath = Path.Combine(_keyStoragePath, "private_key.json");
        var publicKeyPath = Path.Combine(_keyStoragePath, "public_key.json");

        // Save private key
        var privateKeyParameters = _privateKey!.ExportParameters(true);
        var privateKeyData = RSAKeyData.FromRSAParameters(privateKeyParameters, true);
        var privateKeyJson = JsonSerializer.Serialize(privateKeyData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(privateKeyPath, privateKeyJson);

        // Save public key
        var publicKeyParameters = _privateKey.ExportParameters(false);
        var publicKeyData = RSAKeyData.FromRSAParameters(publicKeyParameters, false);
        var publicKeyJson = JsonSerializer.Serialize(publicKeyData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(publicKeyPath, publicKeyJson);
    }

    public RSA GetPrivateKey()
    {
        lock (_keyLock)
        {
            if (_privateKey == null)
                throw new InvalidOperationException("Private key not initialized");
            return _privateKey;
        }
    }

    public RSA GetPublicKey()
    {
        lock (_keyLock)
        {
            if (_publicKey == null)
                throw new InvalidOperationException("Public key not initialized");
            return _publicKey;
        }
    }

    public RsaSecurityKey GetSigningKey()
    {
        return new RsaSecurityKey(GetPrivateKey())
        {
            KeyId = "erp-identity-signing-key"
        };
    }

    public RsaSecurityKey GetValidationKey()
    {
        return new RsaSecurityKey(GetPublicKey())
        {
            KeyId = "erp-identity-validation-key"
        };
    }

    public string ExportPublicKeyPem()
    {
        lock (_keyLock)
        {
            if (_publicKey == null)
                throw new InvalidOperationException("Public key not initialized");

            return _publicKey.ExportSubjectPublicKeyInfoPem();
        }
    }

    public async Task RotateKeysAsync()
    {
        lock (_keyLock)
        {
            try
            {
                _logger.LogInformation("🔄 Starting key rotation...");
                
                // Dispose old keys
                _privateKey?.Dispose();
                _publicKey?.Dispose();
                
                // Generate new keys
                GenerateNewKeyPair();
                SaveKeysToStorage();
                
                _logger.LogInformation("✅ Key rotation completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to rotate keys");
                throw;
            }
        }
    }

    public void Dispose()
    {
        _privateKey?.Dispose();
        _publicKey?.Dispose();
    }
}

/// <summary>
/// Data structure for serializing RSA key parameters
/// </summary>
public class RSAKeyData
{
    public string? Modulus { get; set; }
    public string? Exponent { get; set; }
    public string? D { get; set; }
    public string? P { get; set; }
    public string? Q { get; set; }
    public string? DP { get; set; }
    public string? DQ { get; set; }
    public string? InverseQ { get; set; }

    public static RSAKeyData FromRSAParameters(RSAParameters parameters, bool includePrivateParameters)
    {
        var keyData = new RSAKeyData
        {
            Modulus = parameters.Modulus != null ? Convert.ToBase64String(parameters.Modulus) : null,
            Exponent = parameters.Exponent != null ? Convert.ToBase64String(parameters.Exponent) : null
        };

        if (includePrivateParameters)
        {
            keyData.D = parameters.D != null ? Convert.ToBase64String(parameters.D) : null;
            keyData.P = parameters.P != null ? Convert.ToBase64String(parameters.P) : null;
            keyData.Q = parameters.Q != null ? Convert.ToBase64String(parameters.Q) : null;
            keyData.DP = parameters.DP != null ? Convert.ToBase64String(parameters.DP) : null;
            keyData.DQ = parameters.DQ != null ? Convert.ToBase64String(parameters.DQ) : null;
            keyData.InverseQ = parameters.InverseQ != null ? Convert.ToBase64String(parameters.InverseQ) : null;
        }

        return keyData;
    }

    public RSAParameters ToRSAParameters(bool includePrivateParameters)
    {
        var parameters = new RSAParameters
        {
            Modulus = Modulus != null ? Convert.FromBase64String(Modulus) : null,
            Exponent = Exponent != null ? Convert.FromBase64String(Exponent) : null
        };

        if (includePrivateParameters)
        {
            parameters.D = D != null ? Convert.FromBase64String(D) : null;
            parameters.P = P != null ? Convert.FromBase64String(P) : null;
            parameters.Q = Q != null ? Convert.FromBase64String(Q) : null;
            parameters.DP = DP != null ? Convert.FromBase64String(DP) : null;
            parameters.DQ = DQ != null ? Convert.FromBase64String(DQ) : null;
            parameters.InverseQ = InverseQ != null ? Convert.FromBase64String(InverseQ) : null;
        }

        return parameters;
    }
}
