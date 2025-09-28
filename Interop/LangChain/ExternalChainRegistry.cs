namespace LangChainPipeline.Interop.LangChain;

/// <summary>
/// Simple in-memory registry for external (NuGet) LangChain BaseStackableChain instances so they can be referenced from DSL tokens.
/// </summary>
public static class ExternalChainRegistry
{
    private static readonly Dictionary<string, object> _chains = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string name, object chain)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        _chains[name] = chain;
    }
    public static bool TryGet(string name, out object? chain) => _chains.TryGetValue(name, out chain);
    public static IReadOnlyCollection<string> Names => _chains.Keys.ToArray();
}
