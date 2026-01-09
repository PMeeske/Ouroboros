// <copyright file="EpisodicVectorCompatibility.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Memory;

/// <summary>
/// Compatibility layer for Vector types used in episodic memory.
/// </summary>
public static class EpisodicVectorCompatibility
{
    /// <summary>
    /// Creates a Vector from serializable data.
    /// </summary>
    public static Vector Create(string id, string text, float[] embedding, IDictionary<string, object> metadata)
    {
        // Use reflection to create Vector instance
        var vectorType = typeof(Vector);
        var constructor = vectorType.GetConstructor(new[] { typeof(string), typeof(string), typeof(float[]), typeof(IDictionary<string, object>) });
        
        if (constructor != null)
        {
            return (Vector)constructor.Invoke(new object[] { id, text, embedding, metadata ?? new Dictionary<string, object>() });
        }
        
        // Fallback: create simple wrapper
        return new EpisodicVectorWrapper(id, text, embedding, metadata);
    }
}

/// <summary>
/// Fallback Vector wrapper for compatibility.
/// </summary>
public class EpisodicVectorWrapper : Vector
{
    public EpisodicVectorWrapper(string id, string text, float[] embedding, IDictionary<string, object> metadata)
    {
        // Initialize base properties
        this.Id = id;
        this.Text = text;
        this.Embedding = embedding;
        this.Metadata = metadata ?? new Dictionary<string, object>();
    }
}

/// <summary>
/// Base Vector class that matches IVectorStore expectations.
/// </summary>
public abstract class Vector
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Interface documentation for the IVectorStore-compatible Vector type.
/// </summary>
public static class VectorExtensions
{
    /// <summary>
    /// Creates a Vector from episodic data.
    /// </summary>
    public static Vector FromEpisode(Episode episode)
    {
        return EpisodicVectorCompatibility.Create(
            episode.Id.Value.ToString(),
            episode.Goal,
            episode.Embedding,
            new Dictionary<string, object>
            {
                ["episode_data"] = episode,
                ["timestamp"] = episode.Timestamp,
                ["success_score"] = episode.SuccessScore
            });
    }
}