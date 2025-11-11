// <copyright file="ApiResponse.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.WebApi.Models;

/// <summary>
/// Generic response wrapper for API endpoints.
/// </summary>
public sealed record ApiResponse<T>
{
    /// <summary>
    /// Gets a value indicating whether indicates if the request was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets response data (null if request failed).
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Gets error message (null if request succeeded).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets execution time in milliseconds.
    /// </summary>
    public long? ExecutionTimeMs { get; init; }

    public static ApiResponse<T> Ok(T data, long? executionTimeMs = null) =>
        new() { Success = true, Data = data, ExecutionTimeMs = executionTimeMs };

    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Response model for ask endpoint.
/// </summary>
public sealed record AskResponse
{
    public required string Answer { get; init; }

    public string? Model { get; init; }
}

/// <summary>
/// Response model for pipeline endpoint.
/// </summary>
public sealed record PipelineResponse
{
    public required string Result { get; init; }

    public string? FinalState { get; init; }
}
