// <copyright file="Relationship.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents a relationship between two entities in the knowledge graph.
/// </summary>
/// <param name="Id">Unique identifier for the relationship.</param>
/// <param name="Type">The type of relationship (e.g., "WorksFor", "LocatedIn", "HasSkill").</param>
/// <param name="SourceEntityId">The ID of the source entity.</param>
/// <param name="TargetEntityId">The ID of the target entity.</param>
/// <param name="Properties">Additional properties of the relationship.</param>
public sealed record Relationship(
    string Id,
    string Type,
    string SourceEntityId,
    string TargetEntityId,
    IReadOnlyDictionary<string, object> Properties)
{
    /// <summary>
    /// Gets the weight/strength of the relationship (0.0 to 1.0).
    /// </summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// Gets whether this is a bidirectional relationship.
    /// </summary>
    public bool IsBidirectional { get; init; }

    /// <summary>
    /// Creates a relationship with minimal properties.
    /// </summary>
    /// <param name="id">Relationship identifier.</param>
    /// <param name="type">Relationship type.</param>
    /// <param name="sourceId">Source entity ID.</param>
    /// <param name="targetId">Target entity ID.</param>
    /// <returns>A new Relationship instance.</returns>
    public static Relationship Create(string id, string type, string sourceId, string targetId) =>
        new(id, type, sourceId, targetId, new Dictionary<string, object>());

    /// <summary>
    /// Creates a copy with additional properties.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Property value.</param>
    /// <returns>A new Relationship with the added property.</returns>
    public Relationship WithProperty(string key, object value)
    {
        var newProps = new Dictionary<string, object>(Properties)
        {
            [key] = value
        };
        return this with { Properties = newProps };
    }
}
