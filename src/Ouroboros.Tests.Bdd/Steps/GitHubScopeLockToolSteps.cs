namespace Ouroboros.Specs.Steps;

[Binding]
[Scope(Feature = "GitHub Scope Lock Tool")]
public class GitHubScopeLockToolSteps
{
    private string? _token;
    private string? _owner;
    private string? _repo;
    private GitHubScopeLockTool? _tool;
    private string? _name;
    private string? _description;
    private string? _schema;
    private Result<string, string>? _result;
    private GitHubScopeLockArgs? _args;

    [Given(@"I have a GitHub token ""(.*)""")]
    public void GivenIHaveAGitHubToken(string token)
    {
        _token = token;
    }

    [Given(@"I have a repository owner ""(.*)""")]
    public void GivenIHaveARepositoryOwner(string owner)
    {
        _owner = owner;
    }

    [Given(@"I have a repository name ""(.*)""")]
    public void GivenIHaveARepositoryName(string repo)
    {
        _repo = repo;
    }

    [Given("I create a GitHubScopeLockTool with the given parameters")]
    [When("I create a GitHubScopeLockTool with the given parameters")]
    public void WhenICreateAGitHubScopeLockToolWithTheGivenParameters()
    {
        _tool = new GitHubScopeLockTool(_token!, _owner!, _repo!);
    }

    [When("I get the tool name")]
    public void WhenIGetTheToolName()
    {
        _name = _tool!.Name;
    }

    [When("I get the tool description")]
    public void WhenIGetTheToolDescription()
    {
        _description = _tool!.Description;
    }

    [When("I get the tool JSON schema")]
    public void WhenIGetTheToolJsonSchema()
    {
        _schema = _tool!.JsonSchema;
    }

    [When(@"I invoke the tool with ""(.*)""")]
    public async Task WhenIInvokeTheToolWith(string json)
    {
        _result = await _tool!.InvokeAsync(json);
    }

    [When(@"I create GitHubScopeLockArgs with issue number (.*) and milestone ""(.*)""")]
    public void WhenICreateGitHubScopeLockArgsWithIssueNumberAndMilestone(int issueNumber, string milestone)
    {
        _args = new GitHubScopeLockArgs
        {
            IssueNumber = issueNumber,
            Milestone = milestone
        };
    }

    [When(@"I create GitHubScopeLockArgs with issue number (.*) and no milestone")]
    public void WhenICreateGitHubScopeLockArgsWithIssueNumberAndNoMilestone(int issueNumber)
    {
        _args = new GitHubScopeLockArgs
        {
            IssueNumber = issueNumber
        };
    }

    [Then("the tool should not be null")]
    public void ThenTheToolShouldNotBeNull()
    {
        _tool.Should().NotBeNull();
    }

    [Then(@"the name should be ""(.*)""")]
    public void ThenTheNameShouldBe(string expectedName)
    {
        _name.Should().Be(expectedName);
    }

    [Then("the description should not be empty")]
    public void ThenTheDescriptionShouldNotBeEmpty()
    {
        _description.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"the description should contain ""(.*)""")]
    public void ThenTheDescriptionShouldContain(string expectedSubstring)
    {
        _description.Should().Contain(expectedSubstring);
    }

    [Then("the schema should not be empty")]
    public void ThenTheSchemaShouldNotBeEmpty()
    {
        _schema.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"the schema should contain ""(.*)""")]
    public void ThenTheSchemaShouldContain(string expectedSubstring)
    {
        _schema.Should().Contain(expectedSubstring);
    }

    [Then("the result should be a failure")]
    public void ThenTheResultShouldBeAFailure()
    {
        _result.Should().NotBeNull();
        _result!.Value.IsFailure.Should().BeTrue();
    }

    [Then(@"the error should contain ""(.*)""")]
    public void ThenTheErrorShouldContain(string expectedSubstring)
    {
        _result.Should().NotBeNull();
        _result!.Value.Error.Should().Contain(expectedSubstring);
    }

    [Then(@"the args issue number should be (.*)")]
    public void ThenTheArgsIssueNumberShouldBe(int expectedIssueNumber)
    {
        _args.Should().NotBeNull();
        _args!.IssueNumber.Should().Be(expectedIssueNumber);
    }

    [Then(@"the args milestone should be ""(.*)""")]
    public void ThenTheArgsMilestoneShouldBe(string expectedMilestone)
    {
        _args.Should().NotBeNull();
        _args!.Milestone.Should().Be(expectedMilestone);
    }

    [Then("the args milestone should be null")]
    public void ThenTheArgsMilestoneShouldBeNull()
    {
        _args.Should().NotBeNull();
        _args!.Milestone.Should().BeNull();
    }
}
