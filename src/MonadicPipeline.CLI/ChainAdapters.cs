#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.Abstractions.Schema;            // IChainValues
// BaseStackableChain (namespace assumption)
using LangChain.Chains.HelperChains;            // StackChain (optional)
using LangChain.Chains.StackableChains.Context; // StackableChainValues
using LangChainPipeline.Core.Steps;

namespace LangChainPipeline.CLI.Interop;

/// <summary>
/// Adapters to interoperate NuGet LangChain <c>BaseStackableChain</c> / <c>StackChain</c> with the functional <c>Step&lt;CliPipelineState,CliPipelineState&gt;</c> pipeline.
/// </summary>
public static class ChainAdapters
{
    private static readonly Dictionary<string, Action<CliPipelineState, StackableChainValues>> Export = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Prompt"] = (s, v) => v.Value["Prompt"] = s.Prompt,
        ["Query"] = (s, v) => v.Value["Query"] = s.Query,
        ["Topic"] = (s, v) => v.Value["Topic"] = s.Topic,
        ["Context"] = (s, v) => v.Value["Context"] = s.Context,
        ["Output"] = (s, v) => v.Value["Output"] = s.Output
    };

    private static readonly Dictionary<string, Action<StackableChainValues, CliPipelineState>> Import = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Prompt"] = (v, s) => s.Prompt = v.Value.TryGetValue("Prompt", out object? o) ? o?.ToString() ?? string.Empty : s.Prompt,
        ["Query"] = (v, s) => s.Query = v.Value.TryGetValue("Query", out object? o) ? o?.ToString() ?? string.Empty : s.Query,
        ["Topic"] = (v, s) => s.Topic = v.Value.TryGetValue("Topic", out object? o) ? o?.ToString() ?? string.Empty : s.Topic,
        ["Context"] = (v, s) => s.Context = v.Value.TryGetValue("Context", out object? o) ? o?.ToString() ?? string.Empty : s.Context,
        ["Output"] = (v, s) => s.Output = v.Value.TryGetValue("Output", out object? o) ? o?.ToString() ?? string.Empty : s.Output
    };

    /// <summary>
    /// Wrap a <see cref="BaseStackableChain"/> as a pipeline Step with explicit key isolation.
    /// </summary>
    /// <param name="chain">Underlying LangChain stackable chain.</param>
    /// <param name="inputKeys">State property names to export into the chain value dictionary.</param>
    /// <param name="outputKeys">Property names to import back after execution.</param>
    /// <param name="trace">Optional trace flag for console diagnostics.</param>
    public static Step<CliPipelineState, CliPipelineState> ToStep(
        this BaseStackableChain chain,
        IEnumerable<string>? inputKeys = null,
        IEnumerable<string>? outputKeys = null,
        bool trace = false)
    {
        string[] inKeys = (inputKeys ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] outKeys = (outputKeys ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return async state =>
        {
            StackableChainValues values = new StackableChainValues();
            // export selected keys
            foreach (string? k in inKeys)
                if (Export.TryGetValue(k, out Action<CliPipelineState, StackableChainValues>? exporter)) exporter(state, values);
            if (trace) Console.WriteLine($"[chain] export keys={string.Join(',', inKeys)} -> values={values.Value.Count}");

            // execute chain
            IChainValues _ = await chain.CallAsync(values).ConfigureAwait(false); // return value often same ref

            // import back
            foreach (string? k in outKeys)
                if (Import.TryGetValue(k, out Action<StackableChainValues, CliPipelineState>? importer)) importer(values, state);
            if (trace) Console.WriteLine($"[chain] import keys={string.Join(',', outKeys)}");
            return state;
        };
    }

    /// <summary>
    /// Compose two stackable chains as a single Step with isolation (syntactic sugar for StackChain + ToStep).
    /// </summary>
    public static Step<CliPipelineState, CliPipelineState> StackToStep(
        BaseStackableChain first,
        BaseStackableChain second,
        IEnumerable<string>? inputKeys = null,
        IEnumerable<string>? outputKeys = null,
        bool trace = false)
    {
        StackChain stack = new StackChain(first, second);
        return stack.ToStep(inputKeys, outputKeys, trace);
    }
}
