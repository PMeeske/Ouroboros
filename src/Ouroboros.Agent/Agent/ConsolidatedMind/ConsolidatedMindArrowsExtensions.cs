// <copyright file="ConsolidatedMindArrowsExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Steps;

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Extended arrow factory methods for ConsolidatedMind with fully explicit dependency parameterization.
/// These methods demonstrate the arrow parameterization pattern where all dependencies
/// (including configuration) are explicit parameters rather than constructor-injected state.
/// </summary>
public static class ConsolidatedMindArrowsExtensions
{
    /// <summary>
    /// Creates an arrow factory that configures specialists and processes prompts.
    /// All dependencies are explicit parameters, demonstrating pure functional composition.
    /// </summary>
    /// <param name="specialists">The collection of specialized models to use.</param>
    /// <param name="config">Configuration for the mind behavior.</param>
    /// <param name="tools">Optional tool registry.</param>
    /// <returns>A factory function that creates processing arrows for any prompt.</returns>
    public static Func<string, Step<PipelineBranch, PipelineBranch>> CreateProcessingArrowFactory(
        IEnumerable<SpecializedModel> specialists,
        MindConfig config,
        ToolRegistry? tools = null)
    {
        // Create mind instance with explicit dependencies
        var mind = new ConsolidatedMind(config, tools);
        mind.RegisterSpecialists(specialists);

        return prompt => async branch =>
        {
            var response = await mind.ProcessAsync(prompt);

            ReasoningState state = response.ThinkingContent != null
                ? new Thinking(response.ThinkingContent)
                : new Draft(response.Response);

            return branch.WithReasoning(state, prompt, null);
        };
    }

    /// <summary>
    /// Creates a reasoning arrow with explicit specialist and configuration dependencies.
    /// This demonstrates the arrow parameterization pattern where the mind is configured
    /// at composition time rather than instantiation time.
    /// </summary>
    /// <param name="specialists">The collection of specialized models to use.</param>
    /// <param name="config">Configuration for the mind behavior.</param>
    /// <param name="embed">Embedding model for context retrieval.</param>
    /// <param name="topic">The topic for reasoning.</param>
    /// <param name="query">The query for context retrieval.</param>
    /// <param name="k">Number of similar documents to retrieve.</param>
    /// <returns>A pipeline step for intelligent reasoning.</returns>
    public static Step<PipelineBranch, PipelineBranch> ReasoningArrowWithExplicitConfig(
        IEnumerable<SpecializedModel> specialists,
        MindConfig config,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return async branch =>
        {
            // Configure mind with explicit dependencies
            var mind = new ConsolidatedMind(config, null);
            mind.RegisterSpecialists(specialists);

            // Retrieve context
            var docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            // Build prompt with context
            string prompt = $@"Context:
{context}

Topic: {topic}

Please provide a comprehensive response addressing the topic based on the context provided.";

            // Process through the consolidated mind
            var response = await mind.ProcessAsync(prompt);

            // Create appropriate reasoning state based on the response
            ReasoningState state = response.ThinkingContent != null
                ? new Thinking(response.ThinkingContent)
                : new Draft(response.Response);

            return branch.WithReasoning(state, prompt, null);
        };
    }

    /// <summary>
    /// Creates a Result-safe reasoning arrow with explicit dependencies and comprehensive error handling.
    /// </summary>
    /// <param name="specialists">The collection of specialized models to use.</param>
    /// <param name="config">Configuration for the mind behavior.</param>
    /// <param name="embed">Embedding model for context retrieval.</param>
    /// <param name="topic">The topic for reasoning.</param>
    /// <param name="query">The query for context retrieval.</param>
    /// <param name="k">Number of similar documents to retrieve.</param>
    /// <returns>A Kleisli arrow that returns a Result with the updated branch or error.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeReasoningArrowWithExplicitConfig(
        IEnumerable<SpecializedModel> specialists,
        MindConfig config,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return async branch =>
        {
            try
            {
                var result = await ReasoningArrowWithExplicitConfig(
                    specialists, config, embed, topic, query, k)(branch);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Reasoning failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Creates a complex task processing arrow with explicit dependencies.
    /// Demonstrates decomposition and synthesis with arrow parameterization.
    /// </summary>
    /// <param name="specialists">The collection of specialized models to use.</param>
    /// <param name="config">Configuration for the mind behavior.</param>
    /// <param name="embed">Embedding model for context retrieval.</param>
    /// <param name="task">The complex task description.</param>
    /// <param name="k">Number of documents for context.</param>
    /// <returns>A step for complex task processing.</returns>
    public static Step<PipelineBranch, PipelineBranch> ComplexTaskArrowWithExplicitConfig(
        IEnumerable<SpecializedModel> specialists,
        MindConfig config,
        IEmbeddingModel embed,
        string task,
        int k = 8)
    {
        return async branch =>
        {
            var mind = new ConsolidatedMind(config, null);
            mind.RegisterSpecialists(specialists);

            var docs = await branch.Store.GetSimilarDocuments(embed, task, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = $@"Context:
{context}

Complex Task: {task}";

            var response = await mind.ProcessComplexAsync(prompt);

            // Record thinking if available
            if (response.ThinkingContent != null)
            {
                branch = branch.WithReasoning(new Thinking(response.ThinkingContent), "thinking", null);
            }

            return branch.WithReasoning(new FinalSpec(response.Response), prompt, null);
        };
    }

    /// <summary>
    /// Creates a pre-configured arrow system with common specialists.
    /// This demonstrates how to create reusable arrow configurations.
    /// </summary>
    /// <param name="endpoint">Ollama Cloud endpoint.</param>
    /// <param name="apiKey">Ollama Cloud API key.</param>
    /// <param name="config">Mind configuration.</param>
    /// <param name="useHighQuality">Whether to use high-quality models.</param>
    /// <returns>A configured arrow system for common operations.</returns>
    public static ConfiguredMindArrowSystem CreateConfiguredSystem(
        string endpoint,
        string apiKey,
        MindConfig config,
        bool useHighQuality = false)
    {
        // Get specialist configurations
        var specialistConfigs = useHighQuality
            ? OllamaCloudDefaults.GetHighQualityConfigs()
            : OllamaCloudDefaults.GetAllDefaultConfigs();

        // Create specialists
        var specialists = new List<SpecializedModel>();
        foreach (var specConfig in specialistConfigs)
        {
            var model = new OllamaCloudChatModel(
                specConfig.Endpoint ?? endpoint,
                apiKey,
                specConfig.OllamaModel,
                new ChatRuntimeSettings { Temperature = (float)specConfig.Temperature });

            var specialist = new SpecializedModel(
                specConfig.Role,
                model,
                specConfig.OllamaModel,
                specConfig.Capabilities ?? Array.Empty<string>(),
                specConfig.Priority,
                specConfig.MaxTokens);

            specialists.Add(specialist);
        }

        return new ConfiguredMindArrowSystem(specialists, config);
    }

    /// <summary>
    /// Creates a minimal configured system for resource-constrained environments.
    /// </summary>
    /// <param name="endpoint">Ollama Cloud endpoint.</param>
    /// <param name="apiKey">Ollama Cloud API key.</param>
    /// <returns>A minimal arrow system.</returns>
    public static ConfiguredMindArrowSystem CreateMinimalSystem(string endpoint, string apiKey)
    {
        var config = MindConfig.Minimal();
        var specialistConfigs = OllamaCloudDefaults.GetMinimalConfigs();

        var specialists = new List<SpecializedModel>();
        foreach (var specConfig in specialistConfigs)
        {
            var model = new OllamaCloudChatModel(
                specConfig.Endpoint ?? endpoint,
                apiKey,
                specConfig.OllamaModel,
                new ChatRuntimeSettings { Temperature = (float)specConfig.Temperature });

            var specialist = new SpecializedModel(
                specConfig.Role,
                model,
                specConfig.OllamaModel,
                specConfig.Capabilities ?? Array.Empty<string>(),
                specConfig.Priority,
                specConfig.MaxTokens);

            specialists.Add(specialist);
        }

        return new ConfiguredMindArrowSystem(specialists, config);
    }
}

/// <summary>
/// Pre-configured ConsolidatedMind arrow system with explicit dependencies.
/// This class demonstrates the arrow parameterization pattern where configuration
/// is explicit and arrows are created on-demand rather than holding state.
/// </summary>
public sealed class ConfiguredMindArrowSystem
{
    private readonly IEnumerable<SpecializedModel> _specialists;
    private readonly MindConfig _config;

    internal ConfiguredMindArrowSystem(IEnumerable<SpecializedModel> specialists, MindConfig config)
    {
        _specialists = specialists;
        _config = config;
    }

    /// <summary>
    /// Creates a reasoning arrow for the given topic and query.
    /// Dependencies are passed explicitly, not stored as instance state.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> CreateReasoningArrow(
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
        => ConsolidatedMindArrowsExtensions.ReasoningArrowWithExplicitConfig(
            _specialists,
            _config,
            embed,
            topic,
            query,
            k);

    /// <summary>
    /// Creates a complex task arrow with the configured specialists and settings.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> CreateComplexTaskArrow(
        IEmbeddingModel embed,
        string task,
        int k = 8)
        => ConsolidatedMindArrowsExtensions.ComplexTaskArrowWithExplicitConfig(
            _specialists,
            _config,
            embed,
            task,
            k);

    /// <summary>
    /// Creates a processing arrow factory for custom prompts.
    /// </summary>
    public Func<string, Step<PipelineBranch, PipelineBranch>> CreateProcessingFactory(ToolRegistry? tools = null)
        => ConsolidatedMindArrowsExtensions.CreateProcessingArrowFactory(_specialists, _config, tools);

    /// <summary>
    /// Gets the configuration used by this system.
    /// </summary>
    public MindConfig Configuration => _config;

    /// <summary>
    /// Gets the specialists available in this system.
    /// </summary>
    public IEnumerable<SpecializedModel> Specialists => _specialists;
}
