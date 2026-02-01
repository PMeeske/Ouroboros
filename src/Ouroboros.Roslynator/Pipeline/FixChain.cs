using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Ouroboros.Roslynator.Pipeline;

/// <summary>
/// Composable fix chain using arrow-based pipeline composition.
/// Provides a base for building lazy fix chains using Future composition.
/// </summary>
public abstract class FixChain
{
    public abstract string Title { get; }
    public abstract string EquivalenceKey { get; }

    /// <summary>
    /// Gets the pipeline builder function.
    /// Override to provide custom pipeline composition logic.
    /// </summary>
    protected virtual Func<Future<FixState>, Future<FixState>> PipelineBuilder =>
        DefinePipeline;

    /// <summary>
    /// Compose your pipeline here using the | operator on Future&lt;FixState&gt;.
    /// Nothing runs during DefinePipeline; only when RunAsync is called.
    /// </summary>
    protected abstract Future<FixState> DefinePipeline(Future<FixState> input);

    /// <summary>
    /// Register a Roslyn CodeFix that will execute the pipeline lazily when invoked.
    /// </summary>
    public Task RegisterAsync(CodeFixContext context)
    {
        var registrationFunc = FixChainArrows.CreateFixChain(Title, EquivalenceKey, PipelineBuilder);
        return registrationFunc(context);
    }

    /// <summary>
    /// Run the chain and return the changed Document (or original if no changes).
    /// This is used by CodeAction and CLI runners.
    /// </summary>
    public async Task<Document> ExecuteAsync(Document document, Diagnostic diagnostic, CancellationToken token)
    {
        var root = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
        var initial = new FixState(document, diagnostic, root!).WithCancellation(token);

        var future = new Future<FixState>(_ => Task.FromResult(initial));
        var pipeline = PipelineBuilder(future);

        var final = await pipeline.RunAsync(token).ConfigureAwait(false);

        return final.Changes.IsEmpty ? document : document.WithSyntaxRoot(final.CurrentRoot);
    }
}