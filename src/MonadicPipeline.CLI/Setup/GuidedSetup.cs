// <copyright file="GuidedSetup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace MonadicPipeline.CLI.Setup;

using System.Diagnostics;
using System.Runtime.InteropServices;
using LangChainPipeline.Options;

/// <summary>
/// Provides guided setup for the local development environment.
/// </summary>
public static class GuidedSetup
{
    /// <summary>
    /// Runs the guided setup based on the provided options.
    /// </summary>
    /// <param name="options">The setup options.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAsync(SetupOptions options)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║      Welcome to the MonadicPipeline Guided Setup Wizard          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This utility will help you configure your local development environment");
        Console.WriteLine("for running AI pipelines with small, efficient models locally.");
        Console.WriteLine();

        if (options.All)
        {
            Console.WriteLine("Running complete setup with all components...\n");
            await InstallOllamaAsync();
            await ConfigureAuthAsync();
            await InstallMeTTaAsync();
            await InstallVectorStoreAsync();
            await ShowQuickStartExamplesAsync();
            Console.WriteLine("\n✨ All setup steps completed successfully!");
            Console.WriteLine("\nYou're ready to start using MonadicPipeline.");
            Console.WriteLine("Run 'dotnet run -- --help' to see all available commands.");
            return;
        }

        bool anyStepRan = false;

        if (options.InstallOllama)
        {
            await InstallOllamaAsync();
            anyStepRan = true;
        }

        if (options.ConfigureAuth)
        {
            await ConfigureAuthAsync();
            anyStepRan = true;
        }

        if (options.InstallMeTTa)
        {
            await InstallMeTTaAsync();
            anyStepRan = true;
        }

        if (options.InstallVectorStore)
        {
            await InstallVectorStoreAsync();
            anyStepRan = true;
        }

        if (anyStepRan)
        {
            Console.WriteLine("\n✨ Selected setup steps completed successfully!");
            await ShowQuickStartExamplesAsync();
        }
        else
        {
            Console.WriteLine("No setup options specified. Use --help to see available options.");
            Console.WriteLine("\nQuick setup commands:");
            Console.WriteLine("  --all              Run complete setup");
            Console.WriteLine("  --ollama           Install Ollama for local LLMs");
            Console.WriteLine("  --auth             Configure external provider authentication");
            Console.WriteLine("  --metta            Install MeTTa symbolic reasoning engine");
            Console.WriteLine("  --vector-store     Setup local vector database");
        }
    }

    private static async Task InstallOllamaAsync()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                   Ollama Installation Guide                       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        if (IsCommandAvailable("ollama"))
        {
            Console.WriteLine("✅ Ollama is already installed on your system!");

            // Check if Ollama is running
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ollama",
                        ArgumentList = { "list" },
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    Console.WriteLine("\n📦 Currently installed models:");
                    Console.WriteLine(output);

                    if (!output.Contains("phi3") && !output.Contains("qwen") && !output.Contains("llama"))
                    {
                        Console.WriteLine("\n💡 Recommended small models for efficient orchestration:");
                        Console.WriteLine("   • phi3:mini (2.3GB) - Fast, general-purpose reasoning");
                        Console.WriteLine("   • qwen2.5:3b (2GB) - Excellent for complex tasks");
                        Console.WriteLine("   • deepseek-coder:1.3b (800MB) - Specialized for coding");
                        Console.WriteLine("   • tinyllama (637MB) - Ultra-light for simple tasks");
                        Console.WriteLine();

                        if (PromptYesNo("Would you like guidance on pulling recommended models?"))
                        {
                            Console.WriteLine("\nTo install recommended models, run:");
                            Console.WriteLine("   ollama pull phi3:mini");
                            Console.WriteLine("   ollama pull qwen2.5:3b");
                            Console.WriteLine("   ollama pull deepseek-coder:1.3b");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\n⚠️  Ollama is installed but not running.");
                    Console.WriteLine("Please start Ollama by running: ollama serve");
                }
            }
            catch
            {
                Console.WriteLine("\n⚠️  Could not verify Ollama status.");
            }

            return;
        }

        Console.WriteLine("Ollama is not found in your PATH.");
        Console.WriteLine();
        Console.WriteLine("Ollama is a lightweight runtime for running LLMs locally.");
        Console.WriteLine("It enables efficient orchestration with small, specialized models.");
        Console.WriteLine();

        if (!PromptYesNo("Do you want to install Ollama now?"))
        {
            Console.WriteLine("Skipping Ollama installation.");
            return;
        }

        string url = "https://ollama.com/download";
        Console.WriteLine($"\n📥 Download Ollama from: {url}");
        Console.WriteLine();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Installation steps for Windows:");
            Console.WriteLine("1. Download the installer from the website");
            Console.WriteLine("2. Run the installer (it will add Ollama to your PATH)");
            Console.WriteLine("3. Restart your terminal");
            Console.WriteLine("4. Verify by running: ollama --version");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Console.WriteLine("Installation steps for Linux:");
            Console.WriteLine("Run this command in your terminal:");
            Console.WriteLine();
            Console.WriteLine("   curl -fsSL https://ollama.com/install.sh | sh");
            Console.WriteLine();
            Console.WriteLine("Then verify by running: ollama --version");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Console.WriteLine("Installation steps for macOS:");
            Console.WriteLine("1. Download Ollama.app from the website");
            Console.WriteLine("2. Move it to your Applications folder");
            Console.WriteLine("3. Open Ollama.app (it will add the CLI to your PATH)");
            Console.WriteLine("4. Verify by running: ollama --version");
        }

        Console.WriteLine();
        Console.WriteLine("📦 After installation, recommended first steps:");
        Console.WriteLine("1. Start Ollama: ollama serve (or it may start automatically)");
        Console.WriteLine("2. Pull a small model: ollama pull phi3:mini");
        Console.WriteLine("3. Test it: ollama run phi3:mini \"Hello!\"");
        Console.WriteLine();
        Console.WriteLine("💡 For efficient multi-model orchestration, consider installing:");
        Console.WriteLine("   ollama pull qwen2.5:3b         # General reasoning");
        Console.WriteLine("   ollama pull deepseek-coder:1.3b # Code tasks");
        Console.WriteLine("   ollama pull phi3:mini          # Quick responses");

        await Task.Delay(2000); // Give user time to read
    }

    private static Task ConfigureAuthAsync()
    {
        Console.WriteLine("\n--- External Provider Authentication ---");
        Console.WriteLine("To use remote providers like OpenAI or Ollama Cloud, you need to set environment variables.");
        Console.WriteLine("You can set these in your system, or create a '.env' file in the project root.");

        Console.WriteLine("\nExample for an OpenAI-compatible endpoint:");
        Console.WriteLine("  CHAT_ENDPOINT=\"https://api.example.com/v1\"");
        Console.WriteLine("  CHAT_API_KEY=\"your-api-key\"");
        Console.WriteLine("  CHAT_ENDPOINT_TYPE=\"openai\"");

        Console.WriteLine("\nExample for Ollama Cloud:");
        Console.WriteLine("  CHAT_ENDPOINT=\"https://ollama.cloud.ai\"");
        Console.WriteLine("  CHAT_API_KEY=\"your-ollama-cloud-key\"");
        Console.WriteLine("  CHAT_ENDPOINT_TYPE=\"ollama-cloud\"");

        Console.WriteLine("\nThese variables are loaded automatically when you run the CLI.");
        return Task.CompletedTask;
    }

    private static async Task InstallMeTTaAsync()
    {
        Console.WriteLine("\n--- MeTTa Engine Installation ---");
        if (IsCommandAvailable("metta"))
        {
            Console.WriteLine("MeTTa appears to be installed already. Skipping.");
            return;
        }

        Console.WriteLine("The MeTTa (Meta-language for Type-Theoretic Agents) engine is not found in your PATH.");
        if (!PromptYesNo("Do you want to proceed with installation guidance for MeTTa?"))
        {
            return;
        }

        Console.WriteLine("MeTTa is required for advanced symbolic reasoning features.");
        Console.WriteLine("Installation instructions can be found at the TrueAGI Hyperon-Experimental repository:");
        Console.WriteLine("https://github.com/trueagi-io/hyperon-experimental");
        Console.WriteLine("\nPlease follow their instructions to build and install the 'metta' executable and ensure it is in your system's PATH.");
        await Task.Delay(1000);
    }

    private static async Task InstallVectorStoreAsync()
    {
        Console.WriteLine("\n--- Local Vector Store Installation (Qdrant) ---");
        Console.WriteLine("For local vector persistence, this project can use Qdrant.");
        Console.WriteLine("The easiest way to run Qdrant is with Docker.");

        if (!IsCommandAvailable("docker"))
        {
            Console.WriteLine("Docker is not found. Please install Docker Desktop from: https://www.docker.com/products/docker-desktop/");
            return;
        }

        if (!PromptYesNo("Do you want to see the command to run a local Qdrant container?"))
        {
            return;
        }

        Console.WriteLine("\nRun the following Docker command to start a Qdrant instance:");
        Console.WriteLine("docker run -p 6333:6333 -p 6334:6334 \\");
        Console.WriteLine("  -v $(pwd)/qdrant_storage:/qdrant/storage:z \\");
        Console.WriteLine("  qdrant/qdrant");

        Console.WriteLine("\nThis will store vector data in a 'qdrant_storage' directory in your current folder.");
        await Task.Delay(1000);
    }

    /// <summary>
    /// Prompts the user with a yes/no question.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <returns><c>true</c> if the user answers yes, <c>false</c> otherwise.</returns>
    public static bool PromptYesNo(string prompt)
    {
        Console.Write($"{prompt} (y/n): ");
        string? response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response == "y" || response == "yes";
    }

    private static bool IsCommandAvailable(string command)
    {
        Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                ArgumentList = { command },
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static Task ShowQuickStartExamplesAsync()
    {
        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Quick Start Examples                           ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Here are some commands to get you started:");
        Console.WriteLine();
        Console.WriteLine("1️⃣  Ask a simple question:");
        Console.WriteLine("   dotnet run -- ask -q \"What is functional programming?\"");
        Console.WriteLine();
        Console.WriteLine("2️⃣  Use the orchestrator with small models for complex tasks:");
        Console.WriteLine("   dotnet run -- orchestrator --goal \"Explain monadic composition\" \\");
        Console.WriteLine("     --model \"phi3\" --show-metrics");
        Console.WriteLine();
        Console.WriteLine("3️⃣  Run a pipeline with iterative refinement:");
        Console.WriteLine("   dotnet run -- pipeline \\");
        Console.WriteLine("     -d \"SetTopic('AI Safety') | UseDraft | UseCritique | UseImprove\"");
        Console.WriteLine();
        Console.WriteLine("4️⃣  Use MeTTa for symbolic reasoning:");
        Console.WriteLine("   dotnet run -- metta --goal \"Analyze data patterns\" --plan-only");
        Console.WriteLine();
        Console.WriteLine("5️⃣  Chain operations efficiently (orchestration example):");
        Console.WriteLine("   dotnet run -- pipeline \\");
        Console.WriteLine("     -d \"SetTopic('Code Review') | UseDraft | UseCritique | UseImprove\" \\");
        Console.WriteLine("     --router auto \\");
        Console.WriteLine("     --general-model phi3 \\");
        Console.WriteLine("     --coder-model deepseek-coder:1.3b \\");
        Console.WriteLine("     --reason-model qwen2.5:3b");
        Console.WriteLine();
        Console.WriteLine("💡 Tips for efficient orchestration with small models:");
        Console.WriteLine("   • Use --router auto to automatically select the best model for each task");
        Console.WriteLine("   • Combine specialized small models (phi3, qwen, deepseek) for complex workflows");
        Console.WriteLine("   • Enable --trace to see which model handles each pipeline step");
        Console.WriteLine("   • Use --show-metrics to track performance and optimize model selection");
        Console.WriteLine();
        Console.WriteLine("📚 For more information, see the README or run 'dotnet run -- --help'");
        Console.WriteLine();
        return Task.CompletedTask;
    }
}
