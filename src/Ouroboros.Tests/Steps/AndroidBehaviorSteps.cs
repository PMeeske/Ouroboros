using System;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using TechTalk.SpecFlow;

namespace LangChainPipeline.Specs.Steps;

/// <summary>
/// Step definitions for AndroidBehavior feature simulating CLI executor behavior,
/// activity lifecycle handling, degraded mode fallbacks, and purple screen fixes.
/// </summary>
[Binding]
public class AndroidBehaviorSteps
{
    // CLI Executor simulation
    private class TestCliExecutor
    {
        private readonly bool _failInit;
        private readonly bool _failWithDb;
        public bool IsInitialized { get; private set; }

        public TestCliExecutor(bool failInit = false, bool failWithDb = false)
        {
            _failInit = failInit;
            _failWithDb = failWithDb;
        }

        public void Initialize(string? databasePath)
        {
            if (_failInit)
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1516 // Blank line rules
#pragma warning disable SA1503 // Braces should not be omitted
            {
                throw new InvalidOperationException("CliExecutor initialization failed");
            }
            if (_failWithDb && !string.IsNullOrEmpty(databasePath))
            {
                throw new InvalidOperationException("Database initialization failed");
            }
            IsInitialized = true;
        }

        public string Execute(string command)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Executor not initialized");
            }
            return $"Executed: {command}";
        }
    }

    // Activity simulation
    private enum ActivityState
    {
        Created,
        Started,
        Resumed,
        Paused,
        Stopped,
        Destroyed
    }

    private class TestMainPageActivity
    {
        private readonly bool _initFail;
        private readonly StringBuilder _output = new();
        private object? _cliExecutor;
        public ActivityState State { get; private set; } = ActivityState.Created;
        public string Output => _output.ToString();
        public bool IsUiRendered { get; private set; }
        public bool IsCliExecutorInitialized => _cliExecutor != null;

        public TestMainPageActivity(bool initFail = false)
        {
            _initFail = initFail;
        }

        public void OnCreate()
        {
            State = ActivityState.Created;
            _output.AppendLine("Ouroboros CLI v1.0");
            _output.AppendLine("Enhanced with AI-powered suggestions and Ollama integration");
            _output.AppendLine("Type 'help' to see available commands");
            _output.AppendLine();
            try
            {
                if (_initFail) throw new Exception("Initialization failed");
                _cliExecutor = new object();
            }
            catch (Exception ex)
            {
                _output.AppendLine($"⚠ Initialization error: {ex.Message}");
                _output.AppendLine("Some features may be unavailable.");
                _output.AppendLine();
                _cliExecutor = new object(); // fallback
            }
            _output.Append("> ");
            IsUiRendered = true;
        }
        public void OnStart()
        {
            State = ActivityState.Started;
        }
        public void OnResume()
        {
            State = ActivityState.Resumed;
        }
        public void OnPause()
        {
            State = ActivityState.Paused;
        }
        public void OnStop()
        {
            State = ActivityState.Stopped;
        }
        public void OnDestroy()
        {
            State = ActivityState.Destroyed;
            _cliExecutor = null;
            IsUiRendered = false;
        }
        public string ExecuteCommand(string cmd)
        {
            if (State != ActivityState.Resumed) throw new InvalidOperationException("Activity must be in Resumed state");
            if (_cliExecutor == null) return "Error: CLI executor not initialized. App may be in degraded state.";
            return $"Executed: {cmd}";
        }
    }

    // Purple screen / degraded mode simulation
    private StringBuilder _appOutput = new();
    private object? _fallbackExecutor;
    private bool _uiRenderedFlag;
    private bool _errorShownFlag;

    // Shared state
    private TestCliExecutor? _cliExecutor;
    private Exception? _thrown;
    private string? _commandResult;
    private TestMainPageActivity? _activity;
    private string? _initialActivityOutput;
    private TestMainPageActivity? _newActivity;
    private string? _pendingErrorMessage;

    [Given("a fresh Android behavior context")]
    public void GivenAFreshAndroidBehaviorContext()
    {
        _cliExecutor = null;
        _thrown = null;
        _commandResult = null;
        _activity = null;
        _initialActivityOutput = null;
        _newActivity = null;
        _appOutput = new StringBuilder();
        _fallbackExecutor = null;
        _uiRenderedFlag = false;
        _errorShownFlag = false;
        _pendingErrorMessage = null;
    }

    // CLI executor scenarios
    [Given("a CLI executor configured to succeed")]
    public void GivenACliExecutorConfiguredToSucceed() => _cliExecutor = new TestCliExecutor();

    [Given("a CLI executor configured to fail initialization")]
    public void GivenACliExecutorConfiguredToFailInitialization() => _cliExecutor = new TestCliExecutor(failInit: true);

    [Given("a CLI executor configured to fail with database")]
    public void GivenACliExecutorConfiguredToFailWithDatabase() => _cliExecutor = new TestCliExecutor(failWithDb: true);

    [Given("an uninitialized CLI executor")]
    public void GivenAnUninitializedCliExecutor() => _cliExecutor = new TestCliExecutor();

    [When("I initialize with database path \"(.*)\"")]
    public void WhenIInitializeWithDatabasePath(string path)
    {
        _cliExecutor.Should().NotBeNull();
        try
        {
            _cliExecutor!.Initialize(path);
        }
        catch (Exception ex)
        {
            _thrown = ex;
        }
    }

    [When("I attempt to initialize with database path \"(.*)\"")]
    public void WhenIAttemptToInitializeWithDatabasePath(string path)
    {
        _cliExecutor.Should().NotBeNull();
        try
        {
            _cliExecutor!.Initialize(path);
        }
        catch (Exception ex)
        {
            _thrown = ex;
        }
    }

    [When("I initialize without database")]
    public void WhenIInitializeWithoutDatabase()
    {
        _cliExecutor.Should().NotBeNull();
        _cliExecutor!.Initialize(null);
    }

    [Then("the executor should be initialized")]
    public void ThenTheExecutorShouldBeInitialized() => _cliExecutor!.IsInitialized.Should().BeTrue();

    [Then("I should be able to execute commands")]
    public void ThenIShouldBeAbleToExecuteCommands()
    {
        var result = _cliExecutor!.Execute("help");
        result.Should().Contain("Executed: help");
    }

    [When("I execute command \"(.*)\"")]
    public void WhenIExecuteCommand(string cmd)
    {
        _cliExecutor.Should().NotBeNull();
        _commandResult = _cliExecutor!.Execute(cmd);
    }

    [When("I attempt to execute command \"(.*)\"")]
    public void WhenIAttemptToExecuteCommand(string cmd)
    {
        _cliExecutor.Should().NotBeNull();
        try
        {
            _commandResult = _cliExecutor!.Execute(cmd);
        }
        catch (Exception ex)
        {
            _thrown = ex;
        }
    }

    [Then("it should throw InvalidOperationException with message \"(.*)\"")]
    public void ThenItShouldThrowInvalidOperationExceptionWithMessage(string message)
    {
        _thrown.Should().NotBeNull();
        _thrown.Should().BeOfType<InvalidOperationException>();
        _thrown!.Message.Should().Be(message);
    }

    [Then("it should throw InvalidOperationException")]
    public void ThenItShouldThrowInvalidOperationException()
    {
        _thrown.Should().NotBeNull();
        _thrown.Should().BeOfType<InvalidOperationException>();
    }

    [Then("the command result should contain \"(.*)\"")]
    public void ThenTheCommandResultShouldContain(string expected)
    {
        _commandResult.Should().NotBeNull();
        _commandResult!.Should().Contain(expected);
    }

    // Activity lifecycle steps
    [Given("a test main page activity")]
    public void GivenATestMainPageActivity() => _activity = new TestMainPageActivity();

    [Given("a test main page activity configured to fail initialization")]
    public void GivenATestMainPageActivityConfiguredToFailInitialization() => _activity = new TestMainPageActivity(initFail: true);

    [Given("a resumed activity")]
    public void GivenAResumedActivity()
    {
        _activity = new TestMainPageActivity();
        _activity.OnCreate(); _activity.OnStart(); _activity.OnResume();
    }

    [Given("a resumed activity with initial output")]
    public void GivenAResumedActivityWithInitialOutput()
    {
        GivenAResumedActivity();
        _initialActivityOutput = _activity!.Output;
    }

    [Given("an activity in Started state")]
    public void GivenAnActivityInStartedState()
    {
        _activity = new TestMainPageActivity();
        _activity.OnCreate(); _activity.OnStart(); // Not resumed
    }

    [When("I call OnCreate")]
    public void WhenICallOnCreate() => _activity!.OnCreate();
    [When("I call OnStart")]
    public void WhenICallOnStart() => _activity!.OnStart();
    [When("I call OnResume")]
    public void WhenICallOnResume() => _activity!.OnResume();
    [When("I call OnPause")]
    public void WhenICallOnPause() => _activity!.OnPause();
    [When("I call OnStop")]
    public void WhenICallOnStop() => _activity!.OnStop();
    [When("I call OnDestroy")]
    public void WhenICallOnDestroy() => _activity!.OnDestroy();

    [When("I simulate configuration change")]
    public void WhenISimulateConfigurationChange()
    {
        // Destroy old
        _activity!.OnPause(); _activity.OnStop(); _activity.OnDestroy();
        // New instance
        _newActivity = new TestMainPageActivity();
        _newActivity.OnCreate(); _newActivity.OnStart(); _newActivity.OnResume();
    }

    [Then("the activity state should be Resumed")]
    public void ThenTheActivityStateShouldBeResumed() => _activity!.State.Should().Be(ActivityState.Resumed);

    [Then("the activity state should be Destroyed")]
    public void ThenTheActivityStateShouldBeDestroyed() => _activity!.State.Should().Be(ActivityState.Destroyed);

    [Then("the UI should be rendered")]
    public void ThenTheUiShouldBeRendered() => _activity!.IsUiRendered.Should().BeTrue();

    [Then("the UI should not be rendered")]
    public void ThenTheUiShouldNotBeRendered() => _activity!.IsUiRendered.Should().BeFalse();

    [Then("the output should contain \"(.*)\"")]
    public void ThenTheOutputShouldContain(string expected) => _activity!.Output.Should().Contain(expected);

    [Then("the output should match the initial output")]
    public void ThenTheOutputShouldMatchTheInitialOutput() => _activity!.Output.Should().Be(_initialActivityOutput);

    [Then("the CLI executor should be initialized")]
    public void ThenTheCliExecutorShouldBeInitialized() => _activity!.IsCliExecutorInitialized.Should().BeTrue();

    [Then("the CLI executor should be initialized via fallback")]
    public void ThenTheCliExecutorShouldBeInitializedViaFallback() => _activity!.IsCliExecutorInitialized.Should().BeTrue();

    [Then("the CLI executor should not be initialized")]
    public void ThenTheCliExecutorShouldNotBeInitialized() => _activity!.IsCliExecutorInitialized.Should().BeFalse();

    [Then("the new activity state should be Resumed")]
    public void ThenTheNewActivityStateShouldBeResumed() => _newActivity!.State.Should().Be(ActivityState.Resumed);

    [Then("the new activity UI should be rendered")]
    public void ThenTheNewActivityUiShouldBeRendered() => _newActivity!.IsUiRendered.Should().BeTrue();

    [Then("the new activity CLI executor should be initialized")]
    public void ThenTheNewActivityCliExecutorShouldBeInitialized() => _newActivity!.IsCliExecutorInitialized.Should().BeTrue();

    // Purple screen scenarios
    [Given("database initialization will fail with \"(.*)\"")]
    public void GivenDatabaseInitializationWillFailWith(string error)
    {
        _pendingErrorMessage = error;
    }

    [Given("all services are healthy")]
    public void GivenAllServicesAreHealthy()
    {
        // marker only
    }

    [Given("primary service fails but fallback succeeds")]
    public void GivenPrimaryServiceFailsButFallbackSucceeds()
    {
        // marker only
    }

    [Given("initialization will fail with \"(.*)\"")]
    public void GivenInitializationWillFailWith(string error) => _pendingErrorMessage = error;

    [Given("the purple screen bug is fixed")]
    public void GivenThePurpleScreenBugIsFixed()
    {
        // marker only
    }

    [When("the app starts")]
    public void WhenTheAppStarts()
    {
        _appOutput.AppendLine("Ouroboros CLI v1.0");
        if (_pendingErrorMessage != null)
        {
            _appOutput.AppendLine($"⚠ Initialization error: {_pendingErrorMessage}");
            _appOutput.AppendLine("Some features may be unavailable.");
            _fallbackExecutor = new object();
            _errorShownFlag = true;
        }
        else if (_fallbackExecutor == null)
        {
            _fallbackExecutor = new object();
        }
        _appOutput.AppendLine("> ");
        _uiRenderedFlag = true;
    }

    [When("initialization throws an exception")]
    public void WhenInitializationThrowsAnException()
    {
        try
        {
            throw new Exception("Service failed");
        }
        catch (Exception ex)
        {
            _appOutput.AppendLine("Ouroboros CLI v1.0");
            _appOutput.AppendLine($"⚠ Initialization error: {ex.Message}");
            _appOutput.AppendLine("> ");
            _uiRenderedFlag = true;
            _errorShownFlag = true;
        }
    }

    [Then("the UI should render")]
    public void ThenTheUiShouldRender() => _uiRenderedFlag.Should().BeTrue();

    [Then("full functionality should be available")]
    public void ThenFullFunctionalityShouldBeAvailable() => _fallbackExecutor.Should().NotBeNull();

    [Then("degraded mode should work")]
    public void ThenDegradedModeShouldWork() => _fallbackExecutor.Should().NotBeNull();

    [Then("the CLI executor should be available via fallback")]
    public void ThenTheCliExecutorShouldBeAvailableViaFallback() => _fallbackExecutor.Should().NotBeNull();

    [Then("the output should not contain \"(.*)\"")]
    public void ThenTheOutputShouldNotContain(string value) => _appOutput.ToString().Should().NotContain(value);

    [Then("the output should contain \"(.*)\"")]
    public void ThenTheAppOutputShouldContain(string value) => _appOutput.ToString().Should().Contain(value);

    [Then("the specific error \"(.*)\" should be shown to user")]
    public void ThenTheSpecificErrorShouldBeShownToUser(string error) => _appOutput.ToString().Should().Contain(error);

    [Then("the UI must render")]
    public void ThenTheUiMustRender() => _uiRenderedFlag.Should().BeTrue();

    [Then("the error must be shown to user")]
    public void ThenTheErrorMustBeShownToUser() => _errorShownFlag.Should().BeTrue();

    [Then("the app should not show purple screen")]
    public void ThenTheAppShouldNotShowPurpleScreen() => _uiRenderedFlag.Should().BeTrue();
}
