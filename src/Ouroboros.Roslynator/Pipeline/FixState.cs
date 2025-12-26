using System.Collections.Immutable;

namespace Ouroboros.Roslynator.Pipeline;

/// <summary>
/// Light immutable state that flows through the pipeline.
/// Minimal and independent from CodeFixContext so it can be used in CLI/Roslynator/VS.
/// </summary>
public record FixState
{
    public Document Document { get; init; }
    public Diagnostic Diagnostic { get; init; }
    public SyntaxNode CurrentRoot { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public ImmutableArray<string> Changes { get; init; } = ImmutableArray<string>.Empty;

    public FixState(Document document, Diagnostic diagnostic, SyntaxNode root)
    {
        Document = document;
        Diagnostic = diagnostic;
        CurrentRoot = root;
        CancellationToken = CancellationToken.None;
    }

    public FixState WithNewRoot(SyntaxNode newRoot, string changeName) => this with
    {
        CurrentRoot = newRoot,
        Changes = Changes.Add(changeName)
    };

    public FixState WithCancellation(CancellationToken ct) => this with { CancellationToken = ct };
}