// <copyright file="PromptsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Pipeline.Reasoning;
using Xunit;

/// <summary>
/// Tests for the Prompts static class.
/// </summary>
[Trait("Category", "Unit")]
public class PromptsTests
{
    [Fact]
    public void Draft_IsNotNull()
    {
        // Act
        var draft = Prompts.Draft;

        // Assert
        draft.Should().NotBeNull();
    }

    [Fact]
    public void Draft_Format_ReplacesPlaceholders()
    {
        // Arrange
        var draft = Prompts.Draft;

        // Act
        var formatted = draft.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "test-schema" },
            { "context", "test-context" },
            { "topic", "test-topic" },
        });

        // Assert
        formatted.Should().Contain("test-schema");
        formatted.Should().Contain("test-context");
        formatted.Should().Contain("test-topic");
        formatted.Should().Contain("THOUGHTS");
        formatted.Should().Contain("DRAFT_SPEC");
    }

    [Fact]
    public void Critique_IsNotNull()
    {
        // Act
        var critique = Prompts.Critique;

        // Assert
        critique.Should().NotBeNull();
    }

    [Fact]
    public void Critique_Format_ReplacesPlaceholders()
    {
        // Arrange
        var critique = Prompts.Critique;

        // Act
        var formatted = critique.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "schema" },
            { "context", "context" },
            { "draft", "draft-text" },
            { "topic", "topic" },
        });

        // Assert
        formatted.Should().Contain("schema");
        formatted.Should().Contain("context");
        formatted.Should().Contain("draft-text");
        formatted.Should().Contain("topic");
        formatted.Should().Contain("MAJOR_GAPS");
        formatted.Should().Contain("MINOR_ISSUES");
    }

    [Fact]
    public void Improve_IsNotNull()
    {
        // Act
        var improve = Prompts.Improve;

        // Assert
        improve.Should().NotBeNull();
    }

    [Fact]
    public void Improve_Format_ReplacesPlaceholders()
    {
        // Arrange
        var improve = Prompts.Improve;

        // Act
        var formatted = improve.Format(new Dictionary<string, string>
        {
            { "tools_schemas", "schema" },
            { "context", "context" },
            { "draft", "draft" },
            { "critique", "critique-text" },
            { "topic", "topic" },
        });

        // Assert
        formatted.Should().Contain("schema");
        formatted.Should().Contain("context");
        formatted.Should().Contain("draft");
        formatted.Should().Contain("critique-text");
        formatted.Should().Contain("topic");
        formatted.Should().Contain("FINAL_SPEC");
        formatted.Should().Contain("CHANGELOG");
    }

    [Fact]
    public void AllPrompts_AreDistinct()
    {
        // Act
        var draft = Prompts.Draft;
        var critique = Prompts.Critique;
        var improve = Prompts.Improve;

        // Assert
        draft.Should().NotBeSameAs(critique);
        critique.Should().NotBeSameAs(improve);
        improve.Should().NotBeSameAs(draft);
    }
}
