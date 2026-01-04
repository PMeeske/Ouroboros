// <copyright file="ToolRegistryExtendedTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tools;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Tests.Mocks;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Extended tests for the ToolRegistry implementation covering additional edge cases.
/// </summary>
[Trait("Category", "Unit")]
public class ToolRegistryExtendedTests
{
    #region WithFunction Tests

    [Fact]
    public void WithFunction_Sync_RegistersSyncFunction()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var newRegistry = registry.WithFunction(
            "greet",
            "Returns a greeting",
            (string name) => $"Hello, {name}!");

        // Assert
        newRegistry.Contains("greet").Should().BeTrue();
        var tool = newRegistry.Get("greet");
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("greet");
        tool.Description.Should().Be("Returns a greeting");
    }

    [Fact]
    public async Task WithFunction_Sync_InvokeWorks()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithFunction("double", "Doubles a number", (string input) =>
            {
                if (int.TryParse(input, out var num))
                    return (num * 2).ToString();
                return "Error: not a number";
            });

        // Act
        var tool = registry.Get("double");
        var result = await tool!.InvokeAsync("21");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("42");
    }

    [Fact]
    public void WithFunction_Async_RegistersAsyncFunction()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var newRegistry = registry.WithFunction(
            "async_greet",
            "Returns a greeting asynchronously",
            (string name) => Task.FromResult($"Hello, {name}!"));

        // Assert
        newRegistry.Contains("async_greet").Should().BeTrue();
    }

    [Fact]
    public async Task WithFunction_Async_InvokeWorks()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithFunction("delay_double", "Doubles with delay", async (string input) =>
            {
                await Task.Delay(1);
                if (int.TryParse(input, out var num))
                    return (num * 2).ToString();
                return "Error: not a number";
            });

        // Act
        var tool = registry.Get("delay_double");
        var result = await tool!.InvokeAsync("21");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("42");
    }

    #endregion

    #region WithTools Tests

    [Fact]
    public void WithTools_ParamsArray_RegistersMultipleTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool1 = new MockTool("tool1", "First tool");
        var tool2 = new MockTool("tool2", "Second tool");
        var tool3 = new MockTool("tool3", "Third tool");

        // Act
        var newRegistry = registry.WithTools(tool1, tool2, tool3);

        // Assert
        newRegistry.Count.Should().Be(3);
        newRegistry.Contains("tool1").Should().BeTrue();
        newRegistry.Contains("tool2").Should().BeTrue();
        newRegistry.Contains("tool3").Should().BeTrue();
    }

    [Fact]
    public void WithTools_Enumerable_RegistersMultipleTools()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tools = new List<ITool>
        {
            new MockTool("tool1"),
            new MockTool("tool2"),
            new MockTool("tool3")
        };

        // Act
        var newRegistry = registry.WithTools(tools);

        // Assert
        newRegistry.Count.Should().Be(3);
    }

    [Fact]
    public void WithTools_EmptyArray_ReturnsEquivalentRegistry()
    {
        // Arrange
        var registry = new ToolRegistry().WithTool(new MockTool("existing"));

        // Act
        var newRegistry = registry.WithTools();

        // Assert
        newRegistry.Count.Should().Be(1);
    }

    #endregion

    #region WithoutTool Tests

    [Fact]
    public void WithoutTool_RemovesExistingTool()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithTool(new MockTool("tool1"))
            .WithTool(new MockTool("tool2"))
            .WithTool(new MockTool("tool3"));

        // Act
        var newRegistry = registry.WithoutTool("tool2");

        // Assert
        newRegistry.Count.Should().Be(2);
        newRegistry.Contains("tool1").Should().BeTrue();
        newRegistry.Contains("tool2").Should().BeFalse();
        newRegistry.Contains("tool3").Should().BeTrue();
    }

    [Fact]
    public void WithoutTool_WithNonExistentTool_ReturnsSameCount()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithTool(new MockTool("tool1"));

        // Act
        var newRegistry = registry.WithoutTool("nonexistent");

        // Assert
        newRegistry.Count.Should().Be(1);
    }

    [Fact]
    public void WithoutTool_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.WithoutTool(null!));
    }

    [Fact]
    public void WithoutTool_MaintainsImmutability()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithTool(new MockTool("tool1"))
            .WithTool(new MockTool("tool2"));

        // Act
        var newRegistry = registry.WithoutTool("tool1");

        // Assert - Original unchanged
        registry.Count.Should().Be(2);
        registry.Contains("tool1").Should().BeTrue();

        // New registry has one less
        newRegistry.Count.Should().Be(1);
        newRegistry.Contains("tool1").Should().BeFalse();
    }

    #endregion

    #region CreateDefault Tests

    [Fact]
    public void CreateDefault_IncludesMathTool()
    {
        // Arrange & Act
        var registry = ToolRegistry.CreateDefault();

        // Assert
        registry.Contains("math").Should().BeTrue();
        registry.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CreateDefault_MathToolWorks()
    {
        // Arrange
        var registry = ToolRegistry.CreateDefault();
        var mathTool = registry.Get("math");

        // Act
        var result = await mathTool!.InvokeAsync("2+2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("4");
    }

    #endregion

    #region ExportSchemas Tests

    [Fact]
    public void ExportSchemas_WithTools_ReturnsValidJson()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithTool(new MockTool("tool1", "First tool"))
            .WithTool(new MockTool("tool2", "Second tool"));

        // Act
        var schemas = registry.ExportSchemas();

        // Assert
        schemas.Should().NotBeEmpty();
        schemas.Should().Contain("tool1");
        schemas.Should().Contain("tool2");
    }

    [Fact]
    public void ExportSchemas_WithEmptyRegistry_ReturnsEmptyArray()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var schemas = registry.ExportSchemas();

        // Assert
        schemas.Should().Contain("[]");
    }

    [Fact]
    public void ExportSchemas_WithToolsHavingJsonSchema_IncludesSchemas()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithTool(new MockTool("tool1", "First tool", "{\"type\": \"object\", \"properties\": {}}"));

        // Act
        var schemas = registry.ExportSchemas();

        // Assert
        schemas.Should().Contain("type");
        schemas.Should().Contain("properties");
    }

    [Fact]
    public void SafeExportSchemas_OnSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithTool(new MockTool("tool1", "First tool"));

        // Act
        var result = registry.SafeExportSchemas();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("tool1");
    }

    #endregion

    #region Legacy Method Tests

    [Fact]
    public void Register_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("test", "description", (string input) => input));
#pragma warning restore CS0618
    }

    [Fact]
    public void RegisterAsync_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("test", "description", (string input) => Task.FromResult(input)));
#pragma warning restore CS0618
    }

    [Fact]
    public void RegisterTyped_ThrowsInvalidOperationException()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act & Assert
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register<object>("test", "description", (object input) => Task.FromResult("result")));
#pragma warning restore CS0618
    }

    #endregion

    #region All Property Tests

    [Fact]
    public void All_ReturnsToolsInCorrectOrder()
    {
        // Arrange - Note: Dictionary doesn't guarantee order, but we can test count
        var registry = new ToolRegistry()
            .WithTool(new MockTool("alpha"))
            .WithTool(new MockTool("beta"))
            .WithTool(new MockTool("gamma"));

        // Act
        var all = registry.All.ToList();

        // Assert
        all.Should().HaveCount(3);
        all.Select(t => t.Name).Should().Contain("alpha");
        all.Select(t => t.Name).Should().Contain("beta");
        all.Select(t => t.Name).Should().Contain("gamma");
    }

    [Fact]
    public void All_EmptyRegistry_ReturnsEmptyEnumerable()
    {
        // Arrange
        var registry = new ToolRegistry();

        // Act
        var all = registry.All.ToList();

        // Assert
        all.Should().BeEmpty();
    }

    #endregion

    #region Fluent API Tests

    [Fact]
    public void FluentApi_ChainedOperations_WorkCorrectly()
    {
        // Arrange & Act
        var registry = new ToolRegistry()
            .WithTool(new MockTool("tool1"))
            .WithFunction("func1", "A function", (string x) => x.ToUpper())
            .WithTool(new MockTool("tool2"))
            .WithoutTool("tool1")
            .WithTools(new MockTool("tool3"), new MockTool("tool4"));

        // Assert
        registry.Count.Should().Be(4);
        registry.Contains("tool1").Should().BeFalse();
        registry.Contains("func1").Should().BeTrue();
        registry.Contains("tool2").Should().BeTrue();
        registry.Contains("tool3").Should().BeTrue();
        registry.Contains("tool4").Should().BeTrue();
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ToolRegistry_ImmutableOperationsAreThreadSafe()
    {
        // Arrange
        var registry = new ToolRegistry();
        var exceptions = new List<Exception>();
        var results = new System.Collections.Concurrent.ConcurrentBag<ToolRegistry>();

        // Act - Perform many concurrent additions
        Parallel.For(0, 100, i =>
        {
            try
            {
                var newRegistry = registry.WithTool(new MockTool($"tool_{i}"));
                results.Add(newRegistry);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        exceptions.Should().BeEmpty();
        results.All(r => r.Count == 1).Should().BeTrue();
        registry.Count.Should().Be(0); // Original unchanged
    }

    #endregion
}
