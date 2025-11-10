using System.Diagnostics;
using System.Runtime.InteropServices;
using LangChainPipeline.Options;

namespace MonadicPipeline.CLI.Setup;

/// <summary>
/// Provides guided setup for the local development environment.
/// </summary>
public static class GuidedSetup
{
    /// <summary>
    /// Runs the guided setup based on the provided options.
    /// </summary>
    /// <param name="options">The setup options.</param>
    public static async Task RunAsync(SetupOptions options)
    {
        Console.WriteLine("Welcome to the MonadicPipeline guided setup.");
        Console.WriteLine("This utility will help you configure your local development environment.");

        if (options.All)
        {
            await InstallOllamaAsync();
            await ConfigureAuthAsync();
            await InstallMeTTaAsync();
            await InstallVectorStoreAsync();
            Console.WriteLine("\nAll setup steps completed.");
            return;
        }

        if (options.InstallOllama) await InstallOllamaAsync();
        if (options.ConfigureAuth) await ConfigureAuthAsync();
        if (options.InstallMeTTa) await InstallMeTTaAsync();
        if (options.InstallVectorStore) await InstallVectorStoreAsync();

        Console.WriteLine("\nSelected setup steps completed.");
    }

    private static async Task InstallOllamaAsync()
    {
        Console.WriteLine("\n--- Ollama Installation ---");
        if (IsCommandAvailable("ollama"))
        {
            Console.WriteLine("Ollama appears to be installed already. Skipping.");
            return;
        }

        Console.WriteLine("Ollama is not found in your PATH. I can guide you through the installation.");
        if (!PromptYesNo("Do you want to install Ollama now?")) return;

        string url = "https://ollama.com/download";
        Console.WriteLine($"Please download and install Ollama from: {url}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("After installation, the installer should add Ollama to your PATH automatically.");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Console.WriteLine("The website provides a command to run in your terminal:");
            Console.WriteLine("curl -fsSL https://ollama.com/install.sh | sh");
        }

        Console.WriteLine("\nAfter installation, please restart your terminal and run 'ollama' to verify.");
        Console.WriteLine("You can then pull models, for example: ollama pull llama3");
        await Task.Delay(1000); // Give user time to read
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
        if (!PromptYesNo("Do you want to proceed with installation guidance for MeTTa?")) return;

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

        if (!PromptYesNo("Do you want to see the command to run a local Qdrant container?")) return;

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
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();
        return response == "y" || response == "yes";
    }

    private static bool IsCommandAvailable(string command)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                ArgumentList = { command },
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}
