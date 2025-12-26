// <copyright file="AskRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.WebApi.Models;

/// <summary>
/// Request model for asking questions to the AI pipeline.
/// </summary>
public sealed record AskRequest
{
    /// <summary>
    /// Gets the question or prompt to ask.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Gets a value indicating whether enable retrieval augmented generation (RAG).
    /// </summary>
    public bool UseRag { get; init; }

    /// <summary>
    /// Gets source path for RAG context (defaults to current directory).
    /// </summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Gets model name to use (defaults to llama3).
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets a value indicating whether enable agent mode with tool usage.
    /// </summary>
    public bool Agent { get; init; }

    /// <summary>
    /// Gets temperature for response generation (0.0 - 1.0).
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Gets maximum tokens for response.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Gets remote endpoint URL (e.g., https://api.ollama.com).
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets aPI key for remote endpoint.
    /// </summary>
    public string? ApiKey { get; init; }
}
