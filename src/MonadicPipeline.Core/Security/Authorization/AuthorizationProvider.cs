using LangChainPipeline.Core.Security.Authentication;

namespace LangChainPipeline.Core.Security.Authorization;

/// <summary>
/// Result of an authorization check.
/// </summary>
public class AuthorizationResult
{
    /// <summary>
    /// Whether the action is authorized.
    /// </summary>
    public bool IsAuthorized { get; init; }

    /// <summary>
    /// Reason for denial (if not authorized).
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// Creates an authorized result.
    /// </summary>
    public static AuthorizationResult Allow() =>
        new() { IsAuthorized = true };

    /// <summary>
    /// Creates a denied result with a reason.
    /// </summary>
    public static AuthorizationResult Deny(string reason) =>
        new() { IsAuthorized = false, DenialReason = reason };
}

/// <summary>
/// Interface for authorization providers.
/// </summary>
public interface IAuthorizationProvider
{
    /// <summary>
    /// Checks if a principal is authorized to execute a tool.
    /// </summary>
    Task<AuthorizationResult> AuthorizeToolExecutionAsync(
        AuthenticationPrincipal principal,
        string toolName,
        string? input = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a principal has a specific permission.
    /// </summary>
    Task<AuthorizationResult> CheckPermissionAsync(
        AuthenticationPrincipal principal,
        string permission,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a principal can access a resource.
    /// </summary>
    Task<AuthorizationResult> CheckResourceAccessAsync(
        AuthenticationPrincipal principal,
        string resourceType,
        string resourceId,
        string action,
        CancellationToken ct = default);
}

/// <summary>
/// Role-based authorization provider.
/// </summary>
public class RoleBasedAuthorizationProvider : IAuthorizationProvider
{
    private readonly Dictionary<string, HashSet<string>> _rolePermissions = new();
    private readonly Dictionary<string, HashSet<string>> _toolRoleRequirements = new();
    private readonly object _lock = new();

    /// <summary>
    /// Assigns a permission to a role.
    /// </summary>
    public void AssignPermissionToRole(string role, string permission)
    {
        lock (_lock)
        {
            if (!_rolePermissions.ContainsKey(role))
            {
                _rolePermissions[role] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            _rolePermissions[role].Add(permission);
        }
    }

    /// <summary>
    /// Requires a role to execute a tool.
    /// </summary>
    public void RequireRoleForTool(string toolName, string role)
    {
        lock (_lock)
        {
            if (!_toolRoleRequirements.ContainsKey(toolName))
            {
                _toolRoleRequirements[toolName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            _toolRoleRequirements[toolName].Add(role);
        }
    }

    /// <summary>
    /// Checks if a principal is authorized to execute a tool.
    /// </summary>
    public Task<AuthorizationResult> AuthorizeToolExecutionAsync(
        AuthenticationPrincipal principal,
        string toolName,
        string? input = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            // Check if tool has role requirements
            if (!_toolRoleRequirements.TryGetValue(toolName, out var requiredRoles))
            {
                // No requirements means anyone can execute
                return Task.FromResult(AuthorizationResult.Allow());
            }

            // Check if principal has any of the required roles
            if (principal.Roles.Any(r => requiredRoles.Contains(r)))
            {
                return Task.FromResult(AuthorizationResult.Allow());
            }

            return Task.FromResult(AuthorizationResult.Deny(
                $"Tool '{toolName}' requires one of the following roles: {string.Join(", ", requiredRoles)}"));
        }
    }

    /// <summary>
    /// Checks if a principal has a specific permission.
    /// </summary>
    public Task<AuthorizationResult> CheckPermissionAsync(
        AuthenticationPrincipal principal,
        string permission,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            // Check if any of the principal's roles have the permission
            foreach (var role in principal.Roles)
            {
                if (_rolePermissions.TryGetValue(role, out var permissions) &&
                    permissions.Contains(permission))
                {
                    return Task.FromResult(AuthorizationResult.Allow());
                }
            }

            return Task.FromResult(AuthorizationResult.Deny(
                $"Missing required permission: {permission}"));
        }
    }

    /// <summary>
    /// Checks if a principal can access a resource.
    /// </summary>
    public Task<AuthorizationResult> CheckResourceAccessAsync(
        AuthenticationPrincipal principal,
        string resourceType,
        string resourceId,
        string action,
        CancellationToken ct = default)
    {
        // Build permission string (e.g., "document:read", "pipeline:execute")
        var permission = $"{resourceType}:{action}";
        return CheckPermissionAsync(principal, permission, ct);
    }
}
