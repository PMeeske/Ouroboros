// <copyright file="EntityRelationshipTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

/// <summary>
/// Tests for Entity and Relationship records.
/// </summary>
[Trait("Category", "Unit")]
public class EntityRelationshipTests
{
    [Fact]
    public void Entity_Create_ShouldCreateWithMinimalProperties()
    {
        // Act
        var entity = Entity.Create("e1", "Person", "John Doe");

        // Assert
        entity.Id.Should().Be("e1");
        entity.Type.Should().Be("Person");
        entity.Name.Should().Be("John Doe");
        entity.Properties.Should().BeEmpty();
        entity.VectorStoreId.Should().BeNull();
    }

    [Fact]
    public void Entity_WithProperty_ShouldAddProperty()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "John Doe");

        // Act
        var updated = entity.WithProperty("age", 30);

        // Assert
        updated.Properties.Should().ContainKey("age");
        updated.Properties["age"].Should().Be(30);
        // Original should be unchanged
        entity.Properties.Should().BeEmpty();
    }

    [Fact]
    public void Entity_WithVectorStoreId_ShouldSetId()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "John Doe");

        // Act
        var updated = entity with { VectorStoreId = "vec-123" };

        // Assert
        updated.VectorStoreId.Should().Be("vec-123");
    }

    [Fact]
    public void Relationship_Create_ShouldCreateWithMinimalProperties()
    {
        // Act
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        // Assert
        rel.Id.Should().Be("r1");
        rel.Type.Should().Be("WorksFor");
        rel.SourceEntityId.Should().Be("e1");
        rel.TargetEntityId.Should().Be("e2");
        rel.Properties.Should().BeEmpty();
        rel.Weight.Should().Be(1.0);
        rel.IsBidirectional.Should().BeFalse();
    }

    [Fact]
    public void Relationship_WithProperty_ShouldAddProperty()
    {
        // Arrange
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        // Act
        var updated = rel.WithProperty("since", "2020");

        // Assert
        updated.Properties.Should().ContainKey("since");
        updated.Properties["since"].Should().Be("2020");
    }

    [Fact]
    public void Relationship_WithWeight_ShouldSetWeight()
    {
        // Arrange
        var rel = Relationship.Create("r1", "Similar", "e1", "e2");

        // Act
        var updated = rel with { Weight = 0.85, IsBidirectional = true };

        // Assert
        updated.Weight.Should().Be(0.85);
        updated.IsBidirectional.Should().BeTrue();
    }
}
