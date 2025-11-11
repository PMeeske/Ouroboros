// <copyright file="StepRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.CLI;

using System.Reflection;

/// <summary>
/// Discovers and provides access to CLI pipeline token steps based on annotated static methods.
/// </summary>
public static class StepRegistry
{
    private static readonly Lazy<Dictionary<string, MethodInfo>> Map = new(BuildMap, isThreadSafe: true);

    private static Dictionary<string, MethodInfo> BuildMap()
    {
        var dict = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);
        var asm = typeof(StepRegistry).Assembly;
        foreach (var type in asm.GetTypes())
        {
            foreach (var mi in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = mi.GetCustomAttribute<PipelineTokenAttribute>();
                if (attr is null)
                {
                    continue;
                }

                foreach (var name in attr.Names)
                {
                    dict[name] = mi;
                }
            }
        }

        return dict;
    }

    /// <summary>
    /// Try resolve a token name to a Step method. Supports optional single string? args parameter.
    /// </summary>
    /// <returns></returns>
    public static bool TryResolve(string tokenName, string? args, out Step<CliPipelineState, CliPipelineState>? step)
    {
        step = null;
        if (!Map.Value.TryGetValue(tokenName, out var mi))
        {
            return false;
        }

        var parameters = mi.GetParameters();
        object?[] callArgs = parameters.Length == 0
            ? Array.Empty<object?>()
            : [args];

        var result = mi.Invoke(null, callArgs);
        if (result is Step<CliPipelineState, CliPipelineState> s)
        {
            step = s;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets list of available token names (for help/diagnostics).
    /// </summary>
    public static IReadOnlyCollection<string> Tokens => Map.Value.Keys.ToArray();

    /// <summary>
    /// Try resolve only the MethodInfo for a token name.
    /// </summary>
    /// <returns></returns>
    public static bool TryResolveInfo(string tokenName, out MethodInfo? method)
    {
        if (Map.Value.TryGetValue(tokenName, out var mi))
        {
            method = mi;
            return true;
        }

        method = null;
        return false;
    }

    /// <summary>
    /// Returns groups of aliases per underlying method.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<(MethodInfo Method, IReadOnlyList<string> Names)> GetTokenGroups()
    {
        return Map.Value
            .GroupBy(kv => kv.Value)
            .Select(g => (g.Key, (IReadOnlyList<string>)g.Select(kv => kv.Key).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()));
    }
}
