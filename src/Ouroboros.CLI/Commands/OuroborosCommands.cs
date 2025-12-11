#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChainPipeline.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Entry point for the unified Ouroboros AI agent mode.
/// </summary>
public static class OuroborosCommands
{
    /// <summary>
    /// Runs the unified Ouroboros agent with all integrated capabilities.
    /// </summary>
    public static async Task RunOuroborosAsync(OuroborosOptions opts)
    {
        Console.Clear();
        PrintBanner();

        if (opts.Debug)
        {
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
        }

        try
        {
            // Create unified config from CLI options
            var config = new OuroborosConfig(
                Persona: opts.Persona,
                Model: opts.Model,
                Endpoint: opts.Endpoint,
                EmbedModel: opts.EmbedModel,
                EmbedEndpoint: opts.EmbedEndpoint,
                QdrantEndpoint: opts.QdrantEndpoint,
                ApiKey: opts.ApiKey ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
                Voice: opts.Voice && !opts.TextOnly,
                VoiceOnly: opts.VoiceOnly,
                LocalTts: opts.LocalTts,
                Debug: opts.Debug,
                Temperature: opts.Temperature,
                MaxTokens: opts.MaxTokens
            );

            // Create the unified Ouroboros agent
            await using var agent = new OuroborosAgent(config);
            await agent.InitializeAsync();

            // Handle initial goal or question if provided
            if (!string.IsNullOrEmpty(opts.Goal))
            {
                await agent.Voice.SayAsync($"I understand your goal: {opts.Goal}. Let me plan how to accomplish this.");
                await agent.ProcessGoalAsync(opts.Goal);
            }
            else if (!string.IsNullOrEmpty(opts.Question))
            {
                await agent.ProcessQuestionAsync(opts.Question);
            }

            // Main interaction loop
            await agent.RunAsync();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Ouroboros] Fatal error: {ex.Message}");
            if (opts.Debug)
            {
                Console.WriteLine(ex.StackTrace);
            }
            Console.ResetColor();
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@"
   ╔═══════════════════════════════════════════════════════════════════════╗
   ║                                                                       ║
   ║       ██████╗ ██╗   ██╗██████╗  ██████╗ ██████╗  ██████╗ ██████╗  ██████╗ ███████╗  ║
   ║      ██╔═══██╗██║   ██║██╔══██╗██╔═══██╗██╔══██╗██╔═══██╗██╔══██╗██╔═══██╗██╔════╝  ║
   ║      ██║   ██║██║   ██║██████╔╝██║   ██║██████╔╝██║   ██║██████╔╝██║   ██║███████╗  ║
   ║      ██║   ██║██║   ██║██╔══██╗██║   ██║██╔══██╗██║   ██║██╔══██╗██║   ██║╚════██║  ║
   ║      ╚██████╔╝╚██████╔╝██║  ██║╚██████╔╝██████╔╝╚██████╔╝██║  ██║╚██████╔╝███████║  ║
   ║       ╚═════╝  ╚═════╝ ╚═╝  ╚═╝ ╚═════╝ ╚═════╝  ╚═════╝ ╚═╝  ╚═╝ ╚═════╝ ╚══════╝  ║
   ║                                                                       ║
   ║          ∞ The Self-Improving AI Agent ∞                             ║
   ║                                                                       ║
   ║   Unified Voice + Skills + Tools + Reasoning + Personality           ║
   ║                                                                       ║
   ╚═══════════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();
        Console.WriteLine();
    }
}
