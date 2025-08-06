using Grpc.Net.Client;
using ERP.Contracts.Identity;
// TODO: Add other service contracts when they are separated
// using ERP.Contracts.Orders;
// using ERP.Contracts.Inventory;
// using ERP.Contracts.Customer;
// using ERP.Contracts.Finance;

namespace BFF.Gateway.Services;

/// <summary>
/// gRPC client service that manages all microservice communications
/// </summary>
public class GrpcClientService : IGrpcClientService, IDisposable
{
    private readonly GrpcChannel _identityChannel;
    private readonly ERP.Contracts.Identity.IdentityService.IdentityServiceClient _identityClient;
    private readonly ILogger<GrpcClientService> _logger;

    public GrpcClientService(ILogger<GrpcClientService> logger)
    {
        _logger = logger;

        // Create gRPC channel for Identity service
        _identityChannel = GrpcChannel.ForAddress("http://localhost:5008"); // Identity service gRPC port

        // Create gRPC client for Identity service
        _identityClient = new ERP.Contracts.Identity.IdentityService.IdentityServiceClient(_identityChannel);

        _logger.LogInformation("🔗 gRPC client initialized for Identity service");
    }

    // Identity Service
    public async Task<ValidateApiKeyResponse> ValidateApiKeyAsync(ValidateApiKeyRequest request)
    {
        try
        {
            _logger.LogInformation("🔍 Calling Identity service ValidateApiKey via gRPC for service: {ServiceName}", request.ServiceName);
            return await _identityClient.ValidateApiKeyAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Identity service ValidateApiKey via gRPC");
            throw;
        }
    }

    public void Dispose()
    {
        _identityChannel?.Dispose();
        _logger.LogInformation("🔌 gRPC channel disposed");
    }
}
