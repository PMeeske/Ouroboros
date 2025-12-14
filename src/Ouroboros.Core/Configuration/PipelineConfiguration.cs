// <copyright file="PipelineConfiguration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Configuration;

/// <summary>
/// Main configuration for the Ouroboros system.
/// </summary>
public class PipelineConfiguration
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Pipeline";

    /// <summary>
    /// Gets or sets lLM provider configuration.
    /// </summary>
    public LlmProviderConfiguration LlmProvider { get; set; } = new();

    /// <summary>
    /// Gets or sets vector store configuration.
    /// </summary>
    public VectorStoreConfiguration VectorStore { get; set; } = new();

    /// <summary>
    /// Gets or sets pipeline execution configuration.
    /// </summary>
    public ExecutionConfiguration Execution { get; set; } = new();

    /// <summary>
    /// Gets or sets observability and logging configuration.
    /// </summary>
    public ObservabilityConfiguration Observability { get; set; } = new();

    /// <summary>
    /// Gets or sets feature flags for evolutionary metacognitive control.
    /// </summary>
    public FeatureFlags Features { get; set; } = new();
}

/// <summary>
/// Configuration for LLM providers (Ollama, OpenAI, etc.).
/// </summary>
public class LlmProviderConfiguration
{
    /// <summary>
    /// Gets or sets the default provider to use (e.g., "Ollama", "OpenAI").
    /// </summary>
    public string DefaultProvider { get; set; } = "Ollama";

    /// <summary>
    /// Gets or sets ollama endpoint URL.
    /// </summary>
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Gets or sets default model name for chat operations.
    /// </summary>
    public string DefaultChatModel { get; set; } = "deepseek-v3.1:671b-cloud";

    /// <summary>
    /// Gets or sets default model name for embeddings.
    /// </summary>
    public string DefaultEmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Gets or sets openAI API key (if using OpenAI provider).
    /// </summary>
    public string? OpenAiApiKey { get; set; }

    /// <summary>
    /// Gets or sets request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Configuration for vector store operations.
/// </summary>
public class VectorStoreConfiguration
{
    /// <summary>
    /// Gets or sets the type of vector store to use ("InMemory", "Qdrant", "Pinecone", etc.).
    /// </summary>
    public string Type { get; set; } = "InMemory";

    /// <summary>
    /// Gets or sets connection string for external vector stores.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets batch size for vector operations.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets default collection/index name.
    /// </summary>
    public string DefaultCollection { get; set; } = "pipeline_vectors";
}

/// <summary>
/// Configuration for pipeline execution.
/// </summary>
public class ExecutionConfiguration
{
    /// <summary>
    /// Gets or sets maximum turns for iterative reasoning.
    /// </summary>
    public int MaxTurns { get; set; } = 5;

    /// <summary>
    /// Gets or sets maximum parallel tool executions.
    /// </summary>
    public int MaxParallelToolExecutions { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether enable detailed debugging output.
    /// </summary>
    public bool EnableDebugOutput { get; set; } = false;

    /// <summary>
    /// Gets or sets tool execution timeout in seconds.
    /// </summary>
    public int ToolExecutionTimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Configuration for observability (logging, metrics, tracing).
/// </summary>
public class ObservabilityConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether enable structured logging.
    /// </summary>
    public bool EnableStructuredLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets minimum log level (e.g., "Debug", "Information", "Warning", "Error").
    /// </summary>
    public string MinimumLogLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets a value indicating whether enable metrics collection.
    /// </summary>
    public bool EnableMetrics { get; set; } = false;

    /// <summary>
    /// Gets or sets metrics export format (e.g., "Prometheus", "ApplicationInsights").
    /// </summary>
    public string MetricsExportFormat { get; set; } = "Prometheus";

    /// <summary>
    /// Gets or sets metrics export endpoint (e.g., "/metrics" for Prometheus scraping).
    /// </summary>
    public string? MetricsExportEndpoint { get; set; } = "/metrics";

    /// <summary>
    /// Gets or sets a value indicating whether enable distributed tracing.
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// Gets or sets tracing service name.
    /// </summary>
    public string TracingServiceName { get; set; } = "Ouroboros";

    /// <summary>
    /// Gets or sets openTelemetry endpoint for trace export (e.g., Jaeger, Zipkin).
    /// </summary>
    public string? OpenTelemetryEndpoint { get; set; }

    /// <summary>
    /// Gets or sets application Insights connection string.
    /// </summary>
    public string? ApplicationInsightsConnectionString { get; set; }
}
