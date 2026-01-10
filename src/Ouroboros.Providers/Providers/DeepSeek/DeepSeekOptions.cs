// <copyright file="DeepSeekOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.DeepSeek;

/// <summary>
/// Configuration options for DeepSeek models via Ollama.
/// </summary>
/// <param name="UseLocal">Whether to use local Ollama (true) or Ollama Cloud (false).</param>
/// <param name="LocalEndpoint">Local Ollama endpoint (default: http://localhost:11434).</param>
/// <param name="CloudEndpoint">Ollama Cloud endpoint URL.</param>
/// <param name="ApiKey">API key for Ollama Cloud access.</param>
/// <param name="DefaultModel">Default DeepSeek model (default: deepseek-r1:8b for local, deepseek-r1:32b for cloud).</param>
/// <param name="ReasoningModel">DeepSeek model for reasoning tasks (default: deepseek-r1:32b).</param>
public record DeepSeekOptions(
    bool UseLocal = true,
    string LocalEndpoint = "http://localhost:11434",
    string? CloudEndpoint = null,
    string? ApiKey = null,
    string? DefaultModel = null,
    string? ReasoningModel = "deepseek-r1:32b")
{
    /// <summary>
    /// Gets the effective default model based on local vs cloud usage.
    /// </summary>
    public string EffectiveDefaultModel => DefaultModel ?? (UseLocal ? "deepseek-r1:8b" : "deepseek-r1:32b");
};
