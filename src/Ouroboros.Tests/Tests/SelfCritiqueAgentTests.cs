// <copyright file="SelfCritiqueAgentTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using LangChain.DocumentLoaders;
using LangChain.Providers;
using Ouroboros.Agent;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.States;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Providers;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Tests for the SelfCritiqueAgent functionality.
/// Validates draft-critique-improve cycles, confidence ratings, and safety constraints.
/// </summary>
public class SelfCritiqueAgentTests
{
    /// <summary>
    /// Tests that the agent generates a proper self-critique result with one iteration.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldProduceDraftCritiqueImprove_WithOneIteration()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Mock response");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.IterationsPerformed);
        Assert.NotEmpty(result.Value.Draft);
        Assert.NotEmpty(result.Value.Critique);
        Assert.NotEmpty(result.Value.ImprovedResponse);
    }

    /// <summary>
    /// Tests that the agent respects the maximum iteration limit of 5.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldCapIterations_AtMaximumFive()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Mock response");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act - Request 10 iterations but should be capped at 5
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 10);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.IterationsPerformed);
    }

    /// <summary>
    /// Tests that the agent performs multiple critique-improve cycles correctly.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldPerformMultipleIterations_Correctly()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Mock response");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.IterationsPerformed);
        
        // Verify events were recorded properly
        var events = result.Value.Branch.Events.OfType<ReasoningStep>().ToList();
        
        // Should have: 1 Draft + (3 iterations * 2 steps each: Critique + Improve) = 7 events
        Assert.True(events.Count >= 7, $"Expected at least 7 events, got {events.Count}");
        
        // Verify first event is a Draft
        Assert.IsType<Draft>(events[0].State);
    }

    /// <summary>
    /// Tests that confidence rating is High when critique indicates high quality.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldReturnHighConfidence_WhenCritiqueIndicatesQuality()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLMWithCritique("This is excellent work with high quality output.");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConfidenceRating.High, result.Value.Confidence);
    }

    /// <summary>
    /// Tests that confidence rating is Low when critique indicates poor quality.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldReturnLowConfidence_WhenCritiqueIndicatesPoorQuality()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLMWithCritique("This needs work and has significant issues.");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ConfidenceRating.Low, result.Value.Confidence);
    }

    /// <summary>
    /// Tests that the agent produces event sourcing for each step.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldEmitEvents_ForEachStep()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Mock response");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 2);

        // Assert
        Assert.True(result.IsSuccess);
        
        var events = result.Value.Branch.Events.OfType<ReasoningStep>().ToList();
        
        // Should have proper event sequence: Draft, Critique, Improve, Critique, Improve
        Assert.Contains(events, e => e.State is Draft);
        Assert.Contains(events, e => e.State is Critique);
        Assert.Contains(events, e => e.State is FinalSpec);
        
        // Count specific event types
        int drafts = events.Count(e => e.State is Draft);
        int critiques = events.Count(e => e.State is Critique);
        int improvements = events.Count(e => e.State is FinalSpec);
        
        Assert.Equal(1, drafts);
        Assert.Equal(2, critiques); // One per iteration
        Assert.Equal(2, improvements); // One per iteration
    }

    /// <summary>
    /// Tests that invalid iteration counts are normalized properly.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldNormalizeInvalidIterations_ToMinimumOne()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Mock response");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act - Request 0 or negative iterations
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 0);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.IterationsPerformed); // Should default to 1
    }

    /// <summary>
    /// Tests that the improved response differs from the initial draft.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation</returns>
    [Fact]
    public async Task GenerateWithCritique_ShouldProduceDifferentImprovedResponse_FromDraft()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLMWithProgression();
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();
        var agent = new SelfCritiqueAgent(llm, tools, embed);

        // Act
        var result = await agent.GenerateWithCritiqueAsync(branch, "test topic", "test query", iterations: 1);

        // Assert
        Assert.True(result.IsSuccess);
        // The mock LLM with progression should generate different outputs
        Assert.NotEqual(result.Value.Draft, result.Value.ImprovedResponse);
    }

    // Helper methods to create mock objects

    private static ToolAwareChatModel CreateMockLLM(string response)
    {
        var mockModel = new MockChatModel(response);
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static ToolAwareChatModel CreateMockLLMWithCritique(string critiqueText)
    {
        var mockModel = new MockChatModelWithCritique(critiqueText);
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static ToolAwareChatModel CreateMockLLMWithProgression()
    {
        var mockModel = new MockChatModelWithProgression();
        return new ToolAwareChatModel(mockModel, new ToolRegistry());
    }

    private static Ouroboros.Domain.IEmbeddingModel CreateMockEmbedding()
    {
        return new MockEmbeddingModel();
    }

    // Mock implementations

    private class MockChatModel : IChatCompletionModel
    {
        private readonly string response;

        public MockChatModel(string response)
        {
            this.response = response;
        }

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.Delay(10, ct); // Simulate async operation
            return this.response;
        }
    }

    private class MockChatModelWithCritique : IChatCompletionModel
    {
        private readonly string critiqueText;
        private int callCount = 0;

        public MockChatModelWithCritique(string critiqueText)
        {
            this.critiqueText = critiqueText;
        }

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.Delay(10, ct);
            callCount++;
            
            // Return critique text on even calls (critique phase)
            string content = (callCount % 2 == 0) ? critiqueText : $"Response {callCount}";
            
            return content;
        }
    }

    private class MockChatModelWithProgression : IChatCompletionModel
    {
        private int callCount = 0;

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.Delay(10, ct);
            callCount++;
            
            string content = callCount switch
            {
                1 => "Initial draft response",
                2 => "Critique: This could be improved",
                3 => "Improved response with better content",
                _ => $"Response {callCount}"
            };
            
            return content;
        }
    }

    private class MockEmbeddingModel : Ouroboros.Domain.IEmbeddingModel
    {
        public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            await Task.Delay(5, ct);
            return Enumerable.Range(0, 384).Select(i => (float)i / 384).ToArray();
        }
    }
}
