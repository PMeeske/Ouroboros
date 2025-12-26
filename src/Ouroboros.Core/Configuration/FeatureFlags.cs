// <copyright file="FeatureFlags.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Configuration;

/// <summary>
/// Feature flags for evolutionary metacognitive control.
/// Enables modular enhancements for embodiment, affect, and self-model capabilities.
/// </summary>
public sealed record FeatureFlags
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "FeatureFlags";

    /// <summary>
    /// Gets or sets a value indicating whether embodiment features are enabled.
    /// Embodiment enables the system to interact with physical or virtual environments.
    /// </summary>
    public bool Embodiment { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether self-model features are enabled.
    /// Self-model enables the system to maintain and reason about its own capabilities and state.
    /// </summary>
    public bool SelfModel { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether affect features are enabled.
    /// Affect enables the system to model and respond to emotional and affective states.
    /// </summary>
    public bool Affect { get; set; } = false;

    /// <summary>
    /// Checks if any evolutionary features are enabled.
    /// </summary>
    /// <returns>True if at least one feature flag is enabled.</returns>
    public bool AnyEnabled() => Embodiment || SelfModel || Affect;

    /// <summary>
    /// Checks if all evolutionary features are enabled.
    /// </summary>
    /// <returns>True if all feature flags are enabled.</returns>
    public bool AllEnabled() => Embodiment && SelfModel && Affect;

    /// <summary>
    /// Gets a collection of enabled feature names.
    /// </summary>
    /// <returns>A read-only list of enabled feature names.</returns>
    public IReadOnlyList<string> GetEnabledFeatures()
    {
        var enabled = new List<string>();
        if (Embodiment) enabled.Add(nameof(Embodiment));
        if (SelfModel) enabled.Add(nameof(SelfModel));
        if (Affect) enabled.Add(nameof(Affect));
        return enabled.AsReadOnly();
    }

    /// <summary>
    /// Creates a new FeatureFlags instance with all features enabled.
    /// Useful for testing and development scenarios.
    /// </summary>
    /// <returns>A FeatureFlags instance with all flags set to true.</returns>
    public static FeatureFlags AllOn() => new()
    {
        Embodiment = true,
        SelfModel = true,
        Affect = true
    };

    /// <summary>
    /// Creates a new FeatureFlags instance with all features disabled.
    /// This is the default safe configuration.
    /// </summary>
    /// <returns>A FeatureFlags instance with all flags set to false.</returns>
    public static FeatureFlags AllOff() => new();
}
