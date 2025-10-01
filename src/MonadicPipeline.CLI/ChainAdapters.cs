using LangChain.Chains.StackableChains.Context; // StackableChainValues
using LangChain.Abstractions.Schema;            // IChainValues
// BaseStackableChain (namespace assumption)
using LangChain.Chains.HelperChains;            // StackChain (optional)
using LangChainPipeline.Core.Steps;

namespace LangChainPipeline.CLI.Interop;

/// <summary>
/// Adapters to interoperate NuGet LangChain <c>BaseStackableChain</c> / <c>StackChain</c> with the functional <c>Step&lt;CliPipelineState,CliPipelineState&gt;</c> pipeline.
/// </summary>
public static class ChainAdapters
{
    private static readonly Dictionary<string, Action<CliPipelineState, StackableChainValues>> Export = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Prompt"]  = (s, v) => v.Value["Prompt"] = s.Prompt,
        ["Query"]   = (s, v) => v.Value["Query"] = s.Query,
        ["Topic"]   = (s, v) => v.Value["Topic"] = s.Topic,
        ["Context"] = (s, v) => v.Value["Context"] = s.Context,
        ["Output"]  = (s, v) => v.Value["Output"] = s.Output
    };

    private static readonly Dictionary<string, Action<StackableChainValues, CliPipelineState>> Import = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Prompt"]  = (v, s) => s.Prompt  = v.Value.TryGetValue("Prompt", out var o) ? o?.ToString() ?? string.Empty : s.Prompt,
        ["Query"]   = (v, s) => s.Query   = v.Value.TryGetValue("Query", out var o) ? o?.ToString() ?? string.Empty : s.Query,
        ["Topic"]   = (v, s) => s.Topic   = v.Value.TryGetValue("Topic", out var o) ? o?.ToString() ?? string.Empty : s.Topic,
        ["Context"] = (v, s) => s.Context = v.Value.TryGetValue("Context", out var o) ? o?.ToString() ?? string.Empty : s.Context,
        ["Output"]  = (v, s) => s.Output  = v.Value.TryGetValue("Output", out var o) ? o?.ToString() ?? string.Empty : s.Output
    };

    /// <summary>
    /// Wrap a <see cref="BaseStackableChain"/> as a pipeline <see cref="Step"/> with explicit key isolation.
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
        var inKeys = (inputKeys ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var outKeys = (outputKeys ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return async state =>
        {
            var values = new StackableChainValues();
            // export selected keys
            foreach (var k in inKeys)
                if (Export.TryGetValue(k, out var exporter)) exporter(state, values);
            if (trace) Console.WriteLine($"[chain] export keys={string.Join(',', inKeys)} -> values={values.Value.Count}");

            // execute chain
            IChainValues _ = await chain.CallAsync(values).ConfigureAwait(false); // return value often same ref

            // import back
            foreach (var k in outKeys)
                if (Import.TryGetValue(k, out var importer)) importer(values, state);
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
        var stack = new StackChain(first, second);
        return stack.ToStep(inputKeys, outputKeys, trace);
    }
}
