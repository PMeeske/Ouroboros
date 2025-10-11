// ============================================================================
// Original LangChain Pivot Implementation
// This example demonstrates the "traditional" LangChain approach as a reference
// for comparison with the MonadicPipeline's functional approach.
// ============================================================================
// Dependencies: LangChain, LangChain.Databases.Sqlite, LangChain.DocumentLoaders.Pdf
// Note: These dependencies are not included in the project by default.
// To use this example, add the following packages:
// - LangChain.Databases.Sqlite
// - LangChain.DocumentLoaders.Pdf
// - LangChain.Providers.OpenAI (or compatible provider)
// ============================================================================

namespace LangChainPipeline.Examples;

/// <summary>
/// Pivot implementation demonstrating the original LangChain approach.
/// This serves as a reference for understanding how traditional LangChain RAG works
/// compared to the MonadicPipeline's functional approach.
/// 
/// Price to run from zero (create embeddings and request to LLM): ~$0.015
/// Price to re-run if database exists: ~$0.0004
/// </summary>
public static class OriginalLangChainPivotExample
{
    /// <summary>
    /// Demonstrates the original LangChain RAG pattern using direct async methods.
    /// This is the traditional imperative approach before monadic composition.
    /// </summary>
    /// <remarks>
    /// NOTE: This example requires additional packages that are not included by default:
    /// - LangChain.Databases.Sqlite
    /// - LangChain.DocumentLoaders.Pdf  
    /// - LangChain.Providers.OpenAI
    /// 
    /// Uncomment the code below and install packages to use this example.
    /// </remarks>
    public static Task RunOriginalLangChainRAGExample()
    {
        Console.WriteLine("=== ORIGINAL LANGCHAIN PIVOT IMPLEMENTATION ===");
        Console.WriteLine("This demonstrates the traditional LangChain approach\n");

        /*
        // NOTE: Uncomment this section after installing required packages:
        // - LangChain.Databases.Sqlite
        // - LangChain.DocumentLoaders.Pdf
        // - LangChain.Providers.OpenAI
        
        // Initialize models
        var provider = new OpenAiProvider(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
            throw new InvalidOperationException("OPENAI_API_KEY is not set"));
        var llm = new OpenAiLatestFastChatModel(provider);
        var embeddingModel = new TextEmbeddingV3SmallModel(provider);

        // Create vector database from Harry Potter book pdf
        using var vectorDatabase = new SqLiteVectorDatabase(dataSource: "vectors.db");
        var vectorCollection = await vectorDatabase.AddDocumentsFromAsync<PdfPigPdfLoader>(
            embeddingModel, // Used to convert text to embeddings
            dimensions: 1536, // Should be 1536 for TextEmbeddingV3SmallModel
            dataSource: DataSource.FromUrl("https://canonburyprimaryschool.co.uk/wp-content/uploads/2016/01/Joanne-K.-Rowling-Harry-Potter-Book-1-Harry-Potter-and-the-Philosophers-Stone-EnglishOnlineClub.com_.pdf"),
            collectionName: "harrypotter", // Can be omitted, use if you want to have multiple collections
            textSplitter: null); // Default is CharacterTextSplitter(ChunkSize = 4000, ChunkOverlap = 200)

        // ========================================
        // Method 1: Direct Async Methods (Imperative)
        // ========================================
        Console.WriteLine("Method 1: Using Direct Async Methods");
        
        // Find similar documents for the question
        const string question = "Who was drinking a unicorn blood?";
        var similarDocuments = await vectorCollection.GetSimilarDocuments(embeddingModel, question, amount: 5);

        // Use similar documents and LLM to answer the question
        var answer = await llm.GenerateAsync(
            $"""
             Use the following pieces of context to answer the question at the end.
             If the answer is not in context then just say that you don't know, don't try to make up an answer.
             Keep the answer as short as possible.

             {similarDocuments.AsString()}

             Question: {question}
             Helpful Answer:
             """);

        Console.WriteLine($"LLM answer: {answer}"); // Expected: The cloaked figure.

        // ========================================
        // Method 2: LangChain Chains (Declarative)
        // ========================================
        Console.WriteLine("\nMethod 2: Using LangChain Chains");
        
        var promptTemplate =
            @"Use the following pieces of context to answer the question at the end. If the answer is not in context then just say that you don't know, don't try to make up an answer. Keep the answer as short as possible. Always quote the context in your answer.
{context}
Question: {text}
Helpful Answer:";

        var chain =
            Set("Who was drinking a unicorn blood?")     // set the question (default key is "text")
            | RetrieveSimilarDocuments(vectorCollection, embeddingModel, amount: 5) // take 5 most similar documents
            | CombineDocuments(outputKey: "context")     // combine documents together and put them into context
            | Template(promptTemplate)                   // replace context and question in the prompt with their values
            | LLM(llm.UseConsoleForDebug());             // send the result to the language model
        
        var chainAnswer = await chain.RunAsync("text");  // get chain result

        Console.WriteLine("Chain Answer: " + chainAnswer);       // print the result

        Console.WriteLine($"\nLLM usage: {llm.Usage}");    // Print usage and price
        Console.WriteLine($"Embedding model usage: {embeddingModel.Usage}");
        
        */

        Console.WriteLine("⚠️  This example is disabled by default.");
        Console.WriteLine("To enable it, install the required packages:");
        Console.WriteLine("  - LangChain.Databases.Sqlite");
        Console.WriteLine("  - LangChain.DocumentLoaders.Pdf");
        Console.WriteLine("  - LangChain.Providers.OpenAI");
        Console.WriteLine("\nThen uncomment the code in this method.");
        Console.WriteLine("\n✓ Pivot implementation reference provided for comparison");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Compares the original LangChain approach with MonadicPipeline approach.
    /// </summary>
    public static void CompareApproaches()
    {
        Console.WriteLine("\n=== ORIGINAL LANGCHAIN vs MONADICPIPELINE ===\n");

        Console.WriteLine("ORIGINAL LANGCHAIN (Imperative/Chains):");
        Console.WriteLine("  ✓ Direct async/await calls");
        Console.WriteLine("  ✓ Chain syntax with pipe operators");
        Console.WriteLine("  ✓ Straightforward for simple use cases");
        Console.WriteLine("  ✗ Limited error handling");
        Console.WriteLine("  ✗ No type-safe composition guarantees");
        Console.WriteLine("  ✗ Difficult to test and reason about");

        Console.WriteLine("\nMONADICPIPELINE (Functional/Monadic):");
        Console.WriteLine("  ✓ Result<T> monad for safe error handling");
        Console.WriteLine("  ✓ Kleisli arrows for composable operations");
        Console.WriteLine("  ✓ Type-safe pipeline composition");
        Console.WriteLine("  ✓ Immutable state and event sourcing");
        Console.WriteLine("  ✓ Testable and mathematically sound");
        Console.WriteLine("  ✓ Category theory principles");

        Console.WriteLine("\nThe MonadicPipeline approach builds on LangChain's foundations");
        Console.WriteLine("while adding functional programming guarantees and safety.\n");
    }
}
