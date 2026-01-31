#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Consistency Report Type Definitions
// Represents logical consistency checking results
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Report of logical consistency checking.
/// </summary>
/// <remarks>
/// LogicalConflict objects in the Conflicts list may have null Rule1 and Rule2 
/// when conflicts are detected through LLM analysis but the specific conflicting 
/// rules cannot be programmatically identified.
/// </remarks>
public sealed record ConsistencyReport(
    bool IsConsistent,
    List<LogicalConflict> Conflicts,
    List<string> MissingPrerequisites,
    List<string> Suggestions,
    double ConsistencyScore);

/// <summary>
/// Represents a logical conflict between two rules.
/// Rules may be null when conflicts are detected through analysis but specific rules cannot be identified.
/// </summary>
public sealed record LogicalConflict(
    string Description,
    SymbolicRule? Rule1,
    SymbolicRule? Rule2,
    string Resolution);
