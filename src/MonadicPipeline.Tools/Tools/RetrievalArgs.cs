namespace LangChainPipeline.Tools;

/// <summary>
/// Arguments for semantic search retrieval operations.
/// </summary>
public sealed class RetrievalArgs
{
    /// <summary>
    /// The query string for semantic search.
    /// </summary>
    public string Q { get; set; } = "";

    /// <summary>
    /// Number of documents to retrieve (default: 3).
    /// </summary>
    public int K { get; set; } = 3;
}
