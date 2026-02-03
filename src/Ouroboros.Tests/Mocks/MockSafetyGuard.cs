// <copyright file="MockSafetyGuard.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Mock implementation of ISafetyGuard for testing purposes.
/// Provides configurable safety checking behavior.
/// </summary>
public sealed class MockSafetyGuard : ISafetyGuard
{
    private readonly Dictionary<string, Permission> permissions = new();
    private readonly Func<string, Dictionary<string, object>, PermissionLevel, SafetyCheckResult>? checkFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockSafetyGuard"/> class.
    /// By default, all operations are considered safe.
    /// </summary>
    public MockSafetyGuard()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockSafetyGuard"/> class with custom check logic.
    /// </summary>
    /// <param name="checkFactory">Factory function for safety checks.</param>
    public MockSafetyGuard(Func<string, Dictionary<string, object>, PermissionLevel, SafetyCheckResult> checkFactory)
    {
        this.checkFactory = checkFactory;
    }

    /// <summary>
    /// Gets the number of times CheckSafety was called.
    /// </summary>
    public int CheckCallCount { get; private set; }

    /// <summary>
    /// Gets the last operation that was checked.
    /// </summary>
    public string? LastCheckedOperation { get; private set; }

    /// <summary>
    /// Checks if an operation is safe to execute.
    /// </summary>
    public SafetyCheckResult CheckSafety(
        string operation,
        Dictionary<string, object> parameters,
        PermissionLevel currentLevel)
    {
        this.CheckCallCount++;
        this.LastCheckedOperation = operation;

        if (this.checkFactory != null)
        {
            return this.checkFactory(operation, parameters, currentLevel);
        }

        // Default: everything is safe at ReadOnly level
        return new SafetyCheckResult(
            Safe: true,
            Violations: new List<string>(),
            Warnings: new List<string>(),
            RequiredLevel: PermissionLevel.ReadOnly);
    }

    /// <summary>
    /// Validates if tool execution is permitted.
    /// </summary>
    public bool IsToolExecutionPermitted(
        string toolName,
        string arguments,
        PermissionLevel currentLevel)
    {
        // Default: allow all tool executions
        return true;
    }

    /// <summary>
    /// Sandboxes a plan step for safe execution.
    /// </summary>
    public PlanStep SandboxStep(PlanStep step)
    {
        // Default: return step unchanged
        return step;
    }

    /// <summary>
    /// Gets required permission level for an action.
    /// </summary>
    public PermissionLevel GetRequiredPermission(string action)
    {
        // Default: ReadOnly for all actions
        return PermissionLevel.ReadOnly;
    }

    /// <summary>
    /// Registers a permission policy.
    /// </summary>
    public void RegisterPermission(Permission permission)
    {
        this.permissions[permission.Name] = permission;
    }

    /// <summary>
    /// Gets all registered permissions.
    /// </summary>
    public IReadOnlyList<Permission> GetPermissions()
    {
        return this.permissions.Values.ToList();
    }
}
