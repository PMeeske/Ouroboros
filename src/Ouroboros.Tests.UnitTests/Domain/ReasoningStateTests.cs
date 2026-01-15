// <copyright file="ReasoningStateTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Domain;

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Domain.States;
using Xunit;

/// <summary>
/// Tests for ReasoningState and its derived types (Draft, Critique, FinalSpec, etc.)
/// </summary>
[Trait("Category", "Unit")]
public class ReasoningStateTests
{
    #region Draft Tests

    [Fact]
    public void Draft_Constructor_SetsProperties()
    {
        // Act
        var draft = new Draft("Initial draft text");

        // Assert
        draft.DraftText.Should().Be("Initial draft text");
        draft.Kind.Should().Be("Draft");
        draft.Text.Should().Be("Initial draft text");
    }

    [Fact]
    public void Draft_WithEmptyText_CreatesInstance()
    {
        // Act
        var draft = new Draft("");

        // Assert
        draft.DraftText.Should().BeEmpty();
    }

    [Fact]
    public void Draft_Equality_SameText_AreEqual()
    {
        // Arrange
        var draft1 = new Draft("same text");
        var draft2 = new Draft("same text");

        // Assert
        draft1.Should().Be(draft2);
        (draft1 == draft2).Should().BeTrue();
    }

    [Fact]
    public void Draft_Equality_DifferentText_AreNotEqual()
    {
        // Arrange
        var draft1 = new Draft("text 1");
        var draft2 = new Draft("text 2");

        // Assert
        draft1.Should().NotBe(draft2);
        (draft1 != draft2).Should().BeTrue();
    }

    [Fact]
    public void Draft_GetHashCode_SameText_SameHash()
    {
        // Arrange
        var draft1 = new Draft("same text");
        var draft2 = new Draft("same text");

        // Assert
        draft1.GetHashCode().Should().Be(draft2.GetHashCode());
    }

    [Fact]
    public void Draft_ToString_ContainsText()
    {
        // Arrange
        var draft = new Draft("test text");

        // Act
        var str = draft.ToString();

        // Assert
        str.Should().Contain("test text");
    }

    #endregion

    #region Critique Tests

    [Fact]
    public void Critique_Constructor_SetsProperties()
    {
        // Act
        var critique = new Critique("This needs improvement");

        // Assert
        critique.CritiqueText.Should().Be("This needs improvement");
        critique.Kind.Should().Be("Critique");
        critique.Text.Should().Be("This needs improvement");
    }

    [Fact]
    public void Critique_WithMultilineText_PreservesFormatting()
    {
        // Arrange
        var multilineText = @"Issue 1: Missing error handling
Issue 2: No unit tests
Suggestion: Add try-catch blocks";

        // Act
        var critique = new Critique(multilineText);

        // Assert
        critique.CritiqueText.Should().Contain("Issue 1");
        critique.CritiqueText.Should().Contain("Suggestion");
    }

    [Fact]
    public void Critique_Equality_SameText_AreEqual()
    {
        // Arrange
        var critique1 = new Critique("feedback");
        var critique2 = new Critique("feedback");

        // Assert
        critique1.Should().Be(critique2);
    }

    [Fact]
    public void Critique_Equality_DifferentText_AreNotEqual()
    {
        // Arrange
        var critique1 = new Critique("feedback 1");
        var critique2 = new Critique("feedback 2");

        // Assert
        critique1.Should().NotBe(critique2);
    }

    #endregion

    #region FinalSpec Tests

    [Fact]
    public void FinalSpec_Constructor_SetsProperties()
    {
        // Act
        var finalSpec = new FinalSpec("Final refined text");

        // Assert
        finalSpec.FinalText.Should().Be("Final refined text");
        finalSpec.Kind.Should().Be("Final");
        finalSpec.Text.Should().Be("Final refined text");
    }

    [Fact]
    public void FinalSpec_WithComplexText_PreservesContent()
    {
        // Arrange
        var complexText = @"# Final Specification

## Overview
This is the final specification.

## Details
- Item 1
- Item 2

## Changelog
- Fixed all issues from critique";

        // Act
        var finalSpec = new FinalSpec(complexText);

        // Assert
        finalSpec.FinalText.Should().Contain("Overview");
        finalSpec.FinalText.Should().Contain("Changelog");
    }

    [Fact]
    public void FinalSpec_Equality_SameText_AreEqual()
    {
        // Arrange
        var spec1 = new FinalSpec("final");
        var spec2 = new FinalSpec("final");

        // Assert
        spec1.Should().Be(spec2);
    }

    #endregion

    #region Polymorphism Tests

    [Fact]
    public void ReasoningState_CanStoreDifferentTypes()
    {
        // Arrange
        var states = new List<ReasoningState>
        {
            new Draft("draft text"),
            new Critique("critique text"),
            new FinalSpec("final text")
        };

        // Assert
        states.Should().HaveCount(3);
        states[0].Should().BeOfType<Draft>();
        states[1].Should().BeOfType<Critique>();
        states[2].Should().BeOfType<FinalSpec>();
    }

    [Fact]
    public void ReasoningState_KindProperty_DistinguishesTypes()
    {
        // Arrange
        ReasoningState draft = new Draft("text");
        ReasoningState critique = new Critique("text");
        ReasoningState finalSpec = new FinalSpec("text");

        // Assert
        draft.Kind.Should().Be("Draft");
        critique.Kind.Should().Be("Critique");
        finalSpec.Kind.Should().Be("Final");
    }

    [Fact]
    public void ReasoningState_TextProperty_IsAccessibleFromBase()
    {
        // Arrange
        ReasoningState state = new Draft("base text access");

        // Assert
        state.Text.Should().Be("base text access");
    }

    #endregion

    #region JSON Serialization Tests

    [Fact]
    public void Draft_SerializesToJson()
    {
        // Arrange
        var draft = new Draft("draft content");

        // Act
        var json = JsonSerializer.Serialize<ReasoningState>(draft);

        // Assert
        json.Should().Contain("Draft");
        json.Should().Contain("draft content");
    }

    [Fact]
    public void Critique_SerializesToJson()
    {
        // Arrange
        var critique = new Critique("critique content");

        // Act
        var json = JsonSerializer.Serialize<ReasoningState>(critique);

        // Assert
        json.Should().Contain("Critique");
        json.Should().Contain("critique content");
    }

    [Fact]
    public void FinalSpec_SerializesToJson()
    {
        // Arrange
        var finalSpec = new FinalSpec("final content");

        // Act
        var json = JsonSerializer.Serialize<ReasoningState>(finalSpec);

        // Assert
        json.Should().Contain("Final");
        json.Should().Contain("final content");
    }

    [Fact]
    public void Draft_RoundTripsViaJson()
    {
        // Arrange
        var original = new Draft("roundtrip test");

        // Act
        var json = JsonSerializer.Serialize<ReasoningState>(original);
        var deserialized = JsonSerializer.Deserialize<ReasoningState>(json);

        // Assert
        deserialized.Should().BeOfType<Draft>();
        var asDraft = (Draft)deserialized!;
        asDraft.DraftText.Should().Be("roundtrip test");
    }

    [Fact]
    public void Critique_RoundTripsViaJson()
    {
        // Arrange
        var original = new Critique("critique roundtrip");

        // Act
        var json = JsonSerializer.Serialize<ReasoningState>(original);
        var deserialized = JsonSerializer.Deserialize<ReasoningState>(json);

        // Assert
        deserialized.Should().BeOfType<Critique>();
        var asCritique = (Critique)deserialized!;
        asCritique.CritiqueText.Should().Be("critique roundtrip");
    }

    [Fact]
    public void FinalSpec_RoundTripsViaJson()
    {
        // Arrange
        var original = new FinalSpec("final roundtrip");

        // Act
        var json = JsonSerializer.Serialize<ReasoningState>(original);
        var deserialized = JsonSerializer.Deserialize<ReasoningState>(json);

        // Assert
        deserialized.Should().BeOfType<FinalSpec>();
        var asFinalSpec = (FinalSpec)deserialized!;
        asFinalSpec.FinalText.Should().Be("final roundtrip");
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void Draft_IsImmutable()
    {
        // Arrange
        var draft = new Draft("original");

        // Act - Use with expression to create new instance
        var modified = draft with { DraftText = "modified" };

        // Assert
        draft.DraftText.Should().Be("original"); // Original unchanged
        modified.DraftText.Should().Be("modified");
        draft.Should().NotBe(modified);
    }

    [Fact]
    public void Critique_IsImmutable()
    {
        // Arrange
        var critique = new Critique("original");

        // Act
        var modified = critique with { CritiqueText = "modified" };

        // Assert
        critique.CritiqueText.Should().Be("original");
        modified.CritiqueText.Should().Be("modified");
    }

    [Fact]
    public void FinalSpec_IsImmutable()
    {
        // Arrange
        var spec = new FinalSpec("original");

        // Act
        var modified = spec with { FinalText = "modified" };

        // Assert
        spec.FinalText.Should().Be("original");
        modified.FinalText.Should().Be("modified");
    }

    #endregion
}
