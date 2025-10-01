namespace LangChainPipeline.Domain.Vectors;

public sealed class SerializableVector
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
