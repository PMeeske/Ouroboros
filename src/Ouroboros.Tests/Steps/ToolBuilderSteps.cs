namespace Ouroboros.Specs.Steps;

[Binding]
[Scope(Feature = "Tool Builder")]
public class ToolBuilderSteps
{
    private ITool? _tool;
    private Result<string, string>? _result;
    private int _secondToolInvocations;

    [Given("a fresh tool builder context")]
    public void GivenAFreshToolBuilderContext()
    {
        _tool = null;
        _result = null;
        _secondToolInvocations = 0;
    }

    [Given(@"a chain of ""(.*)"" and ""(.*)"" tools")]
    public void GivenAChainOfTools(string tool1, string tool2)
    {
        _tool = ToolBuilder.Chain(
            "pipeline",
            "Runs tools sequentially",
            new DelegateTool("uppercase", "Upper", value => value.ToUpperInvariant()),
            new DelegateTool("exclaim", "Exclaim", value => value + "!")
        );
    }

    [Given("a chain with a failing first tool and a second tool")]
    public void GivenAChainWithAFailingFirstToolAndASecondTool()
    {
        _tool = ToolBuilder.Chain(
            "stopper",
            "Stops on failure",
            new DelegateTool("first", "First", (_, __) => Task.FromResult(Result<string, string>.Failure("fail"))),
            new DelegateTool("second", "Second", async (value, ct) =>
            {
                await Task.CompletedTask;
                _secondToolInvocations++;
                return Result<string, string>.Success(value);
            })
        );
    }

    [Given("a simple chain tool")]
    public void GivenASimpleChainTool()
    {
        _tool = ToolBuilder.Chain(
            "cancel",
            "Handles cancellation",
            new DelegateTool("noop", "Noop", value => value)
        );
    }

    [Given(@"a FirstSuccess tool with ""(.*)"", ""(.*)"", and ""(.*)"" tools")]
    public void GivenAFirstSuccessToolWith(string tool1, string tool2, string tool3)
    {
        _tool = ToolBuilder.FirstSuccess(
            "first-success",
            "Uses first success",
            new DelegateTool("fail", "Fail", (_, __) => Task.FromResult(Result<string, string>.Failure("nope"))),
            new DelegateTool("ok", "Ok", value => value + "-ok"),
            new DelegateTool("skip", "Skip", value => value + "-skip")
        );
    }

    [Given("a FirstSuccess tool where all tools fail")]
    public void GivenAFirstSuccessToolWhereAllToolsFail()
    {
        _tool = ToolBuilder.FirstSuccess(
            "all-fail",
            "All fail",
            new DelegateTool("one", "One", (_, __) => Task.FromResult(Result<string, string>.Failure("first"))),
            new DelegateTool("two", "Two", (_, __) => Task.FromResult(Result<string, string>.Failure("second")))
        );
    }

    [Given("a conditional tool that selects based on input")]
    public void GivenAConditionalToolThatSelectsBasedOnInput()
    {
        _tool = ToolBuilder.Conditional(
            "conditional",
            "Selects tool",
            value => value switch
            {
                "upper" => new DelegateTool("upper", "Upper", s => s.ToUpperInvariant()),
                "lower" => new DelegateTool("lower", "Lower", s => s.ToLowerInvariant()),
                _ => new DelegateTool("noop", "Noop", s => s)
            });
    }

    [Given("a conditional tool with a throwing selector")]
    public void GivenAConditionalToolWithAThrowingSelector()
    {
        _tool = ToolBuilder.Conditional(
            "conditional",
            "Selector throws",
            _ => throw new InvalidOperationException("boom"));
    }

    [When(@"I invoke the chain with ""(.*)""")]
    public async Task WhenIInvokeTheChainWith(string input)
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync(input);
    }

    [When(@"I invoke it with ""(.*)""")]
    public async Task WhenIInvokeItWith(string input)
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync(input);
    }

    [When("I invoke it with a cancelled token")]
    public async Task WhenIInvokeItWithACancelledToken()
    {
        _tool.Should().NotBeNull();
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();
        _result = await _tool!.InvokeAsync("anything", cts.Token);
    }

    [Then("the result should be successful")]
    public void ThenTheResultShouldBeSuccessful()
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
    }

    [Then("the result should be a failure")]
    public void ThenTheResultShouldBeAFailure()
    {
        _result.Should().NotBeNull();
        _result.Value.IsFailure.Should().BeTrue();
    }

    [Then(@"the result value should be ""(.*)""")]
    public void ThenTheResultValueShouldBe(string expected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.Should().Be(expected);
    }

    [Then(@"the error message should be ""(.*)""")]
    public void ThenTheErrorMessageShouldBe(string expected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Be(expected);
    }

    [Then(@"the error message should contain ""(.*)""")]
    public void ThenTheErrorMessageShouldContain(string expected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Contain(expected);
    }

    [Then("the second tool should not have been invoked")]
    public void ThenTheSecondToolShouldNotHaveBeenInvoked()
    {
        _secondToolInvocations.Should().Be(0);
    }
}
