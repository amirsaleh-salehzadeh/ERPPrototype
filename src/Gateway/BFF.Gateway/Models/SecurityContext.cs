namespace BFF.Gateway.Models;

/// <summary>
/// Security context that flows through the middleware pipeline
/// </summary>
public class SecurityContext
{
    public string RequestId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    
    // API Key Information
    public ApiKeyInfo? ApiKey { get; set; }
    
    // User Information
    public UserInfo? User { get; set; }
    
    // Security Decisions
    public List<SecurityDecision> Decisions { get; set; } = new();
    
    public bool IsAuthenticated => ApiKey?.IsValid == true && User?.IsAuthenticated == true;
    public bool IsAuthorized => Decisions.All(d => d.IsAllowed);
}

public class ApiKeyInfo
{
    public string KeyId { get; set; } = string.Empty;
    public string MaskedKey { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public ApiAccessLevel AccessLevel { get; set; }
    public List<string> AllowedServices { get; set; } = new();
    public List<string> AllowedEndpoints { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public UserAccessLevel AccessLevel { get; set; }
    public string TokenType { get; set; } = string.Empty; // JWT, Session, etc.
    public DateTime TokenExpiresAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class SecurityDecision
{
    public string Stage { get; set; } = string.Empty; // ApiKeyValidation, ApiAccess, UserAuth, UserAccess
    public bool IsAllowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
}

public enum ApiAccessLevel
{
    None = 0,
    ReadOnly = 1,
    Limited = 2,
    Standard = 3,
    Premium = 4,
    Admin = 5
}

public enum UserAccessLevel
{
    None = 0,
    Guest = 1,
    User = 2,
    PowerUser = 3,
    Manager = 4,
    Admin = 5,
    SuperAdmin = 6
}

public enum EndpointAccessLevel
{
    Public = 0,      // No authentication required
    ApiKeyOnly = 1,  // Only API key required
    UserAuth = 2,    // User authentication required
    UserRole = 3,    // Specific user role required
    Admin = 4,       // Admin access required
    SuperAdmin = 5   // Super admin access required
}

/// <summary>
/// Configuration for endpoint access requirements
/// </summary>
public class EndpointSecurityConfig
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public EndpointAccessLevel RequiredAccessLevel { get; set; }
    public ApiAccessLevel RequiredApiAccessLevel { get; set; }
    public UserAccessLevel RequiredUserAccessLevel { get; set; }
    public List<string> RequiredRoles { get; set; } = new();
    public List<string> RequiredPermissions { get; set; } = new();
    public bool AllowAnonymous { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Security configuration for the entire application
/// </summary>
public class SecurityConfiguration
{
    public List<EndpointSecurityConfig> Endpoints { get; set; } = new();
    public List<string> PublicPaths { get; set; } = new();
    public SecuritySettings Settings { get; set; } = new();
}

public class SecuritySettings
{
    public bool EnableApiKeyValidation { get; set; } = true;
    public bool EnableUserAuthentication { get; set; } = true;
    public bool EnableUserAuthorization { get; set; } = true;
    public bool LogSecurityDecisions { get; set; } = true;
    public bool LogSecurityFailures { get; set; } = true;
    public int TokenExpirationToleranceMinutes { get; set; } = 5;
    public bool AllowExpiredTokens { get; set; } = false;
}
