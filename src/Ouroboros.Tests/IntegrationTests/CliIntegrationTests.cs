using Ouroboros.Tests;

namespace Ouroboros.Tests.IntegrationTests;

[Trait("Category", "Integration")]
public class CliIntegrationTests
{
    [Fact]
    public async Task RunCliEndToEndTests()
    {
        await CliEndToEndTests.RunAllTests();
    }
}
