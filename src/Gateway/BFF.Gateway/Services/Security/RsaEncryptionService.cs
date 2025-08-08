using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace BFF.Gateway.Services.Security;

/// <summary>
/// RSA encryption service for securing inter-service communication
/// </summary>
public class RsaEncryptionService : IRsaEncryptionService, IDisposable
{
    private readonly RSA _gatewayPrivateKey;
    private readonly Dictionary<string, RSA> _servicePublicKeys;
    private readonly RsaKeyConfiguration _configuration;
    private readonly ILogger<RsaEncryptionService> _logger;

    public RsaEncryptionService(IOptions<RsaKeyConfiguration> configuration, ILogger<RsaEncryptionService> logger)
    {
        _configuration = configuration.Value;
        _logger = logger;
        _gatewayPrivateKey = RSA.Create(2048);
        _servicePublicKeys = new Dictionary<string, RSA>();

        InitializeKeys();
    }

    private void InitializeKeys()
    {
        try
        {
            // Load gateway private key
            if (!string.IsNullOrEmpty(_configuration.GatewayPrivateKey))
            {
                _gatewayPrivateKey.ImportRSAPrivateKey(Convert.FromBase64String(_configuration.GatewayPrivateKey), out _);
                _logger.LogInformation("Gateway private key loaded successfully");
            }
            else
            {
                _logger.LogWarning("No gateway private key configured, generating new key");
                // In production, you should store this key securely
                var privateKey = Convert.ToBase64String(_gatewayPrivateKey.ExportRSAPrivateKey());
                _logger.LogWarning("Generated private key (STORE SECURELY): {PrivateKey}", privateKey);
            }

            // Load service public keys
            foreach (var serviceKey in _configuration.ServicePublicKeys)
            {
                var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(serviceKey.Value), out _);
                _servicePublicKeys[serviceKey.Key] = rsa;
                _logger.LogInformation("Loaded public key for service: {ServiceName}", serviceKey.Key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RSA keys");
            throw;
        }
    }

    public async Task<string> EncryptAsync(string data, string serviceName)
    {
        if (string.IsNullOrEmpty(data))
            throw new ArgumentException("Data cannot be null or empty", nameof(data));

        if (!_servicePublicKeys.TryGetValue(serviceName, out var servicePublicKey))
        {
            _logger.LogError("No public key found for service: {ServiceName}", serviceName);
            throw new InvalidOperationException($"No public key configured for service: {serviceName}");
        }

        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var encryptedBytes = servicePublicKey.Encrypt(dataBytes, RSAEncryptionPadding.OaepSHA256);
            var result = Convert.ToBase64String(encryptedBytes);
            
            _logger.LogDebug("Successfully encrypted data for service: {ServiceName}", serviceName);
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt data for service: {ServiceName}", serviceName);
            throw;
        }
    }

    public async Task<string> DecryptAsync(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            throw new ArgumentException("Encrypted data cannot be null or empty", nameof(encryptedData));

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedData);
            var decryptedBytes = _gatewayPrivateKey.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);
            var result = Encoding.UTF8.GetString(decryptedBytes);
            
            _logger.LogDebug("Successfully decrypted data");
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt data");
            throw;
        }
    }

    public async Task<string> SignDataAsync(string data)
    {
        if (string.IsNullOrEmpty(data))
            throw new ArgumentException("Data cannot be null or empty", nameof(data));

        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = _gatewayPrivateKey.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var result = Convert.ToBase64String(signatureBytes);
            
            _logger.LogDebug("Successfully signed data");
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign data");
            throw;
        }
    }

    public async Task<bool> VerifySignatureAsync(string data, string signature, string serviceName)
    {
        if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(signature))
            return false;

        if (!_servicePublicKeys.TryGetValue(serviceName, out var servicePublicKey))
        {
            _logger.LogError("No public key found for service: {ServiceName}", serviceName);
            return false;
        }

        try
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var signatureBytes = Convert.FromBase64String(signature);
            var isValid = servicePublicKey.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            
            _logger.LogDebug("Signature verification result for service {ServiceName}: {IsValid}", serviceName, isValid);
            return await Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify signature for service: {ServiceName}", serviceName);
            return false;
        }
    }

    public string GetGatewayPublicKey()
    {
        try
        {
            var publicKeyBytes = _gatewayPrivateKey.ExportRSAPublicKey();
            return Convert.ToBase64String(publicKeyBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export gateway public key");
            throw;
        }
    }

    public void Dispose()
    {
        _gatewayPrivateKey?.Dispose();
        foreach (var serviceKey in _servicePublicKeys.Values)
        {
            serviceKey?.Dispose();
        }
        _servicePublicKeys.Clear();
    }
}
