using Xunit;

namespace LangChainPipeline.Tests;

public class StreamingReasoningXUnitTests
{
    [Fact]
    public async Task RunStreamingReasoningTests()
    {
        await StreamingReasoningTests.RunAllTests();
    }
}
