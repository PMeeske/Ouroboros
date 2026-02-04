#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using Microsoft.Extensions.Configuration;
using Ouroboros.Application.Tools;
using Ouroboros.Options;
using Ouroboros.Providers;

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
        // Load configuration (includes user secrets in Development)
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddUserSecrets<OuroborosAgent>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Set configuration for API key provider (used by Firecrawl, etc.)
        ApiKeyProvider.SetConfiguration(configuration);

        // Set configuration for ChatConfig (used for Anthropic, GitHub Models, etc.)
        ChatConfig.SetConfiguration(configuration);

        // Set configuration for Azure Speech TTS
        OuroborosAgent.SetConfiguration(configuration);
        VoiceModeService.SetConfiguration(configuration);

        // Clear console safely (may fail when output is redirected)
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // Ignore - output may be redirected
        }

        PrintBanner();

        if (opts.Debug)
        {
            Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");
        }

        try
        {
            // Determine if Azure TTS should be used (prefer Azure if credentials available, allow override with --local-tts)
            var azureKey = opts.AzureSpeechKey ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            var useAzureTts = opts.LocalTts ? false : (opts.AzureTts && !string.IsNullOrEmpty(azureKey));

            // Create unified config from CLI options - ALL features enabled by default
            var config = new OuroborosConfig(
                Persona: opts.Persona,
                Model: opts.Model,
                Endpoint: opts.Endpoint,
                EmbedModel: opts.EmbedModel,
                EmbedEndpoint: opts.EmbedEndpoint,
                QdrantEndpoint: opts.QdrantEndpoint,
                ApiKey: opts.ApiKey ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"),
                EndpointType: opts.EndpointType,
                // Voice is disabled in push/yolo mode by default unless --push-voice is used
                Voice: (opts.Push || opts.Yolo) ? opts.PushVoice : (opts.Voice && !opts.TextOnly),
                VoiceOnly: opts.VoiceOnly,
                LocalTts: opts.LocalTts,
                AzureTts: useAzureTts,
                AzureSpeechKey: azureKey,
                AzureSpeechRegion: opts.AzureSpeechRegion,
                TtsVoice: opts.TtsVoice,
                VoiceChannel: opts.VoiceChannel,
                VoiceV2: opts.VoiceV2,
                Listen: opts.Listen,
                Debug: opts.Debug,
                Temperature: opts.Temperature,
                MaxTokens: opts.MaxTokens,
                Culture: opts.Culture,
                // Feature toggles - invert the "No" flags
                EnableSkills: !opts.NoSkills,
                EnableMeTTa: !opts.NoMeTTa,
                EnableTools: !opts.NoTools,
                EnablePersonality: !opts.NoPersonality,
                EnableMind: !opts.NoMind,
                EnableBrowser: !opts.NoBrowser,
                // Autonomous/Push mode
                EnablePush: opts.Push,
                YoloMode: opts.Yolo,
                AutoApproveCategories: opts.AutoApprove,
                IntentionIntervalSeconds: opts.IntentionInterval,
                DiscoveryIntervalSeconds: opts.DiscoveryInterval,
                // Governance & Self-Modification
                EnableSelfModification: opts.EnableSelfModification,
                RiskLevel: opts.RiskLevel,
                AutoApproveLow: opts.AutoApproveLow,
                // Additional config
                ThinkingIntervalSeconds: opts.ThinkingInterval,
                AgentMaxSteps: opts.AgentMaxSteps,
                InitialGoal: opts.Goal,
                InitialQuestion: opts.Question,
                InitialDsl: opts.Dsl,
                // Multi-model
                CoderModel: opts.CoderModel,
                ReasonModel: opts.ReasonModel,
                SummarizeModel: opts.SummarizeModel,
                // Piping & Batch mode
                PipeMode: opts.Pipe,
                BatchFile: opts.BatchFile,
                JsonOutput: opts.JsonOutput,
                NoGreeting: opts.NoGreeting || opts.Pipe || !string.IsNullOrWhiteSpace(opts.BatchFile) || !string.IsNullOrWhiteSpace(opts.Exec),
                ExitOnError: opts.ExitOnError,
                ExecCommand: opts.Exec,
                // Cost tracking & efficiency
                ShowCosts: opts.ShowCosts,
                CostAware: opts.CostAware,
                CostSummary: opts.CostSummary,
                // Collective Mind (Multi-Provider)
                CollectiveMode: opts.CollectiveMode,
                CollectivePreset: opts.CollectivePreset,
                CollectiveThinkingMode: opts.CollectiveThinkingMode,
                CollectiveProviders: opts.CollectiveProviders,
                Failover: opts.Failover,
                // Election & Orchestration
                ElectionStrategy: opts.ElectionStrategy,
                MasterModel: opts.MasterModel,
                EvaluationCriteria: opts.EvaluationCriteria,
                ShowElection: opts.ShowElection,
                ShowOptimization: opts.ShowOptimization
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
            else if (!string.IsNullOrEmpty(opts.Dsl))
            {
                await agent.ProcessDslAsync(opts.Dsl);
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
   ╚═══════════════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("   Full-featured mode: Voice • Skills • Tools • MeTTa • Personality • Browser");
        Console.ResetColor();
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("   Quick examples:");
        Console.WriteLine("     ouroboros                        # Interactive with voice (default)");
        Console.WriteLine("     ouroboros --text-only            # Text-only mode");
        Console.WriteLine("     ouroboros -q \"What is AI?\"       # Answer a question");
        Console.WriteLine("     ouroboros -g \"Build a website\"   # Goal-driven planning");
        Console.WriteLine("     ouroboros -d \"SetPrompt | LLM\"   # Execute pipeline DSL");
        Console.WriteLine("     ouroboros --no-browser --no-mind # Disable specific features");
        Console.ResetColor();
        Console.WriteLine();
    }
}
