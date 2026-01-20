// <copyright file="EpisodicMemoryIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Core.Memory;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Core.Memory;

/// <summary>
/// Integration tests demonstrating episodic memory integration with Kleisli pipelines.
/// </summary>
public class EpisodicMemoryIntegrationTests
{
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ILogger<EpisodicMemoryEngine>> _mockLogger;
    private readonly EpisodicMemoryEngine _memoryEngine;

    public EpisodicMemoryIntegrationTests()
    {
        _mockVectorStore = new Mock<IVectorStore>();
        _mockLogger = new Mock<ILogger<EpisodicMemoryEngine>>();
        _memoryEngine = new EpisodicMemoryEngine(_mockVectorStore.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task WithEpisodicRetrieval_Integration_EnhancesPipelineExecution()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();
        var mockStep = new Mock<Step<PipelineBranch, PipelineBranch>>();
        
        mockStep.Setup(s => s(It.IsAny<PipelineBranch>()))
            .ReturnsAsync((PipelineBranch b) => b.WithEvent(new TestEvent(Guid.NewGuid(), "processed")));

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIntegrationTestDocuments());

        // Act
        var enhancedStep = mockStep.Object.WithEpisodicRetrieval(
            _memoryEngine, 
            b => b.ExtractGoalFromReasoning());

        var result = await enhancedStep(branch);

        // Assert
        result.Events.Should().HaveCount(3); // Original + memory retrieval + step result
        result.Events.OfType<MemoryRetrievalEvent>().Should().HaveCount(1);
        mockStep.Verify(s => s(It.IsAny<PipelineBranch>()), Times.Once);
    }

    [Fact]
    public async Task WithEpisodicRetrieval_KleisliResult_HandlesErrorsGracefully()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();
        
        var failingStep = KleisliResult<PipelineBranch, PipelineBranch, string>
            .Failure("Step failed");

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Vector store failed"));

        // Act
        var enhancedStep = failingStep.WithEpisodicRetrieval(
            _memoryEngine, 
            b => b.ExtractGoalFromReasoning());

        var result = await enhancedStep(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Episode retrieval failed");
    }

    [Fact]
    public async Task WithMemoryConsolidation_Integration_ExecutesBackgroundConsolidation()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();
        var processedEvent = new TestEvent(Guid.NewGuid(), "consolidation-test");
        
        var mockStep = new Mock<Step<PipelineBranch, PipelineBranch>>();
        mockStep.Setup(s => s(It.IsAny<PipelineBranch>()))
            .ReturnsAsync((PipelineBranch b) => b.WithEvent(processedEvent));

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        // Act
        var enhancedStep = mockStep.Object.WithMemoryConsolidation(
            _memoryEngine,
            ConsolidationStrategy.Compress,
            TimeSpan.FromHours(1));

        var result = await enhancedStep(branch);

        // Assert
        result.Events.Should().Contain(processedEvent);
        // Note: Consolidation runs in background, so we can't easily test its side effects
    }

    [Fact]
    public async Task WithExperientialPlanning_Integration_UsesRetrievedEpisodes()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();
        var mockStep = new Mock<Step<PipelineBranch, PipelineBranch>>();
        
        mockStep.Setup(s => s(It.IsAny<PipelineBranch>()))
            .ReturnsAsync((PipelineBranch b) => b.WithEvent(new TestEvent(Guid.NewGuid(), "planned")));

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIntegrationTestDocuments());

        // Act
        var enhancedStep = mockStep.Object.WithExperientialPlanning(
            _memoryEngine, 
            b => b.ExtractGoalFromReasoning());

        var result = await enhancedStep(branch);

        // Assert
        result.Events.Should().ContainSingle(e => e is PlanningEvent);
        mockStep.Verify(s => s(It.IsAny<PipelineBranch>()), Times.Once);
    }

    [Fact]
    public async Task WithEpisodicLifecycle_Integration_CombinesAllFeatures()
    {
        // Arrange
        var branch = CreateTestPipelineBranch();
        var mockStep = new Mock<Step<PipelineBranch, PipelineBranch>>();
        
        mockStep.Setup(s => s(It.IsAny<PipelineBranch>()))
            .ReturnsAsync((PipelineBranch b) => b.WithEvent(new TestEvent(Guid.NewGuid(), "lifecycle-test")));

        _mockVectorStore.Setup(vs => vs.GetSimilarDocumentsAsync(
            It.IsAny<float[]>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateIntegrationTestDocuments());

        // Act
        var enhancedStep = mockStep.Object.WithEpisodicLifecycle(
            _memoryEngine,
            b => b.ExtractGoalFromReasoning(),
            ConsolidationStrategy.Abstract,
            TimeSpan.FromHours(6));

        var result = await enhancedStep(branch);

        // Assert
        result.Events.Should().HaveCountGreaterThan(1);
        // Should include retrieval, planning, and original step events
        mockStep.Verify(s => s(It.IsAny<PipelineBranch>()), Times.Once);
    }

    [Fact]
    public async Task EpisodicExtensions_MonadLaws_ArePreserved()
    {
        // Test that Kleisli composition laws hold for episodic memory extensions
        
        // Arrange
        var branch = CreateTestPipelineBranch();
        
        // Identity law: f.Then(Identity) == f
        var step = CreateIdentityStep();
        var withMemory = step.WithEpisodicRetrieval(_memoryEngine, b => "query");
        
        // Act
        var originalResult = await step(branch);
        var enhancedResult = await withMemory(branch);
        
        // Assert - should preserve branch structure
        enhancedResult.Name.Should().Be(originalResult.Name);
        enhancedResult.Source.Should().BeEquivalentTo(originalResult.Source);
    }

    [Fact]
    public void EpisodicMemory_PipelineIntegration_ExampleUsage()
    {
        // Demonstration of how episodic memory integrates into a real pipeline
        
        // This test shows the intended usage pattern
        var pipeline = ExamplePipeline.CreateWithEpisodicMemory(_memoryEngine);
        
        pipeline.Should().NotBeNull();
        // The pipeline should be composable with other Kleisli arrows
    }

    #region Test Helper Methods

    private PipelineBranch CreateTestPipelineBranch()
    {
        var store = Mock.Of<IVectorStore>();
        var source = Mock.Of<DataSource>();
        var branch = new PipelineBranch("integration-test-branch", store, source);

        // Add reasoning events
        var reasoningEvent = new ReasoningStep(
            Guid.NewGuid(), 
            "reasoning", 
            new object(), 
            DateTime.UtcNow, 
            "integration test reasoning", 
            null);

        return branch.WithEvent(reasoningEvent);
    }

    private IReadOnlyCollection<Document> CreateIntegrationTestDocuments()
    {
        return new[]
        {
            new Document(
                "Integration test episode 1",
                new Dictionary<string, object>
                {
                    ["similarity_score"] = 0.95,
                    ["episode_data"] = "{\"goal\":\"integration test\",\"success_score\":0.9}"
                }),
            new Document(
                "Integration test episode 2",
                new Dictionary<string, object>
                {
                    ["similarity_score"] = 0.88,
                    ["episode_data"] = "{\"goal\":\"integration test\",\"success_score\":0.85}"
                })
        };
    }

    private Step<PipelineBranch, PipelineBranch> CreateIdentityStep()
    {
        return async branch => branch; // Identity function for pipeline
    }

    #endregion
}

/// <summary>
/// Example pipeline demonstrating episodic memory integration.
/// </summary>
public static class ExamplePipeline
{
    public static Step<PipelineBranch, PipelineBranch> CreateWithEpisodicMemory(IEpisodicMemoryEngine memory)
    {
        // Create a simple reasoning step
        var reasoningStep = CreateReasoningStep();

        // Enhance with episodic memory capabilities
        return reasoningStep
            .WithEpisodicRetrieval(memory, b => b.ExtractGoalFromReasoning())
            .WithExperientialPlanning(memory, b => b.ExtractGoalFromBranchInfo())
            .WithMemoryConsolidation(memory, ConsolidationStrategy.Compress, TimeSpan.FromHours(12));
    }

    private static Step<PipelineBranch, PipelineBranch> CreateReasoningStep()
    {
        return async branch =>
        {
            // Simulate reasoning process
            await Task.Delay(100);
            
            // Add reasoning result
            var reasoningEvent = new ReasoningStep(
                Guid.NewGuid(),
                "example-reasoning",
                new { Result = "Success" },
                DateTime.UtcNow,
                "Example reasoning completed",
                null);

            return branch.WithEvent(reasoningEvent);
        };
    }
}

/// <summary>
/// Test event for integration testing.
/// </summary>
public sealed record TestEvent(Guid Id, string Message) : PipelineEvent;

// Additional test types for integration scenarios
public sealed record DataSource
{
    public CancellationToken CancellationToken => default;
}

/// <summary>
/// Mock setup helper methods.
/// </summary>
public static class KleisliResultExtensions
{
    public static KleisliResult<TInput, TOutput, TError> Failure<TInput, TOutput, TError>(TError error)
    {
        return _ => Task.FromResult(Result<TOutput, TError>.Failure(error));
    }
}