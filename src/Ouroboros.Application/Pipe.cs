#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Ouroboros.Application.Interop;

/// <summary>
/// Provides static methods for creating pipe operations.
/// </summary>
public static class Pipe
{
    /// <summary>
    /// Creates a new pipe starting with the specified value.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <typeparam name="TR">The result type.</typeparam>
    /// <param name="value">The starting value.</param>
    /// <returns>A new pipe with the specified starting value.</returns>
    public static Pipe<T, TR> Start<T, TR>(T value) => new(value);

    // ============================================================================
    // LangChain-style static helpers for pipeline composition
    // These mirror the original LangChain.Chains.Chain static methods
    // ============================================================================

    /// <summary>
    /// Creates a Set step that sets a value in the pipeline state.
    /// Mirrors LangChain's Chain.Set() operator.
    /// </summary>
    /// <param name="value">The value to set</param>
    /// <param name="key">Optional key name (defaults to setting prompt)</param>
    /// <returns>A CLI pipeline step</returns>
    public static Step<CliPipelineState, CliPipelineState> Set(string value, string? key = null)
    {
        if (key == null || key == "text" || key == "prompt")
            return CliSteps.SetPrompt($"'{value}'");
        else if (key == "topic")
            return CliSteps.SetTopic($"'{value}'");
        else if (key == "query")
            return CliSteps.SetQuery($"'{value}'");
        else
            return CliSteps.SetPrompt($"'{value}'");
    }

    /// <summary>
    /// Creates a RetrieveSimilarDocuments step.
    /// Mirrors LangChain's Chain.RetrieveSimilarDocuments() operator.
    /// </summary>
    /// <param name="amount">Number of documents to retrieve</param>
    /// <returns>A CLI pipeline step</returns>
    public static Step<CliPipelineState, CliPipelineState> RetrieveSimilarDocuments(int amount = 5)
        => CliSteps.LangChainRetrieveStep($"amount={amount}");

    /// <summary>
    /// Creates a CombineDocuments step.
    /// Mirrors LangChain's Chain.CombineDocuments() operator.
    /// </summary>
    /// <param name="separator">Optional separator between documents</param>
    /// <returns>A CLI pipeline step</returns>
    public static Step<CliPipelineState, CliPipelineState> CombineDocuments(string? separator = null)
    {
        if (separator == null)
            return CliSteps.LangChainCombineStep();
        else
            return CliSteps.CombineDocuments($"separator={separator}");
    }

    /// <summary>
    /// Creates a Template step that applies a prompt template.
    /// Mirrors LangChain's Chain.Template() operator.
    /// </summary>
    /// <param name="template">The prompt template with {placeholders}</param>
    /// <returns>A CLI pipeline step</returns>
    public static Step<CliPipelineState, CliPipelineState> Template(string template)
        => CliSteps.LangChainTemplateStep($"'{template}'");

    /// <summary>
    /// Creates an LLM step that sends the prompt to the language model.
    /// Mirrors LangChain's Chain.LLM() operator.
    /// </summary>
    /// <returns>A CLI pipeline step</returns>
    public static Step<CliPipelineState, CliPipelineState> LLM()
        => CliSteps.LangChainLlmStep();
}

/// <summary>
/// Represents a pipe that can transform values through a series of operations.
/// </summary>
/// <typeparam name="T">The current value type.</typeparam>
/// <typeparam name="TR">The target result type.</typeparam>
/// <param name="value">The current value in the pipe.</param>
public readonly struct Pipe<T, TR>(T value)
{
    /// <summary>
    /// Gets the current value in the pipe.
    /// </summary>
    public readonly T Value = value;

    /// <summary>
    /// Pipes the current value into a pure function.
    /// </summary>
    /// <param name="x">The current pipe.</param>
    /// <param name="f">The transformation function.</param>
    /// <returns>A new pipe with the transformed value.</returns>
    public static Pipe<TR, TR> operator |(Pipe<T, TR> x, Func<T, TR> f)
        => new(f(x.Value));

    /// <summary>
    /// Pipes the current value into a Kleisli arrow (async step).
    /// </summary>
    /// <param name="x">The current pipe.</param>
    /// <param name="f">The async transformation step.</param>
    /// <returns>A task representing the transformed result.</returns>
    public static Task<TR> operator |(Pipe<T, TR> x, Step<T, TR> f)
        => f(x.Value);

    /// <summary>
    /// Implicitly converts the pipe back to its contained value.
    /// </summary>
    /// <param name="x">The pipe to convert.</param>
    /// <returns>The value contained in the pipe.</returns>
    public static implicit operator T(Pipe<T, TR> x) => x.Value;
}

