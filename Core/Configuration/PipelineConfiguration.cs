using Microsoft.Extensions.Configuration;

namespace LangChainPipeline.Core.Configuration;

/// <summary>
/// Configuration settings for the pipeline system.
/// Provides environment-specific configuration management.
/// </summary>
public class PipelineConfiguration
{
    public ModelsConfiguration Models { get; set; } = new();
    public ReasoningConfiguration Reasoning { get; set; } = new();
    public ToolsConfiguration Tools { get; set; } = new();
}

/// <summary>
/// Model configuration settings.
/// </summary>
public class ModelsConfiguration
{
    public string ChatModel { get; set; } = "deepseek-coder:33b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}

/// <summary>
/// Reasoning pipeline configuration settings.
/// </summary>
public class ReasoningConfiguration
{
    public string DefaultTopic { get; set; } = "Vita.KeyTables_Grosses_Konzept.md";
    public string DefaultQuery { get; set; } = "KeyTable parameters and edge cases";
    public int SimilarityK { get; set; } = 8;
}

/// <summary>
/// Tools configuration settings.
/// </summary>
public class ToolsConfiguration
{
    public bool EnableMath { get; set; } = true;
    public bool EnableRetrieval { get; set; } = true;
}

/// <summary>
/// Configuration builder extensions for better configuration management.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Builds a configuration from standard sources (appsettings.json, environment variables, etc.)
    /// </summary>
    public static IConfiguration BuildConfiguration(string? environmentName = null)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        if (!string.IsNullOrEmpty(environmentName))
        {
            builder.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);
        }

        builder.AddEnvironmentVariables("MONADIC_PIPELINE_");

        return builder.Build();
    }

    /// <summary>
    /// Gets the pipeline configuration from the IConfiguration instance.
    /// </summary>
    public static PipelineConfiguration GetPipelineConfiguration(this IConfiguration configuration)
    {
        var pipelineConfig = new PipelineConfiguration();
        configuration.GetSection("Pipeline").Bind(pipelineConfig);
        return pipelineConfig;
    }
}