using System.Security.Cryptography;

namespace BFF.Gateway.Services.Security;

/// <summary>
/// Interface for RSA encryption/decryption operations
/// </summary>
public interface IRsaEncryptionService
{
    /// <summary>
    /// Encrypts data using RSA public key for a specific service
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="serviceName">Target service name</param>
    /// <returns>Encrypted data as base64 string</returns>
    Task<string> EncryptAsync(string data, string serviceName);

    /// <summary>
    /// Decrypts data using RSA private key
    /// </summary>
    /// <param name="encryptedData">Base64 encrypted data</param>
    /// <returns>Decrypted data</returns>
    Task<string> DecryptAsync(string encryptedData);

    /// <summary>
    /// Signs data using RSA private key for authentication
    /// </summary>
    /// <param name="data">Data to sign</param>
    /// <returns>Digital signature as base64 string</returns>
    Task<string> SignDataAsync(string data);

    /// <summary>
    /// Verifies signature using service's public key
    /// </summary>
    /// <param name="data">Original data</param>
    /// <param name="signature">Signature to verify</param>
    /// <param name="serviceName">Service that signed the data</param>
    /// <returns>True if signature is valid</returns>
    Task<bool> VerifySignatureAsync(string data, string signature, string serviceName);

    /// <summary>
    /// Gets the public key for this gateway (for services to encrypt responses)
    /// </summary>
    /// <returns>Public key in PEM format</returns>
    string GetGatewayPublicKey();
}
