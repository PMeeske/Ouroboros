namespace LangChainPipeline.WebApi.Models;

/// <summary>
/// Request model for executing pipelines
/// </summary>
public sealed record PipelineRequest
{
    /// <summary>
    /// DSL expression for pipeline execution (e.g., "SetTopic('AI') | UseDraft | UseCritique")
    /// </summary>
    public required string Dsl { get; init; }

    /// <summary>
    /// Model name to use (defaults to llama3)
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Enable debug output
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Temperature for response generation
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Maximum tokens for response
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Remote endpoint URL
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// API key for remote endpoint
    /// </summary>
    public string? ApiKey { get; init; }
}
