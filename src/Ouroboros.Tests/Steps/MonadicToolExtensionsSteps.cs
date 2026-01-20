namespace Ouroboros.Specs.Steps;

[Binding]
public class MonadicToolExtensionsSteps
{
    private DelegateTool? _primary;
    private DelegateTool? _secondary;
    private DelegateTool? _fallback;
    private Result<string, string>? _result;

    [Given("a fresh tool composition context")]
    public void GivenAFreshToolCompositionContext()
    {
        _primary = null;
        _secondary = null;
        _fallback = null;
        _result = null;
    }

    [Given(@"a primary tool that succeeds with ""(.*)""")]
    public void GivenAPrimaryToolThatSucceedsWith(string value)
    {
        _primary = new DelegateTool(
            name: "primary",
            description: "primary",
            executor: (string _, CancellationToken __) => Task.FromResult(Result<string, string>.Success(value))
        );
    }

    [Given(@"a primary tool that fails with error ""(.*)""")]
    public void GivenAPrimaryToolThatFailsWithError(string error)
    {
        _primary = new DelegateTool(
            name: "primary",
            description: "primary",
            executor: (string _, CancellationToken __) => Task.FromResult(Result<string, string>.Failure(error))
        );
    }

    [Given(@"a secondary tool that appends ""(.*)"" and succeeds")]
    public void GivenASecondaryToolThatAppendsAndSucceeds(string suffix)
    {
        _secondary = new DelegateTool(
            name: "secondary",
            description: "secondary",
            executor: (string s, CancellationToken __) => Task.FromResult(Result<string, string>.Success(s + suffix))
        );
    }

    [Given(@"a fallback tool that succeeds with ""(.*)""")]
    public void GivenAFallbackToolThatSucceedsWith(string value)
    {
        _fallback = new DelegateTool(
            name: "fallback",
            description: "fallback",
            executor: (string _, CancellationToken __) => Task.FromResult(Result<string, string>.Success(value))
        );
    }

    [Given(@"a mapping that uppercases the result")]
    public void GivenAMappingThatUppercasesTheResult()
    {
        // stored implicitly by executing Map in the When step
    }

    [When(@"I chain the tools with Then and execute with input ""(.*)""")]
    public async Task WhenIChainTheToolsWithThenAndExecuteWithInput(string input)
    {
        _primary.Should().NotBeNull();
        _secondary.Should().NotBeNull();

        // Then returns a Step, not a Kleisli
        var step = _primary!.Then(_secondary!);
        _result = await step(input);
    }

    [When(@"I compose the tools with OrElse and execute with input ""(.*)""")]
    public async Task WhenIComposeTheToolsWithOrElseAndExecuteWithInput(string input)
    {
        _primary.Should().NotBeNull();
        _fallback.Should().NotBeNull();

        // OrElse returns a Step
        var composed = _primary!.OrElse(_fallback!);
        _result = await composed(input);
    }

    [When(@"I map the tool result and execute with input ""(.*)""")]
    public async Task WhenIMapTheToolResultAndExecuteWithInput(string input)
    {
        _primary.Should().NotBeNull();

        // Map returns a Step<string, Result<string, string>>
        var mapped = _primary!.Map(s => s.ToUpperInvariant());
        _result = await mapped(input);
    }

    [Then(@"the result should be success with ""(.*)""")]
    public void ThenTheResultShouldBeSuccessWith(string expected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.Should().Be(expected);
    }

    [Then(@"the result should be failure with error ""(.*)""")]
    public void ThenTheResultShouldBeFailureWithError(string error)
    {
        _result.Should().NotBeNull();
        _result.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Be(error);
    }
}
