// <copyright file="SpecializedRole.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Defines specialized roles that sub-models can fulfill within the ConsolidatedMind.
/// Each role represents a distinct cognitive capability.
/// </summary>
public enum SpecializedRole
{
    /// <summary>
    /// Fast responder for simple queries and quick answers.
    /// Uses lightweight models optimized for speed.
    /// </summary>
    QuickResponse,

    /// <summary>
    /// Deep reasoning and logical analysis.
    /// Uses models optimized for chain-of-thought reasoning.
    /// </summary>
    DeepReasoning,

    /// <summary>
    /// Code generation, analysis, and debugging.
    /// Uses models trained specifically on code.
    /// </summary>
    CodeExpert,

    /// <summary>
    /// Creative writing, brainstorming, and ideation.
    /// Uses models with high creativity/temperature settings.
    /// </summary>
    Creative,

    /// <summary>
    /// Mathematical computations and formal logic.
    /// Uses models trained on mathematical reasoning.
    /// </summary>
    Mathematical,

    /// <summary>
    /// Analysis, critique, and evaluation of content.
    /// Uses models optimized for analytical tasks.
    /// </summary>
    Analyst,

    /// <summary>
    /// Synthesis and summarization of information.
    /// Uses models optimized for compression and extraction.
    /// </summary>
    Synthesizer,

    /// <summary>
    /// Planning and decomposition of complex tasks.
    /// Uses models with strong planning capabilities.
    /// </summary>
    Planner,

    /// <summary>
    /// Verification and fact-checking.
    /// Uses models for validation and consistency checks.
    /// </summary>
    Verifier,

    /// <summary>
    /// Meta-cognition and self-reflection.
    /// Orchestrates other models and makes routing decisions.
    /// </summary>
    MetaCognitive
}

/// <summary>
/// Represents a specialized sub-model within the ConsolidatedMind architecture.
/// Each specialist has a defined role, capabilities, and underlying model.
/// </summary>
/// <param name="Role">The cognitive role this specialist fulfills.</param>
/// <param name="Model">The underlying chat completion model.</param>
/// <param name="ModelName">Human-readable name for the model.</param>
/// <param name="Capabilities">List of specific capabilities this specialist excels at.</param>
/// <param name="Priority">Priority weight for selection (higher = preferred).</param>
/// <param name="MaxTokens">Maximum tokens this model can handle.</param>
/// <param name="CostPerToken">Relative cost per token (for budget optimization).</param>
/// <param name="AverageLatencyMs">Expected average latency in milliseconds.</param>
public sealed record SpecializedModel(
    SpecializedRole Role,
    IChatCompletionModel Model,
    string ModelName,
    string[] Capabilities,
    double Priority = 1.0,
    int MaxTokens = 4096,
    double CostPerToken = 0.0,
    double AverageLatencyMs = 500.0)
{
    /// <summary>
    /// Calculates a fitness score for this specialist given a task description.
    /// </summary>
    /// <param name="taskCapabilities">Required capabilities for the task.</param>
    /// <returns>A fitness score between 0 and 1.</returns>
    public double CalculateFitness(string[] taskCapabilities)
    {
        if (taskCapabilities == null || taskCapabilities.Length == 0)
            return 0.5; // Default fitness for unknown tasks

        int matches = taskCapabilities.Count(tc =>
            Capabilities.Any(c => c.Equals(tc, StringComparison.OrdinalIgnoreCase)));

        double capabilityScore = (double)matches / taskCapabilities.Length;
        return capabilityScore * Priority;
    }
}

/// <summary>
/// Configuration for a specialized model including Ollama Cloud settings.
/// </summary>
/// <param name="Role">The role this configuration is for.</param>
/// <param name="OllamaModel">The Ollama model identifier (e.g., "llama3.1:70b").</param>
/// <param name="Endpoint">Optional custom endpoint (defaults to Ollama Cloud).</param>
/// <param name="Capabilities">Capabilities this model provides.</param>
/// <param name="Priority">Selection priority.</param>
/// <param name="MaxTokens">Maximum context length.</param>
/// <param name="Temperature">Temperature setting for generation.</param>
public sealed record SpecializedModelConfig(
    SpecializedRole Role,
    string OllamaModel,
    string? Endpoint = null,
    string[]? Capabilities = null,
    double Priority = 1.0,
    int MaxTokens = 4096,
    double Temperature = 0.7);
