namespace Ouroboros.Specs.Steps;

[Binding]
public class ToolRegistrySteps
{
    private ToolRegistry? _originalRegistry;
    private ToolRegistry? _registry;
    private ToolRegistry? _registry1;
    private ToolRegistry? _registry2;
    private ToolRegistry? _newRegistry;
    private ToolRegistry? _newestRegistry;
    private Option<ITool>? _toolOption;
    private ITool? _retrievedTool;
    private ITool? _storedTool;
    private bool? _containsResult;
    private Exception? _thrownException;
    private Result<string>? _exportResult;

    private class TestTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public string? JsonSchema { get; }

        public TestTool(string name, string description = "Test tool", string? jsonSchema = null)
        {
            Name = name;
            Description = description;
            JsonSchema = jsonSchema;
        }

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success($"Result: {input}"));
        }
    }

    [Given("a fresh tool registry context")]
    public void GivenAFreshToolRegistryContext()
    {
        _originalRegistry = null;
        _registry = null;
        _registry1 = null;
        _registry2 = null;
        _newRegistry = null;
        _newestRegistry = null;
        _toolOption = null;
        _retrievedTool = null;
        _storedTool = null;
        _containsResult = null;
        _thrownException = null;
        _exportResult = null;
    }

    [Given("an empty tool registry")]
    public void GivenAnEmptyToolRegistry()
    {
        _originalRegistry = new ToolRegistry();
        _registry = _originalRegistry;
    }

    [Given(@"a registry with tool ""(.*)""")]
    public void GivenARegistryWithTool(string toolName)
    {
        _storedTool = new TestTool(toolName);
        _registry = new ToolRegistry().WithTool(_storedTool);
    }

    [When("I create a new tool registry")]
    public void WhenICreateANewToolRegistry()
    {
        _registry = new ToolRegistry();
    }

    [When(@"I add a tool named ""([^""]*)""(?!.*with description)")]
    public void WhenIAddAToolNamed(string toolName)
    {
        _registry.Should().NotBeNull();
        _newRegistry = _registry!.WithTool(new TestTool(toolName));
    }

    [When("I attempt to add a null tool")]
    public void WhenIAttemptToAddANullTool()
    {
        try
        {
            _registry.Should().NotBeNull();
            _newRegistry = _registry!.WithTool(null!);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When(@"I chain tools ""(.*)"", ""(.*)"", and ""(.*)""")]
    public void WhenIChainTools(string tool1, string tool2, string tool3)
    {
        _registry.Should().NotBeNull();
        _newRegistry = _registry!
            .WithTool(new TestTool(tool1))
            .WithTool(new TestTool(tool2))
            .WithTool(new TestTool(tool3));
    }

    [When(@"I add a tool named ""(.*)"" with description ""(.*)""")]
    public void WhenIAddAToolNamedWithDescription(string toolName, string description)
    {
        _registry.Should().NotBeNull();
        _registry = _registry!.WithTool(new TestTool(toolName, description));
    }

    [When(@"I add another tool named ""(.*)"" with description ""(.*)""")]
    public void WhenIAddAnotherToolNamedWithDescription(string toolName, string description)
    {
        _registry.Should().NotBeNull();
        _registry = _registry!.WithTool(new TestTool(toolName, description));
    }

    [When(@"I get tool ""(.*)""")]
    public void WhenIGetTool(string toolName)
    {
        _registry.Should().NotBeNull();
        _toolOption = _registry!.GetTool(toolName);
    }

    [When("I attempt to get a tool with null name")]
    public void WhenIAttemptToGetAToolWithNullName()
    {
        try
        {
            _registry.Should().NotBeNull();
            _toolOption = _registry!.GetTool(null!);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When(@"I use Get for ""(.*)""")]
    public void WhenIUseGetFor(string toolName)
    {
        _registry.Should().NotBeNull();
        _retrievedTool = _registry!.Get(toolName);
    }

    [When(@"I check if it contains ""(.*)""")]
    public void WhenICheckIfItContains(string toolName)
    {
        _registry.Should().NotBeNull();
        _containsResult = _registry!.Contains(toolName);
    }

    [When(@"I add tools ""(.*)"", ""(.*)"", and ""(.*)""")]
    public void WhenIAddTools(string tool1, string tool2, string tool3)
    {
        _registry.Should().NotBeNull();
        _newRegistry = _registry!
            .WithTool(new TestTool(tool1))
            .WithTool(new TestTool(tool2))
            .WithTool(new TestTool(tool3));
    }

    [When(@"I add a tool ""(.*)"" with schema")]
    public void WhenIAddAToolWithSchema(string toolName)
    {
        _registry.Should().NotBeNull();
        _registry = _registry!.WithTool(new TestTool(toolName, "First tool", "{\"type\": \"object\"}"));
    }

    [When(@"I add a tool ""(.*)"" without schema")]
    public void WhenIAddAToolWithoutSchema(string toolName)
    {
        _registry.Should().NotBeNull();
        _registry = _registry!.WithTool(new TestTool(toolName, "Second tool", null));
    }

    [When("I export schemas")]
    public void WhenIExportSchemas()
    {
        _registry.Should().NotBeNull();
        _exportResult = _registry.SafeExportSchemas();
    }

    [When(@"I add ""(.*)"" creating registry1")]
    public void WhenIAddCreatingRegistry1(string toolName)
    {
        _originalRegistry.Should().NotBeNull();
        _registry1 = _originalRegistry!.WithTool(new TestTool(toolName));
    }

    [When(@"I add ""(.*)"" to registry1 creating registry2")]
    public void WhenIAddToRegistry1CreatingRegistry2(string toolName)
    {
        _registry1.Should().NotBeNull();
        _registry2 = _registry1!.WithTool(new TestTool(toolName));
    }

    [When(@"I add a tool named ""(.*)"" to the new registry")]
    public void WhenIAddAToolNamedToTheNewRegistry(string toolName)
    {
        _newRegistry.Should().NotBeNull();
        _newestRegistry = _newRegistry!.WithTool(new TestTool(toolName));
    }

    [Then(@"the registry count should be (.*)")]
    public void ThenTheRegistryCountShouldBe(int expected)
    {
        if (_newRegistry != null)
        {
            _newRegistry.Count.Should().Be(expected);
        }
        else if (_registry != null)
        {
            _registry.Count.Should().Be(expected);
        }
    }

    [Then("the registry should have no tools")]
    public void ThenTheRegistryShouldHaveNoTools()
    {
        _registry.Should().NotBeNull();
        _registry!.All.Should().BeEmpty();
    }

    [Then(@"the new registry count should be (.*)")]
    public void ThenTheNewRegistryCountShouldBe(int expected)
    {
        _newRegistry.Should().NotBeNull();
        _newRegistry!.Count.Should().Be(expected);
    }

    [Then(@"the newest registry count should be (.*)")]
    public void ThenTheNewestRegistryCountShouldBe(int expected)
    {
        _newestRegistry.Should().NotBeNull();
        _newestRegistry!.Count.Should().Be(expected);
    }

    [Then(@"the new registry should contain ""(.*)""")]
    public void ThenTheNewRegistryShouldContain(string toolName)
    {
        _newRegistry.Should().NotBeNull();
        _newRegistry!.Contains(toolName).Should().BeTrue();
    }

    [Then("a new registry instance should be returned")]
    public void ThenANewRegistryInstanceShouldBeReturned()
    {
        _registry.Should().NotBeNull();
        _newRegistry.Should().NotBeNull();
        _newRegistry.Should().NotBeSameAs(_registry);
    }

    [Then("the original registry should remain empty")]
    public void ThenTheOriginalRegistryShouldRemainEmpty()
    {
        _registry.Should().NotBeNull();
        _registry!.Count.Should().Be(0);
    }

    [Then("it should throw ArgumentNullException")]
    public void ThenItShouldThrowArgumentNullException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<ArgumentNullException>();
    }

    [Then(@"the registry should contain ""(.*)""")]
    public void ThenTheRegistryShouldContain(string toolName)
    {
        _newRegistry.Should().NotBeNull();
        _newRegistry!.Contains(toolName).Should().BeTrue();
    }

    [Then(@"the tool ""(.*)"" should have description ""(.*)""")]
    public void ThenTheToolShouldHaveDescription(string toolName, string expectedDescription)
    {
        _registry.Should().NotBeNull();
        var tool = _registry!.Get(toolName);
        tool.Should().NotBeNull();
        tool!.Description.Should().Be(expectedDescription);
    }

    [Then("the tool option should have a value")]
    public void ThenTheToolOptionShouldHaveAValue()
    {
        _toolOption.Should().NotBeNull();
        _toolOption!.Value.HasValue.Should().BeTrue();
    }

    [Then("the tool option should not have a value")]
    public void ThenTheToolOptionShouldNotHaveAValue()
    {
        _toolOption.Should().NotBeNull();
        _toolOption!.Value.HasValue.Should().BeFalse();
    }

    [Then("the tool should be the same instance")]
    public void ThenTheToolShouldBeTheSameInstance()
    {
        _toolOption.Should().NotBeNull();
        _toolOption!.Value.HasValue.Should().BeTrue();
        _storedTool.Should().NotBeNull();
        _toolOption.Value.Value.Should().BeSameAs(_storedTool);
    }

    [Then("the tool should be returned")]
    public void ThenTheToolShouldBeReturned()
    {
        _retrievedTool.Should().NotBeNull();
    }

    [Then("null should be returned")]
    public void ThenNullShouldBeReturned()
    {
        _retrievedTool.Should().BeNull();
    }

    [Then("the result should be true")]
    public void ThenTheResultShouldBeTrue()
    {
        _containsResult.Should().NotBeNull();
        _containsResult!.Value.Should().BeTrue();
    }

    [Then("the result should be false")]
    public void ThenTheResultShouldBeFalse()
    {
        _containsResult.Should().NotBeNull();
        _containsResult!.Value.Should().BeFalse();
    }

    [Then(@"All should return (.*) tools")]
    public void ThenAllShouldReturnTools(int count)
    {
        _newRegistry.Should().NotBeNull();
        _newRegistry!.All.Should().HaveCount(count);
    }

    [Then("All should contain the tool instances")]
    public void ThenAllShouldContainTheToolInstances()
    {
        _newRegistry.Should().NotBeNull();
        _newRegistry!.All.Should().NotBeEmpty();
    }

    [Then("the result should be successful")]
    public void ThenTheResultShouldBeSuccessful()
    {
        _exportResult.Should().NotBeNull();
        _exportResult.Value.IsSuccess.Should().BeTrue();
    }

    [Then(@"the export should contain ""(.*)""")]
    public void ThenTheExportShouldContain(string expected)
    {
        _exportResult.Should().NotBeNull();
        _exportResult.Value.IsSuccess.Should().BeTrue();
        _exportResult.Value.Value.Should().Contain(expected);
    }

    [Then(@"the original registry count should be (.*)")]
    public void ThenTheOriginalRegistryCountShouldBe(int expected)
    {
        _originalRegistry.Should().NotBeNull();
        _originalRegistry!.Count.Should().Be(expected);
    }

    [Then(@"registry1 count should be (.*)")]
    public void ThenRegistry1CountShouldBe(int expected)
    {
        _registry1.Should().NotBeNull();
        _registry1!.Count.Should().Be(expected);
    }

    [Then(@"registry2 count should be (.*)")]
    public void ThenRegistry2CountShouldBe(int expected)
    {
        _registry2.Should().NotBeNull();
        _registry2!.Count.Should().Be(expected);
    }

    [Then(@"original registry should not contain ""(.*)"" or ""(.*)""")]
    public void ThenOriginalRegistryShouldNotContainOr(string tool1, string tool2)
    {
        _originalRegistry.Should().NotBeNull();
        _originalRegistry!.Contains(tool1).Should().BeFalse();
        _originalRegistry.Contains(tool2).Should().BeFalse();
    }

    [Then(@"registry1 should contain ""(.*)"" but not ""(.*)""")]
    public void ThenRegistry1ShouldContainButNot(string tool1, string tool2)
    {
        _registry1.Should().NotBeNull();
        _registry1!.Contains(tool1).Should().BeTrue();
        _registry1.Contains(tool2).Should().BeFalse();
    }

    [Then(@"registry2 should contain both ""(.*)"" and ""(.*)""")]
    public void ThenRegistry2ShouldContainBoth(string tool1, string tool2)
    {
        _registry2.Should().NotBeNull();
        _registry2!.Contains(tool1).Should().BeTrue();
        _registry2.Contains(tool2).Should().BeTrue();
    }
}
