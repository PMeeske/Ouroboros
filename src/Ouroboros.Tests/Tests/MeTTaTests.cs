// <copyright file="MeTTaTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using Ouroboros.Tools.MeTTa;

/// <summary>
/// Tests for MeTTa symbolic reasoning integration.
/// </summary>
public static class MeTTaTests
{
    /// <summary>
    /// Tests basic MeTTa query tool functionality.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestMeTTaQueryTool()
    {
        Console.WriteLine("=== Test: MeTTa Query Tool ===");

        // Create a mock engine for testing (since we may not have metta installed)
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaQueryTool(engine);

        Console.WriteLine($"Tool Name: {tool.Name}");
        Console.WriteLine($"Tool Description: {tool.Description}");

        // Test query
        var result = await tool.InvokeAsync("(+ 1 2)", CancellationToken.None);

        result.Match(
            success => Console.WriteLine($"✓ Query result: {success}"),
            error => Console.WriteLine($"✗ Query failed: {error}"));

        Console.WriteLine("✓ MeTTa query tool test completed\n");
    }

    /// <summary>
    /// Tests MeTTa rule application.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestMeTTaRuleTool()
    {
        Console.WriteLine("=== Test: MeTTa Rule Tool ===");

        var engine = new MockMeTTaEngine();
        var tool = new MeTTaRuleTool(engine);

        Console.WriteLine($"Tool Name: {tool.Name}");

        var result = await tool.InvokeAsync("(= (human $x) (mortal $x))", CancellationToken.None);

        result.Match(
            success => Console.WriteLine($"✓ Rule applied: {success}"),
            error => Console.WriteLine($"✗ Rule failed: {error}"));

        Console.WriteLine("✓ MeTTa rule tool test completed\n");
    }

    /// <summary>
    /// Tests MeTTa plan verification.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestMeTTaPlanVerifier()
    {
        Console.WriteLine("=== Test: MeTTa Plan Verifier ===");

        var engine = new MockMeTTaEngine();
        var tool = new MeTTaPlanVerifierTool(engine);

        Console.WriteLine($"Tool Name: {tool.Name}");

        var result = await tool.InvokeAsync("(plan (step1) (step2))", CancellationToken.None);

        result.Match(
            success => Console.WriteLine($"✓ Verification result: {success}"),
            error => Console.WriteLine($"✗ Verification failed: {error}"));

        Console.WriteLine("✓ MeTTa plan verifier test completed\n");
    }

    /// <summary>
    /// Tests adding facts to MeTTa.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestMeTTaFactTool()
    {
        Console.WriteLine("=== Test: MeTTa Fact Tool ===");

        var engine = new MockMeTTaEngine();
        var tool = new MeTTaFactTool(engine);

        var result = await tool.InvokeAsync("(human Socrates)", CancellationToken.None);

        result.Match(
            success => Console.WriteLine($"✓ {success}"),
            error => Console.WriteLine($"✗ Failed: {error}"));

        Console.WriteLine("✓ MeTTa fact tool test completed\n");
    }

    /// <summary>
    /// Tests ToolRegistry integration with MeTTa.
    /// </summary>
    public static void TestMeTTaToolRegistryIntegration()
    {
        Console.WriteLine("=== Test: MeTTa ToolRegistry Integration ===");

        var registry = ToolRegistry.CreateDefault();
        Console.WriteLine($"Initial tool count: {registry.Count}");

        var engine = new MockMeTTaEngine();
        var mettaRegistry = registry.WithMeTTaTools(engine);

        Console.WriteLine($"After MeTTa tools: {mettaRegistry.Count}");

        var tools = mettaRegistry.All.ToList();
        var mettaTools = tools.Where(t => t.Name.StartsWith("metta_")).ToList();

        Console.WriteLine($"✓ MeTTa tools registered: {mettaTools.Count}");
        foreach (var tool in mettaTools)
        {
            Console.WriteLine($"  - {tool.Name}: {tool.Description}");
        }

        Console.WriteLine("✓ ToolRegistry integration test completed\n");
    }

    /// <summary>
    /// Tests HTTP MeTTa engine (without actual HTTP server).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestHttpMeTTaEngine()
    {
        Console.WriteLine("=== Test: HTTP MeTTa Engine ===");

        // This will fail to connect, but we can test the API structure
        var engine = new HttpMeTTaEngine("http://localhost:8000");

        var result = await engine.ExecuteQueryAsync("(test)", CancellationToken.None);

        result.Match(
            success => Console.WriteLine($"Unexpected success: {success}"),
            error => Console.WriteLine($"✓ Expected connection error: {error.Substring(0, Math.Min(50, error.Length))}..."));

        engine.Dispose();
        Console.WriteLine("✓ HTTP MeTTa engine test completed\n");
    }

    /// <summary>
    /// Tests subprocess MeTTa engine (will warn if metta not installed).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task TestSubprocessMeTTaEngine()
    {
        Console.WriteLine("=== Test: Subprocess MeTTa Engine ===");

        var engine = new SubprocessMeTTaEngine();

        var result = await engine.ExecuteQueryAsync("(+ 1 2)", CancellationToken.None);

        result.Match(
            success => Console.WriteLine($"✓ Query succeeded: {success}"),
            error => Console.WriteLine($"✓ Expected if metta not installed: {error}"));

        engine.Dispose();
        Console.WriteLine("✓ Subprocess MeTTa engine test completed\n");
    }

    /// <summary>
    /// Runs all MeTTa tests.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║     MeTTa Integration Test Suite          ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        try
        {
            await TestMeTTaQueryTool();
            await TestMeTTaRuleTool();
            await TestMeTTaPlanVerifier();
            await TestMeTTaFactTool();
            TestMeTTaToolRegistryIntegration();
            await TestHttpMeTTaEngine();
            await TestSubprocessMeTTaEngine();

            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║   All MeTTa tests completed successfully   ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test suite failed: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Mock MeTTa engine for testing without requiring actual MeTTa installation.
/// </summary>
internal sealed class MockMeTTaEngine : IMeTTaEngine
{
    private readonly List<string> facts = new();

    public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        // Simulate simple query responses
        var result = query switch
        {
            "(+ 1 2)" => "3",
            _ => $"[Result of: {query}]",
        };

        return Task.FromResult(Result<string, string>.Success(result));
    }

    public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        this.facts.Add(fact);
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        return Task.FromResult(Result<string, string>.Success($"Rule applied: {rule}"));
    }

    public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        // Simple mock verification - always returns true
        return Task.FromResult(Result<bool, string>.Success(true));
    }

    public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        this.facts.Clear();
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    public void Dispose()
    {
        // Nothing to dispose in mock
    }
}
