#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text;
using System.Text.RegularExpressions;

namespace LangChainPipeline.CLI;

public static class PipelineDsl
{
    public static string[] Tokenize(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
        {
            return Array.Empty<string>();
        }

        List<string> tokens = new List<string>();
        StringBuilder current = new StringBuilder();
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        int parenDepth = 0;

        for (int i = 0; i < dsl.Length; i++)
        {
            char c = dsl[i];

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                current.Append(c);
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                current.Append(c);
                continue;
            }

            if (!inSingleQuote && !inDoubleQuote)
            {
                if (c == '(')
                {
                    parenDepth++;
                }
                else if (c == ')' && parenDepth > 0)
                {
                    parenDepth--;
                }
                else if (c == '|' && parenDepth == 0)
                {
                    string token = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(token))
                    {
                        tokens.Add(token);
                    }
                    current.Clear();
                    continue;
                }
            }

            current.Append(c);
        }

        string last = current.ToString().Trim();
        if (!string.IsNullOrEmpty(last))
        {
            tokens.Add(last);
        }

        return tokens.ToArray();
    }

    public static Step<CliPipelineState, CliPipelineState> Build(string dsl)
    {
        string[] parts = Tokenize(dsl);

        // Resolve all tokens to steps first
        List<Step<CliPipelineState, CliPipelineState>> steps = new List<Step<CliPipelineState, CliPipelineState>>(parts.Length);
        foreach (string token in parts)
        {
            (string name, string args) = ParseToken(token);
            if (!StepRegistry.TryResolve(name, args, out Step<CliPipelineState, CliPipelineState>? found) || found is null)
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
        StepDefinition<CliPipelineState, CliPipelineState> baseDef = new StepDefinition<CliPipelineState, CliPipelineState>(state => state);
        StepDefinition<CliPipelineState, CliPipelineState> aggregate = steps.Aggregate(baseDef, (acc, next) => acc | next);
        return aggregate.Build();
    }

    private static (string name, string? args) ParseToken(string token)
    {
        // Generic patterns: Name(), Name(arg), Step<...>(arg)
        string name = token;
        string? args = null;
        Match m = Regex.Match(token, @"^(?<name>[A-Za-z0-9_<>:, ]+)\s*\((?<args>.*)\)\s*$");
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
        string[] parts = Tokenize(dsl);
        List<string> lines = new List<string>(parts.Length + 2)
        {
            "Pipeline tokens:"
        };

        foreach (string token in parts)
        {
            (string name, string args) = ParseToken(token);
            if (StepRegistry.TryResolveInfo(name, out System.Reflection.MethodInfo? mi) && mi is not null)
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
        foreach ((System.Reflection.MethodInfo method, IReadOnlyList<string> names) in StepRegistry.GetTokenGroups())
        {
            string aliasList = string.Join(", ", names);
            lines.Add($"- {method.DeclaringType?.Name}.{method.Name}(): {aliasList}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
