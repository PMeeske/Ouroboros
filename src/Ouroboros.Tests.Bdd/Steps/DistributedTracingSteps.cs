using System.Diagnostics;
using Ouroboros.Diagnostics;

namespace Ouroboros.Specs.Steps;

[Binding]
public class DistributedTracingSteps
{
    private Activity? _currentActivity;
    private Activity? _secondActivity;
    private Dictionary<string, object?>? _tags;
    private string? _traceId;
    private string? _spanId;
    private string? _capturedParentId;
    private int _startedCount;
    private int _stoppedCount;

    [Given("tracing is disabled initially")]
    public void GivenTracingIsDisabledInitially()
    {
        TracingConfiguration.DisableTracing();
    }

    [Given("tracing is enabled")]
    public void GivenTracingIsEnabled()
    {
        TracingConfiguration.EnableTracing();
    }

    [When("I disable tracing")]
    public void WhenIDisableTracing()
    {
        TracingConfiguration.DisableTracing();
    }

    [Given(@"I have tags with key1 ""(.*)"" and key2 (.*)")]
    public void GivenIHaveTagsWithKey1AndKey2(string value1, int value2)
    {
        _tags = new Dictionary<string, object?>
        {
            ["key1"] = value1,
            ["key2"] = value2
        };
    }

    [Given(@"I start an activity named ""(.*)""")]
    [When(@"I start an activity named ""(.*)""")]
    public void WhenIStartAnActivityNamed(string name)
    {
        if (name == "test2")
        {
            _secondActivity = DistributedTracing.StartActivity(name);
        }
        else
        {
            _currentActivity = DistributedTracing.StartActivity(name);
        }
    }

    [When(@"I start an activity named ""(.*)"" with tags")]
    public void WhenIStartAnActivityNamedWithTags(string name)
    {
        _currentActivity = DistributedTracing.StartActivity(name, tags: _tags);
    }

    [When(@"I record an event ""(.*)"" with detail ""(.*)""")]
    public void WhenIRecordAnEventWithDetail(string eventName, string detail)
    {
        DistributedTracing.RecordEvent(eventName, new Dictionary<string, object?> { ["detail"] = detail });
    }

    [When(@"I record an exception with message ""(.*)""")]
    public void WhenIRecordAnExceptionWithMessage(string message)
    {
        var exception = new InvalidOperationException(message);
        DistributedTracing.RecordException(exception);
    }

    [When(@"I set activity status to ""(.*)"" with description ""(.*)""")]
    public void WhenISetActivityStatusToWithDescription(string status, string description)
    {
        var statusCode = status == "Ok" ? ActivityStatusCode.Ok : ActivityStatusCode.Error;
        DistributedTracing.SetStatus(statusCode, description);
    }

    [When(@"I add tag ""(.*)"" with value ""(.*)""")]
    public void WhenIAddTagWithValue(string key, string value)
    {
        DistributedTracing.AddTag(key, value);
    }

    [When("I get the trace ID")]
    public void WhenIGetTheTraceId()
    {
        _traceId = DistributedTracing.GetTraceId();
    }

    [When("I get the span ID")]
    public void WhenIGetTheSpanId()
    {
        _spanId = DistributedTracing.GetSpanId();
    }

    [When(@"I trace tool execution for ""(.*)"" with input ""(.*)""")]
    public void WhenITraceToolExecutionForWithInput(string toolName, string input)
    {
        _currentActivity = TracingExtensions.TraceToolExecution(toolName, input);
    }

    [When(@"I trace pipeline execution for ""(.*)""")]
    public void WhenITracePipelineExecutionFor(string pipelineName)
    {
        _currentActivity = TracingExtensions.TracePipelineExecution(pipelineName);
    }

    [When(@"I trace LLM request for model ""(.*)"" with max tokens (.*)")]
    [Given(@"I trace LLM request for model ""(.*)"" with max tokens (.*)")]
    public void WhenITraceLlmRequestForModelWithMaxTokens(string model, int maxTokens)
    {
        _currentActivity = TracingExtensions.TraceLlmRequest(model, maxTokens);
    }

    [When(@"I trace vector operation ""(.*)"" with dimension (.*)")]
    public void WhenITraceVectorOperationWithDimension(string operation, int dimension)
    {
        _currentActivity = TracingExtensions.TraceVectorOperation(operation, dimension);
    }

    [When(@"I complete the LLM request with response length (.*) and token count (.*)")]
    public void WhenICompleteTheLlmRequestWithResponseLengthAndTokenCount(int responseLength, int tokenCount)
    {
        _currentActivity?.CompleteLlmRequest(responseLength: responseLength, tokenCount: tokenCount);
    }

    [When(@"I complete the tool execution successfully with output length (.*)")]
    public void WhenICompleteTheToolExecutionSuccessfullyWithOutputLength(int outputLength)
    {
        _currentActivity?.CompleteToolExecution(success: true, outputLength: outputLength);
    }

    [When(@"I complete the tool execution with failure and output length (.*)")]
    public void WhenICompleteTheToolExecutionWithFailureAndOutputLength(int outputLength)
    {
        _currentActivity?.CompleteToolExecution(success: false, outputLength: outputLength);
    }

    [When("I capture the parent activity ID")]
    public void WhenICaptureTheParentActivityId()
    {
        _capturedParentId = _currentActivity?.Id;
    }

    [Given("tracing is enabled with activity callbacks")]
    public void GivenTracingIsEnabledWithActivityCallbacks()
    {
        _startedCount = 0;
        _stoppedCount = 0;
        TracingConfiguration.EnableTracing(
            onActivityStarted: _ => _startedCount++,
            onActivityStopped: _ => _stoppedCount++);
    }

    [When(@"I start and complete an activity named ""(.*)""")]
    public void WhenIStartAndCompleteAnActivityNamed(string name)
    {
        using (var activity = DistributedTracing.StartActivity(name))
        {
            // Activity is active
        }
    }

    [Then("the activity should not be null")]
    public void ThenTheActivityShouldNotBeNull()
    {
        _currentActivity.Should().NotBeNull();
    }

    [Then("the second activity should be null")]
    public void ThenTheSecondActivityShouldBeNull()
    {
        _secondActivity.Should().BeNull();
    }

    [Then(@"the activity operation name should be ""(.*)""")]
    public void ThenTheActivityOperationNameShouldBe(string expectedName)
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.OperationName.Should().Be(expectedName);
    }

    [Then(@"the activity operation name should contain ""(.*)""")]
    public void ThenTheActivityOperationNameShouldContain(string expectedSubstring)
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.OperationName.Should().Contain(expectedSubstring);
    }

    [Then("the activity should have at least one tag")]
    public void ThenTheActivityShouldHaveAtLeastOneTag()
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.Tags.Should().NotBeEmpty();
    }

    [Then(@"the activity should have (.*) event")]
    public void ThenTheActivityShouldHaveEvent(int expectedCount)
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.Events.Should().HaveCount(expectedCount);
    }

    [Then(@"the first event name should be ""(.*)""")]
    public void ThenTheFirstEventNameShouldBe(string expectedName)
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.Events.First().Name.Should().Be(expectedName);
    }

    [Then(@"the activity status should be ""(.*)""")]
    public void ThenTheActivityStatusShouldBe(string expectedStatus)
    {
        _currentActivity.Should().NotBeNull();
        var expectedStatusCode = expectedStatus == "Ok" ? ActivityStatusCode.Ok : ActivityStatusCode.Error;
        _currentActivity!.Status.Should().Be(expectedStatusCode);
    }

    [Then(@"the activity should have tag ""(.*)""")]
    public void ThenTheActivityShouldHaveTag(string tagKey)
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.Tags.Should().Contain(t => t.Key == tagKey);
    }

    [Then(@"the activity should have tag ""(.*)"" with value ""(.*)""")]
    public void ThenTheActivityShouldHaveTagWithValue(string tagKey, string tagValue)
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.Tags.Should().Contain(t => t.Key == tagKey && t.Value as string == tagValue);
    }

    [Then("the trace ID should not be null")]
    public void ThenTheTraceIdShouldNotBeNull()
    {
        _traceId.Should().NotBeNull();
    }

    [Then("the trace ID should not be empty")]
    public void ThenTheTraceIdShouldNotBeEmpty()
    {
        _traceId.Should().NotBeEmpty();
    }

    [Then("the span ID should not be null")]
    public void ThenTheSpanIdShouldNotBeNull()
    {
        _spanId.Should().NotBeNull();
    }

    [Then("the span ID should not be empty")]
    public void ThenTheSpanIdShouldNotBeEmpty()
    {
        _spanId.Should().NotBeEmpty();
    }

    [Then("the child activity parent ID should match the captured parent ID")]
    public void ThenTheChildActivityParentIdShouldMatchTheCapturedParentId()
    {
        _currentActivity.Should().NotBeNull();
        _currentActivity!.ParentId.Should().Be(_capturedParentId);
    }

    [Then(@"the started callback count should be (.*)")]
    public void ThenTheStartedCallbackCountShouldBe(int expectedCount)
    {
        _startedCount.Should().Be(expectedCount);
    }

    [Then(@"the stopped callback count should be (.*)")]
    public void ThenTheStoppedCallbackCountShouldBe(int expectedCount)
    {
        _stoppedCount.Should().Be(expectedCount);
    }

    [AfterScenario]
    public void Cleanup()
    {
        _currentActivity?.Dispose();
        _secondActivity?.Dispose();
        TracingConfiguration.DisableTracing();
    }
}
