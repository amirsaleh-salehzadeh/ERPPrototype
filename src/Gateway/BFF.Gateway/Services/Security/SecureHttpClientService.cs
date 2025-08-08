using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BFF.Gateway.Services.Security;

/// <summary>
/// Interface for secure HTTP client with RSA encryption
/// </summary>
public interface ISecureHttpClientService
{
    Task<TResponse?> PostAsync<TRequest, TResponse>(string serviceName, string endpoint, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class;

    Task<TResponse?> GetAsync<TResponse>(string serviceName, string endpoint, CancellationToken cancellationToken = default)
        where TResponse : class;

    Task<string> PostAsync(string serviceName, string endpoint, string jsonContent, CancellationToken cancellationToken = default);
    Task<string> GetAsync(string serviceName, string endpoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Secure HTTP client service that encrypts requests and decrypts responses using RSA
/// </summary>
public class SecureHttpClientService : ISecureHttpClientService
{
    private readonly HttpClient _httpClient;
    private readonly IRsaEncryptionService _rsaService;
    private readonly ILogger<SecureHttpClientService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, string> _serviceBaseUrls;
    private readonly RsaKeyConfiguration _configuration;

    public SecureHttpClientService(
        HttpClient httpClient,
        IRsaEncryptionService rsaService,
        ILogger<SecureHttpClientService> logger,
        IOptions<RsaKeyConfiguration> configuration)
    {
        _httpClient = httpClient;
        _rsaService = rsaService;
        _logger = logger;
        _configuration = configuration.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configure service base URLs
        _serviceBaseUrls = new Dictionary<string, string>
        {
            { "identity", "https://localhost:5001" },
            { "weather", "https://localhost:5002" },
            { "customer", "https://localhost:5003" },
            { "inventory", "https://localhost:5004" },
            { "finance", "https://localhost:5005" },
            { "orders", "https://localhost:5006" }
        };
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string serviceName, string endpoint, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        var jsonRequest = JsonSerializer.Serialize(request, _jsonOptions);
        var jsonResponse = await PostAsync(serviceName, endpoint, jsonRequest, cancellationToken);

        if (string.IsNullOrEmpty(jsonResponse))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TResponse>(jsonResponse, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response from {ServiceName}/{Endpoint}", serviceName, endpoint);
            return null;
        }
    }

    public async Task<TResponse?> GetAsync<TResponse>(string serviceName, string endpoint, CancellationToken cancellationToken = default)
        where TResponse : class
    {
        var jsonResponse = await GetAsync(serviceName, endpoint, cancellationToken);

        if (string.IsNullOrEmpty(jsonResponse))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TResponse>(jsonResponse, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response from {ServiceName}/{Endpoint}", serviceName, endpoint);
            return null;
        }
    }

    public async Task<string> PostAsync(string serviceName, string endpoint, string jsonContent, CancellationToken cancellationToken = default)
    {
        if (!_serviceBaseUrls.TryGetValue(serviceName.ToLowerInvariant(), out var baseUrl))
        {
            throw new ArgumentException($"Unknown service: {serviceName}", nameof(serviceName));
        }

        var fullUrl = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, fullUrl);

            // Add security headers
            await AddSecurityHeadersAsync(httpRequest, serviceName, jsonContent);

            // Encrypt content if RSA encryption is enabled
            if (_configuration.EnableRsaEncryption)
            {
                var encryptedContent = await _rsaService.EncryptAsync(jsonContent, serviceName);
                httpRequest.Content = new StringContent(encryptedContent, Encoding.UTF8, "application/json");
                httpRequest.Headers.Add("X-Encrypted", "true");
                _logger.LogDebug("Request encrypted for service: {ServiceName}", serviceName);
            }
            else
            {
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            }

            _logger.LogInformation("Sending POST request to {ServiceName}: {Url}", serviceName, fullUrl);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return await DecryptResponseIfNeededAsync(response, responseContent);
            }
            else
            {
                _logger.LogWarning("Request to {ServiceName} failed with status: {StatusCode}", serviceName, response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Error response: {ErrorContent}", errorContent);
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending POST request to {ServiceName}/{Endpoint}", serviceName, endpoint);
            throw;
        }
    }

    public async Task<string> GetAsync(string serviceName, string endpoint, CancellationToken cancellationToken = default)
    {
        if (!_serviceBaseUrls.TryGetValue(serviceName.ToLowerInvariant(), out var baseUrl))
        {
            throw new ArgumentException($"Unknown service: {serviceName}", nameof(serviceName));
        }

        var fullUrl = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, fullUrl);

            // Add security headers
            await AddSecurityHeadersAsync(httpRequest, serviceName);

            _logger.LogInformation("Sending GET request to {ServiceName}: {Url}", serviceName, fullUrl);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return await DecryptResponseIfNeededAsync(response, responseContent);
            }
            else
            {
                _logger.LogWarning("Request to {ServiceName} failed with status: {StatusCode}", serviceName, response.StatusCode);
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Error response: {ErrorContent}", errorContent);
                return string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending GET request to {ServiceName}/{Endpoint}", serviceName, endpoint);
            throw;
        }
    }

    private async Task AddSecurityHeadersAsync(HttpRequestMessage request, string serviceName, string? content = null)
    {
        // Add timestamp for replay attack prevention
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        request.Headers.Add("X-Timestamp", timestamp);

        // Add gateway identification
        request.Headers.Add("X-Gateway-Id", "BFF-Gateway");

        // Add target service
        request.Headers.Add("X-Target-Service", serviceName);

        // Create and add signature for request authentication
        var dataToSign = $"{request.Method}|{request.RequestUri}|{timestamp}|{serviceName}";
        if (!string.IsNullOrEmpty(content))
        {
            dataToSign += $"|{content}";
        }

        var signature = await _rsaService.SignDataAsync(dataToSign);
        request.Headers.Add("X-Signature", signature);

        _logger.LogDebug("Added security headers for {ServiceName} request", serviceName);
    }

    private async Task<string> DecryptResponseIfNeededAsync(HttpResponseMessage response, string content)
    {
        // Check if response is encrypted
        if (response.Headers.Contains("X-Encrypted") && 
            response.Headers.GetValues("X-Encrypted").FirstOrDefault() == "true")
        {
            try
            {
                var decryptedContent = await _rsaService.DecryptAsync(content);
                _logger.LogDebug("Response decrypted successfully");
                return decryptedContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt response");
                throw;
            }
        }

        return content;
    }
}
