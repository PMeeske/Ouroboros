#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// MeTTa Expression Type Definitions
// Represents parsed MeTTa expressions
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Represents a MeTTa expression with metadata.
/// </summary>
public sealed record MeTTaExpression(
    string RawExpression,
    ExpressionType Type,
    List<string> Symbols,
    List<string> Variables,
    Dictionary<string, object> Metadata);

/// <summary>
/// Type of MeTTa expression.
/// </summary>
public enum ExpressionType
{
    /// <summary>Atomic expression.</summary>
    Atom,

    /// <summary>Variable expression.</summary>
    Variable,

    /// <summary>Compound expression.</summary>
    Expression,

    /// <summary>Function expression.</summary>
    Function,

    /// <summary>Rule expression.</summary>
    Rule,

    /// <summary>Query expression.</summary>
    Query
}
