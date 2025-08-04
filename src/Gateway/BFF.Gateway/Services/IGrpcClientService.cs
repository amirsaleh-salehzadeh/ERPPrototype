using ERP.Contracts.Identity;
using ERP.Contracts.Orders;
using ERP.Contracts.Inventory;
using ERP.Contracts.Weather;
using ERP.Contracts.Customer;
using ERP.Contracts.Finance;

namespace BFF.Gateway.Services;

/// <summary>
/// Interface for gRPC client service that manages all microservice communications
/// </summary>
public interface IGrpcClientService
{
    // Identity Service - Security Pipeline
    Task<ValidateApiKeyResponse> ValidateApiKeyAsync(ValidateApiKeyRequest request);
    Task<CheckApiAccessResponse> CheckApiAccessAsync(CheckApiAccessRequest request);
    Task<AuthenticateUserResponse> AuthenticateUserAsync(AuthenticateUserRequest request);
    Task<CheckUserAuthorizationResponse> CheckUserAuthorizationAsync(CheckUserAuthorizationRequest request);
    
    // Weather Service
    Task<ERP.Contracts.Weather.HelloResponse> GetWeatherHelloAsync(ERP.Contracts.Weather.HelloRequest request);
    Task<WeatherForecastResponse> GetWeatherForecastAsync(WeatherForecastRequest request);
    Task<ERP.Contracts.Weather.HealthResponse> GetWeatherHealthAsync(ERP.Contracts.Weather.HealthRequest request);
    
    // Order Service
    Task<ERP.Contracts.Orders.HelloResponse> GetOrderHelloAsync(ERP.Contracts.Orders.HelloRequest request);
    Task<GetOrdersResponse> GetOrdersAsync(GetOrdersRequest request);
    Task<GetOrderResponse> GetOrderAsync(GetOrderRequest request);
    Task<GetOrderStatsResponse> GetOrderStatsAsync(GetOrderStatsRequest request);
    Task<ERP.Contracts.Orders.HealthResponse> GetOrderHealthAsync(ERP.Contracts.Orders.HealthRequest request);
    
    // Inventory Service
    Task<ERP.Contracts.Inventory.HelloResponse> GetInventoryHelloAsync(ERP.Contracts.Inventory.HelloRequest request);
    Task<GetProductsResponse> GetProductsAsync(GetProductsRequest request);
    Task<GetProductResponse> GetProductAsync(GetProductRequest request);
    Task<GetInventoryStatsResponse> GetInventoryStatsAsync(GetInventoryStatsRequest request);
    Task<ERP.Contracts.Inventory.HealthResponse> GetInventoryHealthAsync(ERP.Contracts.Inventory.HealthRequest request);
    
    // Customer Service
    Task<ERP.Contracts.Customer.HelloResponse> GetCustomerHelloAsync(ERP.Contracts.Customer.HelloRequest request);
    Task<GetCustomersResponse> GetCustomersAsync(GetCustomersRequest request);
    Task<GetCustomerResponse> GetCustomerAsync(GetCustomerRequest request);
    Task<GetCustomerStatsResponse> GetCustomerStatsAsync(GetCustomerStatsRequest request);
    Task<ERP.Contracts.Customer.HealthResponse> GetCustomerHealthAsync(ERP.Contracts.Customer.HealthRequest request);
    
    // Finance Service
    Task<ERP.Contracts.Finance.HelloResponse> GetFinanceHelloAsync(ERP.Contracts.Finance.HelloRequest request);
    Task<GetInvoicesResponse> GetInvoicesAsync(GetInvoicesRequest request);
    Task<GetTransactionsResponse> GetTransactionsAsync(GetTransactionsRequest request);
    Task<GetFinancialSummaryResponse> GetFinancialSummaryAsync(GetFinancialSummaryRequest request);
    Task<ERP.Contracts.Finance.HealthResponse> GetFinanceHealthAsync(ERP.Contracts.Finance.HealthRequest request);
}
