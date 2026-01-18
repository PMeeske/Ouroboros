// <copyright file="MonadNodeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Domain.States;
using Ouroboros.Network;
using Xunit;

/// <summary>
/// Tests for the MonadNode implementation.
/// Validates node creation, hashing, and serialization.
/// </summary>
[Trait("Category", "Unit")]
public class MonadNodeTests
{
    [Fact]
    public void FromReasoningState_CreatesNodeWithCorrectType()
    {
        // Arrange
        var draft = new Draft("Test draft content");

        // Act
        var node = MonadNode.FromReasoningState(draft);

        // Assert
        node.Should().NotBeNull();
        node.TypeName.Should().Be("Draft");
        node.PayloadJson.Should().Contain("Test draft content");
        node.ParentIds.Should().BeEmpty();
    }

    [Fact]
    public void FromReasoningState_WithParents_IncludesParentIds()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var parentIds = ImmutableArray.Create(parentId);
        var critique = new Critique("Test critique");

        // Act
        var node = MonadNode.FromReasoningState(critique, parentIds);

        // Assert
        node.ParentIds.Should().HaveCount(1);
        node.ParentIds[0].Should().Be(parentId);
    }

    [Fact]
    public void FromPayload_CreatesNodeWithGenericType()
    {
        // Arrange
        var payload = new { Message = "Hello", Count = 42 };

        // Act
        var node = MonadNode.FromPayload("TestType", payload);

        // Assert
        node.TypeName.Should().Be("TestType");
        node.PayloadJson.Should().Contain("Hello");
        node.PayloadJson.Should().Contain("42");
    }

    [Fact]
    public void DeserializePayload_ReturnsCorrectValue()
    {
        // Arrange
        var draft = new Draft("Test content");
        var node = MonadNode.FromReasoningState(draft);

        // Act
        var result = node.DeserializePayload<Draft>();

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.DraftText.Should().Be("Test content");
    }

    [Fact]
    public void VerifyHash_OnValidNode_ReturnsTrue()
    {
        // Arrange
        var node = MonadNode.FromPayload("Test", new { Value = 1 });

        // Act
        var isValid = node.VerifyHash();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var node1 = new MonadNode(id, "Test", "{\"value\":1}", timestamp, ImmutableArray<Guid>.Empty);
        var node2 = new MonadNode(id, "Test", "{\"value\":1}", timestamp, ImmutableArray<Guid>.Empty);

        // Act & Assert
        node1.Hash.Should().Be(node2.Hash);
    }

    [Fact]
    public void Hash_ChangesWithDifferentContent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var node1 = new MonadNode(id, "Test", "{\"value\":1}", timestamp, ImmutableArray<Guid>.Empty);
        var node2 = new MonadNode(id, "Test", "{\"value\":2}", timestamp, ImmutableArray<Guid>.Empty);

        // Act & Assert
        node1.Hash.Should().NotBe(node2.Hash);
    }
}
