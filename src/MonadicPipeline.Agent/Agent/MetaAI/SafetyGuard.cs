#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Safety Guard Implementation
// Permission-based safe execution
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Implementation of safety guard for permission-based execution.
/// </summary>
public sealed class SafetyGuard : ISafetyGuard
{
    private readonly ConcurrentDictionary<string, Permission> _permissions = new();
    private readonly PermissionLevel _defaultLevel;

    public SafetyGuard(PermissionLevel defaultLevel = PermissionLevel.Isolated)
    {
        _defaultLevel = defaultLevel;
        InitializeDefaultPermissions();
    }

    /// <summary>
    /// Checks if an operation is safe to execute.
    /// </summary>
    public SafetyCheckResult CheckSafety(
        string operation,
        Dictionary<string, object> parameters,
        PermissionLevel currentLevel)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(parameters);

        List<string> violations = new List<string>();
        List<string> warnings = new List<string>();
        PermissionLevel requiredLevel = GetRequiredPermission(operation);

        // Check if current permission level is sufficient
        if (currentLevel < requiredLevel)
        {
            violations.Add($"Operation '{operation}' requires {requiredLevel} but current level is {currentLevel}");
        }

        // Check for dangerous patterns
        if (ContainsDangerousPatterns(operation, parameters))
        {
            warnings.Add("Operation contains potentially dangerous patterns");
        }

        // Check parameter safety
        foreach (KeyValuePair<string, object> param in parameters)
        {
            if (param.Value is string strValue)
            {
                if (ContainsInjectionPatterns(strValue))
                {
                    violations.Add($"Parameter '{param.Key}' contains potential injection patterns");
                }
            }
        }

        bool isSafe = violations.Count == 0;
        return new SafetyCheckResult(isSafe, violations, warnings, requiredLevel);
    }

    /// <summary>
    /// Validates tool execution permission.
    /// </summary>
    public bool IsToolExecutionPermitted(
        string toolName,
        string arguments,
        PermissionLevel currentLevel)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;

        PermissionLevel requiredLevel = GetRequiredPermission(toolName);

        if (currentLevel < requiredLevel)
            return false;

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
    public PlanStep SandboxStep(PlanStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        // Create sandboxed version with restricted parameters
        Dictionary<string, object> sandboxedParams = new Dictionary<string, object>();

        foreach (KeyValuePair<string, object> param in step.Parameters)
        {
            if (param.Value is string strValue)
            {
                // Sanitize string values
                sandboxedParams[param.Key] = SanitizeString(strValue);
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
    public PermissionLevel GetRequiredPermission(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return _defaultLevel;

        // Check registered permissions
        if (_permissions.TryGetValue(action, out Permission? permission))
        {
            return permission.Level;
        }

        // Determine based on action patterns
        string actionLower = action.ToLowerInvariant();

        if (actionLower.Contains("read") || actionLower.Contains("get") || actionLower.Contains("list"))
            return PermissionLevel.ReadOnly;

        if (actionLower.Contains("delete") || actionLower.Contains("drop") || actionLower.Contains("remove"))
            return PermissionLevel.UserDataWithConfirmation;

        if (actionLower.Contains("system") || actionLower.Contains("admin"))
            return PermissionLevel.System;

        if (actionLower.Contains("write") || actionLower.Contains("update") || actionLower.Contains("create"))
            return PermissionLevel.UserData;

        return _defaultLevel;
    }

    /// <summary>
    /// Registers a permission policy.
    /// </summary>
    public void RegisterPermission(Permission permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        _permissions[permission.Name] = permission;
    }

    /// <summary>
    /// Gets all registered permissions.
    /// </summary>
    public IReadOnlyList<Permission> GetPermissions()
        => _permissions.Values.OrderBy(p => p.Level).ToList();

    private void InitializeDefaultPermissions()
    {
        // Register common tool permissions
        RegisterPermission(new Permission(
            "math",
            "Mathematical calculations",
            PermissionLevel.ReadOnly,
            new List<string> { "calculate", "compute", "evaluate" }));

        RegisterPermission(new Permission(
            "search",
            "Search operations",
            PermissionLevel.ReadOnly,
            new List<string> { "search", "find", "query" }));

        RegisterPermission(new Permission(
            "llm",
            "LLM text generation",
            PermissionLevel.Isolated,
            new List<string> { "generate", "complete", "chat" }));

        RegisterPermission(new Permission(
            "run_usedraft",
            "Generate draft",
            PermissionLevel.Isolated,
            new List<string> { "draft" }));

        RegisterPermission(new Permission(
            "run_usecritique",
            "Critique content",
            PermissionLevel.Isolated,
            new List<string> { "critique", "review" }));

        RegisterPermission(new Permission(
            "file_write",
            "File write operations",
            PermissionLevel.UserDataWithConfirmation,
            new List<string> { "write_file", "save_file" }));
    }

    private bool ContainsDangerousPatterns(string operation, Dictionary<string, object> parameters)
    {
        string[] dangerousPatterns = new[]
        {
            "eval", "exec", "system", "shell", "subprocess",
            "rm -rf", "delete *", "drop table", "truncate"
        };

        string combined = operation + " " + string.Join(" ", parameters.Values.Select(v => v?.ToString() ?? ""));
        string lowerCombined = combined.ToLowerInvariant();

        return dangerousPatterns.Any(pattern => lowerCombined.Contains(pattern));
    }

    private bool ContainsInjectionPatterns(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] injectionPatterns = new[]
        {
            "';", "\"; ", "' OR '1'='1", "\" OR \"1\"=\"1",
            "../", "..\\", "<script", "javascript:",
            "onload=", "onerror="
        };

        return injectionPatterns.Any(pattern =>
            value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private string SanitizeString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        // Remove potentially dangerous characters
        string sanitized = value
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
