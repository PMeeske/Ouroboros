// <copyright file="KnowledgeGraphTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

/// <summary>
/// Tests for KnowledgeGraph and related models.
/// </summary>
[Trait("Category", "Unit")]
public class KnowledgeGraphTests
{
    [Fact]
    public void Empty_ShouldCreateEmptyGraph()
    {
        // Act
        var graph = KnowledgeGraph.Empty;

        // Assert
        graph.Entities.Should().BeEmpty();
        graph.Relationships.Should().BeEmpty();
    }

    [Fact]
    public void WithEntity_ShouldAddEntityToGraph()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty;
        var entity = Entity.Create("e1", "Person", "John Doe");

        // Act
        var newGraph = graph.WithEntity(entity);

        // Assert
        newGraph.Entities.Should().HaveCount(1);
        newGraph.GetEntity("e1").Should().NotBeNull();
        newGraph.GetEntity("e1")!.Name.Should().Be("John Doe");
        // Original graph should be unchanged (immutability)
        graph.Entities.Should().BeEmpty();
    }

    [Fact]
    public void WithRelationship_ShouldAddRelationshipToGraph()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e1", "Person", "John"))
            .WithEntity(Entity.Create("e2", "Organization", "Acme Corp"));
        var relationship = Relationship.Create("r1", "WorksFor", "e1", "e2");

        // Act
        var newGraph = graph.WithRelationship(relationship);

        // Assert
        newGraph.Relationships.Should().HaveCount(1);
        newGraph.GetRelationships("e1").Should().HaveCount(1);
    }

    [Fact]
    public void GetEntitiesByType_ShouldFilterByType()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e1", "Person", "John"))
            .WithEntity(Entity.Create("e2", "Person", "Jane"))
            .WithEntity(Entity.Create("e3", "Organization", "Acme"));

        // Act
        var people = graph.GetEntitiesByType("Person").ToList();

        // Assert
        people.Should().HaveCount(2);
        people.Should().AllSatisfy(e => e.Type.Should().Be("Person"));
    }

    [Fact]
    public void GetRelationshipsByType_ShouldFilterByType()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e1", "Person", "John"))
            .WithEntity(Entity.Create("e2", "Organization", "Acme"))
            .WithRelationship(Relationship.Create("r1", "WorksFor", "e1", "e2"))
            .WithRelationship(Relationship.Create("r2", "LocatedIn", "e2", "e3"));

        // Act
        var worksForRels = graph.GetRelationshipsByType("WorksFor").ToList();

        // Assert
        worksForRels.Should().HaveCount(1);
        worksForRels.First().Type.Should().Be("WorksFor");
    }

    [Fact]
    public void Merge_ShouldCombineGraphs()
    {
        // Arrange
        var graph1 = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e1", "Person", "John"));
        var graph2 = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e2", "Person", "Jane"));

        // Act
        var merged = graph1.Merge(graph2);

        // Assert
        merged.Entities.Should().HaveCount(2);
        merged.GetEntity("e1").Should().NotBeNull();
        merged.GetEntity("e2").Should().NotBeNull();
    }

    [Fact]
    public void Merge_ShouldNotDuplicateEntities()
    {
        // Arrange
        var entity = Entity.Create("e1", "Person", "John");
        var graph1 = KnowledgeGraph.Empty.WithEntity(entity);
        var graph2 = KnowledgeGraph.Empty.WithEntity(entity);

        // Act
        var merged = graph1.Merge(graph2);

        // Assert
        merged.Entities.Should().HaveCount(1);
    }

    [Fact]
    public void Traverse_ShouldReturnSubgraph()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e1", "Person", "John"))
            .WithEntity(Entity.Create("e2", "Organization", "Acme"))
            .WithEntity(Entity.Create("e3", "Location", "New York"))
            .WithRelationship(Relationship.Create("r1", "WorksFor", "e1", "e2"))
            .WithRelationship(Relationship.Create("r2", "LocatedIn", "e2", "e3"));

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 1);

        // Assert
        subgraph.Entities.Should().Contain(e => e.Id == "e1");
        subgraph.Entities.Should().Contain(e => e.Id == "e2");
        subgraph.Entities.Should().NotContain(e => e.Id == "e3"); // Too far
    }

    [Fact]
    public void Traverse_WithMoreHops_ShouldIncludeMoreEntities()
    {
        // Arrange
        var graph = KnowledgeGraph.Empty
            .WithEntity(Entity.Create("e1", "Person", "John"))
            .WithEntity(Entity.Create("e2", "Organization", "Acme"))
            .WithEntity(Entity.Create("e3", "Location", "New York"))
            .WithRelationship(Relationship.Create("r1", "WorksFor", "e1", "e2"))
            .WithRelationship(Relationship.Create("r2", "LocatedIn", "e2", "e3"));

        // Act
        var subgraph = graph.Traverse("e1", maxHops: 2);

        // Assert
        subgraph.Entities.Should().HaveCount(3);
        subgraph.Entities.Should().Contain(e => e.Id == "e3");
    }
}
