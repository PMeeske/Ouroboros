// <copyright file="Entity.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.GraphRAG.Models;

/// <summary>
/// Represents an entity in the knowledge graph.
/// </summary>
/// <param name="Id">Unique identifier for the entity.</param>
/// <param name="Type">The type/category of the entity (e.g., "Person", "Organization", "Concept").</param>
/// <param name="Name">The display name of the entity.</param>
/// <param name="Properties">Additional properties associated with the entity.</param>
public sealed record Entity(
    string Id,
    string Type,
    string Name,
    IReadOnlyDictionary<string, object> Properties)
{
    /// <summary>
    /// Gets the vector store ID for cross-referencing with embeddings.
    /// </summary>
    public string? VectorStoreId { get; init; }

    /// <summary>
    /// Creates an entity with minimal properties.
    /// </summary>
    /// <param name="id">Entity identifier.</param>
    /// <param name="type">Entity type.</param>
    /// <param name="name">Entity name.</param>
    /// <returns>A new Entity instance.</returns>
    public static Entity Create(string id, string type, string name) =>
        new(id, type, name, new Dictionary<string, object>());

    /// <summary>
    /// Creates a copy with additional properties.
    /// </summary>
    /// <param name="key">Property key.</param>
    /// <param name="value">Property value.</param>
    /// <returns>A new Entity with the added property.</returns>
    public Entity WithProperty(string key, object value)
    {
        var newProps = new Dictionary<string, object>(Properties)
        {
            [key] = value
        };
        return this with { Properties = newProps };
    }
}
