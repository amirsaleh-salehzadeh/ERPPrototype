using Grpc.Net.Client;
using ERP.Contracts.Identity;
using ERP.Contracts.Orders;
using ERP.Contracts.Inventory;
using ERP.Contracts.Weather;
using ERP.Contracts.Customer;
using ERP.Contracts.Finance;

namespace BFF.Gateway.Services;

/// <summary>
/// gRPC client service that manages all microservice communications
/// </summary>
public class GrpcClientService : IGrpcClientService, IDisposable
{
    private readonly GrpcChannel _identityChannel;
    private readonly GrpcChannel _weatherChannel;
    private readonly GrpcChannel _orderChannel;
    private readonly GrpcChannel _inventoryChannel;
    private readonly GrpcChannel _customerChannel;
    private readonly GrpcChannel _financeChannel;
    
    private readonly IdentityService.IdentityServiceClient _identityClient;
    private readonly WeatherService.WeatherServiceClient _weatherClient;
    private readonly OrderService.OrderServiceClient _orderClient;
    private readonly InventoryService.InventoryServiceClient _inventoryClient;
    private readonly CustomerService.CustomerServiceClient _customerClient;
    private readonly FinanceService.FinanceServiceClient _financeClient;
    
    private readonly ILogger<GrpcClientService> _logger;

    public GrpcClientService(ILogger<GrpcClientService> logger)
    {
        _logger = logger;
        
        // Create gRPC channels for each service
        _identityChannel = GrpcChannel.ForAddress("http://localhost:5007");
        _weatherChannel = GrpcChannel.ForAddress("http://localhost:5001");
        _orderChannel = GrpcChannel.ForAddress("http://localhost:5003");
        _inventoryChannel = GrpcChannel.ForAddress("http://localhost:5004");
        _customerChannel = GrpcChannel.ForAddress("http://localhost:5005");
        _financeChannel = GrpcChannel.ForAddress("http://localhost:5006");
        
        // Create gRPC clients
        _identityClient = new IdentityService.IdentityServiceClient(_identityChannel);
        _weatherClient = new WeatherService.WeatherServiceClient(_weatherChannel);
        _orderClient = new OrderService.OrderServiceClient(_orderChannel);
        _inventoryClient = new InventoryService.InventoryServiceClient(_inventoryChannel);
        _customerClient = new CustomerService.CustomerServiceClient(_customerChannel);
        _financeClient = new FinanceService.FinanceServiceClient(_financeChannel);
        
        _logger.LogInformation("🔗 gRPC clients initialized for all microservices");
    }

    // Identity Service
    public async Task<ValidateApiKeyResponse> ValidateApiKeyAsync(ValidateApiKeyRequest request)
    {
        try
        {
            return await _identityClient.ValidateApiKeyAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Identity service ValidateApiKey");
            throw;
        }
    }

    // Weather Service
    public async Task<ERP.Contracts.Weather.HelloResponse> GetWeatherHelloAsync(ERP.Contracts.Weather.HelloRequest request)
    {
        try
        {
            return await _weatherClient.GetHelloAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Weather service GetHello");
            throw;
        }
    }

    public async Task<WeatherForecastResponse> GetWeatherForecastAsync(WeatherForecastRequest request)
    {
        try
        {
            return await _weatherClient.GetWeatherForecastAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Weather service GetWeatherForecast");
            throw;
        }
    }

    public async Task<ERP.Contracts.Weather.HealthResponse> GetWeatherHealthAsync(ERP.Contracts.Weather.HealthRequest request)
    {
        try
        {
            return await _weatherClient.GetHealthAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Weather service GetHealth");
            throw;
        }
    }

    // Order Service
    public async Task<ERP.Contracts.Orders.HelloResponse> GetOrderHelloAsync(ERP.Contracts.Orders.HelloRequest request)
    {
        try
        {
            return await _orderClient.GetHelloAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Order service GetHello");
            throw;
        }
    }

    public async Task<GetOrdersResponse> GetOrdersAsync(GetOrdersRequest request)
    {
        try
        {
            return await _orderClient.GetOrdersAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Order service GetOrders");
            throw;
        }
    }

    public async Task<GetOrderResponse> GetOrderAsync(GetOrderRequest request)
    {
        try
        {
            return await _orderClient.GetOrderAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Order service GetOrder");
            throw;
        }
    }

    public async Task<GetOrderStatsResponse> GetOrderStatsAsync(GetOrderStatsRequest request)
    {
        try
        {
            return await _orderClient.GetOrderStatsAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Order service GetOrderStats");
            throw;
        }
    }

    public async Task<ERP.Contracts.Orders.HealthResponse> GetOrderHealthAsync(ERP.Contracts.Orders.HealthRequest request)
    {
        try
        {
            return await _orderClient.GetHealthAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Order service GetHealth");
            throw;
        }
    }

    // Inventory Service
    public async Task<ERP.Contracts.Inventory.HelloResponse> GetInventoryHelloAsync(ERP.Contracts.Inventory.HelloRequest request)
    {
        try
        {
            return await _inventoryClient.GetHelloAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Inventory service GetHello");
            throw;
        }
    }

    public async Task<GetProductsResponse> GetProductsAsync(GetProductsRequest request)
    {
        try
        {
            return await _inventoryClient.GetProductsAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Inventory service GetProducts");
            throw;
        }
    }

    public async Task<GetProductResponse> GetProductAsync(GetProductRequest request)
    {
        try
        {
            return await _inventoryClient.GetProductAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Inventory service GetProduct");
            throw;
        }
    }

    public async Task<GetInventoryStatsResponse> GetInventoryStatsAsync(GetInventoryStatsRequest request)
    {
        try
        {
            return await _inventoryClient.GetInventoryStatsAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Inventory service GetInventoryStats");
            throw;
        }
    }

    public async Task<ERP.Contracts.Inventory.HealthResponse> GetInventoryHealthAsync(ERP.Contracts.Inventory.HealthRequest request)
    {
        try
        {
            return await _inventoryClient.GetHealthAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Inventory service GetHealth");
            throw;
        }
    }

    // Customer Service
    public async Task<ERP.Contracts.Customer.HelloResponse> GetCustomerHelloAsync(ERP.Contracts.Customer.HelloRequest request)
    {
        try
        {
            return await _customerClient.GetHelloAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Customer service GetHello");
            throw;
        }
    }

    public async Task<GetCustomersResponse> GetCustomersAsync(GetCustomersRequest request)
    {
        try
        {
            return await _customerClient.GetCustomersAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Customer service GetCustomers");
            throw;
        }
    }

    public async Task<GetCustomerResponse> GetCustomerAsync(GetCustomerRequest request)
    {
        try
        {
            return await _customerClient.GetCustomerAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Customer service GetCustomer");
            throw;
        }
    }

    public async Task<GetCustomerStatsResponse> GetCustomerStatsAsync(GetCustomerStatsRequest request)
    {
        try
        {
            return await _customerClient.GetCustomerStatsAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Customer service GetCustomerStats");
            throw;
        }
    }

    public async Task<ERP.Contracts.Customer.HealthResponse> GetCustomerHealthAsync(ERP.Contracts.Customer.HealthRequest request)
    {
        try
        {
            return await _customerClient.GetHealthAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Customer service GetHealth");
            throw;
        }
    }

    // Finance Service
    public async Task<ERP.Contracts.Finance.HelloResponse> GetFinanceHelloAsync(ERP.Contracts.Finance.HelloRequest request)
    {
        try
        {
            return await _financeClient.GetHelloAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Finance service GetHello");
            throw;
        }
    }

    public async Task<GetInvoicesResponse> GetInvoicesAsync(GetInvoicesRequest request)
    {
        try
        {
            return await _financeClient.GetInvoicesAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Finance service GetInvoices");
            throw;
        }
    }

    public async Task<GetTransactionsResponse> GetTransactionsAsync(GetTransactionsRequest request)
    {
        try
        {
            return await _financeClient.GetTransactionsAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Finance service GetTransactions");
            throw;
        }
    }

    public async Task<GetFinancialSummaryResponse> GetFinancialSummaryAsync(GetFinancialSummaryRequest request)
    {
        try
        {
            return await _financeClient.GetFinancialSummaryAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Finance service GetFinancialSummary");
            throw;
        }
    }

    public async Task<ERP.Contracts.Finance.HealthResponse> GetFinanceHealthAsync(ERP.Contracts.Finance.HealthRequest request)
    {
        try
        {
            return await _financeClient.GetHealthAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error calling Finance service GetHealth");
            throw;
        }
    }

    public void Dispose()
    {
        _identityChannel?.Dispose();
        _weatherChannel?.Dispose();
        _orderChannel?.Dispose();
        _inventoryChannel?.Dispose();
        _customerChannel?.Dispose();
        _financeChannel?.Dispose();
        
        _logger.LogInformation("🔌 gRPC channels disposed");
    }
}
