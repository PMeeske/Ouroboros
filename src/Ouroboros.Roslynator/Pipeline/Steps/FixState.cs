#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.Roslynator.Pipeline.Steps;

/// <summary>
/// Represents the state of a code fix operation, holding the current syntax root and diagnostic being fixed.
/// </summary>
/// <param name="CurrentRoot">The current syntax root of the document being fixed.</param>
/// <param name="Diagnostic">The diagnostic that is being addressed by the fix.</param>
public record FixState(SyntaxNode CurrentRoot, Diagnostic Diagnostic)
{
    /// <summary>
    /// Gets a value indicating whether this state is valid for processing.
    /// </summary>
    public bool IsValid => CurrentRoot is not null && Diagnostic is not null;

    /// <summary>
    /// Creates a new FixState with an updated syntax root.
    /// </summary>
    /// <param name="newRoot">The new syntax root.</param>
    /// <returns>A new FixState with the updated root.</returns>
    public FixState WithRoot(SyntaxNode newRoot) => this with { CurrentRoot = newRoot };
}
