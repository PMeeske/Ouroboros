namespace Ouroboros.Tests.IntegrationTests;

/// <summary>
/// xUnit wrapper for GitHub Models integration tests.
/// </summary>
[Trait("Category", "Integration")]
public class GitHubModelsTests
{
    /// <summary>
    /// Runs all GitHub Models integration tests.
    /// </summary>
    [Fact]
    public async Task RunGitHubModelsIntegrationTests()
    {
        await GitHubModelsIntegrationTests.RunAllTests();
    }
}
