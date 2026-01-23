// <copyright file="ChatRuntimeSettings.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Providers;

/// <summary>
/// Specifies how thinking/reasoning content should be handled.
/// </summary>
public enum ThinkingMode
{
    /// <summary>
    /// Thinking mode is disabled. No reasoning content will be extracted.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Thinking mode is enabled. Reasoning content will be extracted separately from response.
    /// Applies to models that support extended thinking (Claude, DeepSeek R1, o1, etc.).
    /// </summary>
    Enabled = 1,

    /// <summary>
    /// Auto-detect based on model response. If reasoning_content or thinking tags are present,
    /// extract them; otherwise treat as normal response.
    /// </summary>
    Auto = 2
}

/// <summary>
/// Lightweight container describing runtime preferences for remote chats.
/// The historic implementation forwarded these values to OpenAI-compatible
/// backends; we keep the shape so callers can continue providing knobs.
/// </summary>
public sealed record ChatRuntimeSettings(
    double Temperature = 0.7,
    int MaxTokens = 512,
    int TimeoutSeconds = 60,
    bool Stream = false,
    string? Culture = null,
    ThinkingMode ThinkingMode = ThinkingMode.Auto,
    int? ThinkingBudgetTokens = null);
