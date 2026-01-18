namespace Ouroboros.Tests.UnitTests;

/// <summary>
/// Tests for ToolAwareChatModel covering tool invocation and error handling.
/// </summary>
[Trait("Category", "Unit")]
public class ToolAwareChatModelTests
{
    private class MockChatModel : IChatCompletionModel
    {
        private readonly string response;
        private readonly bool shouldCheckCancellation;

        public MockChatModel(string response, bool shouldCheckCancellation = false)
        {
            this.response = response;
            this.shouldCheckCancellation = shouldCheckCancellation;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            if (shouldCheckCancellation)
            {
                ct.ThrowIfCancellationRequested();
            }

            return Task.FromResult(this.response);
        }
    }

    [Fact]
    public async Task GenerateWithToolsAsync_NoTools_ReturnsTextOnly()
    {
        // Arrange
        var mockModel = new MockChatModel("This is a plain response without tools.");
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test prompt");

        // Assert
        text.Should().Be("This is a plain response without tools.");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithMathTool_ExecutesTool()
    {
        // Arrange
        var mockModel = new MockChatModel("Let me calculate: [TOOL:math 2+2]");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("Calculate 2+2");

        // Assert
        text.Should().NotContain("[TOOL:math 2+2]"); // Tool invocation should be replaced
        text.Should().Contain("[TOOL-RESULT:math] 4");
        text.Should().Contain("Let me calculate:");
        tools.Should().HaveCount(1);
        tools[0].ToolName.Should().Be("math");
        tools[0].Arguments.Should().Be("2+2");
        tools[0].Output.Should().Be("4");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_ToolNotFound_ReturnsError()
    {
        // Arrange
        var mockModel = new MockChatModel("[TOOL:nonexistent some args]");
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("[TOOL-RESULT:nonexistent] error: tool not found");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_MultipleTools_ExecutesAll()
    {
        // Arrange
        var mockModel = new MockChatModel(
            "First: [TOOL:math 5*3]\nThen: [TOOL:math 10-2]");
        var registry = new ToolRegistry();
        registry = registry.WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(2);
        tools[0].ToolName.Should().Be("math");
        tools[0].Output.Should().Be("15");
        tools[1].ToolName.Should().Be("math");
        tools[1].Output.Should().Be("8");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_ToolWithNoArgs_HandlesGracefully()
    {
        // Arrange
        var mockModel = new MockChatModel("[TOOL:math]");
        var registry = new ToolRegistry();
        registry = registry.WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("[TOOL-RESULT:math]");
        tools.Should().HaveCount(1);
    }

    [Fact]
    public async Task GenerateWithToolsResultAsync_Success_ReturnsSuccessResult()
    {
        // Arrange
        var mockModel = new MockChatModel("Response: [TOOL:math 7+8]");
        var registry = new ToolRegistry();
        registry = registry.WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var result = await toolAwareModel.GenerateWithToolsResultAsync("test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var (text, tools) = result.Value;
        text.Should().Contain("[TOOL-RESULT:math] 15");
        tools.Should().HaveCount(1);
    }

    [Fact]
    public async Task GenerateWithToolsResultAsync_ModelThrows_ReturnsFailure()
    {
        // Arrange
        var mockModel = new ThrowingChatModel();
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var result = await toolAwareModel.GenerateWithToolsResultAsync("test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Tool-aware generation failed");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_ToolThrows_CapturesError()
    {
        // Arrange
        var mockModel = new MockChatModel("[TOOL:throwing_tool test]");
        var registry = new ToolRegistry();
        registry = registry.WithTool(new ThrowingTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("[TOOL-RESULT:throwing_tool] error:");
        tools.Should().HaveCount(1);
        tools[0].Output.Should().Contain("error:");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_CancellationRequested_PropagatesToken()
    {
        // Arrange - use a mock that actually checks cancellation
        var mockModel = new MockChatModel("[TOOL:math 1+1]", shouldCheckCancellation: true);
        var registry = new ToolRegistry();
        registry = registry.WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await toolAwareModel.GenerateWithToolsAsync("test", cts.Token);
        });
    }

    private class ThrowingChatModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Model error");
        }
    }

    private class ThrowingTool : ITool
    {
        public string Name => "throwing_tool";
        public string Description => "A tool that throws";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Tool error");
        }
    }
}
