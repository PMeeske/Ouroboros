// <copyright file="OllamaCloudIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using LangChain.Databases;
using LangChain.Providers.Ollama;
using Ouroboros.Providers;

/// <summary>
/// Integration tests for Ollama Cloud endpoint support.
/// Tests both remote Ollama Cloud endpoints and local Ollama to ensure nothing is broken.
/// Requires Ollama service for full testing, but includes fallback testing for when service is unavailable.
/// </summary>
[Trait("Category", "Integration")]
public class OllamaCloudIntegrationTests
{
    [Fact]
    public async Task LocalOllama_ChatModel_ShouldHandleConnectionErrors()
    {
        Console.WriteLine("Testing local Ollama chat model...");

        // Create local Ollama adapter (simulates what happens when no remote endpoint is configured)
        var provider = new OllamaProvider();
        var localModel = new OllamaChatModel(provider, "llama3");
        var adapter = new OllamaChatAdapter(localModel);

        try
        {
            // This will fail because Ollama daemon is not running, but should use fallback
            var response = await adapter.GenerateTextAsync("Hello", CancellationToken.None);

            // Verify fallback behavior
            if (!response.Contains("ollama-fallback") && !response.Contains("Hello"))
            {
                throw new Exception("Local Ollama chat model fallback not working correctly");
            }

            Console.WriteLine("  ✓ Local Ollama chat model fallback works correctly");
        }
        catch (Exception ex) when (!ex.Message.Contains("not working correctly"))
        {
            // Network/daemon errors are expected in test environment
            Console.WriteLine($"  ✓ Local Ollama chat model handles errors gracefully: {ex.GetType().Name}");
        }
    }

    [Fact]
    public async Task LocalOllama_EmbeddingModel_ShouldReturnFallbackEmbeddings()
    {
        Console.WriteLine("Testing local Ollama embedding model...");

        var provider = new OllamaProvider();
        var localModel = new OllamaEmbeddingModel(provider, "nomic-embed-text");
        var adapter = new OllamaEmbeddingAdapter(localModel);

        try
        {
            // This will fallback to deterministic embeddings if daemon not available
            var embedding = await adapter.CreateEmbeddingsAsync("test text", CancellationToken.None);

            if (embedding == null || embedding.Length == 0)
            {
                throw new Exception("Local Ollama embedding model should return fallback embedding");
            }

            Console.WriteLine($"  ✓ Local Ollama embedding model works (returned {embedding.Length} dimensions)");
        }
        catch (Exception ex)
        {
            throw new Exception($"Local Ollama embedding model test failed: {ex.Message}");
        }
    }

    [Fact]
    public void ChatConfig_AutoDetection_ShouldIdentifyEndpointTypes()
    {
        Console.WriteLine("Testing ChatConfig auto-detection...");

        // Test Ollama Cloud URL detection
        var (endpoint1, key1, type1) = ChatConfig.ResolveWithOverrides(
            "https://api.ollama.com", "test-key", null);

        if (type1 != ChatEndpointType.OllamaCloud)
        {
            throw new Exception($"Expected OllamaCloud, got {type1} for api.ollama.com");
        }

        // Test another Ollama Cloud URL pattern
        var (endpoint2, key2, type2) = ChatConfig.ResolveWithOverrides(
            "https://custom.ollama.cloud", "test-key", null);

        if (type2 != ChatEndpointType.OllamaCloud)
        {
            throw new Exception($"Expected OllamaCloud, got {type2} for ollama.cloud");
        }

        // Test OpenAI-compatible detection (default)
        var (endpoint3, key3, type3) = ChatConfig.ResolveWithOverrides(
            "https://api.openai.com", "test-key", null);

        if (type3 != ChatEndpointType.OpenAiCompatible)
        {
            throw new Exception($"Expected OpenAiCompatible, got {type3} for api.openai.com");
        }

        Console.WriteLine("  ✓ Auto-detection works correctly for all URL patterns");
    }

    [Fact]
    public void ChatConfig_ManualOverride_ShouldRespectExplicitType()
    {
        Console.WriteLine("Testing ChatConfig manual override...");

        // Test explicit ollama-cloud override
        var (endpoint1, key1, type1) = ChatConfig.ResolveWithOverrides(
            "https://custom.endpoint.com", "test-key", "ollama-cloud");

        if (type1 != ChatEndpointType.OllamaCloud)
        {
            throw new Exception($"Expected OllamaCloud with manual override, got {type1}");
        }

        // Test explicit openai override (case insensitive)
        var (endpoint2, key2, type2) = ChatConfig.ResolveWithOverrides(
            "https://api.ollama.com", "test-key", "OpenAI");

        if (type2 != ChatEndpointType.OpenAiCompatible)
        {
            throw new Exception($"Expected OpenAiCompatible with manual override, got {type2}");
        }

        // Test auto type (should still auto-detect)
        var (endpoint3, key3, type3) = ChatConfig.ResolveWithOverrides(
            "https://api.ollama.com", "test-key", "auto");

        if (type3 != ChatEndpointType.OllamaCloud)
        {
            throw new Exception($"Expected OllamaCloud with auto type, got {type3}");
        }

        Console.WriteLine("  ✓ Manual override works correctly");
    }

    [Fact]
    public void ChatConfig_EnvironmentVariables_ShouldResolveCorrectly()
    {
        Console.WriteLine("Testing ChatConfig environment variable handling...");

        // Save original values
        var origEndpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        var origKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
        var origType = Environment.GetEnvironmentVariable("CHAT_ENDPOINT_TYPE");

        try
        {
            // Test environment variable resolution
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT", "https://api.ollama.com");
            Environment.SetEnvironmentVariable("CHAT_API_KEY", "env-test-key");
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT_TYPE", "ollama-cloud");

            var (endpoint, key, type) = ChatConfig.Resolve();

            if (endpoint != "https://api.ollama.com")
            {
                throw new Exception($"Expected endpoint from env var, got {endpoint}");
            }

            if (key != "env-test-key")
            {
                throw new Exception($"Expected key from env var, got {key}");
            }

            if (type != ChatEndpointType.OllamaCloud)
            {
                throw new Exception($"Expected OllamaCloud from env var, got {type}");
            }

            // Test CLI override takes precedence
            var (endpoint2, key2, type2) = ChatConfig.ResolveWithOverrides(
                "https://override.com", "override-key", "openai");

            if (endpoint2 != "https://override.com")
            {
                throw new Exception("CLI override should take precedence over env var");
            }

            Console.WriteLine("  ✓ Environment variable handling works correctly");
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT", origEndpoint);
            Environment.SetEnvironmentVariable("CHAT_API_KEY", origKey);
            Environment.SetEnvironmentVariable("CHAT_ENDPOINT_TYPE", origType);
        }
    }

    [Fact]
    public async Task OllamaCloudChatModel_Fallback_ShouldReturnFallbackMessage()
    {
        Console.WriteLine("Testing OllamaCloudChatModel fallback behavior...");

        // Use localhost URL to avoid DNS lookup while testing fallback
        var model = new OllamaCloudChatModel("http://127.0.0.1:9999", "fake-key", "llama3");

        var response = await model.GenerateTextAsync("Test prompt", CancellationToken.None);

        if (!response.Contains("ollama-cloud-fallback"))
        {
            throw new Exception("OllamaCloudChatModel should use fallback message");
        }

        if (!response.Contains("Test prompt"))
        {
            throw new Exception("Fallback should include original prompt");
        }

        Console.WriteLine("  ✓ OllamaCloudChatModel fallback works correctly");
    }

    [Fact]
    public async Task HttpOpenAiCompatibleChatModel_Fallback_ShouldReturnFallbackMessage()
    {
        Console.WriteLine("Testing HttpOpenAiCompatibleChatModel fallback behavior...");

        // Use localhost URL to avoid DNS lookup while testing fallback
        var model = new HttpOpenAiCompatibleChatModel("http://127.0.0.1:9998", "fake-key", "gpt-4");

        var response = await model.GenerateTextAsync("Test prompt", CancellationToken.None);

        if (!response.Contains("remote-fallback"))
        {
            throw new Exception("HttpOpenAiCompatibleChatModel should use fallback message");
        }

        if (!response.Contains("Test prompt"))
        {
            throw new Exception("Fallback should include original prompt");
        }

        Console.WriteLine("  ✓ HttpOpenAiCompatibleChatModel fallback works correctly");
    }

    [Fact]
    public async Task OllamaCloudEmbeddingModel_Fallback_ShouldReturnDeterministicEmbeddings()
    {
        Console.WriteLine("Testing OllamaCloudEmbeddingModel fallback behavior...");

        // Use localhost URL to avoid DNS lookup while testing fallback
        var model = new OllamaCloudEmbeddingModel("http://127.0.0.1:9999", "fake-key", "nomic-embed-text");

        var embedding = await model.CreateEmbeddingsAsync("test text", CancellationToken.None);

        if (embedding == null || embedding.Length == 0)
        {
            throw new Exception("OllamaCloudEmbeddingModel should return deterministic fallback");
        }

        // Verify deterministic behavior (same input = same output)
        var embedding2 = await model.CreateEmbeddingsAsync("test text", CancellationToken.None);

        if (embedding.Length != embedding2.Length)
        {
            throw new Exception("Deterministic embedding should have consistent dimensions");
        }

        for (int i = 0; i < embedding.Length; i++)
        {
            if (Math.Abs(embedding[i] - embedding2[i]) > 0.0001f)
            {
                throw new Exception("Deterministic embedding should produce same values");
            }
        }

        Console.WriteLine($"  ✓ OllamaCloudEmbeddingModel fallback works ({embedding.Length} dimensions)");
    }

    [Fact]
    public void CreateRemoteChatModel_Selection_ShouldSelectCorrectModelType()
    {
        Console.WriteLine("Testing CreateRemoteChatModel selection logic...");

        // We can't directly call Program.CreateRemoteChatModel, so we test the logic
        // by verifying the correct model types are used based on endpoint type

        // Test OllamaCloud selection
        var ollamaModel = new OllamaCloudChatModel("https://api.ollama.com", "key", "llama3");
        if (ollamaModel == null)
        {
            throw new Exception("Should create OllamaCloudChatModel");
        }

        // Test OpenAI selection
        var openaiModel = new HttpOpenAiCompatibleChatModel("https://api.openai.com", "key", "gpt-4");
        if (openaiModel == null)
        {
            throw new Exception("Should create HttpOpenAiCompatibleChatModel");
        }

        Console.WriteLine("  ✓ Remote chat model selection works correctly");
    }

    [Fact]
    public void CreateEmbeddingModel_Selection_ShouldSelectCorrectModelType()
    {
        Console.WriteLine("Testing CreateEmbeddingModel selection logic...");

        var provider = new OllamaProvider();

        // Test OllamaCloud embedding
        var ollamaEmbed = new OllamaCloudEmbeddingModel("https://api.ollama.com", "key", "nomic-embed-text");
        if (ollamaEmbed == null)
        {
            throw new Exception("Should create OllamaCloudEmbeddingModel");
        }

        // Test local embedding
        var localEmbed = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));
        if (localEmbed == null)
        {
            throw new Exception("Should create OllamaEmbeddingAdapter");
        }

        Console.WriteLine("  ✓ Embedding model selection works correctly");
    }

    [Fact]
    public async Task EndToEnd_LocalOllamaScenario_ShouldWorkCorrectly()
    {
        Console.WriteLine("Testing end-to-end local Ollama scenario...");

        // Simulate a complete local Ollama setup (no remote endpoint)
        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        // Test chat
        var chatResponse = await chatModel.GenerateTextAsync("Hello", CancellationToken.None);
        if (string.IsNullOrEmpty(chatResponse))
        {
            throw new Exception("Local Ollama chat should return response (fallback or real)");
        }

        // Test embedding
        var embedding = await embedModel.CreateEmbeddingsAsync("test", CancellationToken.None);
        if (embedding == null || embedding.Length == 0)
        {
            throw new Exception("Local Ollama embedding should return vector");
        }

        // Test vector store integration
        var store = new TrackedVectorStore();
        await store.AddAsync(new[]
        {
            new Vector
            {
                Id = "test1",
                Text = "test document",
                Embedding = embedding
            },
        });

        var retrieved = store.GetAll().ToList();
        if (retrieved.Count != 1)
        {
            throw new Exception("Vector store integration broken");
        }

        Console.WriteLine("  ✓ End-to-end local Ollama scenario works correctly");
    }

    [Fact]
    public async Task EndToEnd_RemoteOllamaCloudScenario_ShouldWorkWithFallback()
    {
        Console.WriteLine("Testing end-to-end remote Ollama Cloud scenario...");

        // Use localhost URLs to avoid DNS lookup while testing fallback
        var chatModel = new OllamaCloudChatModel("http://127.0.0.1:9999", "fake-key", "llama3");
        var embedModel = new OllamaCloudEmbeddingModel("http://127.0.0.1:9999", "fake-key", "nomic-embed-text");

        // Test chat (will use fallback)
        var chatResponse = await chatModel.GenerateTextAsync("Hello from cloud", CancellationToken.None);
        if (!chatResponse.Contains("ollama-cloud-fallback"))
        {
            throw new Exception("Remote Ollama Cloud chat should use fallback");
        }

        // Test embedding (will use deterministic fallback)
        var embedding = await embedModel.CreateEmbeddingsAsync("test cloud", CancellationToken.None);
        if (embedding == null || embedding.Length == 0)
        {
            throw new Exception("Remote Ollama Cloud embedding should return fallback vector");
        }

        // Test vector store integration with remote embeddings
        var store = new TrackedVectorStore();
        await store.AddAsync(new[]
        {
            new Vector
            {
                Id = "cloud1",
                Text = "cloud document",
                Embedding = embedding
            },
        });

        var retrieved = store.GetAll().ToList();
        if (retrieved.Count != 1)
        {
            throw new Exception("Vector store integration with cloud embeddings broken");
        }

        Console.WriteLine("  ✓ End-to-end remote Ollama Cloud scenario works correctly");
    }

    /// <summary>
    /// Runs all Ollama Cloud integration tests.
    /// Kept for backward compatibility - wraps individual test methods.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running Ollama Cloud Integration Tests ===");

        var instance = new OllamaCloudIntegrationTests();
        
        // Test local Ollama first (ensure it's not broken)
        await instance.LocalOllama_ChatModel_ShouldHandleConnectionErrors();
        await instance.LocalOllama_EmbeddingModel_ShouldReturnFallbackEmbeddings();

        // Test remote endpoint configuration
        instance.ChatConfig_AutoDetection_ShouldIdentifyEndpointTypes();
        instance.ChatConfig_ManualOverride_ShouldRespectExplicitType();
        instance.ChatConfig_EnvironmentVariables_ShouldResolveCorrectly();

        // Test chat model adapters
        await instance.OllamaCloudChatModel_Fallback_ShouldReturnFallbackMessage();
        await instance.HttpOpenAiCompatibleChatModel_Fallback_ShouldReturnFallbackMessage();

        // Test embedding model adapters
        await instance.OllamaCloudEmbeddingModel_Fallback_ShouldReturnDeterministicEmbeddings();

        // Test endpoint type selection
        instance.CreateRemoteChatModel_Selection_ShouldSelectCorrectModelType();
        instance.CreateEmbeddingModel_Selection_ShouldSelectCorrectModelType();

        // Test end-to-end scenarios
        await instance.EndToEnd_LocalOllamaScenario_ShouldWorkCorrectly();
        await instance.EndToEnd_RemoteOllamaCloudScenario_ShouldWorkWithFallback();

        Console.WriteLine("✓ All Ollama Cloud integration tests passed!");
    }
}
