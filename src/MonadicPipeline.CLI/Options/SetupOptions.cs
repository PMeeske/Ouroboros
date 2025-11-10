using CommandLine;

namespace LangChainPipeline.Options
{
    [Verb("setup", HelpText = "Guided setup for local development environment.")]
    public class SetupOptions
    {
        [Option("ollama", HelpText = "Start guided installation for Ollama.")]
        public bool InstallOllama { get; set; }

        [Option("auth", HelpText = "Start guided setup for external provider authentication.")]
        public bool ConfigureAuth { get; set; }

        [Option("metta", HelpText = "Start guided installation for the MeTTa engine.")]
        public bool InstallMeTTa { get; set; }

        [Option("vector-store", HelpText = "Start guided setup for a local vector store (e.g., Qdrant).")]
        public bool InstallVectorStore { get; set; }

        [Option("all", HelpText = "Run all setup steps interactively.")]
        public bool All { get; set; }
    }
}
