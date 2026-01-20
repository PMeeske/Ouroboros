// <copyright file="ChatRuntimeSettings.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Providers;

/// <summary>
/// Lightweight container describing runtime preferences for remote chats.
/// The historic implementation forwarded these values to OpenAI-compatible
/// backends; we keep the shape so callers can continue providing knobs.
/// </summary>
public sealed record ChatRuntimeSettings(
    double Temperature = 0.7,
    int MaxTokens = 512,
    int TimeoutSeconds = 60,
    bool Stream = false);
