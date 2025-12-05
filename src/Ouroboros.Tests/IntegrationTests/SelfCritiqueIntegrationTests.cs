// <copyright file="SelfCritiqueIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests.IntegrationTests;

using LangChain.DocumentLoaders;
using LangChainPipeline.Agent;
using LangChainPipeline.Domain.Vectors;
using LangChainPipeline.Pipeline.Branches;
using LangChainPipeline.Providers;
using LangChainPipeline.Tools;
using Xunit;

/// <summary>
/// Integration tests demonstrating self-critique improvement over baseline drafts.
/// Tests the complete DSL pipeline integration with self-critique.
/// </summary>
public class SelfCritiqueIntegrationTests
{
    /// <summary>
    /// Integration test showing that self-critique produces measurable improvement.
    /// Verifies that the improved response is substantively different from the draft.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SelfCritique_ShouldProduceImprovement_OverBaseline()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("integration-test", store, DataSource.FromPath("."));
        var llm = CreateProgressiveMockLLM();
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(
            branch,
            "Write a clear explanation of monads in functional programming",
            "monads functional programming",
            iterations: 2);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify basic structure
        Assert.NotEmpty(result.Value.Draft);
        Assert.NotEmpty(result.Value.Critique);
        Assert.NotEmpty(result.Value.ImprovedResponse);
        
        // Verify improvement: improved response should be longer and more detailed
        Assert.True(
            result.Value.ImprovedResponse.Length >= result.Value.Draft.Length,
            "Improved response should be at least as detailed as draft");
        
        // Verify that improvement differs from draft
        Assert.NotEqual(result.Value.Draft, result.Value.ImprovedResponse);
        
        // Verify event chain is complete
        var events = result.Value.Branch.Events.OfType<LangChainPipeline.Domain.Events.ReasoningStep>().ToList();
        Assert.True(events.Count >= 5, $"Expected at least 5 events (Draft + 2*(Critique + Improve)), got {events.Count}");
    }

    /// <summary>
    /// Tests DSL composition: ensures self-critique generates proper draft-critique-improve cycle.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SelfCritique_ShouldGenerateCompleteCycle_InPipeline()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("dsl-test", store, DataSource.FromPath("."));
        
        var llm = CreateProgressiveMockLLM();
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act - Self-critique should generate its own draft and improve it
        var result = await agent.GenerateWithCritiqueAsync(
            branch,
            "test topic",
            "test query",
            iterations: 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Draft);
        Assert.NotEmpty(result.Value.Critique);
        Assert.NotEmpty(result.Value.ImprovedResponse);
    }

    /// <summary>
    /// Tests that multiple iterations show progressive improvement.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SelfCritique_ShouldShowProgressiveImprovement_AcrossIterations()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("progressive-test", store, DataSource.FromPath("."));
        var llm = CreateProgressiveMockLLM();
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(
            branch,
            "test topic",
            "test query",
            iterations: 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.IterationsPerformed);
        
        // Get all FinalSpec states to check progression
        var improvements = result.Value.Branch.Events
            .OfType<LangChainPipeline.Domain.Events.ReasoningStep>()
            .Select(e => e.State)
            .OfType<LangChainPipeline.Domain.States.FinalSpec>()
            .ToList();
        
        // Should have 3 improvements (one per iteration)
        Assert.Equal(3, improvements.Count);
        
        // Each improvement should be different
        Assert.NotEqual(improvements[0].Text, improvements[1].Text);
        Assert.NotEqual(improvements[1].Text, improvements[2].Text);
    }

    /// <summary>
    /// Tests confidence progression with multiple iterations.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task SelfCritique_ShouldIncreaseConfidence_WithMoreIterations()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("confidence-test", store, DataSource.FromPath("."));
        var llm = CreateHighQualityMockLLM();
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act - More iterations with positive feedback should yield higher confidence
        var result = await agent.GenerateWithCritiqueAsync(
            branch,
            "test topic",
            "test query",
            iterations: 3);

        // Assert
        Assert.True(result.IsSuccess);
        
        // With quality-indicating critique and multiple iterations, should have at least Medium confidence
        Assert.True(
            result.Value.Confidence >= ConfidenceRating.Medium,
            $"Expected at least Medium confidence, got {result.Value.Confidence}");
    }

    // Helper methods

    private static ToolAwareChatModel CreateProgressiveMockLLM()
    {
        var mockModel = new ProgressiveMockChatModel();
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static ToolAwareChatModel CreateHighQualityMockLLM()
    {
        var mockModel = new HighQualityMockChatModel();
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static LangChainPipeline.Domain.IEmbeddingModel CreateMockEmbedding()
    {
        return new MockEmbeddingModel();
    }

    // Mock implementations

    private class ProgressiveMockChatModel : IChatCompletionModel
    {
        private int callCount = 0;

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.Delay(10, ct);
            callCount++;
            
            // Simulate progressive improvement
            return callCount switch
            {
                1 => "Draft: Basic explanation of monads.",
                2 => "Critique: The draft is too brief and lacks examples.",
                3 => "Improved: Monads are a design pattern in functional programming that allow sequencing operations. They provide a way to handle side effects in a pure functional way.",
                4 => "Critique: Good improvement but could use more concrete examples.",
                5 => "Final: Monads are a design pattern in functional programming that allow sequencing operations. For example, the Maybe monad handles null values elegantly, and the Either monad manages error handling. They provide a way to chain operations while maintaining referential transparency.",
                6 => "Critique: Excellent explanation with good examples.",
                7 => "Enhanced: Monads are a fundamental design pattern in functional programming. They consist of three components: a type constructor, a unit/return function, and a bind operation. Common examples include Maybe (handling null), Either (error handling), and List (non-determinism). Monads enable pure functional code to handle side effects elegantly while maintaining composability.",
                _ => $"Response {callCount}: Further improvements with additional context and clarity."
            };
        }
    }

    private class HighQualityMockChatModel : IChatCompletionModel
    {
        private int callCount = 0;

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.Delay(10, ct);
            callCount++;
            
            // Always generate positive, quality-indicating critiques
            return callCount switch
            {
                1 => "Draft response with good structure",
                2 => "Critique: This is excellent work with high quality content. The explanation is clear and comprehensive.",
                3 => "Improved response building on the strong foundation",
                4 => "Critique: Outstanding improvement. This represents high quality work with no major issues.",
                5 => "Final response with excellent refinement",
                _ => callCount % 2 == 0 
                    ? "Critique: Excellent work demonstrating high quality output." 
                    : $"High quality response {callCount}"
            };
        }
    }

    private class MockEmbeddingModel : LangChainPipeline.Domain.IEmbeddingModel
    {
        public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            await Task.Delay(5, ct);
            return Enumerable.Range(0, 384).Select(i => (float)i / 384).ToArray();
        }
    }
}
