// <copyright file="PipelineRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.WebApi.Models;

/// <summary>
/// Request model for executing pipelines.
/// </summary>
public sealed record PipelineRequest
{
    /// <summary>
    /// Gets dSL expression for pipeline execution (e.g., "SetTopic('AI') | UseDraft | UseCritique").
    /// </summary>
    public required string Dsl { get; init; }

    /// <summary>
    /// Gets model name to use (defaults to llama3).
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets a value indicating whether enable debug output.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Gets temperature for response generation.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Gets maximum tokens for response.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Gets remote endpoint URL.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets aPI key for remote endpoint.
    /// </summary>
    public string? ApiKey { get; init; }
}
