// <copyright file="KnowledgeGraph.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a knowledge graph containing entities and their relationships.
/// </summary>
/// <param name="Entities">The collection of entities in the graph.</param>
/// <param name="Relationships">The collection of relationships between entities.</param>
public sealed record KnowledgeGraph(
    IReadOnlyList<Entity> Entities,
    IReadOnlyList<Relationship> Relationships)
{
    /// <summary>
    /// Creates an empty knowledge graph.
    /// </summary>
    public static KnowledgeGraph Empty => new([], []);

    /// <summary>
    /// Gets an entity by ID.
    /// </summary>
    /// <param name="id">The entity ID.</param>
    /// <returns>The entity if found, null otherwise.</returns>
    public Entity? GetEntity(string id) =>
        Entities.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Gets all relationships for an entity.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <returns>All relationships where the entity is source or target.</returns>
    public IEnumerable<Relationship> GetRelationships(string entityId) =>
        Relationships.Where(r => r.SourceEntityId == entityId || r.TargetEntityId == entityId);

    /// <summary>
    /// Gets entities of a specific type.
    /// </summary>
    /// <param name="type">The entity type.</param>
    /// <returns>Entities matching the type.</returns>
    public IEnumerable<Entity> GetEntitiesByType(string type) =>
        Entities.Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets relationships of a specific type.
    /// </summary>
    /// <param name="type">The relationship type.</param>
    /// <returns>Relationships matching the type.</returns>
    public IEnumerable<Relationship> GetRelationshipsByType(string type) =>
        Relationships.Where(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Adds an entity to the graph, returning a new graph.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>A new KnowledgeGraph with the entity added.</returns>
    public KnowledgeGraph WithEntity(Entity entity) =>
        new([.. Entities, entity], Relationships);

    /// <summary>
    /// Adds a relationship to the graph, returning a new graph.
    /// </summary>
    /// <param name="relationship">The relationship to add.</param>
    /// <returns>A new KnowledgeGraph with the relationship added.</returns>
    public KnowledgeGraph WithRelationship(Relationship relationship) =>
        new(Entities, [.. Relationships, relationship]);

    /// <summary>
    /// Merges another knowledge graph into this one.
    /// </summary>
    /// <param name="other">The graph to merge.</param>
    /// <returns>A new merged KnowledgeGraph.</returns>
    public KnowledgeGraph Merge(KnowledgeGraph other)
    {
        var mergedEntities = Entities
            .Concat(other.Entities.Where(e => !Entities.Any(existing => existing.Id == e.Id)))
            .ToList();

        var mergedRelationships = Relationships
            .Concat(other.Relationships.Where(r => !Relationships.Any(existing => existing.Id == r.Id)))
            .ToList();

        return new KnowledgeGraph(mergedEntities, mergedRelationships);
    }

    /// <summary>
    /// Traverses the graph from a starting entity up to a maximum number of hops.
    /// </summary>
    /// <param name="startEntityId">The starting entity ID.</param>
    /// <param name="maxHops">Maximum number of relationship hops.</param>
    /// <returns>A subgraph containing all reachable entities and relationships.</returns>
    public KnowledgeGraph Traverse(string startEntityId, int maxHops = 2)
    {
        var visitedEntities = new HashSet<string> { startEntityId };
        var resultEntities = new List<Entity>();
        var resultRelationships = new List<Relationship>();
        var frontier = new Queue<(string EntityId, int Depth)>();
        frontier.Enqueue((startEntityId, 0));

        while (frontier.Count > 0)
        {
            var (currentId, depth) = frontier.Dequeue();
            var entity = GetEntity(currentId);
            if (entity != null)
            {
                resultEntities.Add(entity);
            }

            if (depth >= maxHops)
            {
                continue;
            }

            foreach (var rel in GetRelationships(currentId))
            {
                resultRelationships.Add(rel);
                var nextId = rel.SourceEntityId == currentId ? rel.TargetEntityId : rel.SourceEntityId;
                if (visitedEntities.Add(nextId))
                {
                    frontier.Enqueue((nextId, depth + 1));
                }
            }
        }

        return new KnowledgeGraph(resultEntities, resultRelationships);
    }
}
