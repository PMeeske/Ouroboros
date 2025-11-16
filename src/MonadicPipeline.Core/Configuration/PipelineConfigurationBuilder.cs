// <copyright file="PipelineConfigurationBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Builder for creating pipeline configuration from various sources.
/// </summary>
public class PipelineConfigurationBuilder
{
    private readonly IConfigurationBuilder configurationBuilder;
    private string? basePath;
    private string environmentName = "Production";

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineConfigurationBuilder"/> class.
    /// Initializes a new instance of the configuration builder.
    /// </summary>
    public PipelineConfigurationBuilder()
    {
        this.configurationBuilder = new ConfigurationBuilder();
    }

    /// <summary>
    /// Sets the base path for configuration files.
    /// </summary>
    /// <returns></returns>
    public PipelineConfigurationBuilder SetBasePath(string basePath)
    {
        this.basePath = basePath;
        this.configurationBuilder.SetBasePath(basePath);
        return this;
    }

    /// <summary>
    /// Sets the environment name (Development, Staging, Production).
    /// </summary>
    /// <returns></returns>
    public PipelineConfigurationBuilder SetEnvironment(string environmentName)
    {
        this.environmentName = environmentName;
        return this;
    }

    /// <summary>
    /// Adds JSON configuration file.
    /// </summary>
    /// <returns></returns>
    public PipelineConfigurationBuilder AddJsonFile(string fileName, bool optional = false, bool reloadOnChange = false)
    {
        this.configurationBuilder.AddJsonFile(fileName, optional, reloadOnChange);
        return this;
    }

    /// <summary>
    /// Adds environment-specific JSON configuration file.
    /// </summary>
    /// <returns></returns>
    public PipelineConfigurationBuilder AddEnvironmentConfiguration(bool optional = true, bool reloadOnChange = false)
    {
        // Add base appsettings.json
        this.configurationBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: reloadOnChange);

        // Add environment-specific appsettings
        this.configurationBuilder.AddJsonFile($"appsettings.{this.environmentName}.json", optional: optional, reloadOnChange: reloadOnChange);

        return this;
    }

    /// <summary>
    /// Adds environment variables as configuration source.
    /// </summary>
    /// <returns></returns>
    public PipelineConfigurationBuilder AddEnvironmentVariables(string? prefix = null)
    {
        if (prefix != null)
        {
            this.configurationBuilder.AddEnvironmentVariables(prefix);
        }
        else
        {
            this.configurationBuilder.AddEnvironmentVariables();
        }

        return this;
    }

    /// <summary>
    /// Adds user secrets for development environment.
    /// </summary>
    /// <returns></returns>
    public PipelineConfigurationBuilder AddUserSecrets<T>(bool optional = true)
        where T : class
    {
        // Use EnvironmentDetector to check if we're in local development
        // This is more robust than string comparison as it checks multiple indicators
        if (EnvironmentDetector.IsLocalDevelopment())
        {
            this.configurationBuilder.AddUserSecrets<T>(optional);
        }

        return this;
    }

    /// <summary>
    /// Builds the configuration and returns a PipelineConfiguration instance.
    /// </summary>
    /// <returns></returns>
    public PipelineConfiguration Build()
    {
        IConfigurationRoot configuration = this.configurationBuilder.Build();
        PipelineConfiguration pipelineConfig = new PipelineConfiguration();

        // Bind the configuration section to our settings object
        configuration.GetSection(PipelineConfiguration.SectionName).Bind(pipelineConfig);

        return pipelineConfig;
    }

    /// <summary>
    /// Builds and returns the IConfiguration instance.
    /// </summary>
    /// <returns></returns>
    public IConfiguration BuildConfiguration()
    {
        return this.configurationBuilder.Build();
    }

    /// <summary>
    /// Creates a builder with standard defaults for the given environment.
    /// </summary>
    /// <returns></returns>
    public static PipelineConfigurationBuilder CreateDefault(string? basePath = null, string? environmentName = null)
    {
        string environment = environmentName ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                         ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                         ?? "Production";

        PipelineConfigurationBuilder builder = new PipelineConfigurationBuilder()
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
