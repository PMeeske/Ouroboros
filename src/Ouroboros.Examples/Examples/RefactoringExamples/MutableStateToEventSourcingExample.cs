// <copyright file="MutableStateToEventSourcingExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples.RefactoringExamples;

/// <summary>
/// Demonstrates refactoring from imperative mutable state to functional event sourcing.
/// This example shows the before/after patterns for converting traditional class-based
/// code to the Ouroboros PipelineBranch event sourcing pattern.
/// </summary>
public static class MutableStateToEventSourcingExample
{
    /// <summary>
    /// Runs the complete refactoring example demonstrating both patterns.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunExample()
    {
        Console.WriteLine("=== Mutable State to Event Sourcing Refactoring Example ===\n");

        // Show the problems with mutable state
        await RunMutableStateExample();

        Console.WriteLine("\n" + new string('=', 80) + "\n");

        // Show the benefits of event sourcing
        await RunEventSourcingExample();

        Console.WriteLine("\n=== Example Complete ===");
    }

    /// <summary>
    /// Demonstrates the traditional imperative approach with mutable state.
    /// Shows the problems: no audit trail, not thread-safe, hard to test.
    /// </summary>
    private static async Task RunMutableStateExample()
    {
        Console.WriteLine("### BEFORE: Imperative with Mutable State ###\n");

        var processor = new ImperativeDocumentProcessor();

        processor.Process("Initial document about functional programming");
        Console.WriteLine($"After Process: {processor.GetCurrent().Substring(0, 50)}...");

        processor.AddContext("Additional context about monads");
        Console.WriteLine($"After AddContext: {processor.GetCurrent().Substring(0, 50)}...");

        var history = processor.GetHistory();
        Console.WriteLine($"\nHistory count: {history.Count}");
        Console.WriteLine($"Process count: {processor.ProcessCount}");

        Console.WriteLine("\n⚠️  Problems with this approach:");
        Console.WriteLine("   - No visibility into what changed or when");
        Console.WriteLine("   - History is a mutable list (can be modified externally)");
        Console.WriteLine("   - Not thread-safe without locking");
        Console.WriteLine("   - Hard to test intermediate states");
        Console.WriteLine("   - No way to replay or undo operations");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Demonstrates the functional event sourcing approach with PipelineBranch.
    /// Shows the benefits: audit trail, immutability, thread-safe, testable.
    /// </summary>
    private static async Task RunEventSourcingExample()
    {
        Console.WriteLine("### AFTER: Functional Event Sourcing with PipelineBranch ###\n");

        // Create dependencies (mocked for example purposes)
        var vectorStore = new MockVectorStore();
        var dataSource = new DataSource { Name = "example-source" };
        var llm = new MockLLM();
        var embed = new MockEmbedding();

        // Create initial immutable branch
        var branch1 = new PipelineBranch("doc-processor", vectorStore, dataSource);
        Console.WriteLine($"Initial branch: {branch1.Name}, Events: {branch1.Events.Count}");

        // Process document - returns NEW branch
        var processArrow = FunctionalDocumentProcessorArrows.ProcessDocumentArrow(
            llm, embed, "functional programming");
        var branch2 = await processArrow(branch1);

        Console.WriteLine($"\nAfter Process:");
        Console.WriteLine($"  branch1 events: {branch1.Events.Count} (unchanged!)");
        Console.WriteLine($"  branch2 events: {branch2.Events.Count}");

        var lastState1 = branch2.Events
            .OfType<ReasoningStep>()
            .Select(e => e.State)
            .LastOrDefault();
        Console.WriteLine($"  Last state text: {lastState1?.Text.Substring(0, 50)}...");

        // Add context - returns ANOTHER new branch
        var contextArrow = FunctionalDocumentProcessorArrows.AddContextArrow(
            llm, "Additional context about monads");
        var branch3 = await contextArrow(branch2);

        Console.WriteLine($"\nAfter AddContext:");
        Console.WriteLine($"  branch1 events: {branch1.Events.Count} (still unchanged!)");
        Console.WriteLine($"  branch2 events: {branch2.Events.Count} (still unchanged!)");
        Console.WriteLine($"  branch3 events: {branch3.Events.Count}");

        // Query event history
        Console.WriteLine("\n📋 Event History:");
        foreach (var evt in branch3.Events)
        {
            if (evt is ReasoningStep rs)
            {
                Console.WriteLine($"  [{evt.Timestamp:HH:mm:ss}] ReasoningStep: {rs.State.Kind}");
                Console.WriteLine($"    Prompt: {rs.Prompt.Substring(0, Math.Min(50, rs.Prompt.Length))}...");
            }
            else if (evt is IngestBatch ib)
            {
                Console.WriteLine($"  [{evt.Timestamp:HH:mm:ss}] IngestBatch: {ib.Ids.Count} documents");
            }
        }

        // Demonstrate composability
        Console.WriteLine("\n🔗 Demonstrating Composability:");
        var composedPipeline = processArrow
            .Bind(contextArrow);

        var branch4 = await composedPipeline(new PipelineBranch("composed", vectorStore, dataSource));
        Console.WriteLine($"  Composed pipeline result: {branch4.Events.Count} events");

        // Demonstrate querying capabilities
        Console.WriteLine("\n🔍 Querying Event History:");

        var allDrafts = branch3.Events
            .OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<Draft>()
            .ToList();
        Console.WriteLine($"  Total drafts: {allDrafts.Count}");

        var stepsWithTools = branch3.Events
            .OfType<ReasoningStep>()
            .Where(e => e.ToolCalls != null && e.ToolCalls.Any())
            .ToList();
        Console.WriteLine($"  Steps with tool calls: {stepsWithTools.Count}");

        var recentEvents = branch3.Events
            .Where(e => e.Timestamp > DateTime.UtcNow.AddMinutes(-5))
            .ToList();
        Console.WriteLine($"  Recent events (last 5 min): {recentEvents.Count}");

        // Demonstrate ingestion events
        Console.WriteLine("\n📦 Demonstrating Ingestion:");
        var ingestArrow = FunctionalDocumentProcessorArrows.IngestDocumentsArrow(
            embed,
            new[] { "Doc 1", "Doc 2", "Doc 3" },
            "/example/path");

        var branch5 = await ingestArrow(branch3);
        Console.WriteLine($"  Branch after ingestion: {branch5.Events.Count} events");

        var totalIngested = branch5.Events
            .OfType<IngestBatch>()
            .Sum(e => e.Ids.Count);
        Console.WriteLine($"  Total documents ingested: {totalIngested}");

        Console.WriteLine("\n✅ Benefits of this approach:");
        Console.WriteLine("   - Complete audit trail with timestamps");
        Console.WriteLine("   - Immutable - original branches never change");
        Console.WriteLine("   - Thread-safe by design");
        Console.WriteLine("   - Easy to test with pure functions");
        Console.WriteLine("   - Can replay entire pipeline from scratch");
        Console.WriteLine("   - Composable using monadic operations");
    }

    #region Imperative (Before) Implementation

    /// <summary>
    /// Traditional imperative document processor with mutable state.
    /// </summary>
    private class ImperativeDocumentProcessor
    {
        private string _currentDocument = string.Empty;
        private List<string> _history = new();
        private int _processCount = 0;

        public int ProcessCount => _processCount;

        public void Process(string document)
        {
            // Mutate state in-place - no record of changes
            _currentDocument = Transform(document);
            _history.Add(_currentDocument);
            _processCount++;
        }

        public void AddContext(string context)
        {
            // More mutation - tracking changes is manual
            _currentDocument += "\n\n" + context;
            _history.Add(_currentDocument);
        }

        public string GetCurrent() => _currentDocument;

        public List<string> GetHistory() => _history; // Exposes mutable list!

        private string Transform(string doc) =>
            $"Transformed: {doc}";
    }

    #endregion

    #region Functional (After) Implementation

    /// <summary>
    /// Functional document processor using PipelineBranch event sourcing.
    /// </summary>
    public static class FunctionalDocumentProcessorArrows
    {
        /// <summary>
        /// Creates an arrow that processes a document.
        /// Returns a NEW immutable PipelineBranch with the event recorded.
        /// </summary>
        public static Step<PipelineBranch, PipelineBranch> ProcessDocumentArrow(
            MockLLM llm,
            MockEmbedding embed,
            string query) =>
            async branch =>
            {
                // Retrieve similar documents from vector store
                var docs = await branch.Store.GetSimilarDocuments(embed, query, amount: 8);
                var context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                // Generate transformed document
                var transformed = await llm.GenerateAsync(context);

                // Return NEW immutable branch with reasoning event
                return branch.WithReasoning(
                    new Draft(transformed),
                    $"Processed document with query: {query}");
            };

        /// <summary>
        /// Creates an arrow that adds contextual information.
        /// Demonstrates composability - can be chained with other arrows.
        /// </summary>
        public static Step<PipelineBranch, PipelineBranch> AddContextArrow(
            MockLLM llm,
            string additionalContext) =>
            async branch =>
            {
                // Get the last reasoning state (if any)
                var lastState = branch.Events
                    .OfType<ReasoningStep>()
                    .Select(e => e.State)
                    .LastOrDefault();

                var currentText = lastState?.Text ?? "";
                var enhanced = await llm.GenerateAsync($"{currentText}\n\nContext: {additionalContext}");

                // Return new branch with enhancement recorded
                return branch.WithReasoning(
                    new Draft(enhanced),
                    $"Added context: {additionalContext}");
            };

        /// <summary>
        /// Creates an arrow that ingests documents into the vector store.
        /// Records the ingestion as an event for audit trail.
        /// </summary>
        public static Step<PipelineBranch, PipelineBranch> IngestDocumentsArrow(
            MockEmbedding embed,
            IEnumerable<string> documents,
            string sourcePath) =>
            async branch =>
            {
                // Ingest documents into vector store
                var ids = new List<string>();
                foreach (var doc in documents)
                {
                    var id = await branch.Store.AddDocumentAsync(embed, doc);
                    ids.Add(id);
                }

                // Return new branch with ingestion event recorded
                return branch.WithIngestEvent(sourcePath, ids);
            };
    }

    #endregion

    #region Mock Implementations (for demonstration purposes)

    /// <summary>
    /// Mock vector store for demonstration.
    /// </summary>
    private class MockVectorStore : IVectorStore
    {
        private readonly List<Document> _documents = new();

        public Task<IReadOnlyCollection<Document>> GetSimilarDocuments(
            IEmbeddingModel embed,
            string query,
            int amount)
        {
            // Return mock documents
            var docs = Enumerable.Range(1, Math.Min(amount, 3))
                .Select(i => new Document
                {
                    PageContent = $"Document {i} content about {query}",
                    Metadata = new Dictionary<string, object>()
                })
                .ToList();

            return Task.FromResult<IReadOnlyCollection<Document>>(docs);
        }

        public Task<string> AddDocumentAsync(IEmbeddingModel embed, string content)
        {
            var id = Guid.NewGuid().ToString();
            _documents.Add(new Document
            {
                PageContent = content,
                Metadata = new Dictionary<string, object> { ["id"] = id }
            });
            return Task.FromResult(id);
        }
    }

    /// <summary>
    /// Mock LLM for demonstration.
    /// </summary>
    public class MockLLM
    {
        public Task<string> GenerateAsync(string prompt)
        {
            return Task.FromResult($"Generated response for: {prompt.Substring(0, Math.Min(30, prompt.Length))}...");
        }
    }

    /// <summary>
    /// Mock embedding model for demonstration.
    /// </summary>
    public class MockEmbedding : IEmbeddingModel
    {
        public Task<float[]> EmbedAsync(string text)
        {
            return Task.FromResult(new float[384]); // Mock 384-dim embedding
        }
    }

    #endregion
}
