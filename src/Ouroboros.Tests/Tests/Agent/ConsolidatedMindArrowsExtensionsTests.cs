// <copyright file="ConsolidatedMindArrowsExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.Agent;

/// <summary>
/// Tests for ConsolidatedMindArrowsExtensions arrow parameterization pattern.
/// </summary>
[Trait("Category", "Unit")]
public class ConsolidatedMindArrowsExtensionsTests
{
    private class MockChatCompletionModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult("Mock response");
    }

    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string text, CancellationToken ct = default)
        {
            var embedding = new float[768];
            for (int i = 0; i < 768; i++)
            {
                embedding[i] = 0.1f;
            }
            return Task.FromResult(embedding);
        }
    }

    private static PipelineBranch CreateTestBranch()
    {
        var store = new TrackedVectorStore();
        var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch("test", store, dataSource);
    }

    private static List<SpecializedModel> CreateTestSpecialists()
    {
        return new List<SpecializedModel>
        {
            new SpecializedModel(
                SpecializedRole.QuickResponse,
                new MockChatCompletionModel(),
                "mock-model",
                new[] { "general" },
                1.0,
                2048)
        };
    }

    [Fact]
    public void CreateProcessingArrowFactory_ShouldReturnFactory()
    {
        // Arrange
        var specialists = CreateTestSpecialists();
        var config = MindConfig.Minimal();

        // Act
        var factory = ConsolidatedMindArrowsExtensions.CreateProcessingArrowFactory(
            specialists,
            config);

        // Assert
        factory.Should().NotBeNull();

        // Test that the factory can create arrows
        var arrow = factory("test prompt");
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateProcessingArrowFactory_CreatedArrow_ShouldProcess()
    {
        // Arrange
        var specialists = CreateTestSpecialists();
        var config = MindConfig.Minimal();
        var factory = ConsolidatedMindArrowsExtensions.CreateProcessingArrowFactory(
            specialists,
            config);
        var branch = CreateTestBranch();

        // Act
        var arrow = factory("test prompt");
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReasoningArrowWithExplicitConfig_ShouldProcessWithConfig()
    {
        // Arrange
        var specialists = CreateTestSpecialists();
        var config = new MindConfig(
            EnableThinking: false,
            EnableVerification: false,
            EnableParallelExecution: false);
        var embed = new MockEmbeddingModel();
        var branch = CreateTestBranch();

        // Act
        var arrow = ConsolidatedMindArrowsExtensions.ReasoningArrowWithExplicitConfig(
            specialists,
            config,
            embed,
            "test topic",
            "test query",
            5);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SafeReasoningArrowWithExplicitConfig_ShouldReturnSuccess()
    {
        // Arrange
        var specialists = CreateTestSpecialists();
        var config = MindConfig.Minimal();
        var embed = new MockEmbeddingModel();
        var branch = CreateTestBranch();

        // Act
        var arrow = ConsolidatedMindArrowsExtensions.SafeReasoningArrowWithExplicitConfig(
            specialists,
            config,
            embed,
            "test topic",
            "test query",
            5);
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ComplexTaskArrowWithExplicitConfig_ShouldProcessTask()
    {
        // Arrange
        var specialists = CreateTestSpecialists();
        var config = MindConfig.Minimal();
        var embed = new MockEmbeddingModel();
        var branch = CreateTestBranch();

        // Act
        var arrow = ConsolidatedMindArrowsExtensions.ComplexTaskArrowWithExplicitConfig(
            specialists,
            config,
            embed,
            "complex task",
            5);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateConfiguredSystem_ShouldReturnValidSystem()
    {
        // Arrange
        var endpoint = "http://localhost:11434";
        var apiKey = "test-key";
        var config = MindConfig.Minimal();

        // Act
        var system = ConsolidatedMindArrowsExtensions.CreateConfiguredSystem(
            endpoint,
            apiKey,
            config,
            useHighQuality: false);

        // Assert
        system.Should().NotBeNull();
        system.Configuration.Should().Be(config);
        system.Specialists.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateMinimalSystem_ShouldReturnMinimalSystem()
    {
        // Arrange
        var endpoint = "http://localhost:11434";
        var apiKey = "test-key";

        // Act
        var system = ConsolidatedMindArrowsExtensions.CreateMinimalSystem(endpoint, apiKey);

        // Assert
        system.Should().NotBeNull();
        system.Configuration.EnableThinking.Should().BeFalse();
        system.Configuration.EnableVerification.Should().BeFalse();
        system.Configuration.EnableParallelExecution.Should().BeFalse();
    }

    [Fact]
    public void ConfiguredMindArrowSystem_CreateReasoningArrow_ShouldReturnArrow()
    {
        // Arrange
        var config = MindConfig.Minimal();
        var system = ConsolidatedMindArrowsExtensions.CreateConfiguredSystem(
            "http://localhost:11434",
            "test-key",
            config,
            useHighQuality: false);
        var embed = new MockEmbeddingModel();

        // Act
        var arrow = system.CreateReasoningArrow(embed, "topic", "query");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void ConfiguredMindArrowSystem_CreateComplexTaskArrow_ShouldReturnArrow()
    {
        // Arrange
        var config = MindConfig.Minimal();
        var system = ConsolidatedMindArrowsExtensions.CreateConfiguredSystem(
            "http://localhost:11434",
            "test-key",
            config,
            useHighQuality: false);
        var embed = new MockEmbeddingModel();

        // Act
        var arrow = system.CreateComplexTaskArrow(embed, "complex task");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void ConfiguredMindArrowSystem_CreateProcessingFactory_ShouldReturnFactory()
    {
        // Arrange
        var config = MindConfig.Minimal();
        var system = ConsolidatedMindArrowsExtensions.CreateConfiguredSystem(
            "http://localhost:11434",
            "test-key",
            config,
            useHighQuality: false);

        // Act
        var factory = system.CreateProcessingFactory();

        // Assert
        factory.Should().NotBeNull();

        // Test that the factory can create arrows
        var arrow = factory("test prompt");
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task ArrowComposition_MultipleConfiguredArrows_ShouldCompose()
    {
        // Arrange
        var config = MindConfig.Minimal();
        var system = ConsolidatedMindArrowsExtensions.CreateConfiguredSystem(
            "http://localhost:11434",
            "test-key",
            config,
            useHighQuality: false);
        var embed = new MockEmbeddingModel();
        var branch = CreateTestBranch();

        // Act - Create and compose multiple arrows
        var arrow1 = system.CreateReasoningArrow(embed, "topic1", "query1");
        var arrow2 = system.CreateReasoningArrow(embed, "topic2", "query2");

        var result1 = await arrow1(branch);
        var result2 = await arrow2(result1);

        // Assert
        result2.Events.OfType<ReasoningStep>().Should().HaveCount(2);
    }

    [Fact]
    public void ConfiguredMindArrowSystem_Properties_ShouldReturnConfiguredValues()
    {
        // Arrange
        var config = new MindConfig(
            EnableThinking: true,
            EnableVerification: true,
            MaxParallelism: 4);
        var system = ConsolidatedMindArrowsExtensions.CreateConfiguredSystem(
            "http://localhost:11434",
            "test-key",
            config,
            useHighQuality: false);

        // Act & Assert
        system.Configuration.EnableThinking.Should().BeTrue();
        system.Configuration.EnableVerification.Should().BeTrue();
        system.Configuration.MaxParallelism.Should().Be(4);
        system.Specialists.Should().NotBeEmpty();
    }
}
