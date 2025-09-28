using System.Text.RegularExpressions;
using LangChain.DocumentLoaders;
using LangChainPipeline.Core;
using LangChainPipeline.Core.Steps;
using LangChainPipeline.Tools;

namespace LangChainPipeline.CLI;

public static class PipelineDsl
{
    public static string[] Tokenize(string dsl) => dsl.Split('|', StringSplitOptions.RemoveEmptyEntries)
        .Select(t => t.Trim())
        .ToArray();

    public static Step<CliPipelineState, CliPipelineState> Build(string dsl)
    {
        var parts = Tokenize(dsl);

        // Resolve all tokens to steps first
        var steps = new List<Step<CliPipelineState, CliPipelineState>>(parts.Length);
        foreach (var token in parts)
        {
            var (name, args) = ParseToken(token);
            if (!StepRegistry.TryResolve(name, args, out var found) || found is null)
            {
                // Unknown token: no-op but recorded
                found = s =>
                {
                    s.Branch = s.Branch.WithIngestEvent($"noop:{name}", Array.Empty<string>());
                    return Task.FromResult(s);
                };
            }
            steps.Add(found);
        }

        // identity definition for aggregation with | operator
        var baseDef = new StepDefinition<CliPipelineState, CliPipelineState>(state => state);
        var aggregate = steps.Aggregate(baseDef, (acc, next) => acc | next);
        return aggregate.Build();
    }

    private static (string name, string? args) ParseToken(string token)
    {
        // Generic patterns: Name(), Name(arg), Step<...>(arg)
        var name = token;
        string? args = null;
        var m = Regex.Match(token, @"^(?<name>[A-Za-z0-9_<>:, ]+)\s*\((?<args>.*)\)\s*$");
        if (m.Success)
        {
            name = m.Groups["name"].Value.Trim();
            args = m.Groups["args"].Value.Trim();
        }

        // Normalize special Step<...>(...) into Set(...)
        if (args is not null && name.StartsWith("Step<", StringComparison.OrdinalIgnoreCase))
        {
            name = "Set";
        }

        return (name, args);
    }

    // arg parsing is handled by individual steps; keep builder minimal

    public static string Explain(string dsl)
    {
        var parts = Tokenize(dsl);
        var lines = new List<string>(parts.Length + 2)
        {
            "Pipeline tokens:"
        };

        foreach (var token in parts)
        {
            var (name, args) = ParseToken(token);
            if (StepRegistry.TryResolveInfo(name, out var mi) && mi is not null)
            {
                lines.Add($"- {name}{(args is null ? string.Empty : $"({args})")} -> {mi.DeclaringType?.Name}.{mi.Name}()");
            }
            else
            {
                lines.Add($"- {name}{(args is null ? string.Empty : $"({args})")} -> (no-op)");
            }
        }

        lines.Add("");
        lines.Add("Available token groups:");
        foreach (var (method, names) in StepRegistry.GetTokenGroups())
        {
            var aliasList = string.Join(", ", names);
            lines.Add($"- {method.DeclaringType?.Name}.{method.Name}(): {aliasList}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
