namespace LangChainPipeline.WebApi.Models;

/// <summary>
/// Generic response wrapper for API endpoints
/// </summary>
public sealed record ApiResponse<T>
{
    /// <summary>
    /// Indicates if the request was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Response data (null if request failed)
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// Error message (null if request succeeded)
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long? ExecutionTimeMs { get; init; }

    /// <summary>
    /// Creates a successful response with the provided data
    /// </summary>
    /// <param name="data">The response data</param>
    /// <param name="executionTimeMs">Optional execution time in milliseconds</param>
    /// <returns>A successful API response</returns>
    public static ApiResponse<T> Ok(T data, long? executionTimeMs = null) =>
        new() { Success = true, Data = data, ExecutionTimeMs = executionTimeMs };

    /// <summary>
    /// Creates a failed response with the provided error message
    /// </summary>
    /// <param name="error">The error message</param>
    /// <returns>A failed API response</returns>
    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Response model for ask endpoint
/// </summary>
public sealed record AskResponse
{
    /// <summary>
    /// The generated answer text
    /// </summary>
    public required string Answer { get; init; }

    /// <summary>
    /// The model used to generate the answer (optional)
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
/// Response model for pipeline endpoint
/// </summary>
public sealed record PipelineResponse
{
    /// <summary>
    /// The final result text from the pipeline execution
    /// </summary>
    public required string Result { get; init; }

    /// <summary>
    /// The final state of the pipeline (optional)
    /// </summary>
    public string? FinalState { get; init; }
}
