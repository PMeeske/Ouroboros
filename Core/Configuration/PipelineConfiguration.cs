namespace LangChainPipeline.Core.Configuration;

/// <summary>
/// Configuration settings for the MonadicPipeline system.
/// </summary>
public sealed class PipelineConfiguration
{
    /// <summary>
    /// Gets or sets the Ollama endpoint URL for LLM operations.
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets the maximum number of conversation turns to keep in memory.
    /// </summary>
    public int MaxTurns { get; set; } = 10;

    /// <summary>
    /// Gets or sets the batch size for vector store operations.
    /// </summary>
    public int VectorStoreBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the default number of similar documents to retrieve.
    /// </summary>
    public int DefaultSimilarDocumentCount { get; set; } = 8;

    /// <summary>
    /// Gets or sets the default embedding model name.
    /// </summary>
    public string DefaultEmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Gets or sets the default chat model name.
    /// </summary>
    public string DefaultChatModel { get; set; } = "llama3.2";

    /// <summary>
    /// Gets or sets whether to enable verbose logging for pipeline operations.
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout in seconds for LLM operations.
    /// </summary>
    public int LlmTimeoutSeconds { get; set; } = 120;
}