# RSA Security Implementation Guide

## Overview

This document describes the RSA encryption security layer implemented for secure inter-service communication in the ERP Prototype system. The implementation ensures that all communication between the BFF Gateway and microservices is encrypted and authenticated.

## Architecture

### Security Components

1. **IRsaEncryptionService** - Interface for RSA encryption operations
2. **RsaEncryptionService** - Implementation of RSA encryption/decryption and digital signatures
3. **ISecureHttpClientService** - Interface for secure HTTP communication
4. **SecureHttpClientService** - HTTP client with automatic encryption/decryption
5. **RsaKeyConfiguration** - Configuration model for RSA keys

### Security Flow

```
┌─────────────────┐    Encrypted     ┌─────────────────┐
│   BFF Gateway   │ ──────────────► │  Microservice   │
│                 │                  │                 │
│ • Encrypts      │                  │ • Decrypts      │
│ • Signs         │                  │ • Verifies      │
│ • Authenticates │                  │ • Responds      │
└─────────────────┘ ◄────────────── └─────────────────┘
                      Encrypted
```

## Key Management

### RSA Key Pairs

Each service has its own RSA key pair:
- **Private Key**: Used for decryption and signing
- **Public Key**: Used for encryption and verification

### Key Distribution

- **BFF Gateway**: Holds its private key + all service public keys
- **Each Service**: Holds its private key + gateway public key

## Implementation Details

### 1. RSA Encryption Service

```csharp
public interface IRsaEncryptionService
{
    Task<string> EncryptAsync(string data, string serviceName);
    Task<string> DecryptAsync(string encryptedData);
    Task<string> SignDataAsync(string data);
    Task<bool> VerifySignatureAsync(string data, string signature, string serviceName);
    string GetGatewayPublicKey();
}
```

**Features:**
- 2048-bit RSA keys
- OAEP SHA256 padding for encryption
- PKCS1 padding for signatures
- Base64 encoding for transport

### 2. Secure HTTP Client

```csharp
public interface ISecureHttpClientService
{
    Task<TResponse?> PostAsync<TRequest, TResponse>(string serviceName, string endpoint, TRequest request);
    Task<TResponse?> GetAsync<TResponse>(string serviceName, string endpoint);
}
```

**Features:**
- Automatic request encryption
- Response decryption
- Digital signature validation
- Timestamp-based replay protection

### 3. Security Headers

Each request includes security headers:

| Header | Purpose |
|--------|---------|
| `X-Timestamp` | Replay attack prevention |
| `X-Gateway-Id` | Source identification |
| `X-Target-Service` | Target service name |
| `X-Signature` | Request authentication |
| `X-Encrypted` | Encryption flag |

## Configuration

### BFF Gateway Configuration

```json
{
  "RsaEncryption": {
    "EnableRsaEncryption": true,
    "KeySize": 2048,
    "GatewayPrivateKey": "[Base64 Private Key]",
    "ServicePublicKeys": {
      "identity": "[Base64 Public Key]",
      "weather": "[Base64 Public Key]",
      "customer": "[Base64 Public Key]",
      "inventory": "[Base64 Public Key]",
      "finance": "[Base64 Public Key]",
      "orders": "[Base64 Public Key]"
    }
  }
}
```

### Service Configuration

```json
{
  "RsaEncryption": {
    "EnableRsaEncryption": true,
    "KeySize": 2048,
    "ServicePrivateKey": "[Base64 Private Key]",
    "GatewayPublicKey": "[Base64 Public Key]"
  }
}
```

## Service Registration

### BFF Gateway (Program.cs)

```csharp
// Configure RSA encryption
builder.Services.Configure<RsaKeyConfiguration>(
    builder.Configuration.GetSection("RsaEncryption"));

// Add RSA services
builder.Services.AddSingleton<IRsaEncryptionService, RsaEncryptionService>();
builder.Services.AddHttpClient<ISecureHttpClientService, SecureHttpClientService>();
```

## Usage Examples

### Controller Implementation

```csharp
[ApiController]
[Route("api/weather")]
public class WeatherController : ControllerBase
{
    private readonly ISecureHttpClientService _secureHttpClient;

    public WeatherController(ISecureHttpClientService secureHttpClient)
    {
        _secureHttpClient = secureHttpClient;
    }

    [HttpGet("forecast")]
    public async Task<IActionResult> GetWeatherForecast([FromQuery] int days = 5)
    {
        // Make secure request to weather service
        var response = await _secureHttpClient.GetAsync("weather", 
            $"api/WeatherForecast?days={days}");
        
        return Ok(response);
    }
}
```

## Security Features

### 1. Encryption
- **Algorithm**: RSA with OAEP SHA256 padding
- **Key Size**: 2048 bits
- **Transport**: Base64 encoding

### 2. Digital Signatures
- **Algorithm**: RSA with PKCS1 padding
- **Hash Function**: SHA256
- **Purpose**: Request authenticity and integrity

### 3. Replay Protection
- **Method**: Timestamp validation
- **Header**: `X-Timestamp`
- **Window**: Configurable time window

### 4. Service Authentication
- **Method**: Digital signatures
- **Scope**: Request method, URL, timestamp, content
- **Verification**: Automatic signature validation

## Key Generation

### Automated Script

Use the provided PowerShell script to generate all required keys:

```powershell
./generate-rsa-keys.ps1
```

This script:
1. Generates RSA key pairs for all services
2. Creates configuration files
3. Provides deployment instructions

### Manual Key Generation

```csharp
using (var rsa = RSA.Create(2048))
{
    var privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
    var publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
    
    // Store keys securely
}
```

## Deployment Steps

1. **Generate Keys**:
   ```powershell
   ./generate-rsa-keys.ps1
   ```

2. **Deploy Configurations**:
   ```powershell
   ./generated-keys/deploy-rsa-config.ps1
   ```

3. **Build Services**:
   ```powershell
   dotnet build
   ```

4. **Test Implementation**:
   ```powershell
   ./test-rsa-security.ps1
   ```

## Security Considerations

### Production Deployment

1. **Key Storage**: Use Azure Key Vault or similar secure storage
2. **Key Rotation**: Implement regular key rotation
3. **Certificate Management**: Consider using certificates instead of raw keys
4. **Monitoring**: Log all security events and failures
5. **Performance**: Consider AES for payload encryption with RSA for key exchange

### Development vs Production

| Aspect | Development | Production |
|--------|-------------|------------|
| Key Storage | Configuration files | Azure Key Vault |
| Key Rotation | Manual | Automated |
| Monitoring | Basic logging | Full security monitoring |
| Performance | Full RSA encryption | Hybrid encryption |

## Troubleshooting

### Common Issues

1. **Key Mismatch**: Ensure public/private key pairs match
2. **Configuration**: Verify RSA configuration is properly loaded
3. **Dependencies**: Check that required NuGet packages are installed
4. **Permissions**: Ensure services can access configuration files

### Debug Logging

Enable detailed logging to troubleshoot RSA operations:

```json
{
  "Logging": {
    "LogLevel": {
      "BFF.Gateway.Services.Security": "Debug"
    }
  }
}
```

## Performance Impact

### Encryption Overhead

- **RSA Operations**: ~1-5ms per operation
- **Base64 Encoding**: Minimal overhead
- **Network Impact**: ~33% size increase due to Base64

### Optimization Strategies

1. **Caching**: Cache encrypted common payloads
2. **Compression**: Compress before encryption
3. **Hybrid Encryption**: Use AES for large payloads
4. **Connection Pooling**: Reuse HTTP connections

## Future Enhancements

1. **Certificate-based Authentication**: Replace raw keys with X.509 certificates
2. **Hybrid Encryption**: AES for data, RSA for key exchange
3. **Key Rotation**: Automatic key rotation mechanism
4. **Hardware Security Modules**: HSM integration for key protection
5. **Performance Optimization**: Async encryption operations

## Compliance

This implementation provides:
- **Data Confidentiality**: RSA encryption
- **Data Integrity**: Digital signatures
- **Authentication**: Service identity verification
- **Non-repudiation**: Cryptographic proof of origin

## Support

For issues or questions regarding the RSA security implementation:
1. Check the troubleshooting section
2. Review debug logs
3. Verify key configuration
4. Test with the provided scripts
