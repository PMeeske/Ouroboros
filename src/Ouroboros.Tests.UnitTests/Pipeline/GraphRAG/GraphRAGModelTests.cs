// <copyright file="GraphRAGModelTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Pipeline.GraphRAG;

using FluentAssertions;
using Ouroboros.Pipeline.GraphRAG.Models;
using Xunit;

/// <summary>
/// Tests for GraphRAG models: Entity, Relationship, and KnowledgeGraph.
/// </summary>
[Trait("Category", "Unit")]
public class GraphRAGModelTests
{
    #region Entity Tests

    [Fact]
    public void Entity_Constructor_SetsProperties()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["age"] = 30 };

        // Act
        var entity = new Entity("e1", "Person", "John Doe", properties);

        // Assert
        entity.Id.Should().Be("e1");
        entity.Type.Should().Be("Person");
        entity.Name.Should().Be("John Doe");
        entity.Properties.Should().ContainKey("age");
        entity.Properties["age"].Should().Be(30);
    }

    [Fact]
    public void Entity_Create_CreatesWithEmptyProperties()
    {
        // Act
        var entity = Entity.Create("e1", "Concept", "AI");

        // Assert
        entity.Id.Should().Be("e1");
        entity.Type.Should().Be("Concept");
        entity.Name.Should().Be("AI");
        entity.Properties.Should().BeEmpty();
    }

    [Fact]
    public void Entity_WithProperty_AddsNewProperty()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "Jane");

        // Act
        var updated = entity.WithProperty("role", "Engineer");

        // Assert
        updated.Properties.Should().ContainKey("role");
        updated.Properties["role"].Should().Be("Engineer");
        entity.Properties.Should().NotContainKey("role"); // Original unchanged
    }

    [Fact]
    public void Entity_WithProperty_UpdatesExistingProperty()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "Jane").WithProperty("level", 1);

        // Act
        var updated = entity.WithProperty("level", 2);

        // Assert
        updated.Properties["level"].Should().Be(2);
        entity.Properties["level"].Should().Be(1); // Original unchanged
    }

    [Fact]
    public void Entity_VectorStoreId_CanBeSet()
    {
        // Arrange
        var entity = Entity.Create("e1", "Document", "Report");

        // Act
        var withVectorId = entity with { VectorStoreId = "vec-123" };

        // Assert
        withVectorId.VectorStoreId.Should().Be("vec-123");
        entity.VectorStoreId.Should().BeNull(); // Original unchanged
    }

    [Fact]
    public void Entity_Equality_SameValues_AreEqual()
    {
        // Arrange
        var props = new Dictionary<string, object>();
        var entity1 = new Entity("e1", "Person", "John", props);
        var entity2 = new Entity("e1", "Person", "John", props);

        // Assert
        entity1.Should().Be(entity2);
    }

    [Fact]
    public void Entity_Equality_DifferentId_AreNotEqual()
    {
        // Arrange
        var props = new Dictionary<string, object>();
        var entity1 = new Entity("e1", "Person", "John", props);
        var entity2 = new Entity("e2", "Person", "John", props);

        // Assert
        entity1.Should().NotBe(entity2);
    }

    #endregion

    #region Relationship Tests

    [Fact]
    public void Relationship_Constructor_SetsProperties()
    {
        // Arrange
        var properties = new Dictionary<string, object> { ["since"] = 2020 };

        // Act
        var rel = new Relationship("r1", "WorksFor", "e1", "e2", properties);

        // Assert
        rel.Id.Should().Be("r1");
        rel.Type.Should().Be("WorksFor");
        rel.SourceEntityId.Should().Be("e1");
        rel.TargetEntityId.Should().Be("e2");
        rel.Properties["since"].Should().Be(2020);
    }

    [Fact]
    public void Relationship_Create_CreatesWithEmptyProperties()
    {
        // Act
        var rel = Relationship.Create("r1", "KnowsOf", "e1", "e2");

        // Assert
        rel.Id.Should().Be("r1");
        rel.Type.Should().Be("KnowsOf");
        rel.SourceEntityId.Should().Be("e1");
        rel.TargetEntityId.Should().Be("e2");
        rel.Properties.Should().BeEmpty();
    }

    [Fact]
    public void Relationship_DefaultWeight_IsOne()
    {
        // Act
        var rel = Relationship.Create("r1", "Related", "e1", "e2");

        // Assert
        rel.Weight.Should().Be(1.0);
    }

    [Fact]
    public void Relationship_Weight_CanBeCustomized()
    {
        // Act
        var rel = Relationship.Create("r1", "Related", "e1", "e2") with { Weight = 0.5 };

        // Assert
        rel.Weight.Should().Be(0.5);
    }

    [Fact]
    public void Relationship_IsBidirectional_DefaultsFalse()
    {
        // Act
        var rel = Relationship.Create("r1", "Related", "e1", "e2");

        // Assert
        rel.IsBidirectional.Should().BeFalse();
    }

    [Fact]
    public void Relationship_IsBidirectional_CanBeSet()
    {
        // Act
        var rel = Relationship.Create("r1", "Friends", "e1", "e2") with { IsBidirectional = true };

        // Assert
        rel.IsBidirectional.Should().BeTrue();
    }

    [Fact]
    public void Relationship_WithProperty_AddsNewProperty()
    {
        // Arrange
        var rel = Relationship.Create("r1", "WorksFor", "e1", "e2");

        // Act
        var updated = rel.WithProperty("department", "Engineering");

        // Assert
        updated.Properties.Should().ContainKey("department");
        updated.Properties["department"].Should().Be("Engineering");
    }

    [Fact]
    public void Relationship_Equality_SameValues_AreEqual()
    {
        // Arrange
        var props = new Dictionary<string, object>();
        var rel1 = new Relationship("r1", "Type", "e1", "e2", props);
        var rel2 = new Relationship("r1", "Type", "e1", "e2", props);

        // Assert
        rel1.Should().Be(rel2);
    }

    #endregion

    #region KnowledgeGraph Tests

    [Fact]
    public void KnowledgeGraph_Empty_HasNoEntitiesOrRelationships()
    {
        // Act
        var graph = KnowledgeGraph.Empty;

        // Assert
        graph.Entities.Should().BeEmpty();
        graph.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void KnowledgeGraph_Constructor_SetsCollections()
    {
        // Arrange
        var entities = new List<Entity> { Entity.Create("e1", "Person", "John") };
        var relationships = new List<Relationship> { Relationship.Create("r1", "Knows", "e1", "e2") };

        // Act
        var graph = new KnowledgeGraph(entities, relationships);

        // Assert
        graph.Entities.Should().HaveCount(1);
        graph.Relationships.Should().HaveCount(1);
    }

    [Fact]
    public void KnowledgeGraph_GetEntity_ReturnsEntityById()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "John");
        var graph = new KnowledgeGraph([entity], []);

        // Act
        var found = graph.GetEntity("e1");

        // Assert
        found.Should().NotBeNull();
        found!.Name.Should().Be("John");
    }

    [Fact]
    public void KnowledgeGraph_GetEntity_ReturnsNullForMissingId()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;

        // Act
        var found = graph.GetEntity("nonexistent");

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public void KnowledgeGraph_GetRelationships_ReturnsRelationshipsForEntity()
    {
        // Arrange
        var rel1 = Relationship.Create("r1", "Knows", "e1", "e2");
        var rel2 = Relationship.Create("r2", "WorksFor", "e1", "e3");
        var rel3 = Relationship.Create("r3", "LocatedIn", "e4", "e1");
        var rel4 = Relationship.Create("r4", "Unrelated", "e5", "e6");
        var graph = new KnowledgeGraph([], [rel1, rel2, rel3, rel4]);

        // Act
        var relationships = graph.GetRelationships("e1").ToList();

        // Assert
        relationships.Should().HaveCount(3);
        relationships.Should().Contain(rel1);
        relationships.Should().Contain(rel2);
        relationships.Should().Contain(rel3);
        relationships.Should().NotContain(rel4);
    }

    [Fact]
    public void KnowledgeGraph_GetEntitiesByType_FiltersCorrectly()
    {
        // Arrange
        var e1 = Entity.Create("e1", "Person", "John");
        var e2 = Entity.Create("e2", "Person", "Jane");
        var e3 = Entity.Create("e3", "Organization", "Acme");
        var graph = new KnowledgeGraph([e1, e2, e3], []);

        // Act
        var people = graph.GetEntitiesByType("Person").ToList();

        // Assert
        people.Should().HaveCount(2);
        people.Should().Contain(e1);
        people.Should().Contain(e2);
    }

    [Fact]
    public void KnowledgeGraph_GetEntitiesByType_IsCaseInsensitive()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "John");
        var graph = new KnowledgeGraph([entity], []);

        // Act
        var found = graph.GetEntitiesByType("PERSON").ToList();

        // Assert
        found.Should().HaveCount(1);
    }

    [Fact]
    public void KnowledgeGraph_GetRelationshipsByType_FiltersCorrectly()
    {
        // Arrange
        var r1 = Relationship.Create("r1", "WorksFor", "e1", "e2");
        var r2 = Relationship.Create("r2", "WorksFor", "e3", "e4");
        var r3 = Relationship.Create("r3", "Knows", "e1", "e3");
        var graph = new KnowledgeGraph([], [r1, r2, r3]);

        // Act
        var worksFor = graph.GetRelationshipsByType("WorksFor").ToList();

        // Assert
        worksFor.Should().HaveCount(2);
        worksFor.Should().Contain(r1);
        worksFor.Should().Contain(r2);
    }

    [Fact]
    public void KnowledgeGraph_WithEntity_AddsEntity()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;
        var entity = Entity.Create("e1", "Person", "John");

        // Act
        var updated = graph.WithEntity(entity);

        // Assert
        updated.Entities.Should().HaveCount(1);
        updated.Entities[0].Name.Should().Be("John");
        graph.Entities.Should().BeEmpty(); // Original unchanged
    }

    [Fact]
    public void KnowledgeGraph_WithRelationship_AddsRelationship()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;
        var relationship = Relationship.Create("r1", "Knows", "e1", "e2");

        // Act
        var updated = graph.WithRelationship(relationship);

        // Assert
        updated.Relationships.Should().HaveCount(1);
        updated.Relationships[0].Type.Should().Be("Knows");
        graph.Relationships.Should().BeEmpty(); // Original unchanged
    }

    [Fact]
    public void KnowledgeGraph_Merge_CombinesGraphs()
    {
        // Arrange
        var e1 = Entity.Create("e1", "Person", "John");
        var e2 = Entity.Create("e2", "Person", "Jane");
        var r1 = Relationship.Create("r1", "Knows", "e1", "e2");

        var graph1 = new KnowledgeGraph([e1], [r1]);
        var graph2 = new KnowledgeGraph([e2], []);

        // Act
        var merged = graph1.Merge(graph2);

        // Assert
        merged.Entities.Should().HaveCount(2);
        merged.Relationships.Should().HaveCount(1);
    }

    [Fact]
    public void KnowledgeGraph_Merge_DeduplicatesById()
    {
        // Arrange
        var e1 = Entity.Create("e1", "Person", "John");
        var e1Duplicate = Entity.Create("e1", "Person", "John Updated"); // Same ID

        var graph1 = new KnowledgeGraph([e1], []);
        var graph2 = new KnowledgeGraph([e1Duplicate], []);

        // Act
        var merged = graph1.Merge(graph2);

        // Assert
        merged.Entities.Should().HaveCount(1);
        merged.Entities[0].Name.Should().Be("John"); // First one kept
    }

    [Fact]
    public void KnowledgeGraph_Traverse_ReturnsSubgraph()
    {
        // Arrange
        var e1 = Entity.Create("e1", "Person", "John");
        var e2 = Entity.Create("e2", "Person", "Jane");
        var e3 = Entity.Create("e3", "Organization", "Acme");
        var e4 = Entity.Create("e4", "Person", "Bob"); // Disconnected

        var r1 = Relationship.Create("r1", "Knows", "e1", "e2");
        var r2 = Relationship.Create("r2", "WorksFor", "e2", "e3");

        var graph = new KnowledgeGraph([e1, e2, e3, e4], [r1, r2]);

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 2);

        // Assert
        subgraph.Entities.Should().HaveCount(3);
        subgraph.Entities.Select(e => e.Id).Should().Contain("e1");
        subgraph.Entities.Select(e => e.Id).Should().Contain("e2");
        subgraph.Entities.Select(e => e.Id).Should().Contain("e3");
        subgraph.Entities.Select(e => e.Id).Should().NotContain("e4");
    }

    [Fact]
    public void KnowledgeGraph_Traverse_RespectsMaxHops()
    {
        // Arrange
        var e1 = Entity.Create("e1", "A", "A");
        var e2 = Entity.Create("e2", "B", "B");
        var e3 = Entity.Create("e3", "C", "C");
        var e4 = Entity.Create("e4", "D", "D");

        var r1 = Relationship.Create("r1", "R", "e1", "e2"); // 1 hop
        var r2 = Relationship.Create("r2", "R", "e2", "e3"); // 2 hops
        var r3 = Relationship.Create("r3", "R", "e3", "e4"); // 3 hops

        var graph = new KnowledgeGraph([e1, e2, e3, e4], [r1, r2, r3]);

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 1);

        // Assert
        subgraph.Entities.Should().HaveCount(2); // e1 and e2 only
        subgraph.Entities.Select(e => e.Id).Should().Contain("e1");
        subgraph.Entities.Select(e => e.Id).Should().Contain("e2");
        subgraph.Entities.Select(e => e.Id).Should().NotContain("e3");
    }

    [Fact]
    public void KnowledgeGraph_Traverse_HandlesCircularReferences()
    {
        // Arrange
        var e1 = Entity.Create("e1", "A", "A");
        var e2 = Entity.Create("e2", "B", "B");

        var r1 = Relationship.Create("r1", "R", "e1", "e2");
        var r2 = Relationship.Create("r2", "R", "e2", "e1"); // Circular

        var graph = new KnowledgeGraph([e1, e2], [r1, r2]);

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 10);

        // Assert
        subgraph.Entities.Should().HaveCount(2); // Should not loop infinitely
    }

    [Fact]
    public void KnowledgeGraph_Traverse_FromNonexistentEntity_ReturnsEmptySubgraph()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;

        // Act
        var subgraph = graph.Traverse("nonexistent");

        // Assert
        subgraph.Entities.Should().BeEmpty();
    }

    #endregion
}
