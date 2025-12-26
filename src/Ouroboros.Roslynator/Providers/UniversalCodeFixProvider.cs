using Microsoft.CodeAnalysis.CodeFixes;
using Ouroboros.Roslynator.Pipeline;
using Ouroboros.Roslynator.Pipeline.Steps;
using System.Collections.Immutable;
using System.Composition;
using PipelineFixState = Ouroboros.Roslynator.Pipeline.FixState;

namespace Ouroboros.Roslynator.Providers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UniversalCodeFixProvider)), Shared]
public class UniversalCodeFixProvider : CodeFixProvider
{
    // Advertise diagnostics you want to support; Roslynator CLI can pass specific IDs.
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("CS8600", "CS8602", "IDE0008", "CS0168");

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Build the pipeline lazily (no heavy work here).
        var chain = new ConcreteChains.UniversalChain();

        // Register the code fix (Roslyn will call the CodeAction later).
        await chain.RegisterAsync(context).ConfigureAwait(false);
    }

    // Provide concrete chains as nested for discoverability; can be moved to separate files
    public static class ConcreteChains
    {
        public class UniversalChain : FixChain
        {
            public override string Title => "Fix (Standard + AI)";
            public override string EquivalenceKey => "Ouroboros.UniversalFix";

            protected override Future<PipelineFixState> DefinePipeline(Future<PipelineFixState> input)
            {
                return input
                    | StandardSteps.TryResolve               // fast deterministic fixes
                    | ThrottlingSteps.WithLock(OllamaSteps.GenerateFix) // throttled AI
                    | StandardSteps.FormatCode;              // cleanup/format
            }
        }
    }
}
