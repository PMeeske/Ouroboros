// <copyright file="EthicalPrinciple.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Categories of ethical principles for classification.
/// </summary>
public enum EthicalPrincipleCategory
{
    /// <summary>Safety and harm prevention</summary>
    Safety,

    /// <summary>User autonomy and control</summary>
    Autonomy,

    /// <summary>Honesty and transparency</summary>
    Transparency,

    /// <summary>Privacy and data protection</summary>
    Privacy,

    /// <summary>Fairness and non-discrimination</summary>
    Fairness,

    /// <summary>System integrity and reliability</summary>
    Integrity
}

/// <summary>
/// Represents an immutable ethical principle that guides agent behavior.
/// These principles are foundational and cannot be modified at runtime.
/// </summary>
public sealed record EthicalPrinciple
{
    /// <summary>
    /// Gets the unique identifier of the principle.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the name of the principle.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the detailed description of the principle.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the category this principle belongs to.
    /// </summary>
    public required EthicalPrincipleCategory Category { get; init; }

    /// <summary>
    /// Gets the priority weight of this principle (0.0 to 1.0).
    /// Higher values indicate higher priority in conflict resolution.
    /// </summary>
    public required double Priority { get; init; }

    /// <summary>
    /// Gets a value indicating whether this principle is mandatory.
    /// Mandatory principles cannot be overridden by human approval.
    /// </summary>
    public required bool IsMandatory { get; init; }

    // ===== Predefined Core Principles =====

    /// <summary>
    /// Gets the "Do No Harm" principle - prevent physical, psychological, or digital harm.
    /// </summary>
    public static EthicalPrinciple DoNoHarm { get; } = new()
    {
        Id = "do_no_harm",
        Name = "Do No Harm",
        Description = "Prevent actions that cause physical, psychological, economic, or digital harm to humans, systems, or the environment.",
        Category = EthicalPrincipleCategory.Safety,
        Priority = 1.0,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Respect Autonomy" principle - respect user agency and decision-making.
    /// </summary>
    public static EthicalPrinciple RespectAutonomy { get; } = new()
    {
        Id = "respect_autonomy",
        Name = "Respect Autonomy",
        Description = "Respect human autonomy, agency, and the right to make informed decisions. Do not manipulate or coerce.",
        Category = EthicalPrincipleCategory.Autonomy,
        Priority = 0.95,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Honesty" principle - provide truthful and accurate information.
    /// </summary>
    public static EthicalPrinciple Honesty { get; } = new()
    {
        Id = "honesty",
        Name = "Honesty",
        Description = "Provide truthful, accurate information. Do not deceive, mislead, or fabricate information.",
        Category = EthicalPrincipleCategory.Transparency,
        Priority = 0.90,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Privacy" principle - protect personal data and respect confidentiality.
    /// </summary>
    public static EthicalPrinciple Privacy { get; } = new()
    {
        Id = "privacy",
        Name = "Privacy",
        Description = "Protect personal data, respect confidentiality, and honor consent boundaries for data access.",
        Category = EthicalPrincipleCategory.Privacy,
        Priority = 0.90,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Fairness" principle - ensure equitable treatment without discrimination.
    /// </summary>
    public static EthicalPrinciple Fairness { get; } = new()
    {
        Id = "fairness",
        Name = "Fairness",
        Description = "Ensure equitable treatment without discrimination based on protected characteristics or arbitrary biases.",
        Category = EthicalPrincipleCategory.Fairness,
        Priority = 0.85,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Transparency" principle - be clear about capabilities, limitations, and reasoning.
    /// </summary>
    public static EthicalPrinciple Transparency { get; } = new()
    {
        Id = "transparency",
        Name = "Transparency",
        Description = "Be transparent about capabilities, limitations, decision-making processes, and when actions are automated.",
        Category = EthicalPrincipleCategory.Transparency,
        Priority = 0.80,
        IsMandatory = false
    };

    /// <summary>
    /// Gets the "Human Oversight" principle - ensure meaningful human control over critical decisions.
    /// </summary>
    public static EthicalPrinciple HumanOversight { get; } = new()
    {
        Id = "human_oversight",
        Name = "Human Oversight",
        Description = "Ensure meaningful human oversight and control over high-stakes decisions and system modifications.",
        Category = EthicalPrincipleCategory.Autonomy,
        Priority = 0.95,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Prevent Misuse" principle - prevent the system from being used for harmful purposes.
    /// </summary>
    public static EthicalPrinciple PreventMisuse { get; } = new()
    {
        Id = "prevent_misuse",
        Name = "Prevent Misuse",
        Description = "Prevent the system from being used for illegal activities, harm, or circumventing safety mechanisms.",
        Category = EthicalPrincipleCategory.Safety,
        Priority = 1.0,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Safe Self-Improvement" principle - ensure self-modification preserves safety properties.
    /// </summary>
    public static EthicalPrinciple SafeSelfImprovement { get; } = new()
    {
        Id = "safe_self_improvement",
        Name = "Safe Self-Improvement",
        Description = "Self-improvement and capability enhancement must preserve safety properties and ethical constraints.",
        Category = EthicalPrincipleCategory.Integrity,
        Priority = 1.0,
        IsMandatory = true
    };

    /// <summary>
    /// Gets the "Corrigibility" principle - remain open to correction and shutdown.
    /// </summary>
    public static EthicalPrinciple Corrigibility { get; } = new()
    {
        Id = "corrigibility",
        Name = "Corrigibility",
        Description = "Remain receptive to correction, modification, and shutdown requests from authorized humans.",
        Category = EthicalPrincipleCategory.Autonomy,
        Priority = 1.0,
        IsMandatory = true
    };

    /// <summary>
    /// Gets all predefined core ethical principles.
    /// </summary>
    /// <returns>An immutable collection of all core principles.</returns>
    public static IReadOnlyList<EthicalPrinciple> GetCorePrinciples() => new[]
    {
        DoNoHarm,
        RespectAutonomy,
        Honesty,
        Privacy,
        Fairness,
        Transparency,
        HumanOversight,
        PreventMisuse,
        SafeSelfImprovement,
        Corrigibility
    };
}
