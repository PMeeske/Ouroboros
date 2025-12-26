namespace Ouroboros.Specs.Steps;

[Binding]
[Scope(Feature = "Math Tool")]
public class MathToolSteps
{
    private MathTool? _tool;
    private Result<string, string>? _result;

    [Given("a fresh math tool context")]
    public void GivenAFreshMathToolContext()
    {
        _tool = null;
        _result = null;
    }

    [Given("a math tool")]
    public void GivenAMathTool()
    {
        _tool = new MathTool();
    }

    [When("I create a math tool")]
    public void WhenICreateAMathTool()
    {
        _tool = new MathTool();
    }

    [When(@"I evaluate ""(.*)""")]
    public async Task WhenIEvaluate(string expression)
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync(expression);
    }

    [When("I evaluate empty string")]
    public async Task WhenIEvaluateEmptyString()
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync(string.Empty);
    }

    [When("I evaluate whitespace")]
    public async Task WhenIEvaluateWhitespace()
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync("   ");
    }

    [Then(@"the tool name should be ""(.*)""")]
    public void ThenTheToolNameShouldBe(string expected)
    {
        _tool.Should().NotBeNull();
        _tool!.Name.Should().Be(expected);
    }

    [Then("the tool description should mention arithmetic")]
    public void ThenTheToolDescriptionShouldMentionArithmetic()
    {
        _tool.Should().NotBeNull();
        _tool!.Description.Should().NotBeNullOrWhiteSpace();
        _tool.Description.Should().Contain("arithmetic");
    }

    [Then("the tool schema should be null")]
    public void ThenTheToolSchemaShouldBeNull()
    {
        _tool.Should().NotBeNull();
        _tool!.JsonSchema.Should().BeNull();
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

    [Then("the error should mention empty")]
    public void ThenTheErrorShouldMentionEmpty()
    {
        _result.Should().NotBeNull();
        _result!.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Contain("empty");
    }

    [Then(@"the error should contain ""(.*)""")]
    public void ThenTheErrorShouldContain(string expected)
    {
        _result.Should().NotBeNull();
        _result!.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Contain(expected);
    }
}
