// <copyright file="AuthorizationProvider.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Security.Authorization;

using Ouroboros.Core.Security.Authentication;

/// <summary>
/// Result of an authorization check.
/// </summary>
public class AuthorizationResult
{
    /// <summary>
    /// Gets a value indicating whether whether the action is authorized.
    /// </summary>
    public bool IsAuthorized { get; init; }

    /// <summary>
    /// Gets reason for denial (if not authorized).
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// Creates an authorized result.
    /// </summary>
    /// <returns></returns>
    public static AuthorizationResult Allow() =>
        new() { IsAuthorized = true };

    /// <summary>
    /// Creates a denied result with a reason.
    /// </summary>
    /// <returns></returns>
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<AuthorizationResult> AuthorizeToolExecutionAsync(
        AuthenticationPrincipal principal,
        string toolName,
        string? input = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a principal has a specific permission.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    Task<AuthorizationResult> CheckPermissionAsync(
        AuthenticationPrincipal principal,
        string permission,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a principal can access a resource.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    private readonly Dictionary<string, HashSet<string>> rolePermissions = new();
    private readonly Dictionary<string, HashSet<string>> toolRoleRequirements = new();
    private readonly object @lock = new();

    /// <summary>
    /// Assigns a permission to a role.
    /// </summary>
    public void AssignPermissionToRole(string role, string permission)
    {
        lock (this.@lock)
        {
            if (!this.rolePermissions.ContainsKey(role))
            {
                this.rolePermissions[role] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            this.rolePermissions[role].Add(permission);
        }
    }

    /// <summary>
    /// Requires a role to execute a tool.
    /// </summary>
    public void RequireRoleForTool(string toolName, string role)
    {
        lock (this.@lock)
        {
            if (!this.toolRoleRequirements.ContainsKey(toolName))
            {
                this.toolRoleRequirements[toolName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            this.toolRoleRequirements[toolName].Add(role);
        }
    }

    /// <summary>
    /// Checks if a principal is authorized to execute a tool.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<AuthorizationResult> AuthorizeToolExecutionAsync(
        AuthenticationPrincipal principal,
        string toolName,
        string? input = null,
        CancellationToken ct = default)
    {
        lock (this.@lock)
        {
            // Check if tool has role requirements
            if (!this.toolRoleRequirements.TryGetValue(toolName, out HashSet<string>? requiredRoles))
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<AuthorizationResult> CheckPermissionAsync(
        AuthenticationPrincipal principal,
        string permission,
        CancellationToken ct = default)
    {
        lock (this.@lock)
        {
            // Check if any of the principal's roles have the permission
            foreach (string role in principal.Roles)
            {
                if (this.rolePermissions.TryGetValue(role, out HashSet<string>? permissions) &&
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<AuthorizationResult> CheckResourceAccessAsync(
        AuthenticationPrincipal principal,
        string resourceType,
        string resourceId,
        string action,
        CancellationToken ct = default)
    {
        // Build permission string (e.g., "document:read", "pipeline:execute")
        string permission = $"{resourceType}:{action}";
        return this.CheckPermissionAsync(principal, permission, ct);
    }
}
