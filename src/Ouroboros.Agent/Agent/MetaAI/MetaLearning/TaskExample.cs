#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Task Example Type
// Represents an example for few-shot learning
// ==========================================================

namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Represents an example for few-shot task adaptation.
/// </summary>
public sealed record TaskExample(
    string Input,
    string ExpectedOutput,
    Dictionary<string, object>? Context = null,
    double? Importance = null);
