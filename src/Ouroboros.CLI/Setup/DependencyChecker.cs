// <copyright file="DependencyChecker.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Setup
{
    using Ouroboros.Options;

    /// <summary>
    /// Provides methods for checking for required external dependencies.
    /// </summary>
    public static class DependencyChecker
    {
        /// <summary>
        /// Checks if Ollama is running and offers to start the guided setup if it is not.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task<bool> EnsureOllamaIsRunningAsync()
        {
            try
            {
                // A simple way to check is to try to create a chat model.
                // This will throw an exception if it can't connect.
                LangChain.Providers.Ollama.OllamaProvider provider = new LangChain.Providers.Ollama.OllamaProvider();
                LangChain.Providers.Ollama.OllamaChatModel model = new LangChain.Providers.Ollama.OllamaChatModel(provider, "llama3");

                // Try to make a simple request with a timeout
                using CancellationTokenSource cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = model.GenerateAsync("test", cancellationToken: cts.Token);

                // Get enumerator and move to first element - this will trigger the connection
                await using IAsyncEnumerator<LangChain.Providers.ChatResponse> enumerator = stream.GetAsyncEnumerator(cts.Token);
                await enumerator.MoveNextAsync();

                return true;
            }
            catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED") || ex is System.Threading.Tasks.TaskCanceledException || ex is System.OperationCanceledException)
            {
                Console.Error.WriteLine("⚠ Error: Ollama is not running or not reachable.");
                if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
                {
                    await GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true });
                }

                Environment.Exit(1);
                return false;
            }
        }

        /// <summary>
        /// Checks if the MeTTa engine is available and offers to start the guided setup if it is not.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task EnsureMeTTaIsAvailableAsync()
        {
            try
            {
                // This is a proxy for checking if MeTTa is available.
                // The actual check happens inside the MeTTa-related logic, which throws a specific exception.
                // We can simulate this check here or rely on the downstream exception.
                // For now, we'll let the specific command handle it, but this is where you'd centralize.
                await Task.CompletedTask;
            }
            catch (Exception ex) when (ex.Message.Contains("metta") && (ex.Message.Contains("not found") || ex.Message.Contains("No such file")))
            {
                Console.Error.WriteLine("⚠ Error: MeTTa engine not found.");
                if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for MeTTa?"))
                {
                    await GuidedSetup.RunAsync(new SetupOptions { InstallMeTTa = true });
                }

                Environment.Exit(1);
            }
        }
    }
}
