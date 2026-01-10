// <copyright file="HybridRoutingConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Routing;

/// <summary>
/// Configuration for hybrid model routing based on task type.
/// </summary>
/// <param name="DefaultModel">Default model for simple/unknown tasks.</param>
/// <param name="ReasoningModel">Optional model for reasoning tasks (uses DefaultModel if null).</param>
/// <param name="PlanningModel">Optional model for planning tasks (uses DefaultModel if null).</param>
/// <param name="CodingModel">Optional model for code generation tasks (uses DefaultModel if null).</param>
/// <param name="FallbackModel">Optional fallback model when primary models fail (uses DefaultModel if null).</param>
/// <param name="DetectionStrategy">Strategy for detecting task types (default: Heuristic).</param>
public record HybridRoutingConfig(
    IChatCompletionModel DefaultModel,
    IChatCompletionModel? ReasoningModel = null,
    IChatCompletionModel? PlanningModel = null,
    IChatCompletionModel? CodingModel = null,
    IChatCompletionModel? FallbackModel = null,
    TaskDetectionStrategy DetectionStrategy = TaskDetectionStrategy.Heuristic);

/// <summary>
/// Configuration options for hybrid routing with Ollama-based models.
/// </summary>
/// <param name="Enabled">Whether hybrid routing is enabled (default: true).</param>
/// <param name="DefaultOllamaModel">Default Ollama model for simple tasks (default: llama3.1:8b).</param>
/// <param name="ReasoningOllamaModel">Ollama model for reasoning tasks (default: deepseek-r1:32b).</param>
/// <param name="UseDeepSeekForPlanning">Whether to use DeepSeek for planning tasks (default: true).</param>
/// <param name="FallbackToLocal">Whether to fallback to local models when cloud unavailable (default: true).</param>
public record HybridRoutingOptions(
    bool Enabled = true,
    string DefaultOllamaModel = "llama3.1:8b",
    string ReasoningOllamaModel = "deepseek-r1:32b",
    bool UseDeepSeekForPlanning = true,
    bool FallbackToLocal = true);
