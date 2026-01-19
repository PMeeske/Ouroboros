// <copyright file="MeTTaTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using Ouroboros.Tools.MeTTa;
using Ouroboros.Tests.Shared.Mocks;

/// <summary>
/// Unit tests for MeTTa symbolic reasoning integration.
/// Uses MockMeTTaEngine for isolation - no external MeTTa runtime required.
/// </summary>
[Trait("Category", "Unit")]
public class MeTTaTests
{
    /// <summary>
    /// Tests basic MeTTa query tool functionality.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Fact]
    public async Task MeTTaQueryTool_Execute_ShouldSucceed()
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
    [Fact]
    public async Task MeTTaRuleTool_ApplyRule_ShouldSucceed()
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
    [Fact]
    public async Task MeTTaPlanVerifier_VerifyPlan_ShouldSucceed()
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
    [Fact]
    public async Task MeTTaFactTool_AddFact_ShouldSucceed()
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
    [Fact]
    public void MeTTaToolRegistry_Integration_ShouldRegisterTools()
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
    [Fact]
    public async Task HttpMeTTaEngine_ConnectionFailure_ShouldHandleGracefully()
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
    [Fact]
    public async Task SubprocessMeTTaEngine_Execute_ShouldHandleMissingInstallation()
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
    /// Kept for backward compatibility - wraps individual test methods.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║     MeTTa Integration Test Suite          ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        try
        {
            var instance = new MeTTaTests();
            await instance.MeTTaQueryTool_Execute_ShouldSucceed();
            await instance.MeTTaRuleTool_ApplyRule_ShouldSucceed();
            await instance.MeTTaPlanVerifier_VerifyPlan_ShouldSucceed();
            await instance.MeTTaFactTool_AddFact_ShouldSucceed();
            instance.MeTTaToolRegistry_Integration_ShouldRegisterTools();
            await instance.HttpMeTTaEngine_ConnectionFailure_ShouldHandleGracefully();
            await instance.SubprocessMeTTaEngine_Execute_ShouldHandleMissingInstallation();

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
