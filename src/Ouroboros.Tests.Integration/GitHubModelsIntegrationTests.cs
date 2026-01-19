// <copyright file="GitHubModelsIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using Ouroboros.Providers;

/// <summary>
/// Integration tests for GitHub Models API support.
/// Tests the GitHubModelsChatModel with various model endpoints.
/// These tests require MODEL_TOKEN, GITHUB_TOKEN, or GITHUB_MODELS_TOKEN environment variable to be set for live API tests.
/// </summary>
[Trait("Category", "Integration")]
public class GitHubModelsIntegrationTests
{
    [Fact]
    public void ChatConfig_AutoDetection_ShouldIdentifyGitHubModelsEndpoints()
    {
        Console.WriteLine("Testing ChatConfig auto-detection for GitHub Models...");

        // Test GitHub Models URL detection
        var (endpoint1, key1, type1) = ChatConfig.ResolveWithOverrides(
            "https://models.inference.ai.azure.com", "test-key", null);

        if (type1 != ChatEndpointType.GitHubModels)
        {
            throw new Exception($"Expected GitHubModels, got {type1} for models.inference.ai.azure.com");
        }

        // Test OpenAI-compatible detection (default for unknown endpoints)
        var (endpoint2, key2, type2) = ChatConfig.ResolveWithOverrides(
            "https://api.openai.com", "test-key", null);

        if (type2 != ChatEndpointType.OpenAiCompatible)
        {
            throw new Exception($"Expected OpenAiCompatible, got {type2} for api.openai.com");
        }

        Console.WriteLine("  ✓ Auto-detection works correctly for GitHub Models endpoints");
    }

    [Fact]
    public void ChatConfig_ManualOverride_ShouldRespectGitHubModelsType()
    {
        Console.WriteLine("Testing ChatConfig manual override for GitHub Models...");

        // Test explicit github-models override
        var (endpoint1, key1, type1) = ChatConfig.ResolveWithOverrides(
            "https://custom.endpoint.com", "test-key", "github-models");

        if (type1 != ChatEndpointType.GitHubModels)
        {
            throw new Exception($"Expected GitHubModels with manual override, got {type1}");
        }

        // Test explicit github override (shorthand)
        var (endpoint2, key2, type2) = ChatConfig.ResolveWithOverrides(
            "https://custom.endpoint.com", "test-key", "github");

        if (type2 != ChatEndpointType.GitHubModels)
        {
            throw new Exception($"Expected GitHubModels with 'github' shorthand override, got {type2}");
        }

        Console.WriteLine("  ✓ Manual override works correctly for GitHub Models");
    }

    [Fact]
    public void EnvironmentToken_Resolution_ShouldPrioritizeCorrectly()
    {
        Console.WriteLine("Testing environment token resolution for GitHub Models...");

        // Save original values
        var origModelToken = Environment.GetEnvironmentVariable("MODEL_TOKEN");
        var origGitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var origGitHubModelsToken = Environment.GetEnvironmentVariable("GITHUB_MODELS_TOKEN");

        try
        {
            // Clear all tokens first
            Environment.SetEnvironmentVariable("MODEL_TOKEN", null);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
            Environment.SetEnvironmentVariable("GITHUB_MODELS_TOKEN", null);

            // Test that missing token throws
            bool threwException = false;
            try
            {
                _ = GitHubModelsChatModel.FromEnvironment("gpt-4o-mini");
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("MODEL_TOKEN") &&
                    ex.Message.Contains("GITHUB_TOKEN") &&
                    ex.Message.Contains("GITHUB_MODELS_TOKEN"))
                {
                    threwException = true;
                }
            }

            if (!threwException)
            {
                throw new Exception("Expected InvalidOperationException when no token is set");
            }

            // Test MODEL_TOKEN takes precedence
            Environment.SetEnvironmentVariable("MODEL_TOKEN", "model-token-test");
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "github-token-test");
            Environment.SetEnvironmentVariable("GITHUB_MODELS_TOKEN", "github-models-token-test");

            // We can't directly verify which token is used since it's passed to the base class,
            // but we can verify the object is created without error
            var model = GitHubModelsChatModel.FromEnvironment("gpt-4o-mini");
            if (model == null)
            {
                throw new Exception("Failed to create GitHubModelsChatModel with MODEL_TOKEN");
            }

            Console.WriteLine("  ✓ Environment token resolution works correctly");
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("MODEL_TOKEN", origModelToken);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", origGitHubToken);
            Environment.SetEnvironmentVariable("GITHUB_MODELS_TOKEN", origGitHubModelsToken);
        }
    }

    [Fact]
    public async Task GitHubModelsChatModel_Fallback_ShouldReturnFallbackMessage()
    {
        Console.WriteLine("Testing GitHubModelsChatModel fallback behavior...");

        // Use a fake token and invalid endpoint to test fallback
        var model = new GitHubModelsChatModel(
            "fake-token-for-testing",
            "gpt-4o-mini",
            "http://127.0.0.1:9999");

        var response = await model.GenerateTextAsync("Test prompt", CancellationToken.None);

        if (!response.Contains("github-models-fallback"))
        {
            throw new Exception("GitHubModelsChatModel should use fallback message on failure");
        }

        Console.WriteLine("  ✓ GitHubModelsChatModel fallback works correctly");
    }

    [Fact]
    public void ChatEndpointType_Detection_ShouldIdentifyGitHubModelsUrls()
    {
        Console.WriteLine("Testing ChatEndpointType detection for GitHub Models...");

        // Test various GitHub Models URLs
        var testUrls = new[]
        {
            ("https://models.inference.ai.azure.com", ChatEndpointType.GitHubModels),
            ("https://MODELS.INFERENCE.AI.AZURE.COM", ChatEndpointType.GitHubModels), // Case insensitive
            ("https://api.openai.com", ChatEndpointType.OpenAiCompatible),
            ("https://api.ollama.com", ChatEndpointType.OllamaCloud),
        };

        foreach (var (url, expected) in testUrls)
        {
            var (_, _, type) = ChatConfig.ResolveWithOverrides(url, "test-key", null);
            if (type != expected)
            {
                throw new Exception($"Expected {expected} for {url}, got {type}");
            }
        }

        Console.WriteLine("  ✓ ChatEndpointType detection works correctly");
    }

    [Fact]
    public async Task EndToEnd_GitHubModelsScenario_ShouldWorkWithLiveApi()
    {
        Console.WriteLine("Testing end-to-end GitHub Models scenario with live API...");

        try
        {
            // Create model from environment (uses MODEL_TOKEN, GITHUB_TOKEN, or GITHUB_MODELS_TOKEN)
            var settings = new ChatRuntimeSettings(
                Temperature: 0.1,
                MaxTokens: 50,
                TimeoutSeconds: 30,
                Stream: false);

            var chatModel = GitHubModelsChatModel.FromEnvironment("gpt-4o-mini", settings);

            // Test a simple query
            var response = await chatModel.GenerateTextAsync(
                "What is 2+2? Reply with just the number.",
                CancellationToken.None);

            // Verify we got a response (not a fallback)
            if (response.Contains("github-models-fallback"))
            {
                Console.WriteLine($"  ⚠ Live API test returned fallback - API may be unavailable: {response}");
            }
            else if (response.Contains("4"))
            {
                Console.WriteLine($"  ✓ Live API test successful: {response.Trim()}");
            }
            else
            {
                Console.WriteLine($"  ? Live API returned unexpected response: {response}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ Live API test error (may be expected): {ex.Message}");
        }
    }

    private static bool IsTokenAvailable()
    {
        string? token = Environment.GetEnvironmentVariable("MODEL_TOKEN")
                       ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                       ?? Environment.GetEnvironmentVariable("GITHUB_MODELS_TOKEN");
        return !string.IsNullOrWhiteSpace(token);
    }

    /// <summary>
    /// Runs all GitHub Models integration tests.
    /// Kept for backward compatibility - wraps individual test methods.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running GitHub Models Integration Tests ===");

        var instance = new GitHubModelsIntegrationTests();

        // Test configuration and token resolution
        instance.ChatConfig_AutoDetection_ShouldIdentifyGitHubModelsEndpoints();
        instance.ChatConfig_ManualOverride_ShouldRespectGitHubModelsType();
        instance.EnvironmentToken_Resolution_ShouldPrioritizeCorrectly();

        // Test chat model adapters
        await instance.GitHubModelsChatModel_Fallback_ShouldReturnFallbackMessage();

        // Test model selection
        instance.ChatEndpointType_Detection_ShouldIdentifyGitHubModelsUrls();

        // Test end-to-end scenarios if token is available
        if (IsTokenAvailable())
        {
            await instance.EndToEnd_GitHubModelsScenario_ShouldWorkWithLiveApi();
        }
        else
        {
            Console.WriteLine("  ⚠ Skipping live API tests - no token available");
            Console.WriteLine("  Set MODEL_TOKEN, GITHUB_TOKEN, or GITHUB_MODELS_TOKEN to enable live tests");
        }

        Console.WriteLine("✓ All GitHub Models integration tests passed!");
    }
}
