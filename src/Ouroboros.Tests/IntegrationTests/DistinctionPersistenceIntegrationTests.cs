// <copyright file="DistinctionPersistenceIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Integration;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Xunit;

/// <summary>
/// Integration tests for distinction weight persistence.
/// Tests the full cycle of storing, retrieving, and dissolving distinction weights.
/// </summary>
[Trait("Category", "Integration")]
public class DistinctionPersistenceIntegrationTests : IDisposable
{
    private readonly FileSystemDistinctionStorage _storage;
    private readonly QdrantDistinctionMetadataStorage _metadata;
    private readonly string _testDirectory;
    private readonly DistinctionStorageConfig _config;

    public DistinctionPersistenceIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "distinction_integration_tests", Guid.NewGuid().ToString());
        _config = DistinctionStorageConfig.Default with { BaseDirectory = _testDirectory };

        _storage = new FileSystemDistinctionStorage(
            _config,
            NullLogger<FileSystemDistinctionStorage>.Instance);

        _metadata = new QdrantDistinctionMetadataStorage(
            "http://localhost:6333",
            NullLogger<QdrantDistinctionMetadataStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task FullCycle_CreateStoreRetrieveDissolve_WorksEndToEnd()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var originalWeights = CreateTestWeights(id);

        // Act & Assert - Store
        var storeResult = await _storage.StoreDistinctionWeightsAsync(id, originalWeights);
        storeResult.IsSuccess.Should().BeTrue();
        var storagePath = storeResult.Value;

        // Act & Assert - Retrieve
        var retrieveResult = await _storage.GetDistinctionWeightsAsync(storagePath);
        retrieveResult.IsSuccess.Should().BeTrue();
        retrieveResult.Value.Id.Should().Be(id);
        retrieveResult.Value.Circumstance.Should().Be(originalWeights.Circumstance);
        retrieveResult.Value.Embedding.Should().BeEquivalentTo(originalWeights.Embedding);

        // Act & Assert - List
        var listResult = await _storage.ListWeightsAsync();
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Should().ContainSingle(w => w.Id == id && !w.IsDissolved);

        // Act & Assert - Dissolve
        var dissolveResult = await _storage.DissolveWeightsAsync(storagePath);
        dissolveResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Verify dissolved
        var listAfterDissolve = await _storage.ListWeightsAsync();
        listAfterDissolve.IsSuccess.Should().BeTrue();
        listAfterDissolve.Value.Should().ContainSingle(w => w.Id == id && w.IsDissolved);
    }

    [Fact(Skip = "Requires Qdrant server")]
    public async Task FullCycleWithMetadata_WorksEndToEnd()
    {
        // Arrange
        var id = DistinctionId.NewId();
        var weights = CreateTestWeights(id);

        // Act & Assert - Store weights
        var storeResult = await _storage.StoreDistinctionWeightsAsync(id, weights);
        storeResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Store metadata
        var metadataResult = await _metadata.StoreMetadataAsync(weights, storeResult.Value);
        metadataResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Retrieve metadata
        var getMetadata = await _metadata.GetByIdAsync(id);
        getMetadata.IsSuccess.Should().BeTrue();
        getMetadata.Value.Id.Should().Be(id);
        getMetadata.Value.StoragePath.Should().Be(storeResult.Value);
        getMetadata.Value.IsDissolved.Should().BeFalse();

        // Act & Assert - Dissolve
        var dissolveFile = await _storage.DissolveWeightsAsync(storeResult.Value);
        dissolveFile.IsSuccess.Should().BeTrue();

        var dissolveMetadata = await _metadata.MarkDissolvedAsync(id);
        dissolveMetadata.IsSuccess.Should().BeTrue();

        // Verify metadata marked as dissolved
        var getAfterDissolve = await _metadata.GetByIdAsync(id);
        getAfterDissolve.IsFailure.Should().BeTrue(); // Deleted from Qdrant
    }

    [Fact]
    public async Task RecognitionMerge_WithMultipleDistinctions_ProducesValidMerged()
    {
        // Arrange - Create and store multiple distinctions
        var ids = new[]
        {
            DistinctionId.NewId(),
            DistinctionId.NewId(),
            DistinctionId.NewId()
        };

        var weights = ids.Select((id, index) => CreateTestWeights(id, fitness: 0.7 + (index * 0.05)))
            .ToList();

        foreach (var (id, weight) in ids.Zip(weights))
        {
            var storeResult = await _storage.StoreDistinctionWeightsAsync(id, weight);
            storeResult.IsSuccess.Should().BeTrue();
        }

        // Act - Merge on recognition
        var context = new RecognitionContext(
            Circumstance: "merged_recognition",
            SelfEmbedding: new float[] { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f },
            CurrentStage: 6); // Recognition stage

        var mergeResult = await _storage.MergeOnRecognitionAsync(weights, context);

        // Assert
        mergeResult.IsSuccess.Should().BeTrue();
        mergeResult.Value.Circumstance.Should().Be("merged_recognition");
        mergeResult.Value.LearnedAtStage.Should().Be(6);
        mergeResult.Value.Embedding.Length.Should().Be(weights[0].Embedding.Length);
        mergeResult.Value.Fitness.Should().BeInRange(0.6, 1.0);

        // Store merged result
        var storeMerged = await _storage.StoreDistinctionWeightsAsync(
            mergeResult.Value.Id,
            mergeResult.Value);
        storeMerged.IsSuccess.Should().BeTrue();

        // Verify it can be retrieved
        var retrieveMerged = await _storage.GetDistinctionWeightsAsync(storeMerged.Value);
        retrieveMerged.IsSuccess.Should().BeTrue();
        retrieveMerged.Value.Circumstance.Should().Be("merged_recognition");
    }

    [Fact]
    public async Task StorageLimit_EnforcedCorrectly()
    {
        // Arrange - Create config with very small total limit
        var limitedConfig = _config with
        {
            MaxTotalStorageBytes = 1024, // 1 KB
            MaxWeightSizeBytes = 512 // 512 bytes
        };

        var limitedStorage = new FileSystemDistinctionStorage(
            limitedConfig,
            NullLogger<FileSystemDistinctionStorage>.Instance);

        // Act - Try to store weights until limit is reached
        var successCount = 0;
        var failCount = 0;

        for (int i = 0; i < 10; i++)
        {
            var id = DistinctionId.NewId();
            var weights = CreateTestWeights(id);
            var result = await limitedStorage.StoreDistinctionWeightsAsync(id, weights);

            if (result.IsSuccess)
            {
                successCount++;
            }
            else if (result.Error.Contains("exceed"))
            {
                failCount++;
                break; // Stop once we hit the limit
            }
        }

        // Assert
        successCount.Should().BeGreaterThan(0, "Should store at least one weight");
        failCount.Should().BeGreaterThan(0, "Should eventually fail due to size limit");
    }

    [Fact]
    public async Task ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var tasks = new List<Task<Result<string, string>>>();
        var ids = Enumerable.Range(0, 10).Select(_ => DistinctionId.NewId()).ToList();

        // Act - Store multiple distinctions concurrently
        foreach (var id in ids)
        {
            var weights = CreateTestWeights(id);
            tasks.Add(_storage.StoreDistinctionWeightsAsync(id, weights));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());

        // Verify all are stored
        var listResult = await _storage.ListWeightsAsync();
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Count.Should().Be(10);
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
            Circumstance: $"test_circumstance_{id}",
            CreatedAt: DateTime.UtcNow,
            LastUpdatedAt: null);
    }
}
