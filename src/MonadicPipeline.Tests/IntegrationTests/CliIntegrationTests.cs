using Xunit;
using LangChainPipeline.Tests;

namespace MonadicPipeline.Tests.IntegrationTests;

public class CliIntegrationTests
{
    [Fact]
    public async Task RunCliEndToEndTests()
    {
        await CliEndToEndTests.RunAllTests();
    }
}
