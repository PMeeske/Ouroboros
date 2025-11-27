using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading;
using System.Threading.Tasks;

namespace LangChainPipeline.Roslynator.Pipeline;

/// <summary>
/// Template for building a lazy fix chain. Subclasses implement DefinePipeline which composes Futures.
/// </summary>
public abstract class FixChain
{
    public abstract string Title { get; }
    public abstract string EquivalenceKey { get; }

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
        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => ExecuteAsync(context.Document, context.Diagnostics[0], ct),
                equivalenceKey: EquivalenceKey
            ),
            context.Diagnostics);

        return Task.CompletedTask;
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
        var pipeline = DefinePipeline(future);

        var final = await pipeline.RunAsync(token).ConfigureAwait(false);

        return final.Changes.IsEmpty ? document : document.WithSyntaxRoot(final.CurrentRoot);
    }
}