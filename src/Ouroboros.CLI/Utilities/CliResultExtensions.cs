using LangChainPipeline.CLI;
using LangChainPipeline.Core.Monads;

namespace LangChainPipeline.CLI.Utilities;

/// <summary>
/// Extension methods for Result monad in CLI context.
/// </summary>
public static class CliResultExtensions
{
    /// <summary>
    /// Transforms a CliPipelineState based on the result of an operation.
    /// </summary>
    /// <typeparam name="T">The type of the result value.</typeparam>
    /// <param name="state">The current pipeline state.</param>
    /// <param name="result">The result to process.</param>
    /// <param name="onSuccess">Function to execute if the result is successful.</param>
    /// <param name="onFailure">Function to execute if the result is a failure.</param>
    /// <returns>The transformed pipeline state.</returns>
    public static CliPipelineState WithResult<T>(
        this CliPipelineState state,
        Result<T> result,
        Func<CliPipelineState, T, CliPipelineState> onSuccess,
        Func<CliPipelineState, string, CliPipelineState> onFailure)
    {
        return result.Match(
            onSuccess: value => onSuccess(state, value),
            onFailure: error => onFailure(state, error));
    }

    /// <summary>
    /// Asynchronously matches on a Result.
    /// </summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="result">The result to match on.</param>
    /// <param name="success">Function to execute if successful.</param>
    /// <param name="failure">Function to execute if failed.</param>
    /// <returns>The result of the executed function.</returns>
    public static async Task<TResult> MatchAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, Task<TResult>> success,
        Func<string, Task<TResult>> failure)
    {
        if (result.IsSuccess)
        {
            return await success(result.Value);
        }
        else
        {
            return await failure(result.Error);
        }
    }
}
