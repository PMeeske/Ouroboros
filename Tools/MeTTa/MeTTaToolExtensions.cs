namespace LangChainPipeline.Tools.MeTTa;

/// <summary>
/// Extension methods for integrating MeTTa tools with ToolRegistry.
/// </summary>
public static class MeTTaToolExtensions
{
    /// <summary>
    /// Registers MeTTa symbolic reasoning tools with the tool registry.
    /// </summary>
    /// <param name="registry">The tool registry to extend.</param>
    /// <param name="engine">The MeTTa engine to use (optional - creates subprocess engine if not provided).</param>
    /// <returns>A new ToolRegistry with MeTTa tools registered.</returns>
    public static ToolRegistry WithMeTTaTools(this ToolRegistry registry, IMeTTaEngine? engine = null)
    {
        var mettaEngine = engine ?? new SubprocessMeTTaEngine();

        return registry
            .WithTool(new MeTTaQueryTool(mettaEngine))
            .WithTool(new MeTTaRuleTool(mettaEngine))
            .WithTool(new MeTTaPlanVerifierTool(mettaEngine))
            .WithTool(new MeTTaFactTool(mettaEngine));
    }

    /// <summary>
    /// Registers MeTTa tools using a subprocess-based engine with custom executable path.
    /// </summary>
    /// <param name="registry">The tool registry to extend.</param>
    /// <param name="mettaExecutablePath">Path to the MeTTa executable.</param>
    /// <returns>A new ToolRegistry with MeTTa tools registered.</returns>
    public static ToolRegistry WithMeTTaSubprocessTools(this ToolRegistry registry, string mettaExecutablePath)
    {
        var engine = new SubprocessMeTTaEngine(mettaExecutablePath);
        return registry.WithMeTTaTools(engine);
    }

    /// <summary>
    /// Registers MeTTa tools using an HTTP-based engine for Python hyperon service.
    /// </summary>
    /// <param name="registry">The tool registry to extend.</param>
    /// <param name="serviceUrl">Base URL of the MeTTa/Hyperon HTTP service.</param>
    /// <param name="apiKey">Optional API key for authentication.</param>
    /// <returns>A new ToolRegistry with MeTTa tools registered.</returns>
    public static ToolRegistry WithMeTTaHttpTools(this ToolRegistry registry, string serviceUrl, string? apiKey = null)
    {
        var engine = new HttpMeTTaEngine(serviceUrl, apiKey);
        return registry.WithMeTTaTools(engine);
    }

    /// <summary>
    /// Creates a default ToolRegistry with MeTTa tools pre-registered.
    /// </summary>
    /// <param name="engine">The MeTTa engine to use (optional - creates subprocess engine if not provided).</param>
    /// <returns>A new ToolRegistry with MeTTa and standard tools.</returns>
    public static ToolRegistry CreateWithMeTTa(IMeTTaEngine? engine = null)
    {
        return ToolRegistry.CreateDefault().WithMeTTaTools(engine);
    }
}
