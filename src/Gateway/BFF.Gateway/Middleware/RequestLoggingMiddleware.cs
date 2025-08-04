using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BFF.Gateway.Services;
using Serilog;
using Serilog.Context;
using Microsoft.Extensions.Logging;

namespace BFF.Gateway.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Microsoft.Extensions.Logging.ILogger<RequestLoggingMiddleware> _logger;
    private readonly IServiceMappingService _serviceMappingService;
    private static readonly Serilog.ILogger _serilogLogger = Log.ForContext<RequestLoggingMiddleware>();

    public RequestLoggingMiddleware(
        RequestDelegate next,
        Microsoft.Extensions.Logging.ILogger<RequestLoggingMiddleware> logger,
        IServiceMappingService serviceMappingService)
    {
        _next = next;
        _logger = logger;
        _serviceMappingService = serviceMappingService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();
        
        // Add request ID to context for correlation
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
        using (LogContext.PushProperty("SpanId", Activity.Current?.SpanId.ToString()))
        {
            // Capture request details
            var requestDetails = await CaptureRequestDetailsAsync(context, requestId);
            
            // Log request start
            _serilogLogger.Information("🚀 HTTP Request Started: {Method} {Path}", 
                context.Request.Method, context.Request.Path);

            // Capture original response body stream
            var originalBodyStream = context.Response.Body;
            
            try
            {
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                // Process the request
                await _next(context);

                stopwatch.Stop();

                // Capture response details
                var responseDetails = await CaptureResponseDetailsAsync(context, responseBody);

                // Log complete request/response
                await LogRequestResponseAsync(requestDetails, responseDetails, stopwatch.ElapsedMilliseconds);

                // Copy response back to original stream
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                // Log error
                _serilogLogger.Error(ex, "❌ HTTP Request Failed: {Method} {Path} - Duration: {Duration}ms", 
                    context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
                
                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
    }

    private async Task<RequestDetails> CaptureRequestDetailsAsync(HttpContext context, string requestId)
    {
        var request = context.Request;
        var serviceMapping = _serviceMappingService.GetServiceMapping(request.Path.Value ?? string.Empty);
        
        // Capture request body if present and not too large
        string? requestBody = null;
        if (request.ContentLength > 0 && request.ContentLength < 10240) // 10KB limit
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            request.Body.Position = 0;
        }

        return new RequestDetails
        {
            RequestId = requestId,
            Timestamp = DateTimeOffset.UtcNow,
            Method = request.Method,
            Path = request.Path.Value ?? string.Empty,
            QueryString = request.QueryString.Value ?? string.Empty,
            Headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            Body = requestBody,
            UserAgent = request.Headers.UserAgent.ToString(),
            RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
            ServiceName = serviceMapping?.ServiceName,
            ServiceDisplayName = serviceMapping?.DisplayName,
            UserId = request.Headers["X-User-Id"].FirstOrDefault(),
            UserName = request.Headers["X-User-Name"].FirstOrDefault(),
            ApiKey = request.Headers["X-API-Key"].FirstOrDefault()?.Substring(0, Math.Min(8, request.Headers["X-API-Key"].FirstOrDefault()?.Length ?? 0)) + "***"
        };
    }

    private async Task<ResponseDetails> CaptureResponseDetailsAsync(HttpContext context, MemoryStream responseBody)
    {
        var response = context.Response;
        
        // Capture response body if not too large
        string? responseBodyContent = null;
        if (responseBody.Length > 0 && responseBody.Length < 10240) // 10KB limit
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(responseBody, Encoding.UTF8, leaveOpen: true);
            responseBodyContent = await reader.ReadToEndAsync();
        }

        return new ResponseDetails
        {
            StatusCode = response.StatusCode,
            Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
            ContentType = response.ContentType,
            ContentLength = responseBody.Length,
            Body = responseBodyContent
        };
    }

    private async Task LogRequestResponseAsync(RequestDetails request, ResponseDetails response, long durationMs)
    {
        var logData = new
        {
            Request = request,
            Response = response,
            Duration = durationMs,
            Success = response.StatusCode < 400
        };

        // Determine log level based on status code
        var logLevel = response.StatusCode switch
        {
            >= 500 => "Error",
            >= 400 => "Warning",
            _ => "Information"
        };

        var statusEmoji = response.StatusCode switch
        {
            >= 500 => "💥",
            >= 400 => "⚠️",
            >= 300 => "🔄",
            >= 200 => "✅",
            _ => "❓"
        };

        // Log with structured data
        using (LogContext.PushProperty("RequestDetails", request, true))
        using (LogContext.PushProperty("ResponseDetails", response, true))
        using (LogContext.PushProperty("Duration", durationMs))
        using (LogContext.PushProperty("Success", response.StatusCode < 400))
        using (LogContext.PushProperty("StatusCode", response.StatusCode))
        using (LogContext.PushProperty("Method", request.Method))
        using (LogContext.PushProperty("Path", request.Path))
        using (LogContext.PushProperty("ServiceName", request.ServiceName))
        using (LogContext.PushProperty("UserId", request.UserId))
        using (LogContext.PushProperty("UserName", request.UserName))
        using (LogContext.PushProperty("RemoteIpAddress", request.RemoteIpAddress))
        {
            switch (logLevel)
            {
                case "Error":
                    _serilogLogger.Error("{StatusEmoji} HTTP {StatusCode} {Method} {Path} - {Duration}ms - Service: {ServiceName} - User: {UserName}",
                        statusEmoji, response.StatusCode, request.Method, request.Path, durationMs, request.ServiceName, request.UserName);
                    break;
                case "Warning":
                    _serilogLogger.Warning("{StatusEmoji} HTTP {StatusCode} {Method} {Path} - {Duration}ms - Service: {ServiceName} - User: {UserName}",
                        statusEmoji, response.StatusCode, request.Method, request.Path, durationMs, request.ServiceName, request.UserName);
                    break;
                default:
                    _serilogLogger.Information("{StatusEmoji} HTTP {StatusCode} {Method} {Path} - {Duration}ms - Service: {ServiceName} - User: {UserName}",
                        statusEmoji, response.StatusCode, request.Method, request.Path, durationMs, request.ServiceName, request.UserName);
                    break;
            }
        }
    }
}

public class RequestDetails
{
    public string RequestId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public string? Body { get; set; }
    public string? UserAgent { get; set; }
    public string? RemoteIpAddress { get; set; }
    public string? ServiceName { get; set; }
    public string? ServiceDisplayName { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? ApiKey { get; set; }
}

public class ResponseDetails
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? ContentType { get; set; }
    public long ContentLength { get; set; }
    public string? Body { get; set; }
}
