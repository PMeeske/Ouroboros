using LangChain.Databases;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the TrackedVectorStore fix to verify the PR issues are resolved.
/// </summary>
// ReSharper disable once UnusedMember.Global
public static class TrackedVectorStoreTests
{
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running TrackedVectorStore Tests ===");
        
        await TestAddAsync();
        await TestGetSimilarDocumentsAsync();
        await TestClearAsync();
        TestGetAll();
        
        Console.WriteLine("✓ All TrackedVectorStore tests passed!");
    }

    private static async Task TestAddAsync()
    {
        Console.WriteLine("Testing AddAsync...");
        
        var store = new TrackedVectorStore();
        var vectors = new List<Vector>
        {
            new()
            {
                Id = "test1",
                Text = "This is a test document",
                Embedding = [1f, 0f, 0f],
                Metadata = new Dictionary<string, object> { ["type"] = "test" }
            },
            new()
            {
                Id = "test2", 
                Text = "Another test document",
                Embedding = [0f, 1f, 0f],
                Metadata = new Dictionary<string, object> { ["type"] = "test" }
            }
        };

        await store.AddAsync(vectors);
        var allVectors = store.GetAll().ToList();
        
        if (allVectors.Count != 2)
            throw new Exception($"Expected 2 vectors, got {allVectors.Count}");
        
        Console.WriteLine("✓ AddAsync test passed");
    }

    private static async Task TestGetSimilarDocumentsAsync()
    {
        Console.WriteLine("Testing GetSimilarDocumentsAsync...");
        
        var store = new TrackedVectorStore();
        var vectors = new List<Vector>
        {
            new()
            {
                Id = "doc1",
                Text = "Machine learning is fascinating",
                Embedding = [1f, 0.5f, 0f],
                Metadata = new Dictionary<string, object> { ["category"] = "ai" }
            },
            new()
            {
                Id = "doc2",
                Text = "Cooking recipes are useful",
                Embedding = [0f, 0f, 1f],
                Metadata = new Dictionary<string, object> { ["category"] = "cooking" }
            }
        };

        await store.AddAsync(vectors);
        
        // Test similarity search with embedding close to first document
        var queryEmbedding = new[] { 0.9f, 0.4f, 0.1f };
        var results = await store.GetSimilarDocumentsAsync(queryEmbedding, amount: 1);
        
        if (results.Count != 1)
            throw new Exception($"Expected 1 result, got {results.Count}");
        
        var firstResult = results.First();
        if (!firstResult.PageContent.Contains("Machine learning"))
            throw new Exception("Expected most similar result to be about machine learning");
        
        Console.WriteLine("✓ GetSimilarDocumentsAsync test passed");
    }

    private static async Task TestClearAsync()
    {
        Console.WriteLine("Testing ClearAsync...");
        
        var store = new TrackedVectorStore();
        var vector = new Vector
        {
            Id = "clear-test",
            Text = "Document to be cleared",
            Embedding = [1f, 1f, 1f]
        };

        await store.AddAsync(new[] { vector });
        
        // Verify it was added
        var beforeClear = store.GetAll().ToList();
        if (beforeClear.Count != 1)
            throw new Exception($"Expected 1 vector before clear, got {beforeClear.Count}");
        
        // Clear the store
        await store.ClearAsync();
        
        // Verify it was cleared
        var afterClear = store.GetAll().ToList();
        if (afterClear.Count != 0)
            throw new Exception($"Expected 0 vectors after clear, got {afterClear.Count}");
        
        Console.WriteLine("✓ ClearAsync test passed");
    }

    private static void TestGetAll()
    {
        Console.WriteLine("Testing GetAll consistency...");
        
        var store = new TrackedVectorStore();
        
        // Initially should be empty
        var initialVectors = store.GetAll().ToList();
        if (initialVectors.Count != 0)
            throw new Exception($"Expected 0 vectors initially, got {initialVectors.Count}");
        
        Console.WriteLine("✓ GetAll test passed");
    }
}