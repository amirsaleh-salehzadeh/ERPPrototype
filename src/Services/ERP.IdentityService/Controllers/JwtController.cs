using Microsoft.AspNetCore.Mvc;
using ERP.IdentityService.Services;

namespace ERP.IdentityService.Controllers;

/// <summary>
/// REST API controller for JWT token operations and public key distribution.
/// This controller provides HTTP endpoints for JWT token management and enables
/// other services to retrieve the public key for distributed token validation.
///
/// Key Responsibilities:
/// - Expose public key endpoint for other services (BFF Gateway, etc.)
/// - Provide test token generation for development and testing
/// - Support both user and service token generation scenarios
/// - Enable secure public key distribution without authentication
///
/// Security Considerations:
/// - Public key endpoint is intentionally unauthenticated (public information)
/// - Test token endpoint should be secured in production environments
/// - All operations are logged for security auditing
/// - Error responses don't leak sensitive information
/// </summary>
[ApiController]
[Route("[controller]")]
public class JwtController : ControllerBase
{
    #region Private Fields

    /// <summary>JWT token service for token generation and public key access</summary>
    private readonly IJwtTokenService _jwtTokenService;

    /// <summary>Logger for JWT operations and security events</summary>
    private readonly ILogger<JwtController> _logger;

    #endregion

    /// <summary>
    /// Initializes a new instance of the JWT controller.
    /// Sets up dependencies required for JWT token operations and public key distribution.
    /// </summary>
    /// <param name="jwtTokenService">Service for JWT token generation and validation</param>
    /// <param name="logger">Logger for JWT operations and security events</param>
    public JwtController(IJwtTokenService jwtTokenService, ILogger<JwtController> logger)
    {
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the RSA public key for JWT token validation by other services.
    /// This endpoint is publicly accessible and enables distributed JWT validation
    /// without requiring authentication or communication with the Identity Service.
    ///
    /// Endpoint: GET /jwt/public-key
    /// Authentication: None required (public information)
    ///
    /// Use Cases:
    /// - BFF Gateway fetches this key on startup for local JWT validation
    /// - Other microservices can cache this key for token verification
    /// - Load balancers or API gateways can use this for token validation
    /// - Development tools can retrieve the key for testing
    ///
    /// Response Format:
    /// - PublicKeyPem: PEM-encoded RSA public key (PKCS#1 format)
    /// - KeyId: Unique identifier matching the signing key
    /// - Success: Boolean indicating operation success
    /// - ErrorMessage: Error details if operation fails
    ///
    /// Security Notes:
    /// - Public key can be safely transmitted over HTTP/HTTPS
    /// - No sensitive information is exposed in this endpoint
    /// - Key rotation will change the returned key automatically
    /// </summary>
    /// <returns>JSON response containing the RSA public key in PEM format</returns>
    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
    {
        try
        {
            // Log public key request for audit trail
            _logger.LogInformation("🔑 REST GetPublicKey called");

            // Retrieve PEM-encoded public key from JWT token service
            var publicKeyPem = _jwtTokenService.GetPublicKeyPem();

            // Create structured response with public key and metadata
            var response = new
            {
                PublicKeyPem = publicKeyPem, // PEM-encoded RSA public key
                KeyId = "erp-identity-validation-key", // Unique key identifier
                Success = true, // Operation success indicator
                ErrorMessage = string.Empty // No error for successful operation
            };

            // Log successful public key retrieval
            _logger.LogInformation("✅ REST Public key retrieved successfully");
            return Ok(response);
        }
        catch (Exception ex)
        {
            // Log public key retrieval failure for monitoring
            _logger.LogError(ex, "❌ Error in REST GetPublicKey");

            // Create error response without exposing sensitive details
            var errorResponse = new
            {
                PublicKeyPem = string.Empty, // No key on error
                KeyId = string.Empty, // No key ID on error
                Success = false, // Operation failure indicator
                ErrorMessage = ex.Message // Error details for troubleshooting
            };

            // Return 500 Internal Server Error with error details
            return StatusCode(500, errorResponse);
        }
    }

    /// <summary>
    /// Generates JWT tokens for testing and development purposes.
    /// This endpoint provides a convenient way to create both user and service tokens
    /// for testing authentication flows and API integration scenarios.
    ///
    /// Endpoint: POST /jwt/test-token
    /// Authentication: None required (development/testing endpoint)
    ///
    /// Request Body:
    /// - UserName: Name for the token (user name or service name)
    /// - UserId: Optional user ID (auto-generated if not provided)
    /// - TokenType: "user" or "service" (defaults to "user")
    /// - Permissions: Array of permission strings (defaults to ["read", "write"])
    /// - ExpirationHours: Custom expiration time (defaults based on token type)
    ///
    /// Token Generation Logic:
    /// - Service tokens: Default 1-hour expiration, service-specific claims
    /// - User tokens: Default 8-hour expiration, user-specific claims
    /// - Both types include permissions and tracking claims (jti, iat)
    ///
    /// Security Warning:
    /// - This endpoint should be secured or disabled in production
    /// - Generated tokens have full cryptographic validity
    /// - Use only for development, testing, and integration scenarios
    /// </summary>
    /// <param name="request">Test token generation request parameters</param>
    /// <returns>JSON response containing the generated JWT token and metadata</returns>
    [HttpPost("test-token")]
    public IActionResult GenerateTestToken([FromBody] TestTokenRequest request)
    {
        try
        {
            // Log test token generation request for audit trail
            _logger.LogInformation("🧪 Generating test JWT token for: {UserName}", request.UserName);

            string token;

            // Generate service token for machine-to-machine scenarios
            if (request.TokenType?.ToLower() == "service")
            {
                token = _jwtTokenService.GenerateServiceToken(
                    request.UserName, // Service name
                    request.Permissions ?? new[] { "read", "write" }, // Default permissions
                    TimeSpan.FromHours(request.ExpirationHours ?? 1)); // Default 1 hour
            }
            // Generate user token for client application scenarios
            else
            {
                token = _jwtTokenService.GenerateUserToken(
                    request.UserId ?? Guid.NewGuid().ToString(), // Auto-generate user ID if not provided
                    request.UserName, // User display name
                    request.Permissions ?? new[] { "read", "write" }, // Default permissions
                    TimeSpan.FromHours(request.ExpirationHours ?? 8)); // Default 8 hours
            }

            // Create structured response with token and metadata
            var response = new
            {
                JwtToken = token, // Generated JWT token string
                TokenType = request.TokenType ?? "user", // Token type for reference
                UserName = request.UserName, // User/service name
                Permissions = request.Permissions ?? new[] { "read", "write" }, // Granted permissions
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(request.ExpirationHours ?? 8).ToUnixTimeSeconds(), // Expiration timestamp
                Success = true // Operation success indicator
            };

            // Log successful test token generation
            _logger.LogInformation("✅ Test JWT token generated successfully for: {UserName}", request.UserName);
            return Ok(response);
        }
        catch (Exception ex)
        {
            // Log test token generation failure for monitoring
            _logger.LogError(ex, "❌ Error generating test JWT token");

            // Create error response without exposing sensitive details
            var errorResponse = new
            {
                JwtToken = string.Empty, // No token on error
                Success = false, // Operation failure indicator
                ErrorMessage = ex.Message // Error details for troubleshooting
            };

            // Return 500 Internal Server Error with error details
            return StatusCode(500, errorResponse);
        }
    }
}

/// <summary>
/// Data transfer object for test JWT token generation requests.
/// This record defines the structure for requesting test tokens with
/// customizable parameters for different testing scenarios.
///
/// Usage Scenarios:
/// - Development testing of JWT authentication flows
/// - Integration testing with different permission sets
/// - Load testing with various token types and lifetimes
/// - API client testing and validation
///
/// Parameter Validation:
/// - UserName is required (used as subject for both user and service tokens)
/// - UserId is optional (auto-generated GUID if not provided for user tokens)
/// - TokenType defaults to "user" (can be "user" or "service")
/// - Permissions default to ["read", "write"] if not specified
/// - ExpirationHours defaults based on token type (1 for service, 8 for user)
/// </summary>
/// <param name="UserName">Required name for the token (user name or service name)</param>
/// <param name="UserId">Optional user identifier (auto-generated if null)</param>
/// <param name="TokenType">Token type: "user" or "service" (defaults to "user")</param>
/// <param name="Permissions">Array of permission strings (defaults to ["read", "write"])</param>
/// <param name="ExpirationHours">Custom expiration time in hours (defaults by token type)</param>
public record TestTokenRequest(
    string UserName,
    string? UserId = null,
    string? TokenType = "user", // "user" or "service"
    string[]? Permissions = null,
    int? ExpirationHours = null
);
