using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Middleware to log detailed information about incoming HTTP requests and responses
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();
        
        // Add correlation ID to response headers for tracking
        context.Response.Headers["X-Correlation-ID"] = requestId;

        // Capture request details
        var requestDetails = await CaptureRequestDetails(context.Request, requestId);

        // Log request start
        _logger.LogInformation("🚀 Request Started: {RequestDetails}", 
            JsonSerializer.Serialize(requestDetails, new JsonSerializerOptions { WriteIndented = false }));

        // Capture original response body stream
        var originalResponseBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            var errorDetails = new
            {
                RequestId = requestId,
                Exception = ex.Message,
                StackTrace = ex.StackTrace,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogError("❌ Request Failed: {ErrorDetails}", 
                JsonSerializer.Serialize(errorDetails, new JsonSerializerOptions { WriteIndented = false }));
            
            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Capture response details
            var responseDetails = await CaptureResponseDetails(context.Response, requestId, stopwatch.ElapsedMilliseconds);

            // Log request completion
            _logger.LogInformation("✅ Request Completed: {ResponseDetails}", 
                JsonSerializer.Serialize(responseDetails, new JsonSerializerOptions { WriteIndented = false }));

            // Copy response body back to original stream
            await responseBody.CopyToAsync(originalResponseBodyStream);
        }
    }

    private async Task<object> CaptureRequestDetails(HttpRequest request, string requestId)
    {
        var body = string.Empty;
        
        // Only capture body for non-GET requests and if content type suggests it's readable
        if (request.Method != "GET" && request.ContentLength > 0 && 
            (request.ContentType?.Contains("application/json") == true || 
             request.ContentType?.Contains("application/xml") == true ||
             request.ContentType?.Contains("text/") == true))
        {
            try
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                request.Body.Position = 0;

                // Truncate large bodies
                if (body.Length > 10000)
                {
                    body = body.Substring(0, 10000) + "... [TRUNCATED]";
                }
            }
            catch
            {
                body = "[BODY_READ_ERROR]";
            }
        }

        return new
        {
            RequestId = requestId,
            Method = request.Method,
            Path = request.Path.Value,
            QueryString = request.QueryString.Value,
            Headers = request.Headers
                .Where(h => !IsSensitiveHeader(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToString()),
            ContentType = request.ContentType,
            ContentLength = request.ContentLength,
            Body = body,
            RemoteIpAddress = request.HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request.Headers.UserAgent.ToString(),
            Referer = request.Headers.Referer.ToString(),
            Timestamp = DateTime.UtcNow,
            Protocol = request.Protocol,
            Scheme = request.Scheme,
            Host = request.Host.Value,
            IsHttps = request.IsHttps
        };
    }

    private async Task<object> CaptureResponseDetails(HttpResponse response, string requestId, long elapsedMs)
    {
        var body = string.Empty;
        
        // Only capture response body for successful JSON/XML responses
        if (response.Body.CanRead && response.Body.CanSeek && 
            (response.ContentType?.Contains("application/json") == true ||
             response.ContentType?.Contains("application/xml") == true ||
             response.ContentType?.Contains("text/") == true))
        {
            try
            {
                response.Body.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
                body = await reader.ReadToEndAsync();
                response.Body.Seek(0, SeekOrigin.Begin);

                // Truncate large responses
                if (body.Length > 5000)
                {
                    body = body.Substring(0, 5000) + "... [TRUNCATED]";
                }
            }
            catch
            {
                body = "[BODY_READ_ERROR]";
            }
        }

        return new
        {
            RequestId = requestId,
            StatusCode = response.StatusCode,
            StatusText = GetStatusText(response.StatusCode),
            Headers = response.Headers
                .Where(h => !IsSensitiveHeader(h.Key))
                .ToDictionary(h => h.Key, h => h.Value.ToString()),
            ContentType = response.ContentType,
            ContentLength = response.ContentLength,
            Body = body,
            ElapsedMs = elapsedMs,
            Timestamp = DateTime.UtcNow
        };
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        var sensitiveHeaders = new[]
        {
            "authorization", "cookie", "set-cookie", "x-api-key", 
            "x-auth-token", "bearer", "password", "secret"
        };
        
        return sensitiveHeaders.Any(h => 
            headerName.Equals(h, StringComparison.OrdinalIgnoreCase) ||
            headerName.Contains(h, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetStatusText(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            204 => "No Content",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            500 => "Internal Server Error",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            _ => "Unknown"
        };
    }
}
