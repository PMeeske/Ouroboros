using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Reqnroll.Bindings;
using Ouroboros.Core.Monads; // For Result<T> and Option<T>
using Ouroboros.Core.Tracing; // Assuming this namespace for tracing interfaces
using Ouroboros.Pipeline; // For Step<TInput, TOutput> composition

[Binding]
public class DistributedTracingStepDefinitions
{
    private readonly ScenarioContext _scenarioContext;
    private ITracingService _tracingService;
    private Option<Activity> _currentActivity;
    private Option<string> _capturedParentId;
    private int _startedCallbackCount;
    private int _stoppedCallbackCount;

    public DistributedTracingStepDefinitions(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
        _tracingService = new TracingService(); // Mock or real implementation, should be injected via DI if possible
    }

    /// <summary>
    /// Composable step to disable tracing initially.
    /// </summary>
    [Given(@"tracing is disabled initially")]
    public async Task GivenTracingIsDisabledInitially()
    {
        var result = await _tracingService.DisableTracing();
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to disable tracing: {error}")
        );
    }

    /// <summary>
    /// Composable step to enable tracing.
    /// </summary>
    [Given(@"tracing is enabled")]
    public async Task GivenTracingIsEnabled()
    {
        var result = await _tracingService.EnableTracing();
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to enable tracing: {error}")
        );
    }

    /// <summary>
    /// Composable step to enable tracing with callbacks.
    /// </summary>
    [Given(@"tracing is enabled with activity callbacks")]
    public async Task GivenTracingIsEnabledWithActivityCallbacks()
    {
        var result = await _tracingService.EnableTracingWithCallbacks(
            () => _startedCallbackCount++,
            () => _stoppedCallbackCount++
        );
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to enable tracing with callbacks: {error}")
        );
    }

    /// <summary>
    /// Stores tags in scenario context.
    /// </summary>
    [Given(@"I have tags with key1 ""(.*)"" and key2 (.*)")]
    public void GivenIHaveTagsWithKey1AndKey2(string value1, int value2)
    {
        _scenarioContext["tags"] = new Dictionary<string, object>
        {
            { "key1", value1 },
            { "key2", value2 }
        };
    }

    /// <summary>
    /// Starts an activity and stores it in context.
    /// </summary>
    [Given(@"I start an activity named ""(.*)""")]
    [When(@"I start an activity named ""(.*)""")]
    public async Task GivenIStartAnActivityNamed(string name)
    {
        var result = await _tracingService.StartActivity(name);
        _currentActivity = result.Match(
            onSuccess: activity => Option<Activity>.Some(activity),
            onFailure: error => Option<Activity>.None()
        );
    }

    /// <summary>
    /// Starts an activity with tags.
    /// </summary>
    [When(@"I start an activity named ""(.*)"" with tags")]
    public async Task WhenIStartAnActivityNamedWithTags(string name)
    {
        var tags = (Dictionary<string, object>)_scenarioContext["tags"];
        var result = await _tracingService.StartActivity(name, tags);
        result.Match(
            onSuccess: activity => _currentActivity = Option<Activity>.Some(activity),
            onFailure: error => throw new Exception($"Failed to start activity: {error}")
        );
    }

    /// <summary>
    /// Records an event on the current activity.
    /// </summary>
    [When(@"I record an event ""(.*)"" with detail ""(.*)""")]
    public async Task WhenIRecordAnEventWithDetail(string eventName, string detail)
    {
        if (_currentActivity == Option<Activity>.None())
            return;

        var result = await _tracingService.RecordEvent(_currentActivity.Match(
            func: activity => activity,
            defaultValue: null
        ), eventName, detail);
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to record event: {error}")
        );
    }

    /// <summary>
    /// Records an exception on the current activity.
    /// </summary>
    [When(@"I record an exception with message ""(.*)""")]
    public async Task WhenIRecordAnExceptionWithMessage(string message)
    {
        if (_currentActivity == Option<Activity>.None())
            return;

        var exception = new Exception(message);
        var result = await _tracingService.RecordException(_currentActivity.Match(
            func: activity => activity,
            defaultValue: null
        ), exception);
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to record exception: {error}")
        );
    }

    /// <summary>
    /// Sets status on the current activity.
    /// </summary>
    [When(@"I set activity status to ""(.*)"" with description ""(.*)""")]
    public async Task WhenISetActivityStatusToWithDescription(string status, string description)
    {
        if (_currentActivity == Option<Activity>.None())
            return;

        var result = await _tracingService.SetStatus(_currentActivity.Match(
            func: activity => activity,
            defaultValue: null
        ), status, description);
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to set status: {error}")
        );
    }

    /// <summary>
    /// Adds a tag to the current activity.
    /// </summary>
    [When(@"I add tag ""(.*)"" with value ""(.*)""")]
    public async Task WhenIAddTagWithValue(string key, string value)
    {
        if (_currentActivity == Option<Activity>.None())
            return;

        var result = await _tracingService.AddTag(_currentActivity.Match(
            func: activity => activity,
            defaultValue: null
        ), key, value);
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to add tag: {error}")
        );
    }

    /// <summary>
    /// Retrieves and stores the trace ID.
    /// </summary>
    [When(@"I get the trace ID")]
    public void WhenIGetTheTraceId()
    {
        var traceIdOption = _currentActivity.Match(
            func: activity => _tracingService.GetTraceId(activity),
            defaultValue: Option<string>.None()
        );
        _scenarioContext["traceId"] = traceIdOption.Match(
            func: id => id,
            defaultValue: null
        );
    }

    /// <summary>
    /// Retrieves and stores the span ID.
    /// </summary>
    [When(@"I get the span ID")]
    public void WhenIGetTheSpanId()
    {
        var spanIdOption = _currentActivity.Match(
            func: activity => _tracingService.GetSpanId(activity),
            defaultValue: Option<string>.None()
        );
        _scenarioContext["spanId"] = spanIdOption.Match(
            func: id => id,
            defaultValue: null
        );
    }

    /// <summary>
    /// Traces tool execution.
    /// </summary>
    [Given(@"I trace tool execution for ""(.*)"" with input ""(.*)""")]
    [When(@"I trace tool execution for ""(.*)"" with input ""(.*)""")]
    public async Task GivenITraceToolExecutionForWithInput(string toolName, string input)
    {
        var result = await _tracingService.TraceToolExecution(toolName, input);
        result.Match(
            onSuccess: activity => _currentActivity = Option<Activity>.Some(activity),
            onFailure: error => throw new Exception($"Failed to trace tool: {error}")
        );
    }

    /// <summary>
    /// Traces pipeline execution.
    /// </summary>
    [When(@"I trace pipeline execution for ""(.*)""")]
    public async Task WhenITracePipelineExecutionFor(string pipelineName)
    {
        var result = await _tracingService.TracePipelineExecution(pipelineName);
        result.Match(
            onSuccess: activity => _currentActivity = Option<Activity>.Some(activity),
            onFailure: error => throw new Exception($"Failed to trace pipeline: {error}")
        );
    }

    /// <summary>
    /// Traces LLM request.
    /// </summary>
    [Given(@"I trace LLM request for model ""(.*)"" with max tokens (.*)")]
    [When(@"I trace LLM request for model ""(.*)"" with max tokens (.*)")]
    public async Task GivenITraceLlmRequestForModelWithMaxTokens(string model, int maxTokens)
    {
        var result = await _tracingService.TraceLlmRequest(model, maxTokens);
        result.Match(
            onSuccess: activity => _currentActivity = Option<Activity>.Some(activity),
            onFailure: error => throw new Exception($"Failed to trace LLM: {error}")
        );
    }

    /// <summary>
    /// Traces vector operation.
    /// </summary>
    [When(@"I trace vector operation ""(.*)"" with dimension (.*)")]
    public async Task WhenITraceVectorOperationWithDimension(string operation, int dimension)
    {
        var result = await _tracingService.TraceVectorOperation(operation, dimension);
        result.Match(
            onSuccess: activity => _currentActivity = Option<Activity>.Some(activity),
            onFailure: error => throw new Exception($"Failed to trace vector: {error}")
        );
    }

    /// <summary>
    /// Completes LLM request.
    /// </summary>
    [When(@"I complete the LLM request with response length (.*) and token count (.*)")]
    public async Task WhenICompleteTheLlmRequestWithResponseLengthAndTokenCount(int responseLength, int tokenCount)
    {
        if (_currentActivity == Option<Activity>.None())
            return;

        var result = await _tracingService.CompleteLlmRequest(_currentActivity.Match(
            func: activity => activity,
            defaultValue: null
        ), responseLength, tokenCount);
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to complete LLM request: {error}")
        );
    }

    /// <summary>
    /// Completes tool execution successfully.
    /// </summary>
    [When(@"I complete the tool execution successfully with output length (.*)")]
    public async Task WhenICompleteTheToolExecutionSuccessfullyWithOutputLength(int outputLength)
    {
        if (_currentActivity == Option<Activity>.None())
            return;

        var result = await _tracingService.CompleteToolExecution(_currentActivity.Match(
            func: activity => activity,
            defaultValue: null
        ), true, outputLength);
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to complete tool execution: {error}")
        );
    }

    /// <summary>
    /// Completes tool execution with failure.
    /// </summary>
    [When(@"I complete the tool execution with failure and output length (.*)")]
    public async Task WhenICompleteTheToolExecutionWithFailureAndOutputLength(int outputLength)
    {
        if (_currentActivity == Option<Activity>.None())
            return;

        var result = await _tracingService.CompleteToolExecution(_currentActivity.Match(
            func: activity => activity,
            defaultValue: null
        ), false, outputLength);
        result.Match(
            onSuccess: _ => { },
            onFailure: error => throw new Exception($"Failed to complete tool execution: {error}")
        );
    }

    /// <summary>
    /// Captures parent ID.
    /// </summary>
    [When(@"I capture the parent activity ID")]
    public void WhenICaptureTheParentActivityId()
    {
        _capturedParentId = _currentActivity.Match(
            func: activity => _tracingService.GetSpanId(activity),
            defaultValue: Option<string>.None()
        );
    }

    /// <summary>
    /// Starts and completes an activity.
    /// </summary>
    [When(@"I start and complete an activity named ""(.*)""")]
    public async Task WhenIStartAndCompleteAnActivityNamed(string name)
    {
        var result = await _tracingService.StartActivity(name);
        if (result.IsSuccess)
        {
            var stopResult = await _tracingService.StopActivity(result.Value);
            stopResult.Match(
                onSuccess: _ => { },
                onFailure: error => throw new Exception($"Failed to stop activity: {error}")
            );
        }
        else
        {
            throw new Exception($"Failed to start activity: {result.Error}");
        }
    }

    /// <summary>
    /// Disables tracing.
    /// </summary>
    [When(@"I disable tracing")]
    public async Task WhenIDisableTracing()
    {
        await _tracingService.DisableTracing();
    }

    // Then steps remain similar, using assertions on _currentActivity and context
    [Then(@"the activity should not be null")]
    public void ThenTheActivityShouldNotBeNull()
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
    }

    [Then(@"the activity operation name should be ""(.*)""")]
    public void ThenTheActivityOperationNameShouldBe(string expectedName)
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.OperationName.Should().Be(expectedName),
            defaultValue: null
        );
    }

    [Then(@"the activity operation name should contain ""(.*)""")]
    public void ThenTheActivityOperationNameShouldContain(string expectedSubstring)
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.OperationName.Should().Contain(expectedSubstring),
            defaultValue: null
        );
    }

    [Then(@"the activity should have at least one tag")]
    public void ThenTheActivityShouldHaveAtLeastOneTag()
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.Tags.Should().NotBeEmpty(),
            defaultValue: null
        );
    }

    [Then(@"the activity should have (.*) event")]
    public void ThenTheActivityShouldHaveEvent(int count)
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.Events.Should().HaveCount(count),
            defaultValue: null
        );
    }

    [Then(@"the first event name should be ""(.*)""")]
    public void ThenTheFirstEventNameShouldBe(string expectedName)
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.Events.First().Name.Should().Be(expectedName),
            defaultValue: null
        );
    }

    [Then(@"the activity status should be ""(.*)""")]
    public void ThenTheActivityStatusShouldBe(string expectedStatus)
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.Status.ToString().Should().Be(expectedStatus),
            defaultValue: null
        );
    }

    [Then(@"the activity should have tag ""([^""]*)""$")]
    public void ThenTheActivityShouldHaveTag(string key)
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.Tags.Should().ContainKey(key),
            defaultValue: null
        );
    }

    [Then(@"the activity should have tag ""(.*)"" with value ""(.*)""")]
    public void ThenTheActivityShouldHaveTagWithValue(string key, string value)
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        _currentActivity.Match(
            func: activity => activity.Tags.Should().Contain(key, value),
            defaultValue: null
        );
    }

    [Then(@"the trace ID should not be null")]
    public void ThenTheTraceIdShouldNotBeNull()
    {
        _scenarioContext["traceId"].Should().NotBeNull();
    }

    [Then(@"the trace ID should not be empty")]
    public void ThenTheTraceIdShouldNotBeEmpty()
    {
        ((string)_scenarioContext["traceId"]).Should().NotBeEmpty();
    }

    [Then(@"the span ID should not be null")]
    public void ThenTheSpanIdShouldNotBeNull()
    {
        _scenarioContext["spanId"].Should().NotBeNull();
    }

    [Then(@"the span ID should not be empty")]
    public void ThenTheSpanIdShouldNotBeEmpty()
    {
        ((string)_scenarioContext["spanId"]).Should().NotBeEmpty();
    }

    [Then(@"the child activity parent ID should match the captured parent ID")]
    public void ThenTheChildActivityParentIdShouldMatchTheCapturedParentId()
    {
        _currentActivity.Should().NotBe(Option<Activity>.None());
        var parentIdOption = _currentActivity.Match(
            func: activity => _tracingService.GetParentSpanId(activity),
            defaultValue: Option<string>.None()
        );
        var childParentId = parentIdOption.Match(
            func: id => id,
            defaultValue: null
        );
        var capturedId = _capturedParentId.Match(
            func: id => id,
            defaultValue: null
        );
        childParentId.Should().Be(capturedId);
    }

    [Then(@"the started callback count should be (.*)")]
    public void ThenTheStartedCallbackCountShouldBe(int count)
    {
        _startedCallbackCount.Should().Be(count);
    }

    [Then(@"the stopped callback count should be (.*)")]
    public void ThenTheStoppedCallbackCountShouldBe(int count)
    {
        _stoppedCallbackCount.Should().Be(count);
    }

    [Then(@"the second activity should be null")]
    public void ThenTheSecondActivityShouldBeNull()
    {
        _currentActivity.Should().Be(Option<Activity>.None());
    }
}


