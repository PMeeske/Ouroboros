namespace LangChainPipeline.WebApi.Models;

/// <summary>
/// Request model for asking questions to the AI pipeline
/// </summary>
public sealed record AskRequest
{
    /// <summary>
    /// The question or prompt to ask
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Enable retrieval augmented generation (RAG)
    /// </summary>
    public bool UseRag { get; init; }

    /// <summary>
    /// Source path for RAG context (defaults to current directory)
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Model name to use (defaults to llama3)
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Enable agent mode with tool usage
    /// </summary>
    public bool Agent { get; init; }

    /// <summary>
    /// Temperature for response generation (0.0 - 1.0)
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Maximum tokens for response
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Remote endpoint URL (e.g., https://api.ollama.com)
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// API key for remote endpoint
    /// </summary>
    public string? ApiKey { get; init; }
}
