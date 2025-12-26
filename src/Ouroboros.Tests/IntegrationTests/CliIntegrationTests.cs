using Ouroboros.Tests;

namespace Ouroboros.Tests.IntegrationTests;

public class CliIntegrationTests
{
    [Fact]
    public async Task RunCliEndToEndTests()
    {
        await CliEndToEndTests.RunAllTests();
    }
}
