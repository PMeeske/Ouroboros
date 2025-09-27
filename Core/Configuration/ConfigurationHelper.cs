using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace LangChainPipeline.Core.Configuration;

/// <summary>
/// Helper class for setting up configuration management in the MonadicPipeline system.
/// </summary>
public static class ConfigurationHelper
{
    /// <summary>
    /// Creates a configuration builder with default settings.
    /// </summary>
    /// <param name="basePath">Base path for configuration files. Defaults to current directory.</param>
    /// <returns>Configured IConfiguration instance.</returns>
    public static IConfiguration CreateConfiguration(string? basePath = null)
    {
        basePath ??= Directory.GetCurrentDirectory();

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables("MONADICPIPELINE_");

        return builder.Build();
    }

    /// <summary>
    /// Binds configuration to PipelineConfiguration instance.
    /// </summary>
    /// <param name="configuration">The configuration source.</param>
    /// <returns>Bound PipelineConfiguration instance.</returns>
    public static PipelineConfiguration GetPipelineConfiguration(IConfiguration configuration)
    {
        var pipelineConfig = new PipelineConfiguration();
        configuration.GetSection(PipelineConfiguration.SectionName).Bind(pipelineConfig);
        return pipelineConfig;
    }

    /// <summary>
    /// Creates an IOptions wrapper for the configuration.
    /// </summary>
    /// <param name="configuration">The configuration source.</param>
    /// <returns>IOptions&lt;PipelineConfiguration&gt; instance.</returns>
    public static IOptions<PipelineConfiguration> GetPipelineOptions(IConfiguration configuration)
    {
        var pipelineConfig = GetPipelineConfiguration(configuration);
        return Options.Create(pipelineConfig);
    }
}