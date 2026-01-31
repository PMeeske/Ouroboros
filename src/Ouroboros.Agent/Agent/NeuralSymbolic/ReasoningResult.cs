#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Reasoning Result Type Definitions
// Represents results of hybrid reasoning operations
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Result of a hybrid reasoning operation.
/// </summary>
public sealed record ReasoningResult(
    string Query,
    string Answer,
    ReasoningMode ModeUsed,
    List<ReasoningStep> Steps,
    double Confidence,
    bool SymbolicSucceeded,
    bool NeuralSucceeded,
    TimeSpan Duration);

/// <summary>
/// A step in the reasoning process.
/// </summary>
public sealed record ReasoningStep(
    int StepNumber,
    string Description,
    string RuleApplied,
    ReasoningStepType Type);

/// <summary>
/// Type of reasoning step.
/// </summary>
public enum ReasoningStepType
{
    /// <summary>Symbolic deduction step.</summary>
    SymbolicDeduction,

    /// <summary>Symbolic induction step.</summary>
    SymbolicInduction,

    /// <summary>Neural inference step.</summary>
    NeuralInference,

    /// <summary>Combined neural-symbolic step.</summary>
    Combination
}

/// <summary>
/// Mode for hybrid reasoning operations.
/// </summary>
public enum ReasoningMode
{
    /// <summary>Try symbolic first, fall back to neural.</summary>
    SymbolicFirst,

    /// <summary>Try neural first, verify with symbolic.</summary>
    NeuralFirst,

    /// <summary>Run both in parallel, combine results.</summary>
    Parallel,

    /// <summary>Use only symbolic reasoning.</summary>
    SymbolicOnly,

    /// <summary>Use only neural reasoning.</summary>
    NeuralOnly
}
