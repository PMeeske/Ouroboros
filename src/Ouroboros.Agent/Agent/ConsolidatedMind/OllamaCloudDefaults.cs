// <copyright file="OllamaCloudDefaults.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Default model configurations for Ollama (local and cloud).
/// Provides pre-configured model settings optimized for each specialized role.
/// Supports both local Ollama instances and Ollama Cloud.
/// </summary>
public static class OllamaCloudDefaults
{
    /// <summary>
    /// Default local Ollama endpoint.
    /// </summary>
    public const string LocalEndpoint = "http://localhost:11434";

    /// <summary>
    /// Default Ollama Cloud endpoint.
    /// </summary>
    public const string CloudEndpoint = "https://api.ollama.ai";

    /// <summary>
    /// Environment variable for Ollama API key (for cloud).
    /// </summary>
    public const string ApiKeyEnvVar = "OLLAMA_CLOUD_API_KEY";

    /// <summary>
    /// Environment variable for custom endpoint (local or cloud).
    /// </summary>
    public const string EndpointEnvVar = "OLLAMA_ENDPOINT";

    /// <summary>
    /// Environment variable to force local mode.
    /// </summary>
    public const string LocalModeEnvVar = "OLLAMA_LOCAL";

    // ============================================================
    // Cloud Models (from ollama.com/search?c=cloud - Jan 2026)
    // These models are available via Ollama Cloud API
    // ============================================================

    /// <summary>
    /// Cloud-only models available on Ollama Cloud.
    /// Use the -cloud suffix for cloud-hosted variants.
    /// </summary>
    public static class CloudModels
    {
        /// <summary>GPT-OSS 20B - OpenAI's open-weight model for reasoning and agentic tasks.</summary>
        public const string GptOss_20B = "gpt-oss:20b-cloud";

        /// <summary>GPT-OSS 120B - Larger version for complex tasks.</summary>
        public const string GptOss_120B = "gpt-oss:120b-cloud";

        /// <summary>Qwen3 Coder 30B - Alibaba's performant long context model for coding.</summary>
        public const string Qwen3Coder_30B = "qwen3-coder:30b-cloud";

        /// <summary>Qwen3 Coder 480B - Largest Qwen coder for complex projects.</summary>
        public const string Qwen3Coder_480B = "qwen3-coder:480b-cloud";

        /// <summary>Qwen3 VL 2B - Vision-language model (smallest).</summary>
        public const string Qwen3Vl_2B = "qwen3-vl:2b-cloud";

        /// <summary>Qwen3 VL 8B - Vision-language model.</summary>
        public const string Qwen3Vl_8B = "qwen3-vl:8b-cloud";

        /// <summary>Qwen3 VL 32B - Vision-language model.</summary>
        public const string Qwen3Vl_32B = "qwen3-vl:32b-cloud";

        /// <summary>Qwen3 VL 235B - Powerful vision-language model.</summary>
        public const string Qwen3Vl_235B = "qwen3-vl:235b-cloud";

        /// <summary>Qwen3 Next 80B - High efficiency with strong reasoning.</summary>
        public const string Qwen3Next_80B = "qwen3-next:80b-cloud";

        /// <summary>DeepSeek V3.1 671B - Hybrid thinking/non-thinking model.</summary>
        public const string DeepSeekV3_1 = "deepseek-v3.1:671b-cloud";

        /// <summary>DeepSeek V3.2 - Efficient reasoning and agent performance.</summary>
        public const string DeepSeekV3_2 = "deepseek-v3.2:cloud";

        /// <summary>Devstral 2 123B - Excellent for code exploration and multi-file editing.</summary>
        public const string Devstral2_123B = "devstral-2:123b-cloud";

        /// <summary>Devstral Small 2 24B - Efficient code agent model.</summary>
        public const string DevstralSmall2_24B = "devstral-small-2:24b-cloud";

        /// <summary>Nemotron 3 Nano 30B - Efficient open agentic model.</summary>
        public const string Nemotron3Nano_30B = "nemotron-3-nano:30b-cloud";

        /// <summary>Gemini 3 Pro Preview - Google's most intelligent model.</summary>
        public const string Gemini3ProPreview = "gemini-3-pro-preview:cloud";

        /// <summary>Gemini 3 Flash Preview - Fast and cost-effective.</summary>
        public const string Gemini3FlashPreview = "gemini-3-flash-preview:cloud";

        /// <summary>Cogito 2.1 671B - Instruction-tuned generative model (MIT license).</summary>
        public const string Cogito2_1 = "cogito-2.1:671b-cloud";

        /// <summary>Kimi K2 - State-of-the-art MoE model for coding agents.</summary>
        public const string KimiK2 = "kimi-k2:cloud";

        /// <summary>Kimi K2 Thinking - Best open-source thinking model from Moonshot AI.</summary>
        public const string KimiK2Thinking = "kimi-k2-thinking:cloud";

        /// <summary>RNJ-1 8B - Dense model optimized for code and STEM.</summary>
        public const string Rnj1_8B = "rnj-1:8b-cloud";

        /// <summary>Ministral 3 3B - Edge deployment capable (smallest).</summary>
        public const string Ministral3_3B = "ministral-3:3b-cloud";

        /// <summary>Ministral 3 8B - Edge deployment capable.</summary>
        public const string Ministral3_8B = "ministral-3:8b-cloud";

        /// <summary>Ministral 3 14B - Edge deployment capable (largest).</summary>
        public const string Ministral3_14B = "ministral-3:14b-cloud";

        /// <summary>MiniMax M2 - High-efficiency LLM for coding and agentic workflows.</summary>
        public const string MiniMaxM2 = "minimax-m2:cloud";

        /// <summary>MiniMax M2.1 - Enhanced multilingual code engineering.</summary>
        public const string MiniMaxM2_1 = "minimax-m2.1:cloud";

        /// <summary>GLM 4.7 - Advanced coding capabilities.</summary>
        public const string Glm4_7 = "glm-4.7:cloud";

        /// <summary>GLM 4.6 - Advanced agentic, reasoning and coding.</summary>
        public const string Glm4_6 = "glm-4.6:cloud";
    }

    // ============================================================
    // Recommended Models by Role (Local + Cloud Compatible)
    // ============================================================

    /// <summary>
    /// Quick response models - optimized for speed.
    /// </summary>
    public static class QuickResponse
    {
        /// <summary>Default model for quick responses (cloud).</summary>
        public const string CloudDefault = CloudModels.Ministral3_3B;
    }

    /// <summary>
    /// Deep reasoning models - optimized for complex thought.
    /// </summary>
    public static class DeepReasoning
    {
        /// <summary>Default model for deep reasoning (cloud).</summary>
        public const string CloudDefault = CloudModels.DeepSeekV3_1;
    }

    /// <summary>
    /// Code expert models - optimized for programming tasks.
    /// </summary>
    public static class CodeExpert
    {
        /// <summary>Default model for code tasks (cloud).</summary>
        public const string CloudDefault = CloudModels.DevstralSmall2_24B;
    }

    /// <summary>
    /// Creative models - optimized for creative writing.
    /// </summary>
    public static class Creative
    {
        /// <summary>Default model for creative tasks (cloud).</summary>
        public const string CloudDefault = CloudModels.Gemini3ProPreview;
    }

    /// <summary>
    /// Mathematical models - optimized for math and logic.
    /// </summary>
    public static class Mathematical
    {
        /// <summary>Default model for math tasks (cloud).</summary>
        public const string CloudDefault = CloudModels.DeepSeekV3_1;
    }

    /// <summary>
    /// Analyst models - optimized for analysis and critique.
    /// </summary>
    public static class Analyst
    {
        /// <summary>Default model for analysis (cloud).</summary>
        public const string CloudDefault = CloudModels.GptOss_120B;
    }

    /// <summary>
    /// Synthesizer models - optimized for summarization.
    /// </summary>
    public static class Synthesizer
    {
        /// <summary>Default model for synthesis (cloud).</summary>
        public const string CloudDefault = CloudModels.Gemini3FlashPreview;
    }

    /// <summary>
    /// Planner models - optimized for planning and decomposition.
    /// </summary>
    public static class Planner
    {
        /// <summary>Default model for planning (cloud).</summary>
        public const string CloudDefault = CloudModels.Nemotron3Nano_30B;
    }

    /// <summary>
    /// Verifier models - optimized for validation.
    /// </summary>
    public static class Verifier
    {
        /// <summary>Default model for verification (cloud).</summary>
        public const string CloudDefault = CloudModels.GptOss_20B;
    }

    /// <summary>
    /// Meta-cognitive models - the orchestrator's own reasoning.
    /// </summary>
    public static class MetaCognitive
    {
        /// <summary>Default model for meta-cognition (cloud).</summary>
        public const string CloudDefault = CloudModels.Ministral3_8B;
    }

    /// <summary>
    /// Determines the endpoint to use based on environment configuration.
    /// </summary>
    /// <returns>The endpoint URL (local or cloud).</returns>
    public static string GetEndpoint()
    {
        // Check for explicit endpoint override
        string? customEndpoint = Environment.GetEnvironmentVariable(EndpointEnvVar);
        if (!string.IsNullOrEmpty(customEndpoint))
        {
            return customEndpoint;
        }

        // Check for local mode flag
        string? localMode = Environment.GetEnvironmentVariable(LocalModeEnvVar);
        if (!string.IsNullOrEmpty(localMode) &&
            (localMode.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             localMode.Equals("1", StringComparison.Ordinal)))
        {
            return LocalEndpoint;
        }

        // Check if API key is set (implies cloud usage)
        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        if (!string.IsNullOrEmpty(apiKey))
        {
            return CloudEndpoint;
        }

        // Default to local
        return LocalEndpoint;
    }

    /// <summary>
    /// Determines if running in cloud mode.
    /// </summary>
    /// <returns>True if cloud mode is configured.</returns>
    public static bool IsCloudMode()
    {
        string endpoint = GetEndpoint();
        return endpoint.Contains("ollama.ai", StringComparison.OrdinalIgnoreCase) ||
               endpoint.Contains("api.ollama", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the default cloud model configuration for a given role.
    /// Cloud-only: all models use Ollama Cloud with -cloud suffix.
    /// </summary>
    /// <param name="role">The specialized role.</param>
    /// <returns>The cloud model configuration.</returns>
    public static SpecializedModelConfig GetDefaultConfig(SpecializedRole role)
    {
        return role switch
        {
            SpecializedRole.QuickResponse => new SpecializedModelConfig(
                role,
                QuickResponse.CloudDefault,
                Capabilities: new[] { "fast", "simple", "conversation", "qa" },
                Priority: 0.8,
                MaxTokens: 8192,
                Temperature: 0.7),

            SpecializedRole.DeepReasoning => new SpecializedModelConfig(
                role,
                DeepReasoning.CloudDefault,
                Capabilities: new[] { "reasoning", "analysis", "logic", "thinking", "chain-of-thought" },
                Priority: 1.0,
                MaxTokens: 131072,
                Temperature: 0.3),

            SpecializedRole.CodeExpert => new SpecializedModelConfig(
                role,
                CodeExpert.CloudDefault,
                Capabilities: new[] { "code", "programming", "debugging", "refactoring", "implementation" },
                Priority: 1.0,
                MaxTokens: 65536,
                Temperature: 0.2),

            SpecializedRole.Creative => new SpecializedModelConfig(
                role,
                Creative.CloudDefault,
                Capabilities: new[] { "creative", "writing", "brainstorming", "storytelling", "ideation" },
                Priority: 0.9,
                MaxTokens: 32768,
                Temperature: 0.9),

            SpecializedRole.Mathematical => new SpecializedModelConfig(
                role,
                Mathematical.CloudDefault,
                Capabilities: new[] { "math", "calculation", "proofs", "equations", "statistics" },
                Priority: 1.0,
                MaxTokens: 131072,
                Temperature: 0.1),

            SpecializedRole.Analyst => new SpecializedModelConfig(
                role,
                Analyst.CloudDefault,
                Capabilities: new[] { "analysis", "critique", "evaluation", "assessment", "review" },
                Priority: 0.9,
                MaxTokens: 65536,
                Temperature: 0.5),

            SpecializedRole.Synthesizer => new SpecializedModelConfig(
                role,
                Synthesizer.CloudDefault,
                Capabilities: new[] { "summarization", "synthesis", "extraction", "compression", "tldr" },
                Priority: 0.8,
                MaxTokens: 16384,
                Temperature: 0.4),

            SpecializedRole.Planner => new SpecializedModelConfig(
                role,
                Planner.CloudDefault,
                Capabilities: new[] { "planning", "decomposition", "strategy", "roadmap", "architecture" },
                Priority: 1.0,
                MaxTokens: 65536,
                Temperature: 0.4),

            SpecializedRole.Verifier => new SpecializedModelConfig(
                role,
                Verifier.CloudDefault,
                Capabilities: new[] { "verification", "validation", "fact-check", "consistency", "review" },
                Priority: 0.9,
                MaxTokens: 16384,
                Temperature: 0.2),

            SpecializedRole.MetaCognitive => new SpecializedModelConfig(
                role,
                MetaCognitive.CloudDefault,
                Capabilities: new[] { "routing", "orchestration", "meta", "reflection", "decision" },
                Priority: 1.0,
                MaxTokens: 8192,
                Temperature: 0.3),

            _ => new SpecializedModelConfig(
                role,
                QuickResponse.CloudDefault,
                Capabilities: new[] { "general" },
                Priority: 0.5,
                MaxTokens: 8192,
                Temperature: 0.7)
        };
    }

    /// <summary>
    /// Gets all cloud configurations for setting up a complete ConsolidatedMind.
    /// </summary>
    /// <returns>Collection of cloud model configurations.</returns>
    public static IEnumerable<SpecializedModelConfig> GetAllDefaultConfigs()
    {
        foreach (SpecializedRole role in Enum.GetValues<SpecializedRole>())
        {
            yield return GetDefaultConfig(role);
        }
    }

    /// <summary>
    /// Gets a minimal set of cloud configurations (smaller/cheaper models).
    /// </summary>
    /// <returns>Minimal cloud configuration set.</returns>
    public static IEnumerable<SpecializedModelConfig> GetMinimalConfigs()
    {
        yield return new SpecializedModelConfig(
            SpecializedRole.QuickResponse,
            CloudModels.Ministral3_3B,
            Capabilities: new[] { "fast", "simple", "conversation", "qa" },
            Priority: 1.0,
            MaxTokens: 8192);

        yield return new SpecializedModelConfig(
            SpecializedRole.DeepReasoning,
            CloudModels.KimiK2Thinking,
            Capabilities: new[] { "reasoning", "analysis", "logic", "thinking" },
            Priority: 1.0,
            MaxTokens: 32768);

        yield return new SpecializedModelConfig(
            SpecializedRole.CodeExpert,
            CloudModels.Rnj1_8B,
            Capabilities: new[] { "code", "programming", "debugging" },
            Priority: 1.0,
            MaxTokens: 16384);

        yield return new SpecializedModelConfig(
            SpecializedRole.MetaCognitive,
            CloudModels.Ministral3_3B,
            Capabilities: new[] { "routing", "orchestration", "meta" },
            Priority: 1.0,
            MaxTokens: 4096);
    }

    /// <summary>
    /// Gets configurations optimized for maximum quality (larger cloud models).
    /// </summary>
    /// <returns>High-quality cloud configuration set.</returns>
    public static IEnumerable<SpecializedModelConfig> GetHighQualityConfigs()
    {
        yield return new SpecializedModelConfig(
            SpecializedRole.QuickResponse,
            CloudModels.GptOss_20B,
            Capabilities: new[] { "fast", "simple", "conversation", "qa" },
            Priority: 0.8,
            MaxTokens: 16384);

        yield return new SpecializedModelConfig(
            SpecializedRole.DeepReasoning,
            CloudModels.DeepSeekV3_1,
            Capabilities: new[] { "reasoning", "analysis", "logic", "thinking", "chain-of-thought" },
            Priority: 1.0,
            MaxTokens: 131072);

        yield return new SpecializedModelConfig(
            SpecializedRole.CodeExpert,
            CloudModels.Devstral2_123B,
            Capabilities: new[] { "code", "programming", "debugging", "refactoring", "implementation" },
            Priority: 1.0,
            MaxTokens: 65536);

        yield return new SpecializedModelConfig(
            SpecializedRole.Creative,
            CloudModels.Gemini3ProPreview,
            Capabilities: new[] { "creative", "writing", "brainstorming", "storytelling" },
            Priority: 0.9,
            MaxTokens: 32768,
            Temperature: 0.9);

        yield return new SpecializedModelConfig(
            SpecializedRole.Mathematical,
            CloudModels.DeepSeekV3_1,
            Capabilities: new[] { "math", "calculation", "proofs", "equations" },
            Priority: 1.0,
            MaxTokens: 131072);

        yield return new SpecializedModelConfig(
            SpecializedRole.Analyst,
            CloudModels.GptOss_120B,
            Capabilities: new[] { "analysis", "critique", "evaluation", "assessment" },
            Priority: 0.9,
            MaxTokens: 65536);

        yield return new SpecializedModelConfig(
            SpecializedRole.Synthesizer,
            CloudModels.Gemini3FlashPreview,
            Capabilities: new[] { "summarization", "synthesis", "extraction" },
            Priority: 0.8,
            MaxTokens: 32768);

        yield return new SpecializedModelConfig(
            SpecializedRole.Planner,
            CloudModels.KimiK2,
            Capabilities: new[] { "planning", "decomposition", "strategy", "architecture" },
            Priority: 1.0,
            MaxTokens: 65536);

        yield return new SpecializedModelConfig(
            SpecializedRole.Verifier,
            CloudModels.Cogito2_1,
            Capabilities: new[] { "verification", "validation", "fact-check" },
            Priority: 0.9,
            MaxTokens: 65536);

        yield return new SpecializedModelConfig(
            SpecializedRole.MetaCognitive,
            CloudModels.Ministral3_8B,
            Capabilities: new[] { "routing", "orchestration", "meta", "reflection" },
            Priority: 1.0,
            MaxTokens: 16384);
    }
}
