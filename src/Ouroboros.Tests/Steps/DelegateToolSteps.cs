namespace Ouroboros.Specs.Steps;

[Binding]
[Scope(Feature = "Delegate Tool")]
public class DelegateToolSteps
{
    private DelegateTool? _tool;
    private Result<string, string>? _result;
    private Exception? _thrownException;
    private CancellationToken _receivedToken;
    private CancellationTokenSource? _cts;

    private class TestArgs
    {
        public string Value { get; set; } = string.Empty;
    }

    [Given("a fresh delegate tool context")]
    public void GivenAFreshDelegateToolContext()
    {
        _tool = null;
        _result = null;
        _thrownException = null;
        _receivedToken = default;
        _cts = null;
    }

    [Given("a delegate tool that processes input")]
    public void GivenADelegateToolThatProcessesInput()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (input, ct) => Task.FromResult(Result<string, string>.Success($"processed: {input}")));
    }

    [Given("a delegate tool that fails")]
    public void GivenADelegateToolThatFails()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (input, ct) => Task.FromResult(Result<string, string>.Failure("error occurred")));
    }

    [Given("a delegate tool that captures cancellation token")]
    public void GivenADelegateToolThatCapturesCancellationToken()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (input, ct) =>
            {
                _receivedToken = ct;
                return Task.FromResult(Result<string, string>.Success("result"));
            });
    }

    [Given("a delegate tool with async func")]
    public void GivenADelegateToolWithAsyncFunc()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (Func<string, Task<string>>)(input => Task.FromResult($"processed: {input}")));
    }

    [Given("a delegate tool with throwing async func")]
    public void GivenADelegateToolWithThrowingAsyncFunc()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (Func<string, Task<string>>)(input => throw new InvalidOperationException("test error")));
    }

    [Given("a delegate tool with sync func")]
    public void GivenADelegateToolWithSyncFunc()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (input) => $"processed: {input}");
    }

    [Given("a delegate tool with throwing sync func")]
    public void GivenADelegateToolWithThrowingSyncFunc()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (Func<string, string>)(input => throw new InvalidOperationException("sync error")));
    }

    [Given("a FromJson tool")]
    public void GivenAFromJsonTool()
    {
        _tool = DelegateTool.FromJson<TestArgs>(
            "test",
            "description",
            args => Task.FromResult($"Received: {args.Value}"));
    }

    [Given("a FromJson tool that throws")]
    public void GivenAFromJsonToolThatThrows()
    {
        _tool = DelegateTool.FromJson<TestArgs>(
            "test",
            "description",
            args => throw new InvalidOperationException("function error"));
    }

    [When(@"I create a delegate tool named ""(.*)"" with description ""(.*)""")]
    public void WhenICreateADelegateToolNamedWithDescription(string name, string description)
    {
        _tool = new DelegateTool(
            name,
            description,
            (input, ct) => Task.FromResult(Result<string, string>.Success("result")));
    }

    [When("I create a delegate tool with schema")]
    public void WhenICreateADelegateToolWithSchema()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (input, ct) => Task.FromResult(Result<string, string>.Success("result")),
            "{\"type\": \"object\"}");
    }

    [When("I attempt to create a delegate tool with null name")]
    public void WhenIAttemptToCreateADelegateToolWithNullName()
    {
        try
        {
            _tool = new DelegateTool(
                null!,
                "description",
                (input, ct) => Task.FromResult(Result<string, string>.Success("result")));
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I attempt to create a delegate tool with null description")]
    public void WhenIAttemptToCreateADelegateToolWithNullDescription()
    {
        try
        {
            _tool = new DelegateTool(
                "test",
                null!,
                (input, ct) => Task.FromResult(Result<string, string>.Success("result")));
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I attempt to create a delegate tool with null executor")]
    public void WhenIAttemptToCreateADelegateToolWithNullExecutor()
    {
        try
        {
            _tool = new DelegateTool(
                "test",
                "description",
                (Func<string, CancellationToken, Task<Result<string, string>>>)null!);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When(@"I invoke it with ""(.*)""")]
    public async Task WhenIInvokeItWith(string input)
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync(input);
    }

    [When("I invoke it with a cancellation token")]
    public async Task WhenIInvokeItWithACancellationToken()
    {
        _tool.Should().NotBeNull();
        _cts = new CancellationTokenSource();
        _result = await _tool!.InvokeAsync("input", _cts.Token);
    }

    [When("I create a delegate tool with async func")]
    public void WhenICreateADelegateToolWithAsyncFunc()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (Func<string, Task<string>>)(input => Task.FromResult($"result: {input}")));
    }

    [When("I create a delegate tool with sync func")]
    public void WhenICreateADelegateToolWithSyncFunc()
    {
        _tool = new DelegateTool(
            "test",
            "description",
            (input) => $"result: {input}");
    }

    [When("I create a tool using FromJson")]
    public void WhenICreateAToolUsingFromJson()
    {
        _tool = DelegateTool.FromJson<TestArgs>(
            "test",
            "description",
            args => Task.FromResult($"Value: {args.Value}"));
    }

    [When("I invoke it with valid JSON")]
    public async Task WhenIInvokeItWithValidJson()
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync("{\"value\": \"test\"}");
    }

    [When("I invoke it with invalid JSON")]
    public async Task WhenIInvokeItWithInvalidJson()
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync("invalid json");
    }

    [Then(@"the tool name should be ""(.*)""")]
    public void ThenTheToolNameShouldBe(string expected)
    {
        _tool.Should().NotBeNull();
        _tool!.Name.Should().Be(expected);
    }

    [Then(@"the tool description should be ""(.*)""")]
    public void ThenTheToolDescriptionShouldBe(string expected)
    {
        _tool.Should().NotBeNull();
        _tool!.Description.Should().Be(expected);
    }

    [Then("the tool schema should be null")]
    public void ThenTheToolSchemaShouldBeNull()
    {
        _tool.Should().NotBeNull();
        _tool!.JsonSchema.Should().BeNull();
    }

    [Then("the tool schema should be stored")]
    public void ThenTheToolSchemaShouldBeStored()
    {
        _tool.Should().NotBeNull();
        _tool!.JsonSchema.Should().Be("{\"type\": \"object\"}");
    }

    [Then("it should throw ArgumentNullException")]
    public void ThenItShouldThrowArgumentNullException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<ArgumentNullException>();
    }

    [Then("the result should be successful")]
    public void ThenTheResultShouldBeSuccessful()
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeTrue();
    }

    [Then("the result should be a failure")]
    public void ThenTheResultShouldBeAFailure()
    {
        _result.Should().NotBeNull();
        _result!.Value.IsFailure.Should().BeTrue();
    }

    [Then(@"the result value should be ""(.*)""")]
    public void ThenTheResultValueShouldBe(string expected)
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.Should().Be(expected);
    }

    [Then(@"the error message should be ""(.*)""")]
    public void ThenTheErrorMessageShouldBe(string expected)
    {
        _result.Should().NotBeNull();
        _result!.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Be(expected);
    }

    [Then(@"the error message should contain ""(.*)""")]
    public void ThenTheErrorMessageShouldContain(string expected)
    {
        _result.Should().NotBeNull();
        _result!.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Contain(expected);
    }

    [Then("the cancellation token should be passed correctly")]
    public void ThenTheCancellationTokenShouldBePassedCorrectly()
    {
        _cts.Should().NotBeNull();
        _receivedToken.Should().Be(_cts!.Token);
    }

    [Then("the tool should be created successfully")]
    public void ThenTheToolShouldBeCreatedSuccessfully()
    {
        _tool.Should().NotBeNull();
        _tool!.Name.Should().Be("test");
        _tool.Description.Should().Be("description");
    }

    [Then("the tool should have a schema")]
    public void ThenTheToolShouldHaveASchema()
    {
        _tool.Should().NotBeNull();
        _tool!.JsonSchema.Should().NotBeNullOrEmpty();
    }

    [Then("the result should contain the parsed value")]
    public void ThenTheResultShouldContainTheParsedValue()
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.Should().Contain("Received: test");
    }
}
