using Microsoft.AspNetCore.Mvc;
using ERP.IdentityService.Services;

namespace ERP.IdentityService.Controllers;

/// <summary>
/// Controller for JWT-related operations
/// </summary>
[ApiController]
[Route("[controller]")]
public class JwtController : ControllerBase
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<JwtController> _logger;

    public JwtController(IJwtTokenService jwtTokenService, ILogger<JwtController> logger)
    {
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the public key for JWT validation (publicly accessible)
    /// </summary>
    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
    {
        try
        {
            _logger.LogInformation("🔑 REST GetPublicKey called");

            var publicKeyPem = _jwtTokenService.GetPublicKeyPem();

            var response = new
            {
                PublicKeyPem = publicKeyPem,
                KeyId = "erp-identity-validation-key",
                Success = true,
                ErrorMessage = string.Empty
            };

            _logger.LogInformation("✅ REST Public key retrieved successfully");
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in REST GetPublicKey");
            
            var errorResponse = new
            {
                PublicKeyPem = string.Empty,
                KeyId = string.Empty,
                Success = false,
                ErrorMessage = ex.Message
            };
            
            return StatusCode(500, errorResponse);
        }
    }

    /// <summary>
    /// Test endpoint to generate a JWT token for testing purposes
    /// </summary>
    [HttpPost("test-token")]
    public IActionResult GenerateTestToken([FromBody] TestTokenRequest request)
    {
        try
        {
            _logger.LogInformation("🧪 Generating test JWT token for: {UserName}", request.UserName);

            string token;
            if (request.TokenType?.ToLower() == "service")
            {
                token = _jwtTokenService.GenerateServiceToken(
                    request.UserName,
                    request.Permissions ?? new[] { "read", "write" },
                    TimeSpan.FromHours(request.ExpirationHours ?? 1));
            }
            else
            {
                token = _jwtTokenService.GenerateUserToken(
                    request.UserId ?? Guid.NewGuid().ToString(),
                    request.UserName,
                    request.Permissions ?? new[] { "read", "write" },
                    TimeSpan.FromHours(request.ExpirationHours ?? 8));
            }

            var response = new
            {
                JwtToken = token,
                TokenType = request.TokenType ?? "user",
                UserName = request.UserName,
                Permissions = request.Permissions ?? new[] { "read", "write" },
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(request.ExpirationHours ?? 8).ToUnixTimeSeconds(),
                Success = true
            };

            _logger.LogInformation("✅ Test JWT token generated successfully for: {UserName}", request.UserName);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating test JWT token");

            var errorResponse = new
            {
                JwtToken = string.Empty,
                Success = false,
                ErrorMessage = ex.Message
            };

            return StatusCode(500, errorResponse);
        }
    }
}

/// <summary>
/// Request model for test token generation
/// </summary>
public record TestTokenRequest(
    string UserName,
    string? UserId = null,
    string? TokenType = "user", // "user" or "service"
    string[]? Permissions = null,
    int? ExpirationHours = null
);
