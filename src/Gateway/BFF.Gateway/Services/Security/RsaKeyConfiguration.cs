namespace BFF.Gateway.Services.Security;

/// <summary>
/// Configuration for RSA keys used in inter-service communication
/// </summary>
public class RsaKeyConfiguration
{
    /// <summary>
    /// Base64 encoded RSA private key for the BFF Gateway
    /// </summary>
    public string GatewayPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Dictionary of service names to their Base64 encoded RSA public keys
    /// </summary>
    public Dictionary<string, string> ServicePublicKeys { get; set; } = new();

    /// <summary>
    /// Key size in bits (default: 2048)
    /// </summary>
    public int KeySize { get; set; } = 2048;

    /// <summary>
    /// Whether to enable RSA encryption for inter-service communication
    /// </summary>
    public bool EnableRsaEncryption { get; set; } = true;
}
