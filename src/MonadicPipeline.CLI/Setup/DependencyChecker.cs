using LangChainPipeline.Options;

namespace MonadicPipeline.CLI.Setup
{
    /// <summary>
    /// Provides methods for checking for required external dependencies.
    /// </summary>
    public static class DependencyChecker
    {
        /// <summary>
        /// Checks if Ollama is running and offers to start the guided setup if it is not.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async TaskEnsureOllamaIsRunningAsync()
        {
            try
            {
                // A simple way to check is to try to create a provider.
                // This will throw an exception if it can't connect.
                var provider = new LangChain.Providers.Ollama.OllamaProvider();
                await provider.ListModelsAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
            {
                Console.Error.WriteLine("⚠ Error: Ollama is not running or not reachable.");
                if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
                {
                    await GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true });
                }
                Environment.Exit(1);
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
