using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LangChainPipeline.Core.Configuration;

/// <summary>
/// Builder for creating pipeline configuration from various sources.
/// </summary>
public class PipelineConfigurationBuilder
{
    private readonly IConfigurationBuilder _configurationBuilder;
    private string? _basePath;
    private string _environmentName = "Production";

    /// <summary>
    /// Initializes a new instance of the configuration builder.
    /// </summary>
    public PipelineConfigurationBuilder()
    {
        _configurationBuilder = new ConfigurationBuilder();
    }

    /// <summary>
    /// Sets the base path for configuration files.
    /// </summary>
    public PipelineConfigurationBuilder SetBasePath(string basePath)
    {
        _basePath = basePath;
        _configurationBuilder.SetBasePath(basePath);
        return this;
    }

    /// <summary>
    /// Sets the environment name (Development, Staging, Production).
    /// </summary>
    public PipelineConfigurationBuilder SetEnvironment(string environmentName)
    {
        _environmentName = environmentName;
        return this;
    }

    /// <summary>
    /// Adds JSON configuration file.
    /// </summary>
    public PipelineConfigurationBuilder AddJsonFile(string fileName, bool optional = false, bool reloadOnChange = false)
    {
        _configurationBuilder.AddJsonFile(fileName, optional, reloadOnChange);
        return this;
    }

    /// <summary>
    /// Adds environment-specific JSON configuration file.
    /// </summary>
    public PipelineConfigurationBuilder AddEnvironmentConfiguration(bool optional = true, bool reloadOnChange = false)
    {
        // Add base appsettings.json
        _configurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: reloadOnChange);
        
        // Add environment-specific appsettings
        _configurationBuilder.AddJsonFile($"appsettings.{_environmentName}.json", optional: optional, reloadOnChange: reloadOnChange);
        
        return this;
    }

    /// <summary>
    /// Adds environment variables as configuration source.
    /// </summary>
    public PipelineConfigurationBuilder AddEnvironmentVariables(string? prefix = null)
    {
        if (prefix != null)
        {
            _configurationBuilder.AddEnvironmentVariables(prefix);
        }
        else
        {
            _configurationBuilder.AddEnvironmentVariables();
        }
        return this;
    }

    /// <summary>
    /// Adds user secrets for development environment.
    /// </summary>
    public PipelineConfigurationBuilder AddUserSecrets<T>(bool optional = true) where T : class
    {
        if (_environmentName == "Development" || _environmentName == "Local")
        {
            _configurationBuilder.AddUserSecrets<T>(optional);
        }
        return this;
    }

    /// <summary>
    /// Builds the configuration and returns a PipelineConfiguration instance.
    /// </summary>
    public PipelineConfiguration Build()
    {
        var configuration = _configurationBuilder.Build();
        var pipelineConfig = new PipelineConfiguration();
        
        // Bind the configuration section to our settings object
        configuration.GetSection(PipelineConfiguration.SectionName).Bind(pipelineConfig);
        
        return pipelineConfig;
    }

    /// <summary>
    /// Builds and returns the IConfiguration instance.
    /// </summary>
    public IConfiguration BuildConfiguration()
    {
        return _configurationBuilder.Build();
    }

    /// <summary>
    /// Creates a builder with standard defaults for the given environment.
    /// </summary>
    public static PipelineConfigurationBuilder CreateDefault(string? basePath = null, string? environmentName = null)
    {
        var environment = environmentName ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                         ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") 
                         ?? "Production";

        var builder = new PipelineConfigurationBuilder()
            .SetEnvironment(environment);

        if (basePath != null)
        {
            builder.SetBasePath(basePath);
        }

        return builder
            .AddEnvironmentConfiguration()
            .AddEnvironmentVariables("PIPELINE_");
    }
}
