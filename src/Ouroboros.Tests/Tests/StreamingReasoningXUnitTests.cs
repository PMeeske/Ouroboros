namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class StreamingReasoningXUnitTests
{
    [Fact]
    public async Task RunStreamingReasoningTests()
    {
        await StreamingReasoningTests.RunAllTests();
    }
}
