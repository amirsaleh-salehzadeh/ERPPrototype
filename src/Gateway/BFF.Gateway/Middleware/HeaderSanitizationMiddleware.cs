using System.Text.RegularExpressions;

namespace BFF.Gateway.Middleware;

/// <summary>
/// Middleware that sanitizes request and response headers by removing sensitive information
/// before passing requests to downstream services and before returning responses to clients
/// </summary>
public class HeaderSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HeaderSanitizationMiddleware> _logger;
    private readonly HeaderSanitizationOptions _options;

    public HeaderSanitizationMiddleware(
        RequestDelegate next, 
        ILogger<HeaderSanitizationMiddleware> logger,
        HeaderSanitizationOptions options)
    {
        _next = next;
        _logger = logger;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Sanitize request headers before processing
        SanitizeRequestHeaders(context);

        // Register callback to sanitize response headers before response starts
        context.Response.OnStarting(() =>
        {
            SanitizeResponseHeaders(context);
            return Task.CompletedTask;
        });

        // Continue to next middleware
        await _next(context);
    }

    private void SanitizeRequestHeaders(HttpContext context)
    {
        var request = context.Request;
        var headersToRemove = new List<string>();
        var headersToMask = new List<(string key, string maskedValue)>();

        foreach (var header in request.Headers)
        {
            var headerName = header.Key.ToLowerInvariant();
            var headerValue = header.Value.ToString();

            // Check if header should be completely removed
            if (_options.HeadersToRemove.Any(pattern => IsHeaderMatch(headerName, pattern)))
            {
                headersToRemove.Add(header.Key);
                _logger.LogDebug("🧹 Removing sensitive request header: {HeaderName}", header.Key);
                continue;
            }

            // Check if header should be masked
            if (_options.HeadersToMask.Any(pattern => IsHeaderMatch(headerName, pattern)))
            {
                var maskedValue = MaskHeaderValue(headerValue);
                headersToMask.Add((header.Key, maskedValue));
                _logger.LogDebug("🎭 Masking sensitive request header: {HeaderName}", header.Key);
                continue;
            }

            // Check for sensitive patterns in header values
            if (ContainsSensitivePattern(headerValue))
            {
                var maskedValue = MaskSensitivePatterns(headerValue);
                headersToMask.Add((header.Key, maskedValue));
                _logger.LogDebug("🔍 Masking sensitive patterns in request header: {HeaderName}", header.Key);
            }
        }

        // Remove headers marked for removal
        foreach (var headerName in headersToRemove)
        {
            request.Headers.Remove(headerName);
        }

        // Update headers marked for masking
        foreach (var (key, maskedValue) in headersToMask)
        {
            request.Headers.Remove(key);
            request.Headers[key] = maskedValue; // Use indexer to avoid duplicate key issues
        }

        if (headersToRemove.Count > 0 || headersToMask.Count > 0)
        {
            _logger.LogInformation("🛡️ Sanitized {RemovedCount} removed and {MaskedCount} masked request headers", 
                headersToRemove.Count, headersToMask.Count);
        }
    }

    private void SanitizeResponseHeaders(HttpContext context)
    {
        var response = context.Response;

        // Check if response has already started - if so, we can't modify headers
        if (response.HasStarted)
        {
            _logger.LogDebug("⚠️ Response has already started, cannot sanitize response headers");
            return;
        }

        var headersToRemove = new List<string>();
        var headersToMask = new List<(string key, string maskedValue)>();

        foreach (var header in response.Headers.ToList()) // ToList() to avoid modification during enumeration
        {
            var headerName = header.Key.ToLowerInvariant();
            var headerValue = header.Value.ToString();

            // Check if header should be completely removed
            if (_options.ResponseHeadersToRemove.Any(pattern => IsHeaderMatch(headerName, pattern)))
            {
                headersToRemove.Add(header.Key);
                _logger.LogDebug("🧹 Removing sensitive response header: {HeaderName}", header.Key);
                continue;
            }

            // Check if header should be masked
            if (_options.ResponseHeadersToMask.Any(pattern => IsHeaderMatch(headerName, pattern)))
            {
                var maskedValue = MaskHeaderValue(headerValue);
                headersToMask.Add((header.Key, maskedValue));
                _logger.LogDebug("🎭 Masking sensitive response header: {HeaderName}", header.Key);
                continue;
            }

            // Check for sensitive patterns in header values
            if (ContainsSensitivePattern(headerValue))
            {
                var maskedValue = MaskSensitivePatterns(headerValue);
                headersToMask.Add((header.Key, maskedValue));
                _logger.LogDebug("🔍 Masking sensitive patterns in response header: {HeaderName}", header.Key);
            }
        }

        // Remove headers marked for removal
        foreach (var headerName in headersToRemove)
        {
            response.Headers.Remove(headerName);
        }

        // Update headers marked for masking
        foreach (var (key, maskedValue) in headersToMask)
        {
            response.Headers.Remove(key);
            response.Headers[key] = maskedValue; // Use indexer instead of Add to avoid duplicate key issues
        }

        if (headersToRemove.Count > 0 || headersToMask.Count > 0)
        {
            _logger.LogInformation("🛡️ Sanitized {RemovedCount} removed and {MaskedCount} masked response headers",
                headersToRemove.Count, headersToMask.Count);
        }
    }

    private bool IsHeaderMatch(string headerName, string pattern)
    {
        // Support both exact match and wildcard patterns
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(headerName, regexPattern, RegexOptions.IgnoreCase);
        }
        
        return string.Equals(headerName, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsSensitivePattern(string value)
    {
        return _options.SensitivePatterns.Any(pattern => 
            Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase));
    }

    private string MaskHeaderValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // For short values, show first and last character
        if (value.Length <= 4)
            return "***";

        // For longer values, show first 2 and last 2 characters
        return value.Substring(0, 2) + new string('*', Math.Max(3, value.Length - 4)) + value.Substring(value.Length - 2);
    }

    private string MaskSensitivePatterns(string value)
    {
        var result = value;
        
        foreach (var pattern in _options.SensitivePatterns)
        {
            result = Regex.Replace(result, pattern, match =>
            {
                var matchValue = match.Value;
                return MaskHeaderValue(matchValue);
            }, RegexOptions.IgnoreCase);
        }
        
        return result;
    }
}

/// <summary>
/// Configuration options for header sanitization
/// </summary>
public class HeaderSanitizationOptions
{
    /// <summary>
    /// Request headers to completely remove (supports wildcards with *)
    /// </summary>
    public List<string> HeadersToRemove { get; set; } = new();

    /// <summary>
    /// Request headers to mask (supports wildcards with *)
    /// </summary>
    public List<string> HeadersToMask { get; set; } = new();

    /// <summary>
    /// Response headers to completely remove (supports wildcards with *)
    /// </summary>
    public List<string> ResponseHeadersToRemove { get; set; } = new();

    /// <summary>
    /// Response headers to mask (supports wildcards with *)
    /// </summary>
    public List<string> ResponseHeadersToMask { get; set; } = new();

    /// <summary>
    /// Regex patterns to detect sensitive information in header values
    /// </summary>
    public List<string> SensitivePatterns { get; set; } = new();

    /// <summary>
    /// Default configuration for common sensitive headers
    /// </summary>
    public static HeaderSanitizationOptions CreateDefault()
    {
        return new HeaderSanitizationOptions
        {
            HeadersToRemove = new List<string>
            {
                "x-api-key",           // Remove API keys completely
                "authorization",       // Remove authorization headers
                "x-auth-token",        // Remove auth tokens
                "x-session-id",        // Remove session IDs
                "cookie",              // Remove cookies
                "x-forwarded-*",       // Remove forwarding info
                "x-real-ip",           // Remove real IP
                "x-original-*"         // Remove original headers
            },
            HeadersToMask = new List<string>
            {
                "x-user-id",           // Mask user IDs
                "x-correlation-id",    // Mask correlation IDs
                "x-request-id",        // Mask request IDs
                "x-trace-id"           // Mask trace IDs
            },
            ResponseHeadersToRemove = new List<string>
            {
                "x-service-*",         // Remove internal service headers
                "x-user-*",            // Remove user context headers
                "x-internal-*",        // Remove internal headers
                "server",              // Remove server information
                "x-powered-by"         // Remove technology stack info
            },
            ResponseHeadersToMask = new List<string>
            {
                "x-correlation-id",    // Mask correlation IDs in responses
                "x-request-id"         // Mask request IDs in responses
            },
            SensitivePatterns = new List<string>
            {
                @"\b[A-Za-z0-9]{32,}\b",           // Long alphanumeric strings (likely tokens)
                @"\b[A-Za-z0-9+/]{20,}={0,2}\b",   // Base64 encoded strings
                @"\bBearer\s+[A-Za-z0-9\-._~+/]+=*", // Bearer tokens
                @"\bBasic\s+[A-Za-z0-9+/]+=*",     // Basic auth
                @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b" // UUIDs
            }
        };
    }
}
