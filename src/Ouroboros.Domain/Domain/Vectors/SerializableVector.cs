namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// Serializable representation of a vector with associated text and metadata.
/// Used for persistence and transfer of vector store data.
/// </summary>
public sealed class SerializableVector
{
    /// <summary>
    /// Unique identifier for this vector
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// Text content associated with this vector
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Optional metadata key-value pairs
    /// </summary>
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// The embedding vector as an array of floats
    /// </summary>
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
