namespace Ouroboros.Tests;

public class StreamingReasoningXUnitTests
{
    [Fact]
    public async Task RunStreamingReasoningTests()
    {
        await StreamingReasoningTests.RunAllTests();
    }
}
