// <copyright file="SafetyGuard.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using System.Collections.Concurrent;

/// <summary>
/// Implementation of safety guard for permission-based execution.
/// </summary>
public sealed class SafetyGuard : ISafetyGuard
{
    private readonly ConcurrentDictionary<string, Permission> permissions = new();
    private readonly PermissionLevel defaultLevel;

    public SafetyGuard(PermissionLevel defaultLevel = PermissionLevel.Isolated)
    {
        this.defaultLevel = defaultLevel;
        this.InitializeDefaultPermissions();
    }

    /// <summary>
    /// Checks if an operation is safe to execute.
    /// </summary>
    /// <returns></returns>
    public SafetyCheckResult CheckSafety(
        string operation,
        Dictionary<string, object> parameters,
        PermissionLevel currentLevel)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(parameters);

        var violations = new List<string>();
        var warnings = new List<string>();
        var requiredLevel = this.GetRequiredPermission(operation);

        // Check if current permission level is sufficient
        if (currentLevel < requiredLevel)
        {
            violations.Add($"Operation '{operation}' requires {requiredLevel} but current level is {currentLevel}");
        }

        // Check for dangerous patterns
        if (this.ContainsDangerousPatterns(operation, parameters))
        {
            warnings.Add("Operation contains potentially dangerous patterns");
        }

        // Check parameter safety
        foreach (var param in parameters)
        {
            if (param.Value is string strValue)
            {
                if (this.ContainsInjectionPatterns(strValue))
                {
                    violations.Add($"Parameter '{param.Key}' contains potential injection patterns");
                }
            }
        }

        var isSafe = violations.Count == 0;
        return new SafetyCheckResult(isSafe, violations, warnings, requiredLevel);
    }

    /// <summary>
    /// Validates tool execution permission.
    /// </summary>
    /// <returns></returns>
    public bool IsToolExecutionPermitted(
        string toolName,
        string arguments,
        PermissionLevel currentLevel)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        var requiredLevel = this.GetRequiredPermission(toolName);

        if (currentLevel < requiredLevel)
        {
            return false;
        }

        // Additional checks for specific tools
        if (toolName.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("remove", StringComparison.OrdinalIgnoreCase))
        {
            return currentLevel >= PermissionLevel.UserDataWithConfirmation;
        }

        if (toolName.Contains("system", StringComparison.OrdinalIgnoreCase))
        {
            return currentLevel >= PermissionLevel.System;
        }

        return true;
    }

    /// <summary>
    /// Sandboxes a plan step for safe execution.
    /// </summary>
    /// <returns></returns>
    public PlanStep SandboxStep(PlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        // Create sandboxed version with restricted parameters
        var sandboxedParams = new Dictionary<string, object>();

        foreach (var param in step.Parameters)
        {
            if (param.Value is string strValue)
            {
                // Sanitize string values
                sandboxedParams[param.Key] = this.SanitizeString(strValue);
            }
            else
            {
                sandboxedParams[param.Key] = param.Value;
            }
        }

        // Add sandbox metadata
        sandboxedParams["__sandboxed__"] = true;
        sandboxedParams["__original_action__"] = step.Action;

        return step with { Parameters = sandboxedParams };
    }

    /// <summary>
    /// Gets required permission level for an action.
    /// </summary>
    /// <returns></returns>
    public PermissionLevel GetRequiredPermission(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return this.defaultLevel;
        }

        // Check registered permissions
        if (this.permissions.TryGetValue(action, out var permission))
        {
            return permission.Level;
        }

        // Determine based on action patterns
        var actionLower = action.ToLowerInvariant();

        if (actionLower.Contains("read") || actionLower.Contains("get") || actionLower.Contains("list"))
        {
            return PermissionLevel.ReadOnly;
        }

        if (actionLower.Contains("delete") || actionLower.Contains("drop") || actionLower.Contains("remove"))
        {
            return PermissionLevel.UserDataWithConfirmation;
        }

        if (actionLower.Contains("system") || actionLower.Contains("admin"))
        {
            return PermissionLevel.System;
        }

        if (actionLower.Contains("write") || actionLower.Contains("update") || actionLower.Contains("create"))
        {
            return PermissionLevel.UserData;
        }

        return this.defaultLevel;
    }

    /// <summary>
    /// Registers a permission policy.
    /// </summary>
    public void RegisterPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        this.permissions[permission.Name] = permission;
    }

    /// <summary>
    /// Gets all registered permissions.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<Permission> GetPermissions()
        => this.permissions.Values.OrderBy(p => p.Level).ToList();

    private void InitializeDefaultPermissions()
    {
        // Register common tool permissions
        this.RegisterPermission(new Permission(
            "math",
            "Mathematical calculations",
            PermissionLevel.ReadOnly,
            new List<string> { "calculate", "compute", "evaluate" }));

        this.RegisterPermission(new Permission(
            "search",
            "Search operations",
            PermissionLevel.ReadOnly,
            new List<string> { "search", "find", "query" }));

        this.RegisterPermission(new Permission(
            "llm",
            "LLM text generation",
            PermissionLevel.Isolated,
            new List<string> { "generate", "complete", "chat" }));

        this.RegisterPermission(new Permission(
            "run_usedraft",
            "Generate draft",
            PermissionLevel.Isolated,
            new List<string> { "draft" }));

        this.RegisterPermission(new Permission(
            "run_usecritique",
            "Critique content",
            PermissionLevel.Isolated,
            new List<string> { "critique", "review" }));

        this.RegisterPermission(new Permission(
            "file_write",
            "File write operations",
            PermissionLevel.UserDataWithConfirmation,
            new List<string> { "write_file", "save_file" }));
    }

    private bool ContainsDangerousPatterns(string operation, Dictionary<string, object> parameters)
    {
        var dangerousPatterns = new[]
        {
            "eval", "exec", "system", "shell", "subprocess",
            "rm -rf", "delete *", "drop table", "truncate",
        };

        var combined = operation + " " + string.Join(" ", parameters.Values.Select(v => v?.ToString() ?? string.Empty));
        var lowerCombined = combined.ToLowerInvariant();

        return dangerousPatterns.Any(pattern => lowerCombined.Contains(pattern));
    }

    private bool ContainsInjectionPatterns(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var injectionPatterns = new[]
        {
            "';", "\"; ", "' OR '1'='1", "\" OR \"1\"=\"1",
            "../", "..\\", "<script", "javascript:",
            "onload=", "onerror=",
        };

        return injectionPatterns.Any(pattern =>
            value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private string SanitizeString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Remove potentially dangerous characters
        var sanitized = value
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&#39;")
            .Replace("\"", "&quot;");

        // Limit length to prevent DOS
        if (sanitized.Length > 10000)
        {
            sanitized = sanitized.Substring(0, 10000);
        }

        return sanitized;
    }
}
