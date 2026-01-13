// <copyright file="PromptsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Pipeline;

using FluentAssertions;
using Ouroboros.Pipeline.Reasoning;
using Xunit;

/// <summary>
/// Tests for the Prompts static class containing predefined prompt templates.
/// </summary>
[Trait("Category", "Unit")]
public class PromptsTests
{
    #region Thinking Prompt Tests

    [Fact]
    public void Thinking_ContainsToolsSchemasPlaceholder()
    {
        // Act
        var template = Prompts.Thinking;

        // Assert
        template.ToString().Should().Contain("{tools_schemas}");
    }

    [Fact]
    public void Thinking_ContainsContextPlaceholder()
    {
        // Act
        var template = Prompts.Thinking;

        // Assert
        template.ToString().Should().Contain("{context}");
    }

    [Fact]
    public void Thinking_ContainsTopicPlaceholder()
    {
        // Act
        var template = Prompts.Thinking;

        // Assert
        template.ToString().Should().Contain("{topic}");
    }

    [Fact]
    public void Thinking_FormatWithAllVariables_ReplacesPlaceholders()
    {
        // Arrange
        var vars = new Dictionary<string, string>
        {
            ["tools_schemas"] = "[{\"name\": \"search\"}]",
            ["context"] = "Background information",
            ["topic"] = "Functional Programming"
        };

        // Act
        var result = Prompts.Thinking.Format(vars);

        // Assert
        result.Should().Contain("[{\"name\": \"search\"}]");
        result.Should().Contain("Background information");
        result.Should().Contain("Functional Programming");
        result.Should().NotContain("{tools_schemas}");
        result.Should().NotContain("{context}");
        result.Should().NotContain("{topic}");
    }

    [Fact]
    public void Thinking_RequiredVariables_ContainsExpectedPlaceholders()
    {
        // Act
        var required = Prompts.Thinking.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("context");
        required.Should().Contain("topic");
    }

    #endregion

    #region Draft Prompt Tests

    [Fact]
    public void Draft_ContainsToolsSchemasPlaceholder()
    {
        // Act
        var template = Prompts.Draft;

        // Assert
        template.ToString().Should().Contain("{tools_schemas}");
    }

    [Fact]
    public void Draft_ContainsContextPlaceholder()
    {
        // Act
        var template = Prompts.Draft;

        // Assert
        template.ToString().Should().Contain("{context}");
    }

    [Fact]
    public void Draft_ContainsTopicPlaceholder()
    {
        // Act
        var template = Prompts.Draft;

        // Assert
        template.ToString().Should().Contain("{topic}");
    }

    [Fact]
    public void Draft_FormatWithAllVariables_ReplacesPlaceholders()
    {
        // Arrange
        var vars = new Dictionary<string, string>
        {
            ["tools_schemas"] = "{}",
            ["context"] = "Some context",
            ["topic"] = "AI Architecture"
        };

        // Act
        var result = Prompts.Draft.Format(vars);

        // Assert
        result.Should().Contain("AI Architecture");
        result.Should().NotContain("{topic}");
    }

    [Fact]
    public void Draft_MentionsThoughtsSection()
    {
        // Act
        var template = Prompts.Draft;

        // Assert
        template.ToString().Should().Contain("[THOUGHTS]");
    }

    [Fact]
    public void Draft_MentionsDraftSpecSection()
    {
        // Act
        var template = Prompts.Draft;

        // Assert
        template.ToString().Should().Contain("[DRAFT_SPEC]");
    }

    #endregion

    #region Critique Prompt Tests

    [Fact]
    public void Critique_ContainsAllRequiredPlaceholders()
    {
        // Act
        var template = Prompts.Critique;
        var required = template.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("context");
        required.Should().Contain("draft");
        required.Should().Contain("topic");
    }

    [Fact]
    public void Critique_ContainsDraftPlaceholder()
    {
        // Act
        var template = Prompts.Critique;

        // Assert
        template.ToString().Should().Contain("{draft}");
    }

    [Fact]
    public void Critique_FormatWithAllVariables_ReplacesPlaceholders()
    {
        // Arrange
        var vars = new Dictionary<string, string>
        {
            ["tools_schemas"] = "[]",
            ["context"] = "Context here",
            ["draft"] = "This is the draft to critique",
            ["topic"] = "Error Handling"
        };

        // Act
        var result = Prompts.Critique.Format(vars);

        // Assert
        result.Should().Contain("This is the draft to critique");
        result.Should().Contain("Error Handling");
        result.Should().NotContain("{draft}");
    }

    [Fact]
    public void Critique_MentionsMajorGaps()
    {
        // Act
        var template = Prompts.Critique;

        // Assert
        template.ToString().Should().Contain("[MAJOR_GAPS]");
    }

    [Fact]
    public void Critique_MentionsMinorIssues()
    {
        // Act
        var template = Prompts.Critique;

        // Assert
        template.ToString().Should().Contain("[MINOR_ISSUES]");
    }

    [Fact]
    public void Critique_MentionsUnsupportedClaims()
    {
        // Act
        var template = Prompts.Critique;

        // Assert
        template.ToString().Should().Contain("[UNSUPPORTED_CLAIMS]");
    }

    #endregion

    #region Improve Prompt Tests

    [Fact]
    public void Improve_ContainsAllRequiredPlaceholders()
    {
        // Act
        var template = Prompts.Improve;
        var required = template.RequiredVariables;

        // Assert
        required.Should().Contain("tools_schemas");
        required.Should().Contain("context");
        required.Should().Contain("draft");
        required.Should().Contain("critique");
        required.Should().Contain("topic");
    }

    [Fact]
    public void Improve_ContainsCritiquePlaceholder()
    {
        // Act
        var template = Prompts.Improve;

        // Assert
        template.ToString().Should().Contain("{critique}");
    }

    [Fact]
    public void Improve_FormatWithAllVariables_ReplacesPlaceholders()
    {
        // Arrange
        var vars = new Dictionary<string, string>
        {
            ["tools_schemas"] = "[]",
            ["context"] = "Context",
            ["draft"] = "Original draft",
            ["critique"] = "Needs more examples",
            ["topic"] = "Testing"
        };

        // Act
        var result = Prompts.Improve.Format(vars);

        // Assert
        result.Should().Contain("Original draft");
        result.Should().Contain("Needs more examples");
        result.Should().Contain("Testing");
        result.Should().NotContain("{critique}");
    }

    [Fact]
    public void Improve_MentionsFinalSpec()
    {
        // Act
        var template = Prompts.Improve;

        // Assert
        template.ToString().Should().Contain("FINAL_SPEC");
    }

    [Fact]
    public void Improve_MentionsChangelog()
    {
        // Act
        var template = Prompts.Improve;

        // Assert
        template.ToString().Should().Contain("[CHANGELOG]");
    }

    [Fact]
    public void Improve_MentionsExamples()
    {
        // Act
        var template = Prompts.Improve;

        // Assert
        template.ToString().Should().Contain("examples");
    }

    [Fact]
    public void Improve_MentionsMigrationRisks()
    {
        // Act
        var template = Prompts.Improve;

        // Assert
        template.ToString().Should().Contain("migration");
    }

    #endregion

    #region All Prompts Integration Tests

    [Fact]
    public void AllPrompts_AreNotNull()
    {
        // Assert
        Prompts.Thinking.Should().NotBeNull();
        Prompts.Draft.Should().NotBeNull();
        Prompts.Critique.Should().NotBeNull();
        Prompts.Improve.Should().NotBeNull();
    }

    [Fact]
    public void AllPrompts_ContainToolSchemasPlaceholder()
    {
        // Assert
        Prompts.Thinking.ToString().Should().Contain("{tools_schemas}");
        Prompts.Draft.ToString().Should().Contain("{tools_schemas}");
        Prompts.Critique.ToString().Should().Contain("{tools_schemas}");
        Prompts.Improve.ToString().Should().Contain("{tools_schemas}");
    }

    [Fact]
    public void AllPrompts_ContainContextPlaceholder()
    {
        // Assert
        Prompts.Thinking.ToString().Should().Contain("{context}");
        Prompts.Draft.ToString().Should().Contain("{context}");
        Prompts.Critique.ToString().Should().Contain("{context}");
        Prompts.Improve.ToString().Should().Contain("{context}");
    }

    [Fact]
    public void AllPrompts_ContainTopicPlaceholder()
    {
        // Assert
        Prompts.Thinking.ToString().Should().Contain("{topic}");
        Prompts.Draft.ToString().Should().Contain("{topic}");
        Prompts.Critique.ToString().Should().Contain("{topic}");
        Prompts.Improve.ToString().Should().Contain("{topic}");
    }

    [Fact]
    public void Prompts_CanBeUsedInSequence()
    {
        // Arrange - Simulate a reasoning pipeline
        var baseVars = new Dictionary<string, string>
        {
            ["tools_schemas"] = "[{\"name\": \"search\"}]",
            ["context"] = "Building a web application",
            ["topic"] = "Authentication System"
        };

        // Act - Step 1: Draft
        var draftResult = Prompts.Draft.Format(baseVars);
        draftResult.Should().NotBeNullOrEmpty();

        // Act - Step 2: Critique (requires draft)
        var critiqueVars = new Dictionary<string, string>(baseVars)
        {
            ["draft"] = "Initial authentication design..."
        };
        var critiqueResult = Prompts.Critique.Format(critiqueVars);
        critiqueResult.Should().NotBeNullOrEmpty();

        // Act - Step 3: Improve (requires draft and critique)
        var improveVars = new Dictionary<string, string>(critiqueVars)
        {
            ["critique"] = "Missing MFA considerations..."
        };
        var improveResult = Prompts.Improve.Format(improveVars);
        improveResult.Should().NotBeNullOrEmpty();
    }

    #endregion
}
