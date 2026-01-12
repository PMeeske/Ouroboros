// <copyright file="DistinctionWeightStorageTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Xunit;

/// <summary>
/// Unit tests for FileSystemDistinctionStorage.
/// </summary>
[Trait("Category", "Unit")]
public class DistinctionWeightStorageTests : IDisposable
{
    private readonly FileSystemDistinctionStorage _storage;
    private readonly string _testDirectory;
    private readonly DistinctionStorageConfig _config;

    public DistinctionWeightStorageTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "distinction_tests", Guid.NewGuid().ToString());
        _config = new DistinctionStorageConfig(
            BaseDirectory: _testDirectory,
            MaxWeightSizeBytes: 5 * 1024 * 1024,
            MaxTotalStorageBytes: 500 * 1024 * 1024,
            ArchiveOnDissolution: true,
            DissolvedRetentionPeriod: TimeSpan.FromDays(30));

        _storage = new FileSystemDistinctionStorage(
            _config,
            NullLogger<FileSystemDistinctionStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StoreDistinctionWeightsAsync_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id);

        // Act
        var result = await _storage.StoreDistinctionWeightsAsync(id, weights);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        File.Exists(result.Value).Should().BeTrue();
    }

    [Fact]
    public async Task StoreDistinctionWeightsAsync_WithEmptyEmbedding_ReturnsFailure()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id) with { Embedding = Array.Empty<float>() };

        // Act
        var result = await _storage.StoreDistinctionWeightsAsync(id, weights);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Embedding cannot be empty");
    }

    [Fact]
    public async Task GetDistinctionWeightsAsync_WithValidPath_ReturnsWeights()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var originalWeights = CreateTestWeights(id);
        var storeResult = await _storage.StoreDistinctionWeightsAsync(id, originalWeights);
        storeResult.IsSuccess.Should().BeTrue();

        // Act
        var result = await _storage.GetDistinctionWeightsAsync(storeResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(originalWeights.Id);
        result.Value.Circumstance.Should().Be(originalWeights.Circumstance);
        result.Value.Fitness.Should().Be(originalWeights.Fitness);
        result.Value.Embedding.Should().BeEquivalentTo(originalWeights.Embedding);
    }

    [Fact]
    public async Task GetDistinctionWeightsAsync_WithInvalidPath_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.distinction.bin");

        // Act
        var result = await _storage.GetDistinctionWeightsAsync(nonExistentPath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task DissolveWeightsAsync_WithArchiveEnabled_MovesToDissolvedFile()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id);
        var storeResult = await _storage.StoreDistinctionWeightsAsync(id, weights);
        storeResult.IsSuccess.Should().BeTrue();
        var originalPath = storeResult.Value;

        // Act
        var result = await _storage.DissolveWeightsAsync(originalPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(originalPath).Should().BeFalse();
        var dissolvedPath = originalPath.Replace(".distinction.bin", ".dissolved.bin");
        File.Exists(dissolvedPath).Should().BeTrue();
    }

    [Fact]
    public async Task DissolveWeightsAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.distinction.bin");

        // Act
        var result = await _storage.DissolveWeightsAsync(nonExistentPath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task MergeOnRecognitionAsync_WithMultipleWeights_ReturnsMergedWeights()
    {
        // Arrange
        var weights1 = CreateTestWeights(DistinctionId.NewId(), fitness: 0.8);
        var weights2 = CreateTestWeights(DistinctionId.NewId(), fitness: 0.9);
        var weights3 = CreateTestWeights(DistinctionId.NewId(), fitness: 0.7);
        var weightsList = new[] { weights1, weights2, weights3 };

        var context = new RecognitionContext(
            Circumstance: "merged_context",
            SelfEmbedding: new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f },
            CurrentStage: 6); // Recognition stage

        // Act
        var result = await _storage.MergeOnRecognitionAsync(weightsList, context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Fitness.Should().BeApproximately(0.8, 0.1); // Average
        result.Value.Circumstance.Should().Be("merged_context");
        result.Value.Embedding.Length.Should().Be(weights1.Embedding.Length);
    }

    [Fact]
    public async Task MergeOnRecognitionAsync_WithEmptyList_ReturnsFailure()
    {
        // Arrange
        var emptyList = Array.Empty<DistinctionWeights>();
        var context = new RecognitionContext(
            Circumstance: "test",
            SelfEmbedding: new float[] { 1.0f },
            CurrentStage: 6);

        // Act
        var result = await _storage.MergeOnRecognitionAsync(emptyList, context);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task MergeOnRecognitionAsync_WithSingleWeight_ReturnsSameWeight()
    {
        // Arrange
        var weights = CreateTestWeights(DistinctionId.NewId());
        var context = new RecognitionContext(
            Circumstance: "test",
            SelfEmbedding: new float[] { 1.0f },
            CurrentStage: 6);

        // Act
        var result = await _storage.MergeOnRecognitionAsync(new[] { weights }, context);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(weights);
    }

    [Fact]
    public async Task ListWeightsAsync_WithMultipleStoredWeights_ReturnsAllWeights()
    {
        // Arrange
        var id1 = DistinctionId.NewId();
        var id2 = DistinctionId.NewId();
        var id3 = DistinctionId.NewId();

        await _storage.StoreDistinctionWeightsAsync(id1, CreateTestWeights(id1));
        await _storage.StoreDistinctionWeightsAsync(id2, CreateTestWeights(id2));
        await _storage.StoreDistinctionWeightsAsync(id3, CreateTestWeights(id3));

        // Act
        var result = await _storage.ListWeightsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(3);
        result.Value.Should().Contain(w => w.Id == id1);
        result.Value.Should().Contain(w => w.Id == id2);
        result.Value.Should().Contain(w => w.Id == id3);
    }

    [Fact]
    public async Task ListWeightsAsync_IncludesDissolvedWeights()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var storeResult = await _storage.StoreDistinctionWeightsAsync(id, CreateTestWeights(id));
        await _storage.DissolveWeightsAsync(storeResult.Value);

        // Act
        var result = await _storage.ListWeightsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.First().IsDissolved.Should().BeTrue();
    }

    [Fact]
    public async Task GetTotalStorageSizeAsync_WithMultipleWeights_ReturnsTotalSize()
    {
        // Arrange
        var id1 = DistinctionId.NewId();
        var id2 = DistinctionId.NewId();

        await _storage.StoreDistinctionWeightsAsync(id1, CreateTestWeights(id1));
        await _storage.StoreDistinctionWeightsAsync(id2, CreateTestWeights(id2));

        // Act
        var result = await _storage.GetTotalStorageSizeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTotalStorageSizeAsync_WithEmptyDirectory_ReturnsZero()
    {
        // Act
        var result = await _storage.GetTotalStorageSizeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task StoreDistinctionWeightsAsync_EnforcesSizeLimit()
    {
        // Arrange
        var smallConfig = _config with { MaxWeightSizeBytes = 100 };
        var smallStorage = new FileSystemDistinctionStorage(
            smallConfig,
            NullLogger<FileSystemDistinctionStorage>.Instance);

        var id = DistinctionId.NewId();
        var largeWeights = CreateTestWeights(id) with
        {
            Embedding = new float[10000] // Large array
        };

        // Act
        var result = await smallStorage.StoreDistinctionWeightsAsync(id, largeWeights);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("exceeds maximum");
    }

    private DistinctionWeights CreateTestWeights(
        DistinctionId id,
        double fitness = 0.85)
    {
        return new DistinctionWeights(
            Id: id,
            Embedding: new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f },
            DissolutionMask: new float[] { 0.01f, 0.02f, 0.03f, 0.04f, 0.05f },
            RecognitionTransform: new float[] { 1.1f, 1.2f, 1.3f, 1.4f, 1.5f },
            LearnedAtStage: 1, // Distinction stage
            Fitness: fitness,
            Circumstance: "test_circumstance",
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);
    }
}
