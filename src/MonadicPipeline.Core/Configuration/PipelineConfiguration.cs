namespace LangChainPipeline.Core.Configuration;

/// <summary>
/// Main configuration for the MonadicPipeline system.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Pipeline";

    /// <summary>
    /// LLM provider configuration.
    /// </summary>
    public LlmProviderConfiguration LlmProvider { get; set; } = new();

    /// <summary>
    /// Vector store configuration.
    /// </summary>
    public VectorStoreConfiguration VectorStore { get; set; } = new();

    /// <summary>
    /// Pipeline execution configuration.
    /// </summary>
    public ExecutionConfiguration Execution { get; set; } = new();

    /// <summary>
    /// Observability and logging configuration.
    /// </summary>
    public ObservabilityConfiguration Observability { get; set; } = new();
}

/// <summary>
/// Configuration for LLM providers (Ollama, OpenAI, etc.).
/// </summary>
public class LlmProviderConfiguration
{
    /// <summary>
    /// The default provider to use (e.g., "Ollama", "OpenAI").
    /// </summary>
    public string DefaultProvider { get; set; } = "Ollama";

    /// <summary>
    /// Ollama endpoint URL.
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Default model name for chat operations.
    /// </summary>
    public string DefaultChatModel { get; set; } = "llama3";

    /// <summary>
    /// Default model name for embeddings.
    /// </summary>
    public string DefaultEmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// OpenAI API key (if using OpenAI provider).
    /// </summary>
    public string? OpenAiApiKey { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Configuration for vector store operations.
/// </summary>
public class VectorStoreConfiguration
{
    /// <summary>
    /// The type of vector store to use ("InMemory", "Qdrant", "Pinecone", etc.).
    /// </summary>
    public string Type { get; set; } = "InMemory";

    /// <summary>
    /// Connection string for external vector stores.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Batch size for vector operations.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Default collection/index name.
    /// </summary>
    public string DefaultCollection { get; set; } = "pipeline_vectors";
}

/// <summary>
/// Configuration for pipeline execution.
/// </summary>
public class ExecutionConfiguration
{
    /// <summary>
    /// Maximum turns for iterative reasoning.
    /// </summary>
    public int MaxTurns { get; set; } = 5;

    /// <summary>
    /// Maximum parallel tool executions.
    /// </summary>
    public int MaxParallelToolExecutions { get; set; } = 5;

    /// <summary>
    /// Enable detailed debugging output.
    /// </summary>
    public bool EnableDebugOutput { get; set; } = false;

    /// <summary>
    /// Tool execution timeout in seconds.
    /// </summary>
    public int ToolExecutionTimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Configuration for observability (logging, metrics, tracing).
/// </summary>
public class ObservabilityConfiguration
{
    /// <summary>
    /// Enable structured logging.
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Minimum log level (e.g., "Debug", "Information", "Warning", "Error").
    /// </summary>
    public string MinimumLogLevel { get; set; } = "Information";

    /// <summary>
    /// Enable metrics collection.
    /// </summary>
    public bool EnableMetrics { get; set; } = false;

    /// <summary>
    /// Metrics export format (e.g., "Prometheus", "ApplicationInsights").
    /// </summary>
    public string MetricsExportFormat { get; set; } = "Prometheus";

    /// <summary>
    /// Metrics export endpoint (e.g., "/metrics" for Prometheus scraping).
    /// </summary>
    public string? MetricsExportEndpoint { get; set; } = "/metrics";

    /// <summary>
    /// Enable distributed tracing.
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// Tracing service name.
    /// </summary>
    public string TracingServiceName { get; set; } = "MonadicPipeline";

    /// <summary>
    /// OpenTelemetry endpoint for trace export (e.g., Jaeger, Zipkin).
    /// </summary>
    public string? OpenTelemetryEndpoint { get; set; }

    /// <summary>
    /// Application Insights connection string.
    /// </summary>
    public string? ApplicationInsightsConnectionString { get; set; }
}
