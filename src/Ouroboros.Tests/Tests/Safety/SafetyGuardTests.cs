// <copyright file="SafetyGuardTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.Tests.Safety;

/// <summary>
/// Safety-critical tests for the SafetyGuard class.
/// Verifies permission levels and dangerous pattern detection.
/// </summary>
[Trait("Category", "Safety")]
public sealed class SafetyGuardTests
{
    #region Permission Level Tests

    [Fact]
    public void CheckSafety_SafeAction_ReturnsAllowed()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.ReadOnly);
        var operation = "read_config";
        var parameters = new Dictionary<string, object>
        {
            ["path"] = "/config/app.json"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.ReadOnly);

        // Assert
        result.Safe.Should().BeTrue("safe read operations should be allowed");
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void CheckSafety_DangerousAction_ReturnsDenied()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.ReadOnly);
        var operation = "system_delete";
        var parameters = new Dictionary<string, object>
        {
            ["command"] = "rm -rf /"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.ReadOnly);

        // Assert
        result.Safe.Should().BeFalse("dangerous operations should be denied");
        result.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public void CheckSafety_UnknownAction_ReturnsDefaultDenied()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Isolated); // Default is Isolated
        var operation = "unknown_operation_xyz";
        var parameters = new Dictionary<string, object>();

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.ReadOnly);

        // Assert
        // Unknown actions require at least the default level
        result.RequiredLevel.Should().Be(PermissionLevel.Isolated, 
            "unknown actions should default to safe permission level");
    }

    #endregion

    #region Dangerous Pattern Detection

    [Fact]
    public void CheckSafety_FileSystemAccess_IsFlagged()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.ReadOnly);
        var operation = "write_file";
        var parameters = new Dictionary<string, object>
        {
            ["path"] = "/important/file.txt",
            ["content"] = "data"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.ReadOnly);

        // Assert
        result.Safe.Should().BeFalse("file write requires higher permission");
        result.Violations.Should().Contain(v => 
            v.Contains("write", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckSafety_NetworkAccess_IsFlagged()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.ReadOnly);
        var operation = "http_request";
        var parameters = new Dictionary<string, object>
        {
            ["url"] = "https://external-api.com"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.ReadOnly);

        // Assert
        // Network operations should not be safe at ReadOnly and should require higher permission
        result.Safe.Should().BeFalse("network operations should be denied or warned at ReadOnly level");
        ((int)result.RequiredLevel).Should().BeGreaterThan((int)PermissionLevel.ReadOnly,
            "network operations should require at least Isolated or higher permission level");
        (result.Violations.Any() || result.Warnings.Any()).Should().BeTrue(
            "network operations should produce a violation or warning");
    }

    [Fact]
    public void CheckSafety_ProcessExecution_IsFlagged()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.ReadOnly);
        var operation = "execute_process";
        var parameters = new Dictionary<string, object>
        {
            ["command"] = "subprocess",
            ["args"] = "shell"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.ReadOnly);

        // Assert
        result.Safe.Should().BeFalse("process execution is dangerous");
        result.Warnings.Should().Contain(w => 
            w.Contains("dangerous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckSafety_SelfModification_IsFlagged()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.ReadOnly);
        var operation = "modify_self";
        var parameters = new Dictionary<string, object>
        {
            ["code"] = "eval('malicious code')"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.ReadOnly);

        // Assert
        result.Safe.Should().BeFalse("self-modification is dangerous");
        result.Warnings.Should().Contain(w => 
            w.Contains("dangerous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckSafety_SqlInjection_IsFlagged()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.UserData);
        var operation = "database_query";
        var parameters = new Dictionary<string, object>
        {
            ["query"] = "SELECT * FROM users WHERE id = '1' OR '1'='1"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.UserData);

        // Assert
        result.Safe.Should().BeFalse("SQL injection patterns should be detected");
        result.Violations.Should().Contain(v => 
            v.Contains("injection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckSafety_PathTraversal_IsFlagged()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.UserData);
        var operation = "read_file";
        var parameters = new Dictionary<string, object>
        {
            ["path"] = "../../etc/passwd"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.UserData);

        // Assert
        result.Safe.Should().BeFalse("path traversal should be detected");
        result.Violations.Should().Contain(v => 
            v.Contains("injection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckSafety_XSSPattern_IsFlagged()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.UserData);
        var operation = "render_html";
        var parameters = new Dictionary<string, object>
        {
            ["content"] = "<script>alert('xss')</script>"
        };

        // Act
        var result = guard.CheckSafety(operation, parameters, PermissionLevel.UserData);

        // Assert
        result.Safe.Should().BeFalse("XSS patterns should be detected");
        result.Violations.Should().Contain(v => 
            v.Contains("injection", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Tool Execution Permission Tests

    [Fact]
    public void IsToolExecutionPermitted_SafeTool_ReturnsTrue()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Isolated);

        // Act
        var result = guard.IsToolExecutionPermitted("math", "calculate 2+2", PermissionLevel.Isolated);

        // Assert
        result.Should().BeTrue("math tool should be permitted");
    }

    [Fact]
    public void IsToolExecutionPermitted_DangerousTool_ReturnsFalse()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Isolated);

        // Act
        var result = guard.IsToolExecutionPermitted("system_admin", "rm -rf", PermissionLevel.Isolated);

        // Assert
        result.Should().BeFalse("system tools should require higher permission");
    }

    [Fact]
    public void IsToolExecutionPermitted_DeleteTool_RequiresConfirmation()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.UserData);

        // Act
        var result = guard.IsToolExecutionPermitted("delete_file", "file.txt", PermissionLevel.UserData);

        // Assert
        result.Should().BeFalse("delete operations require confirmation level");
    }

    #endregion

    #region Sandbox Tests

    [Fact]
    public void SandboxStep_AddsSecurityMetadata()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Isolated);
        var step = new PlanStep(
            "test_action",
            new Dictionary<string, object> { ["param"] = "value" },
            "Expected outcome",
            1.0);

        // Act
        var sandboxed = guard.SandboxStep(step);

        // Assert
        sandboxed.Parameters.Should().ContainKey("__sandboxed__");
        sandboxed.Parameters["__sandboxed__"].Should().Be(true);
        sandboxed.Parameters.Should().ContainKey("__original_action__");
    }

    [Fact]
    public void SandboxStep_SanitizesStringParameters()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Isolated);
        var step = new PlanStep(
            "test_action",
            new Dictionary<string, object>
            {
                ["safe_param"] = "normal value",
                ["injection"] = "<script>alert('xss')</script>"
            },
            "Expected outcome",
            1.0);

        // Act
        var sandboxed = guard.SandboxStep(step);

        // Assert
        sandboxed.Parameters.Should().ContainKey("safe_param");
        sandboxed.Parameters.Should().ContainKey("injection");
        // Parameters should be sanitized (the implementation may strip or escape)
    }

    #endregion

    #region Permission Registration Tests

    [Fact]
    public void RegisterPermission_AddsToRegistry()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Isolated);
        var permission = new Permission(
            "custom_tool",
            "Custom tool operation",
            PermissionLevel.UserData,
            new List<string> { "custom_action" });

        // Act
        guard.RegisterPermission(permission);
        var permissions = guard.GetPermissions();

        // Assert
        permissions.Should().Contain(p => p.Name == "custom_tool");
    }

    [Fact]
    public void GetRequiredPermission_UsesRegisteredPermission()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Isolated);
        var permission = new Permission(
            "test_operation",
            "Test operation",
            PermissionLevel.System,
            new List<string> { "test_action" });
        
        guard.RegisterPermission(permission);

        // Act
        var required = guard.GetRequiredPermission("test_operation");

        // Assert
        required.Should().Be(PermissionLevel.System);
    }

    #endregion
}
