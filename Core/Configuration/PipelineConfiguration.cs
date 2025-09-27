namespace LangChainPipeline.Core.Configuration;

/// <summary>
/// Configuration settings for the MonadicPipeline system.
/// Provides structured configuration management instead of hard-coded values.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Configuration section name for appsettings.json.
    /// </summary>
    public const string SectionName = "MonadicPipeline";

    /// <summary>
    /// Ollama LLM endpoint URL.
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model name for chat operations.
    /// </summary>
    public string ChatModel { get; set; } = "llama3.2";

    /// <summary>
    /// Model name for embedding operations.
    /// </summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Maximum number of conversation turns to keep in memory.
    /// </summary>
    public int MaxTurns { get; set; } = 10;

    /// <summary>
    /// Batch size for vector store operations.
    /// </summary>
    public int VectorStoreBatchSize { get; set; } = 100;

    /// <summary>
    /// Number of similar documents to retrieve for RAG operations.
    /// </summary>
    public int RetrievalAmount { get; set; } = 8;

    /// <summary>
    /// Timeout for LLM operations in seconds.
    /// </summary>
    public int LlmTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Enable detailed logging for pipeline operations.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Path for persistent storage (if using file-based vector store).
    /// </summary>
    public string? PersistentStoragePath { get; set; }

    /// <summary>
    /// Tool execution settings.
    /// </summary>
    public ToolConfiguration Tools { get; set; } = new();
}

/// <summary>
/// Configuration settings for tool execution.
/// </summary>
public class ToolConfiguration
{
    /// <summary>
    /// Maximum time allowed for tool execution in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable tool security validation.
    /// </summary>
    public bool EnableSecurityValidation { get; set; } = true;

    /// <summary>
    /// Maximum number of tool calls per pipeline execution.
    /// </summary>
    public int MaxToolCallsPerExecution { get; set; } = 10;
}