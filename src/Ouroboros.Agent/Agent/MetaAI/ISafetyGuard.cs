#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Safety Guard - Permission-based safe execution
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a permission for executing operations.
/// </summary>
public sealed record Permission(
    string Name,
    string Description,
    PermissionLevel Level,
    List<string> AllowedActions);

/// <summary>
/// Permission levels for operations.
/// </summary>
public enum PermissionLevel
{
    /// <summary>
    /// Read-only operations, no side effects.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Can write to temporary/isolated storage.
    /// </summary>
    Isolated,

    /// <summary>
    /// Can modify user data with confirmation.
    /// </summary>
    UserDataWithConfirmation,

    /// <summary>
    /// Can modify user data without confirmation.
    /// </summary>
    UserData,

    /// <summary>
    /// Can modify system state.
    /// </summary>
    System,

    /// <summary>
    /// Unrestricted access (use with extreme caution).
    /// </summary>
    Unrestricted
}

/// <summary>
/// Safety check result.
/// </summary>
public sealed record SafetyCheckResult(
    bool Safe,
    List<string> Violations,
    List<string> Warnings,
    PermissionLevel RequiredLevel);

/// <summary>
/// Interface for safety and permission checks.
/// Ensures operations are executed within authorized boundaries.
/// </summary>
public interface ISafetyGuard
{
    /// <summary>
    /// Checks if an operation is safe to execute.
    /// </summary>
    /// <param name="operation">The operation to check</param>
    /// <param name="parameters">Operation parameters</param>
    /// <param name="currentLevel">Current permission level</param>
    /// <returns>Safety check result</returns>
    SafetyCheckResult CheckSafety(
        string operation,
        Dictionary<string, object> parameters,
        PermissionLevel currentLevel);

    /// <summary>
    /// Validates tool execution is permitted.
    /// </summary>
    /// <param name="toolName">The tool to execute</param>
    /// <param name="arguments">Tool arguments</param>
    /// <param name="currentLevel">Current permission level</param>
    /// <returns>True if permitted, false otherwise</returns>
    bool IsToolExecutionPermitted(
        string toolName,
        string arguments,
        PermissionLevel currentLevel);

    /// <summary>
    /// Sandboxes a plan step for safe execution.
    /// </summary>
    /// <param name="step">The plan step to sandbox</param>
    /// <returns>Sandboxed step with safety restrictions applied</returns>
    PlanStep SandboxStep(PlanStep step);

    /// <summary>
    /// Gets required permission level for an action.
    /// </summary>
    /// <param name="action">The action to check</param>
    /// <returns>Required permission level</returns>
    PermissionLevel GetRequiredPermission(string action);

    /// <summary>
    /// Registers a permission policy.
    /// </summary>
    /// <param name="permission">The permission to register</param>
    void RegisterPermission(Permission permission);

    /// <summary>
    /// Gets all registered permissions.
    /// </summary>
    IReadOnlyList<Permission> GetPermissions();
}
