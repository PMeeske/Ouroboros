namespace Ouroboros.Tests.Integration;

[Trait("Category", "Integration")]
public class StreamingReasoningXUnitTests
{
    [Fact]
    public async Task RunStreamingReasoningTests()
    {
        await StreamingReasoningTests.RunAllTests();
    }
}
