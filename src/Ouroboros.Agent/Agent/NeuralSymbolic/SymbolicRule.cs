#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Symbolic Rule Type Definitions
// Represents extracted or learned symbolic rules
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Represents a symbolic rule that can be used in reasoning.
/// </summary>
public sealed record SymbolicRule(
    string Name,
    string MeTTaRepresentation,
    string NaturalLanguageDescription,
    List<string> Preconditions,
    List<string> Effects,
    double Confidence,
    RuleSource Source);

/// <summary>
/// Source of a symbolic rule.
/// </summary>
public enum RuleSource
{
    /// <summary>Rule extracted from a learned skill.</summary>
    ExtractedFromSkill,

    /// <summary>Rule learned from experience.</summary>
    LearnedFromExperience,

    /// <summary>Rule provided by user.</summary>
    UserProvided,

    /// <summary>Rule inferred from hypothesis.</summary>
    InferredFromHypothesis
}
