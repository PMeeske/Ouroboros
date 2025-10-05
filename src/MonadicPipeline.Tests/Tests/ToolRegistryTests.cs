using FluentAssertions;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Tools;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the ToolRegistry implementation.
/// Validates tool registration, retrieval, and immutability.
/// </summary>
public class ToolRegistryTests
{
    // Test tool implementation
    private class TestTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public string? JsonSchema { get; }

        public TestTool(string name, string description = "Test tool", string? jsonSchema = null)
        {
            Name = name;
            Description = description;
            JsonSchema = jsonSchema;
        }

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success($"Result: {input}"));
        }
    }

    [Fact]
    public void Constructor_CreatesEmptyRegistry()
    {
        // Arrange & Act
        var registry = new ToolRegistry();

        // Assert
        registry.Count.Should().Be(0);
        registry.All.Should().BeEmpty();
    }

    [Fact]
    public void WithTool_AddsToolToRegistry()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test-tool");

        // Act
        var newRegistry = registry.WithTool(tool);

        // Assert
        newRegistry.Count.Should().Be(1);
        newRegistry.Contains("test-tool").Should().BeTrue();
    }

    [Fact]
    public void WithTool_ReturnsNewInstance()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test-tool");

        // Act
        var newRegistry = registry.WithTool(tool);

        // Assert
        newRegistry.Should().NotBeSameAs(registry);
        registry.Count.Should().Be(0); // Original unchanged
        newRegistry.Count.Should().Be(1);
    }

    [Fact]
    public void WithTool_WithNullTool_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.WithTool(null!));
    }

    [Fact]
    public void WithTool_CanChainMultipleTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = new TestTool("tool1");
        var tool2 = new TestTool("tool2");
        var tool3 = new TestTool("tool3");

        // Act
        var newRegistry = registry
            .WithTool(tool1)
            .WithTool(tool2)
            .WithTool(tool3);

        // Assert
        newRegistry.Count.Should().Be(3);
        newRegistry.Contains("tool1").Should().BeTrue();
        newRegistry.Contains("tool2").Should().BeTrue();
        newRegistry.Contains("tool3").Should().BeTrue();
    }

    [Fact]
    public void WithTool_ReplacesExistingTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = new TestTool("my-tool", "First description");
        var tool2 = new TestTool("my-tool", "Second description");

        // Act
        var newRegistry = registry.WithTool(tool1).WithTool(tool2);

        // Assert
        newRegistry.Count.Should().Be(1);
        var retrieved = newRegistry.Get("my-tool");
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be("Second description");
    }

    [Fact]
    public void GetTool_WithExistingTool_ReturnsSome()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test-tool");
        var newRegistry = registry.WithTool(tool);

        // Act
        var option = newRegistry.GetTool("test-tool");

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().BeSameAs(tool);
    }

    [Fact]
    public void GetTool_WithNonExistentTool_ReturnsNone()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var option = registry.GetTool("non-existent");

        // Assert
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetTool_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.GetTool(null!));
    }

    [Fact]
    public void GetTool_IsCaseInsensitive()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("Test-Tool");
        var newRegistry = registry.WithTool(tool);

        // Act
        var option1 = newRegistry.GetTool("test-tool");
        var option2 = newRegistry.GetTool("TEST-TOOL");
        var option3 = newRegistry.GetTool("Test-Tool");

        // Assert
        option1.HasValue.Should().BeTrue();
        option2.HasValue.Should().BeTrue();
        option3.HasValue.Should().BeTrue();
        option1.Value.Should().BeSameAs(tool);
        option2.Value.Should().BeSameAs(tool);
        option3.Value.Should().BeSameAs(tool);
    }

    [Fact]
    public void Get_WithExistingTool_ReturnsTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test-tool");
        var newRegistry = registry.WithTool(tool);

        // Act
        var retrieved = newRegistry.Get("test-tool");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().BeSameAs(tool);
    }

    [Fact]
    public void Get_WithNonExistentTool_ReturnsNull()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var retrieved = registry.Get("non-existent");

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public void Contains_WithExistingTool_ReturnsTrue()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test-tool");
        var newRegistry = registry.WithTool(tool);

        // Act
        var contains = newRegistry.Contains("test-tool");

        // Assert
        contains.Should().BeTrue();
    }

    [Fact]
    public void Contains_WithNonExistentTool_ReturnsFalse()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var contains = registry.Contains("non-existent");

        // Assert
        contains.Should().BeFalse();
    }

    [Fact]
    public void All_ReturnsAllRegisteredTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = new TestTool("tool1");
        var tool2 = new TestTool("tool2");
        var tool3 = new TestTool("tool3");

        // Act
        var newRegistry = registry
            .WithTool(tool1)
            .WithTool(tool2)
            .WithTool(tool3);

        // Assert
        newRegistry.All.Should().HaveCount(3);
        newRegistry.All.Should().Contain(tool1);
        newRegistry.All.Should().Contain(tool2);
        newRegistry.All.Should().Contain(tool3);
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        registry.Count.Should().Be(0);

        var registry1 = registry.WithTool(new TestTool("tool1"));
        registry1.Count.Should().Be(1);

        var registry2 = registry1.WithTool(new TestTool("tool2"));
        registry2.Count.Should().Be(2);

        var registry3 = registry2.WithTool(new TestTool("tool3"));
        registry3.Count.Should().Be(3);
    }

    [Fact]
    public void SafeExportSchemas_WithTools_ReturnsSuccessWithJson()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = new TestTool("tool1", "First tool", "{\"type\": \"object\"}");
        var tool2 = new TestTool("tool2", "Second tool", null);
        var newRegistry = registry.WithTool(tool1).WithTool(tool2);

        // Act
        var result = newRegistry.SafeExportSchemas();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("tool1");
        result.Value.Should().Contain("tool2");
        result.Value.Should().Contain("First tool");
        result.Value.Should().Contain("Second tool");
    }

    [Fact]
    public void SafeExportSchemas_WithEmptyRegistry_ReturnsSuccessWithEmptyArray()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var result = registry.SafeExportSchemas();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("[]");
    }

    [Fact]
    public void Register_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("test-tool");

        // Act & Assert
        #pragma warning disable CS0618 // Type or member is obsolete
        var exception = Assert.Throws<InvalidOperationException>(() => registry.Register(tool));
        #pragma warning restore CS0618
        exception.Message.Should().Contain("WithTool");
    }

    [Theory]
    [InlineData("tool-1")]
    [InlineData("TOOL-1")]
    [InlineData("Tool-1")]
    public void GetTool_WithDifferentCasing_ReturnsCorrectTool(string searchName)
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new TestTool("tool-1");
        var newRegistry = registry.WithTool(tool);

        // Act
        var option = newRegistry.GetTool(searchName);

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().BeSameAs(tool);
    }

    [Fact]
    public void ToolRegistry_MaintainsImmutability()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = new TestTool("tool1");
        var tool2 = new TestTool("tool2");

        // Act
        var registry1 = registry.WithTool(tool1);
        var registry2 = registry1.WithTool(tool2);

        // Assert
        registry.Count.Should().Be(0);
        registry1.Count.Should().Be(1);
        registry2.Count.Should().Be(2);

        // Original registry unchanged
        registry.Contains("tool1").Should().BeFalse();
        registry.Contains("tool2").Should().BeFalse();

        // First update has only tool1
        registry1.Contains("tool1").Should().BeTrue();
        registry1.Contains("tool2").Should().BeFalse();

        // Second update has both
        registry2.Contains("tool1").Should().BeTrue();
        registry2.Contains("tool2").Should().BeTrue();
    }
}
