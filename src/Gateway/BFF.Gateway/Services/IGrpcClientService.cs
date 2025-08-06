using ERP.Contracts.Identity;

namespace BFF.Gateway.Services;

/// <summary>
/// Interface for gRPC client service that manages microservice communications
/// </summary>
public interface IGrpcClientService
{
    // Identity Service
    Task<ValidateApiKeyResponse> ValidateApiKeyAsync(ValidateApiKeyRequest request);
}
