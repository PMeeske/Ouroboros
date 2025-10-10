namespace LangChainPipeline.Core.Security.Authentication;

/// <summary>
/// Represents an authenticated principal (user/service).
/// </summary>
public class AuthenticationPrincipal
{
    /// <summary>
    /// Unique identifier for the principal.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Username or service name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Email address (for users).
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Roles assigned to the principal.
    /// </summary>
    public List<string> Roles { get; init; } = new();

    /// <summary>
    /// Claims/attributes of the principal.
    /// </summary>
    public Dictionary<string, string> Claims { get; init; } = new();

    /// <summary>
    /// When the authentication expires.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Checks if the principal is expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Checks if the principal has a specific role.
    /// </summary>
    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the principal has any of the specified roles.
    /// </summary>
    public bool HasAnyRole(params string[] roles) =>
        roles.Any(r => HasRole(r));

    /// <summary>
    /// Checks if the principal has all of the specified roles.
    /// </summary>
    public bool HasAllRoles(params string[] roles) =>
        roles.All(r => HasRole(r));

    /// <summary>
    /// Gets a claim value.
    /// </summary>
    public string? GetClaim(string key) =>
        Claims.TryGetValue(key, out var value) ? value : null;
}

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public class AuthenticationResult
{
    /// <summary>
    /// Whether authentication was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Authenticated principal (if successful).
    /// </summary>
    public AuthenticationPrincipal? Principal { get; init; }

    /// <summary>
    /// Authentication token (e.g., JWT).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful authentication result.
    /// </summary>
    public static AuthenticationResult Success(AuthenticationPrincipal principal, string token) =>
        new()
        {
            IsSuccess = true,
            Principal = principal,
            Token = token
        };

    /// <summary>
    /// Creates a failed authentication result.
    /// </summary>
    public static AuthenticationResult Failure(string errorMessage) =>
        new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Interface for authentication providers.
/// </summary>
public interface IAuthenticationProvider
{
    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken ct = default);

    /// <summary>
    /// Validates an authentication token.
    /// </summary>
    Task<AuthenticationResult> ValidateTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Refreshes an authentication token.
    /// </summary>
    Task<AuthenticationResult> RefreshTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Revokes an authentication token.
    /// </summary>
    Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default);
}

/// <summary>
/// Simple in-memory authentication provider for development/testing.
/// </summary>
public class InMemoryAuthenticationProvider : IAuthenticationProvider
{
    private readonly Dictionary<string, (string Password, AuthenticationPrincipal Principal)> _users = new();
    private readonly HashSet<string> _revokedTokens = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers a user.
    /// </summary>
    public void RegisterUser(string username, string password, AuthenticationPrincipal principal)
    {
        lock (_lock)
        {
            _users[username] = (password, principal);
        }
    }

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    public Task<AuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(username, out var user))
            {
                return Task.FromResult(AuthenticationResult.Failure("Invalid username or password"));
            }

            if (user.Password != password)
            {
                return Task.FromResult(AuthenticationResult.Failure("Invalid username or password"));
            }

            // Generate a simple token (in production, use JWT)
            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            return Task.FromResult(AuthenticationResult.Success(user.Principal, token));
        }
    }

    /// <summary>
    /// Validates an authentication token.
    /// </summary>
    public Task<AuthenticationResult> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_revokedTokens.Contains(token))
            {
                return Task.FromResult(AuthenticationResult.Failure("Token has been revoked"));
            }

            // In a real implementation, decode the token and extract the principal
            // For now, return a dummy principal
            return Task.FromResult(AuthenticationResult.Failure("Token validation not fully implemented"));
        }
    }

    /// <summary>
    /// Refreshes an authentication token.
    /// </summary>
    public Task<AuthenticationResult> RefreshTokenAsync(string token, CancellationToken ct = default)
    {
        return Task.FromResult(AuthenticationResult.Failure("Token refresh not implemented"));
    }

    /// <summary>
    /// Revokes an authentication token.
    /// </summary>
    public Task<bool> RevokeTokenAsync(string token, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _revokedTokens.Add(token);
            return Task.FromResult(true);
        }
    }
}
