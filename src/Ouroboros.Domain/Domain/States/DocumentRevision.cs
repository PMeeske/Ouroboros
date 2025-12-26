#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Ouroboros.Domain.States;

/// <summary>
/// Represents an LLM-backed rewrite of a markdown document.
/// Stores the rewritten text along with metadata about the revision.
/// </summary>
/// <param name="FilePath">The absolute path of the markdown file being rewritten.</param>
/// <param name="RevisionText">The updated markdown content.</param>
/// <param name="Iteration">The 1-based iteration count for the revision loop.</param>
/// <param name="Goal">Optional goal or rubric supplied to the editor.</param>
public sealed record DocumentRevision(
    string FilePath,
    string RevisionText,
    int Iteration,
    string? Goal)
    : ReasoningState("DocumentRevision", RevisionText);
