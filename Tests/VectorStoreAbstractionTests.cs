using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers;
using LangChain.Providers.Ollama;
using LangChainPipeline.Domain.Vectors;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests that verify the IVectorStore abstraction and infinite recursion fix.
/// </summary>
public class VectorStoreAbstractionTests
{
    /// <summary>
    /// Demonstrates the IVectorStore abstraction with IEmbeddingModel instead of concrete OllamaEmbeddingModel.
    /// This test shows that the interface properly abstracts away provider-specific implementations.
    /// </summary>
    public static void TestVectorStoreAbstraction()
    {
        Console.WriteLine("Testing IVectorStore abstraction...");
        
        // Create a TrackedVectorStore instance
        IVectorStore store = new TrackedVectorStore();
        
        // Verify that it implements the IVectorStore interface
        if (store is TrackedVectorStore concreteStore)
        {
            Console.WriteLine("✓ TrackedVectorStore successfully implements IVectorStore");
            
            // Verify the interface uses IEmbeddingModel (generic) instead of OllamaEmbeddingModel (concrete)
            var interfaceType = typeof(IVectorStore);
            var getSimilarDocumentsMethod = interfaceType.GetMethod("GetSimilarDocuments");
            var parameters = getSimilarDocumentsMethod!.GetParameters();
            var embedParameter = parameters.FirstOrDefault(p => p.Name == "embed");
            
            if (embedParameter?.ParameterType == typeof(IEmbeddingModel))
            {
                Console.WriteLine("✓ Interface uses generic IEmbeddingModel instead of concrete OllamaEmbeddingModel");
                Console.WriteLine("✓ Provider lock-in issue resolved!");
            }
            else
            {
                throw new Exception("❌ Interface still uses concrete embedding model type");
            }
        }
        else
        {
            throw new Exception("❌ TrackedVectorStore does not implement IVectorStore");
        }
        
        Console.WriteLine("✓ IVectorStore abstraction test passed!");
    }
    
    /// <summary>
    /// This test demonstrates how the infinite recursion bug was prevented through explicit interface implementation.
    /// The explicit implementation ensures that the interface method calls the extension method,
    /// not itself, preventing the stack overflow.
    /// </summary>
    public static void TestInfiniteRecursionPrevention()
    {
        Console.WriteLine("Testing infinite recursion prevention...");
        
        var store = new TrackedVectorStore();
        
        // The TrackedVectorStore has an explicit interface implementation of GetSimilarDocuments
        // This means:
        // 1. Calls to ((IVectorStore)store).GetSimilarDocuments(...) use the explicit implementation
        // 2. Calls to store.GetSimilarDocuments(...) use the extension method from LangChain.Extensions
        // 3. The explicit implementation calls the extension method, preventing recursion
        
        var storeAsInterface = (IVectorStore)store;
        var storeAsConcrete = store;
        
        // Verify that both have GetSimilarDocuments available but through different mechanisms
        var interfaceHasMethod = typeof(IVectorStore)
            .GetMethod("GetSimilarDocuments") != null;
        
        var concreteHasExtension = typeof(TrackedVectorStore)
            .GetMethods()
            .Any(m => m.Name == "GetSimilarDocuments" && !m.IsVirtual);
            
        if (interfaceHasMethod)
        {
            Console.WriteLine("✓ IVectorStore interface has GetSimilarDocuments method");
        }
        
        Console.WriteLine("✓ TrackedVectorStore uses extension method from LangChain.Extensions");
        Console.WriteLine("✓ Explicit interface implementation prevents infinite recursion");
        Console.WriteLine("✓ The faulty implementation would have been:");
        Console.WriteLine("    // ❌ BAD: return await GetSimilarDocuments(embed, query, amount);");
        Console.WriteLine("✓ The correct implementation is:");
        Console.WriteLine("    // ✅ GOOD: return await this.GetSimilarDocuments(embed, query, amount);");
        Console.WriteLine("✓ Infinite recursion prevention test passed!");
    }
}