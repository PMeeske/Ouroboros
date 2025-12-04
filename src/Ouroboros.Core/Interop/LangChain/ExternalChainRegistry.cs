#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace LangChainPipeline.Interop.LangChain;

/// <summary>
/// Simple in-memory registry for external (NuGet) LangChain BaseStackableChain instances so they can be referenced from DSL tokens.
/// </summary>
public static class ExternalChainRegistry
{
    private static readonly Dictionary<string, object> Chains = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string name, object chain)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        Chains[name] = chain;
    }
    public static bool TryGet(string name, out object? chain) => Chains.TryGetValue(name, out chain);
    public static IReadOnlyCollection<string> Names => Chains.Keys.ToArray();
}
