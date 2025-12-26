#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Ouroboros.Interop.Hosting;

/// <summary>
/// Elegant minimal host builder using StepDefinition and | composition.
/// </summary>
public static class MinimalHost
{
    public static async Task<IHost> BuildAsync(string[] args)
    {
        // Seed settings object (pure)
        HostApplicationBuilderSettings settings = new HostApplicationBuilderSettings
        {
            Args = args,
            Configuration = new ConfigurationManager(),
            ContentRootPath = Directory.GetCurrentDirectory(),
        };

        // Define configuration pipeline
        StepDefinition<ConfigurationManager, ConfigurationManager> configDef = new StepDefinition<ConfigurationManager, ConfigurationManager>(c => c)
            | HostStepExtensions.Use(c => { c.AddJsonFile("hostsettings.json", optional: true); return c; })
            | HostStepExtensions.Use(c => { c.AddEnvironmentVariables(prefix: "PREFIX_"); return c; })
            | HostStepExtensions.Use(c => { c.AddCommandLine(args); return c; });

        // Apply configuration to settings
        settings.Configuration = await configDef.Build()(settings.Configuration);

        // Host builder pipeline (extensible)
        StepDefinition<HostApplicationBuilder, HostApplicationBuilder> hostDef = new StepDefinition<HostApplicationBuilder, HostApplicationBuilder>(b => b)
            // Add interchangeable model registration (OpenAI key presence => remote reflective provider else local Ollama)
            | HostStepExtensions.AddInterchangeableLlm();

        HostApplicationBuilder builder = await hostDef.Build()(Host.CreateApplicationBuilder(settings));
        return builder.Build();
    }
}
