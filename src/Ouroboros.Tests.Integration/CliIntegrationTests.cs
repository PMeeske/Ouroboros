using Ouroboros.Tests.UnitTests;

namespace Ouroboros.Tests.Integration;

[Trait("Category", "Integration")]
public class CliIntegrationTests
{
    [Fact]
    public async Task RunCliEndToEndTests()
    {
        await CliEndToEndTests.RunAllTests();
    }
}
