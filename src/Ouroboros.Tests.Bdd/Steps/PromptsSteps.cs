namespace Ouroboros.Specs.Steps;

[Binding]
public class PromptsSteps
{
    private PromptTemplate? _currentPrompt;
    private string? _formattedPrompt;
    private PromptTemplate? _draftPrompt;
    private PromptTemplate? _critiquePrompt;
    private PromptTemplate? _improvePrompt;

    [Given("a fresh prompts context")]
    public void GivenAFreshPromptsContext()
    {
        _currentPrompt = null;
        _formattedPrompt = null;
        _draftPrompt = null;
        _critiquePrompt = null;
        _improvePrompt = null;
    }

    [Given("a Draft prompt with placeholders")]
    public void GivenADraftPromptWithPlaceholders()
    {
        _currentPrompt = Prompts.Draft;
    }

    [Given("a Critique prompt with draft placeholder")]
    public void GivenACritiquePromptWithDraftPlaceholder()
    {
        _currentPrompt = Prompts.Critique;
    }

    [Given("an Improve prompt with critique placeholder")]
    public void GivenAnImprovePromptWithCritiquePlaceholder()
    {
        _currentPrompt = Prompts.Improve;
    }

    [When("I access the Draft prompt")]
    public void WhenIAccessTheDraftPrompt()
    {
        _currentPrompt = Prompts.Draft;
    }

    [When("I access the Critique prompt")]
    public void WhenIAccessTheCritiquePrompt()
    {
        _currentPrompt = Prompts.Critique;
    }

    [When("I access the Improve prompt")]
    public void WhenIAccessTheImprovePrompt()
    {
        _currentPrompt = Prompts.Improve;
    }

    [When("I format with tools schemas, context, and topic")]
    public void WhenIFormatWithToolsSchemasContextAndTopic()
    {
        _currentPrompt.Should().NotBeNull();
        _formattedPrompt = _currentPrompt!.Format(new Dictionary<string, string>
        {
            ["tools_schemas"] = "[schema1, schema2]",
            ["context"] = "relevant context",
            ["topic"] = "test topic"
        });
    }

    [When("I format with a draft response")]
    public void WhenIFormatWithADraftResponse()
    {
        _currentPrompt.Should().NotBeNull();
        _formattedPrompt = _currentPrompt!.Format(new Dictionary<string, string>
        {
            ["draft"] = "This is the draft response"
        });
    }

    [When("I format with a critique response")]
    public void WhenIFormatWithACritiqueResponse()
    {
        _currentPrompt.Should().NotBeNull();
        _formattedPrompt = _currentPrompt!.Format(new Dictionary<string, string>
        {
            ["critique"] = "This is the critique feedback"
        });
    }

    [When("I access Draft, Critique, and Improve prompts")]
    public void WhenIAccessDraftCritiqueAndImprovePrompts()
    {
        _draftPrompt = Prompts.Draft;
        _critiquePrompt = Prompts.Critique;
        _improvePrompt = Prompts.Improve;
    }

    [Then("the prompt should not be null")]
    public void ThenThePromptShouldNotBeNull()
    {
        _currentPrompt.Should().NotBeNull();
    }

    [Then("the formatted prompt should contain the tools schemas")]
    public void ThenTheFormattedPromptShouldContainTheToolsSchemas()
    {
        _formattedPrompt.Should().NotBeNull();
        _formattedPrompt.Should().Contain("[schema1, schema2]");
    }

    [Then("the formatted prompt should contain the context")]
    public void ThenTheFormattedPromptShouldContainTheContext()
    {
        _formattedPrompt.Should().NotBeNull();
        _formattedPrompt.Should().Contain("relevant context");
    }

    [Then("the formatted prompt should contain the topic")]
    public void ThenTheFormattedPromptShouldContainTheTopic()
    {
        _formattedPrompt.Should().NotBeNull();
        _formattedPrompt.Should().Contain("test topic");
    }

    [Then("the formatted prompt should contain the draft response")]
    public void ThenTheFormattedPromptShouldContainTheDraftResponse()
    {
        _formattedPrompt.Should().NotBeNull();
        _formattedPrompt.Should().Contain("This is the draft response");
    }

    [Then("the formatted prompt should contain the critique response")]
    public void ThenTheFormattedPromptShouldContainTheCritiqueResponse()
    {
        _formattedPrompt.Should().NotBeNull();
        _formattedPrompt.Should().Contain("This is the critique feedback");
    }

    [Then("Draft should not equal Critique")]
    public void ThenDraftShouldNotEqualCritique()
    {
        _draftPrompt.Should().NotBeNull();
        _critiquePrompt.Should().NotBeNull();
        _draftPrompt.Should().NotBe(_critiquePrompt);
    }

    [Then("Draft should not equal Improve")]
    public void ThenDraftShouldNotEqualImprove()
    {
        _draftPrompt.Should().NotBeNull();
        _improvePrompt.Should().NotBeNull();
        _draftPrompt.Should().NotBe(_improvePrompt);
    }

    [Then("Critique should not equal Improve")]
    public void ThenCritiqueShouldNotEqualImprove()
    {
        _critiquePrompt.Should().NotBeNull();
        _improvePrompt.Should().NotBeNull();
        _critiquePrompt.Should().NotBe(_improvePrompt);
    }
}
