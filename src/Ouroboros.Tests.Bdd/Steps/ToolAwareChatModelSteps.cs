namespace Ouroboros.Specs.Steps;

[Binding]
[Scope(Feature = "Tool-Aware Chat Model")]
public class ToolAwareChatModelSteps
{
    private ToolRegistry? _registry;
    private IChatCompletionModel? _mockModel;
    private ToolAwareChatModel? _toolAwareModel;
    private string? _responseText;
    private List<ToolExecution>? _toolExecutions;
    private Result<(string text, List<ToolExecution> tools), string>? _result;
    private CancellationToken _cancellationToken;
    private Exception? _thrownException;

    private class MockChatModel : IChatCompletionModel
    {
        private readonly string _response;

        public MockChatModel(string response)
        {
            _response = response;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_response);
        }
    }

    private class ThrowingChatModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Model error");
        }
    }

    private class ThrowingTool : ITool
    {
        public string Name => "throwing_tool";
        public string Description => "A tool that throws";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Tool error");
        }
    }

    [Given("I have a tool registry")]
    public void GivenIHaveAToolRegistry()
    {
        _registry = new ToolRegistry();
        _cancellationToken = CancellationToken.None;
    }

    [Given(@"I have a mock chat model that responds with ""(.*)""")]
    public void GivenIHaveAMockChatModelThatRespondsWith(string response)
    {
        _mockModel = new MockChatModel(response);
    }

    [Given("I have a throwing chat model")]
    public void GivenIHaveAThrowingChatModel()
    {
        _mockModel = new ThrowingChatModel();
    }

    [Given("I register the math tool")]
    public void GivenIRegisterTheMathTool()
    {
        _registry = _registry!.WithTool(new MathTool());
    }

    [Given("I register the throwing tool")]
    public void GivenIRegisterTheThrowingTool()
    {
        _registry = _registry!.WithTool(new ThrowingTool());
    }

    [Given("I have a tool-aware chat model with the mock model and registry")]
    public void GivenIHaveAToolAwareChatModelWithTheMockModelAndRegistry()
    {
        _toolAwareModel = new ToolAwareChatModel(_mockModel!, _registry!);
    }

    [Given("I have a cancelled cancellation token")]
    public void GivenIHaveACancelledCancellationToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _cancellationToken = cts.Token;
    }

    [When(@"I generate with tools using prompt ""(.*)""")]
    public async Task WhenIGenerateWithToolsUsingPrompt(string prompt)
    {
        try
        {
            (_responseText, _toolExecutions) = await _toolAwareModel!.GenerateWithToolsAsync(prompt);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When(@"I generate with tools using prompt ""(.*)"" with cancellation token")]
    public async Task WhenIGenerateWithToolsUsingPromptWithCancellationToken(string prompt)
    {
        try
        {
            (_responseText, _toolExecutions) = await _toolAwareModel!.GenerateWithToolsAsync(prompt, _cancellationToken);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When(@"I generate with tools result using prompt ""(.*)""")]
    public async Task WhenIGenerateWithToolsResultUsingPrompt(string prompt)
    {
        _result = await _toolAwareModel!.GenerateWithToolsResultAsync(prompt);
    }

    [Then(@"the response text should be ""(.*)""")]
    public void ThenTheResponseTextShouldBe(string expectedText)
    {
        _responseText.Should().Be(expectedText);
    }

    [Then(@"the response text should contain ""(.*)""")]
    public void ThenTheResponseTextShouldContain(string expectedSubstring)
    {
        _responseText.Should().Contain(expectedSubstring);
    }

    [Then("the tool executions should be empty")]
    public void ThenTheToolExecutionsShouldBeEmpty()
    {
        _toolExecutions.Should().BeEmpty();
    }

    [Then(@"the tool executions should have (.*) execution")]
    [Then(@"the tool executions should have (.*) executions")]
    public void ThenTheToolExecutionsShouldHaveExecutions(int expectedCount)
    {
        _toolExecutions.Should().HaveCount(expectedCount);
    }

    [Then(@"the first tool execution name should be ""(.*)""")]
    public void ThenTheFirstToolExecutionNameShouldBe(string expectedName)
    {
        _toolExecutions.Should().NotBeEmpty();
        _toolExecutions![0].ToolName.Should().Be(expectedName);
    }

    [Then(@"the first tool execution arguments should be ""(.*)""")]
    public void ThenTheFirstToolExecutionArgumentsShouldBe(string expectedArgs)
    {
        _toolExecutions.Should().NotBeEmpty();
        _toolExecutions![0].Arguments.Should().Be(expectedArgs);
    }

    [Then(@"the first tool execution output should be ""(.*)""")]
    public void ThenTheFirstToolExecutionOutputShouldBe(string expectedOutput)
    {
        _toolExecutions.Should().NotBeEmpty();
        _toolExecutions![0].Output.Should().Be(expectedOutput);
    }

    [Then(@"the first tool execution output should contain ""(.*)""")]
    public void ThenTheFirstToolExecutionOutputShouldContain(string expectedSubstring)
    {
        _toolExecutions.Should().NotBeEmpty();
        _toolExecutions![0].Output.Should().Contain(expectedSubstring);
    }

    [Then(@"tool execution (.*) name should be ""(.*)""")]
    public void ThenToolExecutionNameShouldBe(int index, string expectedName)
    {
        _toolExecutions.Should().HaveCountGreaterThan(index);
        _toolExecutions![index].ToolName.Should().Be(expectedName);
    }

    [Then(@"tool execution (.*) output should be ""(.*)""")]
    public void ThenToolExecutionOutputShouldBe(int index, string expectedOutput)
    {
        _toolExecutions.Should().HaveCountGreaterThan(index);
        _toolExecutions![index].Output.Should().Be(expectedOutput);
    }

    [Then("the result should be successful")]
    public void ThenTheResultShouldBeSuccessful()
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeTrue();
    }

    [Then(@"the result text should contain ""(.*)""")]
    public void ThenTheResultTextShouldContain(string expectedSubstring)
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.text.Should().Contain(expectedSubstring);
    }

    [Then(@"the result tool executions should have (.*) execution")]
    public void ThenTheResultToolExecutionsShouldHaveExecution(int expectedCount)
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.tools.Should().HaveCount(expectedCount);
    }

    [Then("the result should be a failure")]
    public void ThenTheResultShouldBeAFailure()
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeFalse();
    }

    [Then(@"the result error should contain ""(.*)""")]
    public void ThenTheResultErrorShouldContain(string expectedSubstring)
    {
        _result.Should().NotBeNull();
        _result!.Value.IsSuccess.Should().BeFalse();
        _result.Value.Error.Should().Contain(expectedSubstring);
    }

    [Then("an OperationCanceledException should be thrown")]
    public void ThenAnOperationCanceledExceptionShouldBeThrown()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<OperationCanceledException>();
    }
}
