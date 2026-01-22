// <copyright file="ConsciousnessOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Configuration options for consciousness scaffold.
/// Controls global workspace, attention mechanisms, and broadcast behavior.
/// </summary>
public sealed class ConsciousnessOptions
{
    /// <summary>
    /// Gets the default consciousness options.
    /// </summary>
    public static ConsciousnessOptions Default => new()
    {
        MaxWorkspaceSize = 100,
        MaxHighPriorityItems = 20,
        DefaultItemLifetime = TimeSpan.FromHours(1),
        MinAttentionThreshold = 0.3,
        BroadcastInterval = TimeSpan.FromMilliseconds(100),
        EnableGlobalBroadcast = true
    };

    /// <summary>
    /// Gets or sets the maximum number of items in global workspace.
    /// Default: 100 items.
    /// </summary>
    public int MaxWorkspaceSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of high-priority items.
    /// Default: 20 items.
    /// </summary>
    public int MaxHighPriorityItems { get; set; } = 20;

    /// <summary>
    /// Gets or sets the default lifetime for workspace items.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan DefaultItemLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the minimum attention threshold for item retention.
    /// Range: 0.0 to 1.0. Default: 0.3.
    /// </summary>
    public double MinAttentionThreshold { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the interval for broadcasting workspace updates.
    /// Default: 100 milliseconds.
    /// </summary>
    public TimeSpan BroadcastInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets a value indicating whether to enable global broadcast.
    /// Default: true.
    /// </summary>
    public bool EnableGlobalBroadcast { get; set; } = true;

    /// <summary>
    /// Gets or sets the attention policy configuration.
    /// Default: null (uses default policy).
    /// </summary>
    public string? AttentionPolicyType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable competition between items.
    /// Default: true.
    /// </summary>
    public bool EnableCompetition { get; set; } = true;
}
