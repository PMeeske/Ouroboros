using FluentAssertions;
using MonadicPipeline.Android.Tests.Infrastructure;
using Xunit;

namespace MonadicPipeline.Android.Tests;

/// <summary>
/// End-to-end UI tests for the Android MainPage using MauiApplicationFactory
/// Similar to integration tests in ASP.NET Core using WebApplicationFactory
/// </summary>
public class MainPageUITests : IClassFixture<MauiApplicationFactory<TestStartup>>
{
    private readonly MauiApplicationFactory<TestStartup> _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPageUITests"/> class.
    /// </summary>
    public MainPageUITests(MauiApplicationFactory<TestStartup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MainPage_SuccessfulInitialization_ShouldDisplayWelcomeScreen()
    {
        // Arrange & Act
        using var client = _factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();
        await client.WaitForPageLoadAsync();

        // Assert
        var content = mainPage.GetContent();
        content.Should().Contain("MonadicPipeline CLI v1.0");
        content.Should().Contain("Type 'help' to see available commands");
        content.Should().Contain("> ");
        mainPage.IsLoaded.Should().BeTrue();
        mainPage.CliExecutor.Should().NotBeNull();
    }

    [Fact]
    public async Task MainPage_ExecuteHelpCommand_ShouldDisplayResult()
    {
        // Arrange
        using var client = _factory
            .WithCommandExecutor(cmd => Task.FromResult($"Help: Available commands - {cmd}"))
            .CreateClient();
        
        var mainPage = await client.NavigateToMainPageAsync();

        // Act
        var result = await mainPage.ExecuteCommandAsync("help");

        // Assert
        result.Should().Contain("Help: Available commands");
        var content = mainPage.GetContent();
        content.Should().Contain("help");
        content.Should().Contain("> ");
    }

    [Fact]
    public async Task MainPage_TypeAndExecuteCommand_ShouldWorkCorrectly()
    {
        // Arrange
        using var client = _factory
            .WithCommandExecutor(cmd => Task.FromResult($"Executed: {cmd}"))
            .CreateClient();
        
        var mainPage = await client.NavigateToMainPageAsync();

        // Act - Simulate user typing
        mainPage.TypeCommand("status");
        await mainPage.ClickButtonAsync("Execute");

        // Assert
        var content = mainPage.GetContent();
        content.Should().Contain("status");
        content.Should().Contain("Executed: status");
        mainPage.CommandEntryText.Should().BeEmpty("entry should be cleared after execution");
    }

    [Fact]
    public async Task MainPage_QuickCommandButton_ShouldExecuteImmediately()
    {
        // Arrange
        using var client = _factory
            .WithCommandExecutor(cmd => Task.FromResult($"Quick: {cmd}"))
            .CreateClient();
        
        var mainPage = await client.NavigateToMainPageAsync();

        // Act - Click quick command button
        await mainPage.ClickButtonAsync("help");

        // Assert
        var content = mainPage.GetContent();
        content.Should().Contain("help");
        content.Should().Contain("Quick: help");
    }

    [Fact]
    public async Task MainPage_ClearButton_ShouldClearOutput()
    {
        // Arrange
        using var client = _factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();
        await mainPage.ExecuteCommandAsync("test command");
        var contentBefore = mainPage.GetContent();
        contentBefore.Should().Contain("test command");

        // Act
        await mainPage.ClickButtonAsync("clear");

        // Assert
        var contentAfter = mainPage.GetContent();
        contentAfter.Should().NotContain("test command");
        contentAfter.Should().Contain("MonadicPipeline CLI");
        contentAfter.Should().Contain("> ");
    }

    [Fact]
    public async Task MainPage_MultipleCommands_ShouldMaintainHistory()
    {
        // Arrange
        using var client = _factory
            .WithCommandExecutor(cmd => Task.FromResult($"Response: {cmd}"))
            .CreateClient();
        
        var mainPage = await client.NavigateToMainPageAsync();

        // Act - Execute multiple commands
        await mainPage.ExecuteCommandAsync("command1");
        await mainPage.ExecuteCommandAsync("command2");
        await mainPage.ExecuteCommandAsync("command3");

        // Assert
        var content = mainPage.GetContent();
        content.Should().Contain("command1");
        content.Should().Contain("command2");
        content.Should().Contain("command3");
        content.Should().Contain("Response: command1");
        content.Should().Contain("Response: command2");
        content.Should().Contain("Response: command3");
    }
}

/// <summary>
/// UI tests for error handling scenarios
/// </summary>
public class MainPageErrorHandlingTests
{
    [Fact]
    public async Task MainPage_InitializationFailure_ShouldDisplayErrorInUI()
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithInitializationFailure("Database connection failed");

        using var client = factory.CreateClient();

        // Act
        var mainPage = await client.NavigateToMainPageAsync();
        await client.WaitForPageLoadAsync();

        // Assert
        var content = mainPage.GetContent();
        content.Should().Contain("⚠ Initialization error: Database connection failed");
        content.Should().Contain("Some features may be unavailable");
        content.Should().Contain("> ", "terminal should still be usable");
        mainPage.IsLoaded.Should().BeTrue("page should load despite errors");
        mainPage.CliExecutor.Should().NotBeNull("fallback should provide executor");
    }

    [Fact]
    public async Task MainPage_SuggestionEngineFailure_ShouldShowWarningButContinue()
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithMockCliExecutor(_ => new object())
            .WithMockSuggestionEngine(_ => throw new Exception("Suggestion engine unavailable"));

        using var client = factory.CreateClient();

        // Act
        var mainPage = await client.NavigateToMainPageAsync();

        // Assert
        var content = mainPage.GetContent();
        content.Should().Contain("⚠ Suggestions unavailable");
        content.Should().Contain("> ");
        mainPage.IsLoaded.Should().BeTrue();
        mainPage.CliExecutor.Should().NotBeNull();
        mainPage.SuggestionEngine.Should().BeNull();
    }

    [Fact]
    public async Task MainPage_CommandExecutionError_ShouldDisplayErrorMessage()
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithCommandExecutor(_ => throw new InvalidOperationException("Command failed"));

        using var client = factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();

        // Act
        var result = await mainPage.ExecuteCommandAsync("test");

        // Assert
        result.Should().Contain("Error executing command: Command failed");
        var content = mainPage.GetContent();
        content.Should().Contain("Error executing command");
    }

    [Fact]
    public async Task MainPage_NoCliExecutor_ShouldShowDegradedStateMessage()
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithMockCliExecutor(_ => throw new Exception("Complete failure"));

        using var client = factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();

        // Act
        var result = await mainPage.ExecuteCommandAsync("help");

        // Assert
        result.Should().Contain("Error: CLI executor not initialized");
        result.Should().Contain("degraded state");
    }
}

/// <summary>
/// Tests for the purple screen bug fix
/// </summary>
public class PurpleScreenBugFixTests
{
    [Fact]
    public async Task PurpleScreenScenario_DatabaseInitializationFails_UIStillRenders()
    {
        // Arrange - Simulate the exact scenario that caused purple screen
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithMockCliExecutor(dbPath =>
            {
                if (dbPath != null)
                {
                    throw new InvalidOperationException("SQLite Error: unable to open database file");
                }
                return new object(); // Fallback succeeds
            });

        using var client = factory.CreateClient();

        // Act - This should NOT cause a purple screen
        var mainPage = await client.NavigateToMainPageAsync();
        await client.WaitForPageLoadAsync();

        // Assert - UI must render with error message (not purple screen)
        mainPage.IsLoaded.Should().BeTrue("CRITICAL: Page must load to prevent purple screen");
        
        var content = mainPage.GetContent();
        content.Should().NotBeNullOrEmpty("UI content must be visible");
        content.Should().Contain("MonadicPipeline CLI v1.0", "app header must display");
        content.Should().Contain("⚠ Initialization error", "error must be shown to user");
        content.Should().Contain("SQLite Error", "specific error details must be visible");
        content.Should().Contain("> ", "terminal prompt must appear");
        
        mainPage.CliExecutor.Should().NotBeNull("fallback executor should be created");
    }

    [Fact]
    public async Task PurpleScreenScenario_CompleteFailure_UIStillRendersWithError()
    {
        // Arrange - Even worse scenario: everything fails
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithMockCliExecutor(_ => throw new Exception("Complete system failure"));

        using var client = factory.CreateClient();

        // Act
        var mainPage = await client.NavigateToMainPageAsync();
        await client.WaitForPageLoadAsync();

        // Assert - UI MUST render even with complete failure
        mainPage.IsLoaded.Should().BeTrue("Page must load even with complete failure");
        
        var content = mainPage.GetContent();
        content.Should().Contain("MonadicPipeline CLI v1.0");
        content.Should().Contain("⚠ Initialization error: Complete system failure");
        content.Should().Contain("> ");
        
        // Most important: No purple screen, user can see what went wrong
        content.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("SQLite Error: unable to open database file")]
    [InlineData("Permission denied: /data/app/")]
    [InlineData("Network unavailable")]
    [InlineData("Service initialization timeout")]
    public async Task PurpleScreenScenario_VariousErrors_UIRendersWithSpecificError(string errorMessage)
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithInitializationFailure(errorMessage);

        using var client = factory.CreateClient();

        // Act
        var mainPage = await client.NavigateToMainPageAsync();
        await client.WaitForPageLoadAsync();

        // Assert
        mainPage.IsLoaded.Should().BeTrue();
        var content = mainPage.GetContent();
        content.Should().Contain(errorMessage, "specific error must be shown");
        content.Should().Contain("⚠ Initialization error");
        content.Should().Contain("> ");
    }
}

/// <summary>
/// Integration tests simulating real user workflows
/// </summary>
public class MainPageUserWorkflowTests
{
    [Fact]
    public async Task UserWorkflow_FirstTimeUser_CompleteOnboardingFlow()
    {
        // Arrange - Simulate a new user starting the app
        var commandHistory = new List<string>();
        
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithCommandExecutor(cmd =>
            {
                commandHistory.Add(cmd);
                return Task.FromResult(cmd switch
                {
                    "help" => "Available commands: help, status, models, ask, config",
                    "status" => "Status: ✓ Ready | Models: 0 | Endpoint: Not configured",
                    "config http://localhost:11434" => "✓ Endpoint configured: http://localhost:11434",
                    "models" => "Available models: tinyllama, phi, gemma",
                    _ => $"Unknown command: {cmd}"
                });
            });

        using var client = factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();

        // Act - Simulate first-time user workflow
        await mainPage.ExecuteCommandAsync("help");
        await mainPage.ExecuteCommandAsync("status");
        await mainPage.ExecuteCommandAsync("config http://localhost:11434");
        await mainPage.ExecuteCommandAsync("models");

        // Assert
        commandHistory.Should().ContainInOrder("help", "status", "config http://localhost:11434", "models");
        
        var content = mainPage.GetContent();
        content.Should().Contain("Available commands");
        content.Should().Contain("Status: ✓ Ready");
        content.Should().Contain("✓ Endpoint configured");
        content.Should().Contain("Available models");
    }

    [Fact]
    public async Task UserWorkflow_QuickCommands_ShouldProvideEfficientAccess()
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithCommandExecutor(cmd => Task.FromResult($"Executed: {cmd}"));

        using var client = factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();

        // Act - User clicks quick command buttons
        await mainPage.ClickButtonAsync("help");
        await mainPage.ClickButtonAsync("status");
        await mainPage.ClickButtonAsync("models");

        // Assert
        var content = mainPage.GetContent();
        content.Should().Contain("Executed: help");
        content.Should().Contain("Executed: status");
        content.Should().Contain("Executed: models");
    }

    [Fact]
    public async Task UserWorkflow_ErrorRecovery_UserCanContinueAfterError()
    {
        // Arrange
        var attemptCount = 0;
        
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithCommandExecutor(cmd =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new Exception("Network timeout");
                }
                return Task.FromResult($"Success: {cmd}");
            });

        using var client = factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();

        // Act - First attempt fails, second succeeds
        var result1 = await mainPage.ExecuteCommandAsync("status");
        var result2 = await mainPage.ExecuteCommandAsync("status");

        // Assert
        result1.Should().Contain("Error executing command: Network timeout");
        result2.Should().Contain("Success: status");
        
        var content = mainPage.GetContent();
        content.Should().Contain("Error executing command");
        content.Should().Contain("Success: status");
    }
}

/// <summary>
/// Performance and stress tests
/// </summary>
public class MainPagePerformanceTests
{
    [Fact]
    public async Task Performance_ManySequentialCommands_ShouldHandleEfficiently()
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>()
            .WithCommandExecutor(cmd => Task.FromResult($"Response: {cmd}"));

        using var client = factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();

        // Act - Execute many commands
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 50; i++)
        {
            await mainPage.ExecuteCommandAsync($"command{i}");
        }
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "should handle 50 commands in under 5 seconds");
        var content = mainPage.GetContent();
        content.Should().Contain("command0");
        content.Should().Contain("command49");
    }

    [Fact]
    public async Task Performance_PageLoad_ShouldBeQuick()
    {
        // Arrange
        using var factory = new MauiApplicationFactory<TestStartup>();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var client = factory.CreateClient();
        var mainPage = await client.NavigateToMainPageAsync();
        await client.WaitForPageLoadAsync();
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "page should load in under 1 second");
        mainPage.IsLoaded.Should().BeTrue();
    }
}
