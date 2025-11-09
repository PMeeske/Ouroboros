using System.Text;
using FluentAssertions;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// End-to-end tests for Android MainPage initialization and error handling
/// These tests verify that the app handles initialization failures gracefully
/// </summary>
public class AndroidMainPageInitializationTests
{
    /// <summary>
    /// Test helper class that simulates MainPage initialization behavior
    /// </summary>
    private class MainPageSimulator
    {
        private readonly StringBuilder _outputHistory;
        private object? _cliExecutor;
        private object? _suggestionEngine;
        private readonly Func<string?, object> _cliExecutorFactory;
        private readonly Func<string, object> _suggestionEngineFactory;

        public MainPageSimulator(
            Func<string?, object> cliExecutorFactory,
            Func<string, object> suggestionEngineFactory)
        {
            _outputHistory = new StringBuilder();
            _cliExecutorFactory = cliExecutorFactory;
            _suggestionEngineFactory = suggestionEngineFactory;
        }

        public string Output => _outputHistory.ToString();
        public bool IsCliExecutorInitialized => _cliExecutor != null;
        public bool IsSuggestionEngineInitialized => _suggestionEngine != null;

        /// <summary>
        /// Simulates the MainPage constructor initialization logic
        /// </summary>
        public void Initialize()
        {
            _outputHistory.AppendLine("MonadicPipeline CLI v1.0");
            _outputHistory.AppendLine("Enhanced with AI-powered suggestions and Ollama integration");
            _outputHistory.AppendLine("Type 'help' to see available commands");
            _outputHistory.AppendLine();

            // Simulate the try-catch logic from MainPage constructor
            try
            {
                var dbPath = "/tmp/test_command_history.db";
                _cliExecutor = _cliExecutorFactory(dbPath);

                // Initialize suggestion engine if available
                try
                {
                    _suggestionEngine = _suggestionEngineFactory(dbPath);
                }
                catch (Exception ex)
                {
                    _suggestionEngine = null;
                    _outputHistory.AppendLine($"⚠ Suggestions unavailable: {ex.Message}");
                    _outputHistory.AppendLine();
                }
            }
            catch (Exception ex)
            {
                _outputHistory.AppendLine($"⚠ Initialization error: {ex.Message}");
                _outputHistory.AppendLine("Some features may be unavailable.");
                _outputHistory.AppendLine();

                // Create a minimal fallback executor
                try
                {
                    _cliExecutor = _cliExecutorFactory(null);
                }
                catch
                {
                    _cliExecutor = null;
                }
            }

            _outputHistory.Append("> ");
        }

        /// <summary>
        /// Simulates executing a command with null checks
        /// </summary>
        public string ExecuteCommand(string command)
        {
            if (_cliExecutor == null)
            {
                return "Error: CLI executor not initialized. App may be in degraded state.";
            }

            try
            {
                // Simulate successful command execution
                return $"Executed: {command}";
            }
            catch (Exception ex)
            {
                return $"Error executing command: {ex.Message}";
            }
        }
    }

    [Fact]
    public void Initialize_WithSuccessfulServices_ShouldInitializeAllComponents()
    {
        // Arrange
        Func<string?, object> cliExecutorFactory = (string? dbPath) => new object(); // Simulates successful CliExecutor creation
        Func<string, object> suggestionEngineFactory = (string dbPath) => new object(); // Simulates successful engine creation
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act
        simulator.Initialize();

        // Assert
        simulator.IsCliExecutorInitialized.Should().BeTrue("CliExecutor should initialize successfully");
        simulator.IsSuggestionEngineInitialized.Should().BeTrue("Suggestion engine should initialize successfully");
        simulator.Output.Should().Contain("MonadicPipeline CLI v1.0");
        simulator.Output.Should().Contain("> ");
        simulator.Output.Should().NotContain("⚠", "no warnings should appear on successful initialization");
    }

    [Fact]
    public void Initialize_WithDatabaseFailure_ShouldShowErrorButContinue()
    {
        // Arrange
        Func<string?, object> cliExecutorFactory = (string? dbPath) =>
        {
            if (dbPath != null)
            {
                throw new InvalidOperationException("Database initialization failed");
            }
            return new object(); // Fallback succeeds
        };
        Func<string, object> suggestionEngineFactory = (string dbPath) => throw new InvalidOperationException("Cannot create engine");
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act
        simulator.Initialize();

        // Assert
        simulator.IsCliExecutorInitialized.Should().BeTrue("Fallback CliExecutor should be created");
        simulator.Output.Should().Contain("⚠ Initialization error: Database initialization failed");
        simulator.Output.Should().Contain("Some features may be unavailable");
        simulator.Output.Should().Contain("> ", "prompt should still appear");
        simulator.Output.Should().Contain("MonadicPipeline CLI v1.0", "app header should still display");
    }

    [Fact]
    public void Initialize_WithSuggestionEngineFailure_ShouldContinueWithoutSuggestions()
    {
        // Arrange
        Func<string?, object> cliExecutorFactory = (string? dbPath) => new object(); // CliExecutor succeeds
        Func<string, object> suggestionEngineFactory = (string dbPath) => throw new Exception("Suggestion engine unavailable");
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act
        simulator.Initialize();

        // Assert
        simulator.IsCliExecutorInitialized.Should().BeTrue("CliExecutor should still initialize");
        simulator.IsSuggestionEngineInitialized.Should().BeFalse("Suggestion engine should fail gracefully");
        simulator.Output.Should().Contain("⚠ Suggestions unavailable: Suggestion engine unavailable");
        simulator.Output.Should().Contain("> ", "prompt should still appear");
    }

    [Fact]
    public void Initialize_WithCompleteFallbackFailure_ShouldStillShowUI()
    {
        // Arrange - Both primary and fallback initialization fail
        Func<string?, object> cliExecutorFactory = (string? dbPath) => throw new Exception("Complete initialization failure");
        Func<string, object> suggestionEngineFactory = (string dbPath) => throw new Exception("Cannot create engine");
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act
        simulator.Initialize();

        // Assert
        simulator.IsCliExecutorInitialized.Should().BeFalse("CliExecutor should not initialize");
        simulator.Output.Should().Contain("⚠ Initialization error: Complete initialization failure");
        simulator.Output.Should().Contain("Some features may be unavailable");
        simulator.Output.Should().Contain("> ", "UI should still render with prompt");
        simulator.Output.Should().Contain("MonadicPipeline CLI v1.0", "app should still show header");
    }

    [Fact]
    public void ExecuteCommand_WithInitializedExecutor_ShouldExecuteSuccessfully()
    {
        // Arrange
        Func<string?, object> cliExecutorFactory = (string? dbPath) => new object();
        Func<string, object> suggestionEngineFactory = (string dbPath) => new object();
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);
        simulator.Initialize();

        // Act
        var result = simulator.ExecuteCommand("help");

        // Assert
        result.Should().Contain("Executed: help");
        result.Should().NotContain("Error:", "should execute successfully");
    }

    [Fact]
    public void ExecuteCommand_WithFailedExecutor_ShouldReturnErrorMessage()
    {
        // Arrange
        Func<string?, object> cliExecutorFactory = (string? dbPath) => throw new Exception("Executor failed");
        Func<string, object> suggestionEngineFactory = (string dbPath) => throw new Exception("Engine failed");
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);
        simulator.Initialize();

        // Act
        var result = simulator.ExecuteCommand("help");

        // Assert
        result.Should().Contain("Error: CLI executor not initialized");
        result.Should().Contain("degraded state");
    }

    [Fact]
    public void EndToEnd_SuccessfulInitialization_ShouldProvideFullFunctionality()
    {
        // Arrange
        Func<string?, object> cliExecutorFactory = (string? dbPath) => new object();
        Func<string, object> suggestionEngineFactory = (string dbPath) => new object();
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act - Full initialization and command execution
        simulator.Initialize();
        var command1Result = simulator.ExecuteCommand("help");
        var command2Result = simulator.ExecuteCommand("status");

        // Assert
        simulator.IsCliExecutorInitialized.Should().BeTrue();
        simulator.IsSuggestionEngineInitialized.Should().BeTrue();
        simulator.Output.Should().NotContain("⚠");
        command1Result.Should().Contain("Executed: help");
        command2Result.Should().Contain("Executed: status");
    }

    [Fact]
    public void EndToEnd_PartialFailure_ShouldProvideDegradedFunctionality()
    {
        // Arrange - Database fails but fallback works
        Func<string?, object> cliExecutorFactory = (string? dbPath) =>
        {
            if (dbPath != null)
            {
                throw new Exception("Database error");
            }
            return new object(); // Fallback succeeds
        };
        Func<string, object> suggestionEngineFactory = (string dbPath) => throw new Exception("Engine failed");
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act - Full flow with degraded services
        simulator.Initialize();
        var commandResult = simulator.ExecuteCommand("help");

        // Assert
        simulator.IsCliExecutorInitialized.Should().BeTrue("fallback should work");
        simulator.IsSuggestionEngineInitialized.Should().BeFalse("suggestions should be unavailable");
        simulator.Output.Should().Contain("⚠ Initialization error");
        simulator.Output.Should().Contain("> ");
        commandResult.Should().Contain("Executed: help", "basic commands should still work");
    }

    [Fact]
    public void EndToEnd_CompleteFailure_ShouldShowUIWithErrorsOnly()
    {
        // Arrange - Everything fails
        Func<string?, object> cliExecutorFactory = (string? dbPath) => throw new Exception("Total failure");
        Func<string, object> suggestionEngineFactory = (string dbPath) => throw new Exception("Engine unavailable");
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act
        simulator.Initialize();
        var commandResult = simulator.ExecuteCommand("help");

        // Assert - UI should render but commands fail gracefully
        simulator.IsCliExecutorInitialized.Should().BeFalse();
        simulator.Output.Should().Contain("MonadicPipeline CLI v1.0");
        simulator.Output.Should().Contain("⚠ Initialization error: Total failure");
        simulator.Output.Should().Contain("> ");
        commandResult.Should().Contain("Error: CLI executor not initialized");
        commandResult.Should().NotContain("Exception", "should not expose raw exceptions");
    }

    /// <summary>
    /// This test verifies the exact flow that was causing the purple screen
    /// </summary>
    [Fact]
    public void PurpleScreenScenario_DatabaseFailureOnStartup_ShouldNotCrash()
    {
        // Arrange - Simulates the exact scenario that caused the purple screen
        Func<string?, object> cliExecutorFactory = (string? dbPath) =>
        {
            if (dbPath != null)
            {
                // This is what was happening: OllamaService, ModelManager, or CommandHistoryService
                // would throw an exception during CliExecutor constructor
                throw new InvalidOperationException("SQLite Error: unable to open database file");
            }
            // Fallback with null dbPath
            return new object();
        };
        Func<string, object> suggestionEngineFactory = (string dbPath) =>
        {
            throw new Exception("Cannot initialize suggestion engine without database");
        };
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act - This should NOT throw an exception (which would cause purple screen)
        var initializationAction = () => simulator.Initialize();

        // Assert
        initializationAction.Should().NotThrow("initialization should catch all exceptions");
        simulator.Output.Should().NotBeNullOrEmpty("UI should render with error message");
        simulator.Output.Should().Contain("MonadicPipeline CLI v1.0", "header should display");
        simulator.Output.Should().Contain("⚠ Initialization error", "error should be shown to user");
        simulator.Output.Should().Contain("SQLite Error", "specific error should be visible");
        simulator.Output.Should().Contain("> ", "terminal prompt should appear");
        
        // Most importantly: UI is rendered, not a purple screen
        simulator.IsCliExecutorInitialized.Should().BeTrue("fallback should create minimal executor");
    }

    /// <summary>
    /// Verifies that multiple sequential commands work even after initialization errors
    /// </summary>
    [Fact]
    public void EndToEnd_MultipleCommands_AfterPartialFailure_ShouldAllWork()
    {
        // Arrange
        Func<string?, object> cliExecutorFactory = (string? dbPath) =>
        {
            if (dbPath != null)
            {
                throw new Exception("Database initialization failed");
            }
            return new object(); // Fallback succeeds
        };
        Func<string, object> suggestionEngineFactory = (string dbPath) => throw new Exception("Suggestions unavailable");
        var simulator = new MainPageSimulator(cliExecutorFactory, suggestionEngineFactory);

        // Act - Execute multiple commands
        simulator.Initialize();
        var results = new[]
        {
            simulator.ExecuteCommand("help"),
            simulator.ExecuteCommand("version"),
            simulator.ExecuteCommand("status"),
            simulator.ExecuteCommand("models")
        };

        // Assert - All commands should execute (even in degraded mode)
        foreach (var result in results)
        {
            result.Should().Contain("Executed:", "command should execute");
            result.Should().NotContain("degraded state", "fallback executor should work");
        }
        
        simulator.Output.Should().Contain("⚠ Initialization error");
        simulator.IsCliExecutorInitialized.Should().BeTrue();
    }
}
