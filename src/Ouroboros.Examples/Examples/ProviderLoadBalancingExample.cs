// <copyright file="ProviderLoadBalancingExample.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Providers;
using Ouroboros.Providers.LoadBalancing;

namespace Ouroboros.Examples;

/// <summary>
/// Demonstrates Provider Load Balancing for handling rate limiting and improving reliability.
/// Shows how to configure multiple providers with different strategies to prevent 429 errors.
/// </summary>
public static class ProviderLoadBalancingExample
{
    /// <summary>
    /// Demonstrates basic load balancing with multiple providers using Round Robin strategy.
    /// </summary>
    public static async Task BasicRoundRobinExample()
    {
        Console.WriteLine("=== Basic Round Robin Load Balancing ===\n");

        // Create load-balanced model with Round Robin strategy
        var loadBalancedModel = new LoadBalancedChatModel(ProviderSelectionStrategies.RoundRobin);

        // Register multiple OpenAI-compatible providers
        var provider1 = new HttpOpenAiCompatibleChatModel(
            "https://api.provider1.com",
            "api-key-1",
            "gpt-4");
        
        var provider2 = new HttpOpenAiCompatibleChatModel(
            "https://api.provider2.com",
            "api-key-2",
            "gpt-4");
        
        var provider3 = new HttpOpenAiCompatibleChatModel(
            "https://api.provider3.com",
            "api-key-3",
            "gpt-4");

        loadBalancedModel.RegisterProvider("provider-1", provider1);
        loadBalancedModel.RegisterProvider("provider-2", provider2);
        loadBalancedModel.RegisterProvider("provider-3", provider3);

        Console.WriteLine($"Registered {loadBalancedModel.ProviderCount} providers");
        Console.WriteLine($"Strategy: {loadBalancedModel.Strategy}\n");

        // Make several requests - they will be distributed evenly
        for (int i = 1; i <= 5; i++)
        {
            Console.WriteLine($"Request {i}:");
            string result = await loadBalancedModel.GenerateTextAsync($"What is the capital of France? (Request {i})");
            Console.WriteLine($"Response: {result.Substring(0, Math.Min(100, result.Length))}...\n");
        }

        // Display health status
        DisplayHealthStatus(loadBalancedModel);
    }

    /// <summary>
    /// Demonstrates adaptive health-based load balancing that automatically routes around
    /// rate-limited or failing providers.
    /// </summary>
    public static async Task AdaptiveHealthExample()
    {
        Console.WriteLine("\n=== Adaptive Health Load Balancing ===\n");

        // Create load-balanced model with Adaptive Health strategy
        var loadBalancedModel = new LoadBalancedChatModel(ProviderSelectionStrategies.AdaptiveHealth);

        // Register multiple providers with different characteristics
        var fastProvider = new OllamaCloudChatModel(
            "https://api.ollama.cloud",
            "fast-api-key",
            "llama3.1:8b"); // Smaller, faster model

        var accurateProvider = new OllamaCloudChatModel(
            "https://api.ollama.cloud",
            "accurate-api-key",
            "llama3.1:70b"); // Larger, more accurate model

        var fallbackProvider = new OllamaChatAdapter(
            new LangChain.Providers.Ollama.OllamaChatModel(
                new LangChain.Providers.Ollama.OllamaProvider(), 
                "llama3.1")); // Local fallback

        loadBalancedModel.RegisterProvider("fast", fastProvider);
        loadBalancedModel.RegisterProvider("accurate", accurateProvider);
        loadBalancedModel.RegisterProvider("fallback", fallbackProvider);

        Console.WriteLine("Adaptive routing will select the healthiest provider based on:");
        Console.WriteLine("- Success rate (70% weight)");
        Console.WriteLine("- Average latency (30% weight)");
        Console.WriteLine("- Availability (not in cooldown)\n");

        // Make requests - adaptive routing will prefer healthier providers
        for (int i = 1; i <= 10; i++)
        {
            string prompt = $"Explain quantum computing in simple terms. (Request {i})";
            string result = await loadBalancedModel.GenerateTextAsync(prompt);
            Console.WriteLine($"Request {i} completed: {result.Length} characters");
        }

        Console.WriteLine();
        DisplayHealthStatus(loadBalancedModel);
    }

    /// <summary>
    /// Demonstrates handling rate limiting with automatic failover and cooldown.
    /// </summary>
    public static async Task RateLimitHandlingExample()
    {
        Console.WriteLine("\n=== Rate Limit Handling Example ===\n");

        var loadBalancedModel = new LoadBalancedChatModel(ProviderSelectionStrategies.AdaptiveHealth);

        // Primary provider (might get rate limited)
        var primaryProvider = new LiteLLMChatModel(
            "https://litellm.example.com",
            "primary-key",
            "gpt-4");

        // Backup providers
        var backup1 = new LiteLLMChatModel(
            "https://litellm.example.com",
            "backup-key-1",
            "gpt-4");

        var backup2 = new LiteLLMChatModel(
            "https://litellm.example.com",
            "backup-key-2",
            "gpt-3.5-turbo");

        loadBalancedModel.RegisterProvider("primary", primaryProvider);
        loadBalancedModel.RegisterProvider("backup-1", backup1);
        loadBalancedModel.RegisterProvider("backup-2", backup2);

        Console.WriteLine("Scenario: High request volume that might trigger rate limits\n");

        // Simulate high request volume
        var tasks = new List<Task<string>>();
        for (int i = 1; i <= 20; i++)
        {
            string prompt = $"Generate a creative story about AI. (Request {i})";
            tasks.Add(loadBalancedModel.GenerateTextAsync(prompt));
        }

        // Wait for all requests to complete
        string[] results = await Task.WhenAll(tasks);

        Console.WriteLine($"Completed {results.Length} requests");
        Console.WriteLine($"Healthy providers: {loadBalancedModel.HealthyProviderCount}/{loadBalancedModel.ProviderCount}\n");

        // Show which providers were rate limited
        var healthStatus = loadBalancedModel.GetHealthStatus();
        foreach (var (providerId, health) in healthStatus)
        {
            if (health.IsInCooldown)
            {
                Console.WriteLine($"⚠️  Provider '{providerId}' is in cooldown until {health.CooldownUntil:HH:mm:ss}");
                Console.WriteLine($"   Reason: Rate limited (429)");
            }
        }

        Console.WriteLine("\n✅ Load balancer automatically failed over to healthy providers!");
        DisplayHealthStatus(loadBalancedModel);
    }

    /// <summary>
    /// Demonstrates least latency strategy for performance-critical applications.
    /// </summary>
    public static async Task LeastLatencyExample()
    {
        Console.WriteLine("\n=== Least Latency Strategy Example ===\n");

        var loadBalancedModel = new LoadBalancedChatModel(ProviderSelectionStrategies.LeastLatency);

        // Providers at different geographical locations (simulated)
        var usEastProvider = new HttpOpenAiCompatibleChatModel(
            "https://us-east.api.com",
            "api-key",
            "gpt-4");

        var usWestProvider = new HttpOpenAiCompatibleChatModel(
            "https://us-west.api.com",
            "api-key",
            "gpt-4");

        var europeProvider = new HttpOpenAiCompatibleChatModel(
            "https://eu.api.com",
            "api-key",
            "gpt-4");

        loadBalancedModel.RegisterProvider("us-east", usEastProvider);
        loadBalancedModel.RegisterProvider("us-west", usWestProvider);
        loadBalancedModel.RegisterProvider("europe", europeProvider);

        Console.WriteLine("Least Latency strategy always routes to the fastest provider");
        Console.WriteLine("Useful for real-time applications and user-facing features\n");

        // Make requests - load balancer learns latencies and adapts
        for (int i = 1; i <= 5; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string result = await loadBalancedModel.GenerateTextAsync("Quick response needed");
            sw.Stop();
            
            Console.WriteLine($"Request {i} completed in {sw.ElapsedMilliseconds}ms");
        }

        Console.WriteLine();
        DisplayHealthStatus(loadBalancedModel);
    }

    /// <summary>
    /// Demonstrates custom load balancer configuration with manual health management.
    /// </summary>
    public static async Task CustomConfigurationExample()
    {
        Console.WriteLine("\n=== Custom Configuration Example ===\n");

        // Create custom load balancer with direct access
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>(
            ProviderSelectionStrategies.WeightedRandom);

        // Register providers
        var provider1 = new MockProvider("provider-1");
        var provider2 = new MockProvider("provider-2");

        loadBalancer.RegisterProvider("provider-1", provider1);
        loadBalancer.RegisterProvider("provider-2", provider2);

        // Create chat model with custom balancer
        var chatModel = new LoadBalancedChatModel(loadBalancer);

        Console.WriteLine("Weighted Random strategy uses health scores to probabilistically select providers");
        Console.WriteLine("Better performing providers are selected more frequently\n");

        // Simulate some failures on provider-1
        Console.WriteLine("Simulating failures on provider-1...");
        loadBalancer.RecordExecution("provider-1", 1000, false);
        loadBalancer.RecordExecution("provider-1", 1100, false);

        // Record successes on provider-2
        loadBalancer.RecordExecution("provider-2", 200, true);
        loadBalancer.RecordExecution("provider-2", 180, true);
        loadBalancer.RecordExecution("provider-2", 220, true);

        Console.WriteLine("\nMaking 10 requests with weighted selection...");
        var selectedProviders = new Dictionary<string, int>();

        for (int i = 0; i < 10; i++)
        {
            var selection = await loadBalancer.SelectProviderAsync();
            selection.Match(
                result =>
                {
                    string providerId = result.ProviderId;
                    selectedProviders[providerId] = selectedProviders.GetValueOrDefault(providerId) + 1;
                    return result;
                },
                _ => default!);
        }

        Console.WriteLine("\nSelection distribution:");
        foreach (var (providerId, count) in selectedProviders)
        {
            Console.WriteLine($"  {providerId}: {count} selections");
        }

        Console.WriteLine("\n✅ Provider-2 was selected more often due to better health score");
        DisplayHealthStatus(loadBalancer.GetHealthStatus());
    }

    /// <summary>
    /// Helper to display health status of all providers.
    /// </summary>
    private static void DisplayHealthStatus(LoadBalancedChatModel model)
    {
        DisplayHealthStatus(model.GetHealthStatus());
    }

    /// <summary>
    /// Helper to display health status dictionary.
    /// </summary>
    private static void DisplayHealthStatus(IReadOnlyDictionary<string, ProviderHealthStatus> healthStatus)
    {
        Console.WriteLine("=== Provider Health Status ===");
        foreach (var (providerId, health) in healthStatus)
        {
            string status = health.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
            string cooldown = health.IsInCooldown ? $" (Cooldown until {health.CooldownUntil:HH:mm:ss})" : "";
            
            Console.WriteLine($"\n{providerId}: {status}{cooldown}");
            Console.WriteLine($"  Health Score: {health.HealthScore:F2}");
            Console.WriteLine($"  Success Rate: {health.SuccessRate:P0} ({health.SuccessfulRequests}/{health.TotalRequests})");
            Console.WriteLine($"  Avg Latency: {health.AverageLatencyMs:F0}ms");
            Console.WriteLine($"  Consecutive Failures: {health.ConsecutiveFailures}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Mock provider for demonstration purposes.
    /// </summary>
    private class MockProvider : IChatCompletionModel
    {
        private readonly string _name;

        public MockProvider(string name)
        {
            _name = name;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            return Task.FromResult($"[Response from {_name}] {prompt}");
        }
    }

    /// <summary>
    /// Main entry point for running all examples.
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Provider Load Balancing Examples                    ║");
        Console.WriteLine("║   Demonstrating rate limit handling and failover      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

        try
        {
            // Note: These examples use mock/offline providers for demonstration
            // In production, replace with actual provider configurations

            await BasicRoundRobinExample();
            await Task.Delay(1000);

            await AdaptiveHealthExample();
            await Task.Delay(1000);

            await RateLimitHandlingExample();
            await Task.Delay(1000);

            await LeastLatencyExample();
            await Task.Delay(1000);

            await CustomConfigurationExample();

            Console.WriteLine("\n✅ All examples completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error running examples: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
