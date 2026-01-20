// <copyright file="AdapterLearningEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Xunit;

/// <summary>
/// Unit tests for AdapterLearningEngine.
/// </summary>
[Trait("Category", "Unit")]
public class AdapterLearningEngineTests
{
    private readonly MockPeftIntegration _peft;
    private readonly InMemoryAdapterStorage _storage;
    private readonly FileSystemBlobStorage _blobStorage;
    private readonly AdapterLearningEngine _engine;
    private readonly string _testDirectory;

    public AdapterLearningEngineTests()
    {
        _peft = new MockPeftIntegration(NullLogger<MockPeftIntegration>.Instance);
        _storage = new InMemoryAdapterStorage(NullLogger<InMemoryAdapterStorage>.Instance);
        _testDirectory = Path.Combine(Path.GetTempPath(), "adapter_tests", Guid.NewGuid().ToString());
        _blobStorage = new FileSystemBlobStorage(_testDirectory, NullLogger<FileSystemBlobStorage>.Instance);
        _engine = new AdapterLearningEngine(
            _peft,
            _storage,
            _blobStorage,
            "test-model",
            NullLogger<AdapterLearningEngine>.Instance);
    }

    [Fact]
    public async Task CreateAdapterAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var config = AdapterConfig.Default();

        // Act
        var result = await _engine.CreateAdapterAsync("test-task", config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAdapterAsync_WithEmptyTaskName_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default();

        // Act
        var result = await _engine.CreateAdapterAsync(string.Empty, config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Task name cannot be empty");
    }

    [Fact]
    public async Task CreateAdapterAsync_WithInvalidConfig_ReturnsFailure()
    {
        // Arrange
        var config = AdapterConfig.Default() with { Rank = -1 };

        // Act
        var result = await _engine.CreateAdapterAsync("test-task", config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Rank must be positive");
    }

    [Fact]
    public async Task CreateAdapterAsync_StoresMetadataAndWeights()
    {
        // Arrange
        var config = AdapterConfig.Default();

        // Act
        var result = await _engine.CreateAdapterAsync("test-task", config);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var metadataResult = await _storage.GetMetadataAsync(result.Value);
        metadataResult.IsSuccess.Should().BeTrue();
        metadataResult.Value.TaskName.Should().Be("test-task");
        metadataResult.Value.Config.Should().Be(config);
    }

    [Fact]
    public async Task TrainAdapterAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var createResult = await _engine.CreateAdapterAsync("test-task", AdapterConfig.Default());
        var adapterId = createResult.Value;

        var examples = new List<TrainingExample>
        {
            new("Input 1", "Output 1", 1.0),
            new("Input 2", "Output 2", 1.0),
        };

        // Act
        var result = await _engine.TrainAdapterAsync(adapterId, examples, TrainingConfig.Default());

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TrainAdapterAsync_WithEmptyExamples_ReturnsFailure()
    {
        // Arrange
        var createResult = await _engine.CreateAdapterAsync("test-task", AdapterConfig.Default());
        var adapterId = createResult.Value;

        // Act
        var result = await _engine.TrainAdapterAsync(adapterId, new List<TrainingExample>(), TrainingConfig.Default());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Training examples cannot be empty");
    }

    [Fact]
    public async Task TrainAdapterAsync_WithNonexistentAdapter_ReturnsFailure()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var examples = new List<TrainingExample>
        {
            new("Input 1", "Output 1", 1.0),
        };

        // Act
        var result = await _engine.TrainAdapterAsync(adapterId, examples, TrainingConfig.Default());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Adapter not found");
    }

    [Fact]
    public async Task GenerateWithAdapterAsync_WithoutAdapter_ReturnsBaseModelResponse()
    {
        // Arrange
        var prompt = "Test prompt";

        // Act
        var result = await _engine.GenerateWithAdapterAsync(prompt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("[BASE]");
        result.Value.Should().Contain(prompt);
    }

    [Fact]
    public async Task GenerateWithAdapterAsync_WithAdapter_ReturnsAdaptedResponse()
    {
        // Arrange
        var createResult = await _engine.CreateAdapterAsync("test-task", AdapterConfig.Default());
        var adapterId = createResult.Value;
        var prompt = "Test prompt";

        // Act
        var result = await _engine.GenerateWithAdapterAsync(prompt, adapterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("[ADAPTED]");
        result.Value.Should().Contain(prompt);
    }

    [Fact]
    public async Task GenerateWithAdapterAsync_WithEmptyPrompt_ReturnsFailure()
    {
        // Act
        var result = await _engine.GenerateWithAdapterAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Prompt cannot be empty");
    }

    [Fact]
    public async Task LearnFromFeedbackAsync_WithUserCorrection_UpdatesAdapter()
    {
        // Arrange
        var createResult = await _engine.CreateAdapterAsync("test-task", AdapterConfig.Default());
        var adapterId = createResult.Value;
        var feedback = FeedbackSignal.UserCorrection("Corrected output");

        // Act
        var result = await _engine.LearnFromFeedbackAsync(
            "Test prompt",
            "Original output",
            feedback,
            adapterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LearnFromFeedbackAsync_WithSuccessSignal_UpdatesAdapter()
    {
        // Arrange
        var createResult = await _engine.CreateAdapterAsync("test-task", AdapterConfig.Default());
        var adapterId = createResult.Value;
        var feedback = FeedbackSignal.Success(0.8);

        // Act
        var result = await _engine.LearnFromFeedbackAsync(
            "Test prompt",
            "Good output",
            feedback,
            adapterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MergeAdaptersAsync_WithTwoAdapters_ReturnsSuccess()
    {
        // Arrange
        var adapter1 = await _engine.CreateAdapterAsync("task1", AdapterConfig.Default());
        var adapter2 = await _engine.CreateAdapterAsync("task2", AdapterConfig.Default());

        // Act
        var result = await _engine.MergeAdaptersAsync(
            new List<AdapterId> { adapter1.Value, adapter2.Value },
            MergeStrategy.Average);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MergeAdaptersAsync_WithSingleAdapter_ReturnsFailure()
    {
        // Arrange
        var adapter1 = await _engine.CreateAdapterAsync("task1", AdapterConfig.Default());

        // Act
        var result = await _engine.MergeAdaptersAsync(
            new List<AdapterId> { adapter1.Value },
            MergeStrategy.Average);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("At least 2 adapters are required");
    }

    [Fact]
    public async Task CreateAdapterAsync_WithCancellation_ReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _engine.CreateAdapterAsync("test-task", AdapterConfig.Default(), cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }
}
