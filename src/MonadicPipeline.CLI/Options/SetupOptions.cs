// <copyright file="SetupOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Options
{
    using CommandLine;

    /// <summary>
    /// Command line options for the guided setup wizard.
    /// </summary>
    [Verb("setup", HelpText = "Guided setup for local development environment.")]
    public class SetupOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to start guided installation for Ollama.
        /// </summary>
        [Option("ollama", HelpText = "Start guided installation for Ollama.")]
        public bool InstallOllama { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to start guided setup for external provider authentication.
        /// </summary>
        [Option("auth", HelpText = "Start guided setup for external provider authentication.")]
        public bool ConfigureAuth { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to start guided installation for the MeTTa engine.
        /// </summary>
        [Option("metta", HelpText = "Start guided installation for the MeTTa engine.")]
        public bool InstallMeTTa { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to start guided setup for a local vector store.
        /// </summary>
        [Option("vector-store", HelpText = "Start guided setup for a local vector store (e.g., Qdrant).")]
        public bool InstallVectorStore { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to run all setup steps interactively.
        /// </summary>
        [Option("all", HelpText = "Run all setup steps interactively.")]
        public bool All { get; set; }
    }
}
