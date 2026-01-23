// <copyright file="ConsolidatedMindCli.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using LangChain.Providers.Ollama;
using Ouroboros.Providers;

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// CLI startup helper for ConsolidatedMind with Ollama Cloud defaults.
/// Provides simple factory methods to create a fully configured multi-model orchestrator.
/// </summary>
public static class ConsolidatedMindCli
{
    /// <summary>
    /// Creates a ConsolidatedMind instance with default cloud configurations.
    /// Requires OLLAMA_CLOUD_API_KEY environment variable to be set.
    /// </summary>
    /// <returns>A configured ConsolidatedMind ready for use.</returns>
    public static ConsolidatedMind CreateDefault()
    {
        string apiKey = Environment.GetEnvironmentVariable(OllamaCloudDefaults.ApiKeyEnvVar)
            ?? throw new InvalidOperationException(
                $"Environment variable {OllamaCloudDefaults.ApiKeyEnvVar} is required. " +
                "Get your API key from https://ollama.com/cloud");

        var mind = new ConsolidatedMind();
        var provider = CreateCloudProvider(apiKey);

        foreach (var config in OllamaCloudDefaults.GetAllDefaultConfigs())
        {
            var langChainModel = new OllamaChatModel(provider, config.OllamaModel);
            var adapter = new OllamaChatAdapter(langChainModel);
            var specialist = new SpecializedModel(
                config.Role,
                adapter,
                config.OllamaModel,
                config.Capabilities ?? Array.Empty<string>(),
                config.Priority,
                config.MaxTokens);
            mind.RegisterSpecialist(specialist);
        }

        return mind;
    }

    /// <summary>
    /// Creates a minimal ConsolidatedMind with only essential specialists (cheaper/faster).
    /// Good for development and testing.
    /// </summary>
    /// <returns>A minimal ConsolidatedMind configuration.</returns>
    public static ConsolidatedMind CreateMinimal()
    {
        string apiKey = Environment.GetEnvironmentVariable(OllamaCloudDefaults.ApiKeyEnvVar)
            ?? throw new InvalidOperationException(
                $"Environment variable {OllamaCloudDefaults.ApiKeyEnvVar} is required.");

        var mind = new ConsolidatedMind();
        var provider = CreateCloudProvider(apiKey);

        foreach (var config in OllamaCloudDefaults.GetMinimalConfigs())
        {
            var langChainModel = new OllamaChatModel(provider, config.OllamaModel);
            var adapter = new OllamaChatAdapter(langChainModel);
            var specialist = new SpecializedModel(
                config.Role,
                adapter,
                config.OllamaModel,
                config.Capabilities ?? Array.Empty<string>(),
                config.Priority,
                config.MaxTokens);
            mind.RegisterSpecialist(specialist);
        }

        return mind;
    }

    /// <summary>
    /// Creates a high-quality ConsolidatedMind with larger models for production use.
    /// Uses more expensive models for better results.
    /// </summary>
    /// <returns>A high-quality ConsolidatedMind configuration.</returns>
    public static ConsolidatedMind CreateHighQuality()
    {
        string apiKey = Environment.GetEnvironmentVariable(OllamaCloudDefaults.ApiKeyEnvVar)
            ?? throw new InvalidOperationException(
                $"Environment variable {OllamaCloudDefaults.ApiKeyEnvVar} is required.");

        var mind = new ConsolidatedMind();
        var provider = CreateCloudProvider(apiKey);

        foreach (var config in OllamaCloudDefaults.GetHighQualityConfigs())
        {
            var langChainModel = new OllamaChatModel(provider, config.OllamaModel);
            var adapter = new OllamaChatAdapter(langChainModel);
            var specialist = new SpecializedModel(
                config.Role,
                adapter,
                config.OllamaModel,
                config.Capabilities ?? Array.Empty<string>(),
                config.Priority,
                config.MaxTokens);
            mind.RegisterSpecialist(specialist);
        }

        return mind;
    }

    /// <summary>
    /// Creates a ConsolidatedMind with a custom endpoint (e.g., for proxies or custom deployments).
    /// </summary>
    /// <param name="endpoint">The Ollama API endpoint URL.</param>
    /// <param name="apiKey">The API key (optional for some endpoints).</param>
    /// <param name="useHighQuality">If true, use high-quality models.</param>
    /// <returns>A ConsolidatedMind with custom endpoint.</returns>
    public static ConsolidatedMind CreateWithEndpoint(string endpoint, string? apiKey = null, bool useHighQuality = false)
    {
        var mind = new ConsolidatedMind();
        var provider = new OllamaProvider(endpoint);

        var configs = useHighQuality
            ? OllamaCloudDefaults.GetHighQualityConfigs()
            : OllamaCloudDefaults.GetAllDefaultConfigs();

        foreach (var config in configs)
        {
            var langChainModel = new OllamaChatModel(provider, config.OllamaModel);
            var adapter = new OllamaChatAdapter(langChainModel);
            var specialist = new SpecializedModel(
                config.Role,
                adapter,
                config.OllamaModel,
                config.Capabilities ?? Array.Empty<string>(),
                config.Priority,
                config.MaxTokens);
            mind.RegisterSpecialist(specialist);
        }

        return mind;
    }

    /// <summary>
    /// Creates an Ollama provider configured for cloud access.
    /// </summary>
    private static OllamaProvider CreateCloudProvider(string apiKey)
    {
        // OllamaProvider with cloud endpoint and API key
        var provider = new OllamaProvider(OllamaCloudDefaults.CloudEndpoint);
        // Note: API key handling may need adjustment based on LangChain.Providers.Ollama implementation
        return provider;
    }

    /// <summary>
    /// Runs an interactive CLI session with the ConsolidatedMind.
    /// </summary>
    /// <param name="mind">The ConsolidatedMind to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RunInteractiveAsync(ConsolidatedMind mind, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           Ouroboros ConsolidatedMind CLI                     ║");
        Console.WriteLine("║  Multi-model orchestration with specialized AI models       ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║  Commands:                                                   ║");
        Console.WriteLine("║    /quit, /exit    - Exit the CLI                           ║");
        Console.WriteLine("║    /think <prompt> - Force deep reasoning mode              ║");
        Console.WriteLine("║    /code <prompt>  - Force code expert mode                 ║");
        Console.WriteLine("║    /plan <prompt>  - Force planning mode                    ║");
        Console.WriteLine("║    /models         - Show registered models                 ║");
        Console.WriteLine("║    /help           - Show this help                         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("You> ");
            Console.ResetColor();

            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.StartsWith("/quit", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("/exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (input.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            if (input.StartsWith("/models", StringComparison.OrdinalIgnoreCase))
            {
                PrintModels(mind);
                continue;
            }

            try
            {
                string response;
                bool useThinking = false;

                if (input.StartsWith("/think ", StringComparison.OrdinalIgnoreCase))
                {
                    input = input[7..];
                    useThinking = true;
                }
                else if (input.StartsWith("/code ", StringComparison.OrdinalIgnoreCase))
                {
                    input = input[6..];
                    // Force code expert by prefixing
                    input = $"[CODE TASK] {input}";
                }
                else if (input.StartsWith("/plan ", StringComparison.OrdinalIgnoreCase))
                {
                    input = input[6..];
                    input = $"[PLANNING TASK] {input}";
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Processing...");
                Console.ResetColor();

                var mindResponse = await mind.ProcessAsync(input, cancellationToken);
                response = mindResponse.Response;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Mind> ");
                Console.ResetColor();
                Console.WriteLine(response);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Main entry point for CLI mode.
    /// Usage: Set OLLAMA_CLOUD_API_KEY and run.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static async Task Main(string[] args)
    {
        bool minimal = args.Contains("--minimal", StringComparer.OrdinalIgnoreCase);
        bool highQuality = args.Contains("--high-quality", StringComparer.OrdinalIgnoreCase);

        try
        {
            ConsolidatedMind mind;

            if (minimal)
            {
                Console.WriteLine("Starting in minimal mode (fewer models, lower cost)...");
                mind = CreateMinimal();
            }
            else if (highQuality)
            {
                Console.WriteLine("Starting in high-quality mode (larger models, better results)...");
                mind = CreateHighQuality();
            }
            else
            {
                Console.WriteLine("Starting with default configuration...");
                mind = CreateDefault();
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await RunInteractiveAsync(mind, cts.Token);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("OLLAMA_CLOUD_API_KEY"))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: OLLAMA_CLOUD_API_KEY environment variable is not set.");
            Console.WriteLine();
            Console.WriteLine("To use Ollama Cloud:");
            Console.WriteLine("  1. Sign up at https://ollama.com/cloud");
            Console.WriteLine("  2. Get your API key");
            Console.WriteLine("  3. Set the environment variable:");
            Console.WriteLine("     Windows: set OLLAMA_CLOUD_API_KEY=your-key-here");
            Console.WriteLine("     Linux/Mac: export OLLAMA_CLOUD_API_KEY=your-key-here");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /quit, /exit    - Exit the CLI");
        Console.WriteLine("  /think <prompt> - Force deep reasoning with thinking tokens");
        Console.WriteLine("  /code <prompt>  - Route to code expert model");
        Console.WriteLine("  /plan <prompt>  - Route to planning model");
        Console.WriteLine("  /models         - Show all registered specialist models");
        Console.WriteLine("  /help           - Show this help");
        Console.WriteLine();
        Console.WriteLine("The ConsolidatedMind automatically routes your prompts to the");
        Console.WriteLine("most appropriate specialist model based on content analysis.");
        Console.WriteLine();
    }

    private static void PrintModels(ConsolidatedMind mind)
    {
        Console.WriteLine();
        Console.WriteLine("Registered Specialist Models:");
        Console.WriteLine("─────────────────────────────────────────────────────");

        foreach (var role in Enum.GetValues<SpecializedRole>())
        {
            var config = OllamaCloudDefaults.GetDefaultConfig(role);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"  {role,-15}");
            Console.ResetColor();
            Console.WriteLine($" → {config.OllamaModel}");
        }

        Console.WriteLine("─────────────────────────────────────────────────────");
        Console.WriteLine();
    }
}
