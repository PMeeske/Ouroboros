// <copyright file="FileSystemDistinctionWeightStorageTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using Xunit;

/// <summary>
/// Tests for the FileSystemDistinctionWeightStorage implementation.
/// Validates file-based storage operations for distinction weights.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FileSystemDistinctionWeightStorageTests : IDisposable
{
    private readonly string _testStoragePath;
    private readonly DistinctionStorageConfig _config;
    private readonly Mock<ILogger<FileSystemDistinctionWeightStorage>> _mockLogger;
    private readonly FileSystemDistinctionWeightStorage _sut;

    public FileSystemDistinctionWeightStorageTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"test-distinction-storage-{Guid.NewGuid()}");
        _config = new DistinctionStorageConfig(
            StoragePath: _testStoragePath,
            MaxTotalStorageBytes: 1024 * 1024,
            DissolvedRetentionPeriod: TimeSpan.FromDays(30));
        _mockLogger = new Mock<ILogger<FileSystemDistinctionWeightStorage>>();
        _sut = new FileSystemDistinctionWeightStorage(_config, _mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testStoragePath))
        {
            Directory.Delete(_testStoragePath, true);
        }
    }

    #region StoreWeightsAsync Tests

    [Fact]
    public async Task StoreWeightsAsync_CreatesStorageDirectory()
    {
        // Arrange
        var id = "test-id";
        var weights = new byte[] { 1, 2, 3, 4, 5 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.8, "Recognition", DateTime.UtcNow, false, weights.Length);

        // Act
        var result = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Directory.Exists(_testStoragePath).Should().BeTrue();
    }

    [Fact]
    public async Task StoreWeightsAsync_SavesWeightsToFile()
    {
        // Arrange
        var id = "test-weights";
        var weights = new byte[] { 10, 20, 30, 40, 50 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.8, "Recognition", DateTime.UtcNow, false, weights.Length);

        // Act
        var result = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expectedPath = Path.Combine(_testStoragePath, $"{id}.weights");
        result.Value.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
        File.ReadAllBytes(expectedPath).Should().Equal(weights);
    }

    [Fact]
    public async Task StoreWeightsAsync_SavesMetadata()
    {
        // Arrange
        var id = "test-id";
        var weights = new byte[] { 1, 2, 3 };
        var metadata = new DistinctionWeightMetadata(
            id, 
            Path.Combine(_testStoragePath, $"{id}.weights"), 
            0.9, 
            "Recognition", 
            DateTime.UtcNow, 
            false, 
            weights.Length);

        // Act
        var result = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var metadataPath = Path.Combine(_testStoragePath, "metadata.json");
        File.Exists(metadataPath).Should().BeTrue();
    }

    [Fact]
    public async Task StoreWeightsAsync_OverwritesExistingWeights()
    {
        // Arrange
        var id = "test-id";
        var weights1 = new byte[] { 1, 2, 3 };
        var weights2 = new byte[] { 4, 5, 6, 7 };
        var metadata1 = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights1.Length);
        var metadata2 = new DistinctionWeightMetadata(
            id, string.Empty, 0.8, "Recognition", DateTime.UtcNow, false, weights2.Length);

        // Act
        await _sut.StoreWeightsAsync(id, weights1, metadata1);
        var result = await _sut.StoreWeightsAsync(id, weights2, metadata2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var filePath = Path.Combine(_testStoragePath, $"{id}.weights");
        File.ReadAllBytes(filePath).Should().Equal(weights2);
    }

    [Fact]
    public async Task StoreWeightsAsync_WithEmptyWeights_Succeeds()
    {
        // Arrange
        var id = "empty-weights";
        var weights = Array.Empty<byte>();
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Void", DateTime.UtcNow, false, 0);

        // Act
        var result = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(result.Value).Should().BeTrue();
        File.ReadAllBytes(result.Value).Should().BeEmpty();
    }

    [Fact]
    public async Task StoreWeightsAsync_WithLargeWeights_Succeeds()
    {
        // Arrange
        var id = "large-weights";
        var weights = new byte[10000];
        new Random().NextBytes(weights);
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.95, "Recognition", DateTime.UtcNow, false, weights.Length);

        // Act
        var result = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.ReadAllBytes(result.Value).Should().Equal(weights);
    }

    #endregion

    #region LoadWeightsAsync Tests

    [Fact]
    public async Task LoadWeightsAsync_LoadsSavedWeights()
    {
        // Arrange
        var id = "test-load";
        var weights = new byte[] { 100, 200, 50 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.7, "SubjectEmerges", DateTime.UtcNow, false, weights.Length);
        await _sut.StoreWeightsAsync(id, weights, metadata);

        // Act
        var result = await _sut.LoadWeightsAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(weights);
    }

    [Fact]
    public async Task LoadWeightsAsync_WithNonExistentId_ReturnsFailure()
    {
        // Arrange
        var id = "non-existent";

        // Act
        var result = await _sut.LoadWeightsAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task LoadWeightsAsync_WithEmptyWeights_ReturnsEmptyArray()
    {
        // Arrange
        var id = "empty-load";
        var weights = Array.Empty<byte>();
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Void", DateTime.UtcNow, false, 0);
        await _sut.StoreWeightsAsync(id, weights, metadata);

        // Act
        var result = await _sut.LoadWeightsAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadWeightsAsync_AfterMultipleStores_LoadsMostRecent()
    {
        // Arrange
        var id = "test-multiple";
        var weights1 = new byte[] { 1, 2 };
        var weights2 = new byte[] { 3, 4, 5 };
        var metadata1 = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights1.Length);
        var metadata2 = new DistinctionWeightMetadata(
            id, string.Empty, 0.8, "Recognition", DateTime.UtcNow, false, weights2.Length);

        await _sut.StoreWeightsAsync(id, weights1, metadata1);
        await _sut.StoreWeightsAsync(id, weights2, metadata2);

        // Act
        var result = await _sut.LoadWeightsAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(weights2);
    }

    #endregion

    #region ListWeightsAsync Tests

    [Fact]
    public async Task ListWeightsAsync_WithNoWeights_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.ListWeightsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ListWeightsAsync_ReturnsAllStoredMetadata()
    {
        // Arrange
        var id1 = "weight1";
        var id2 = "weight2";
        var weights = new byte[] { 1, 2, 3 };
        var metadata1 = new DistinctionWeightMetadata(
            id1, 
            Path.Combine(_testStoragePath, $"{id1}.weights"),
            0.5, 
            "Distinction", 
            DateTime.UtcNow, 
            false, 
            weights.Length);
        var metadata2 = new DistinctionWeightMetadata(
            id2, 
            Path.Combine(_testStoragePath, $"{id2}.weights"),
            0.8, 
            "Recognition", 
            DateTime.UtcNow, 
            false, 
            weights.Length);

        await _sut.StoreWeightsAsync(id1, weights, metadata1);
        await _sut.StoreWeightsAsync(id2, weights, metadata2);

        // Act
        var result = await _sut.ListWeightsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(m => m.Id == id1);
        result.Value.Should().Contain(m => m.Id == id2);
    }

    [Fact]
    public async Task ListWeightsAsync_IncludesCorrectMetadata()
    {
        // Arrange
        var id = "test-metadata";
        var weights = new byte[] { 1, 2, 3, 4, 5 };
        var expectedFitness = 0.75;
        var expectedStage = "WorldCrystallizes";
        var metadata = new DistinctionWeightMetadata(
            id, 
            Path.Combine(_testStoragePath, $"{id}.weights"),
            expectedFitness, 
            expectedStage, 
            DateTime.UtcNow, 
            false, 
            weights.Length);

        await _sut.StoreWeightsAsync(id, weights, metadata);

        // Act
        var result = await _sut.ListWeightsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Should().ContainSingle().Subject;
        item.Id.Should().Be(id);
        item.Fitness.Should().Be(expectedFitness);
        item.LearnedAtStage.Should().Be(expectedStage);
        item.IsDissolved.Should().BeFalse();
    }

    #endregion

    #region DissolveWeightsAsync Tests

    [Fact]
    public async Task DissolveWeightsAsync_MovesFileToDissolvedExtension()
    {
        // Arrange
        var id = "test-dissolve";
        var weights = new byte[] { 1, 2, 3 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights.Length);
        var storeResult = await _sut.StoreWeightsAsync(id, weights, metadata);
        var originalPath = storeResult.Value;

        // Act
        var result = await _sut.DissolveWeightsAsync(originalPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(originalPath).Should().BeFalse("original file should be moved");
        File.Exists(originalPath + ".dissolved").Should().BeTrue("dissolved file should exist");
    }

    [Fact]
    public async Task DissolveWeightsAsync_UpdatesMetadataToMarkAsDissolved()
    {
        // Arrange
        var id = "test-metadata-update";
        var weights = new byte[] { 1, 2, 3 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights.Length);
        var storeResult = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Act
        await _sut.DissolveWeightsAsync(storeResult.Value);
        var listResult = await _sut.ListWeightsAsync();

        // Assert
        listResult.IsSuccess.Should().BeTrue();
        var item = listResult.Value.Should().ContainSingle().Subject;
        item.IsDissolved.Should().BeTrue();
        item.Path.Should().EndWith(".dissolved");
    }

    [Fact]
    public async Task DissolveWeightsAsync_WithNonExistentFile_Succeeds()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testStoragePath, "non-existent.weights");

        // Act
        var result = await _sut.DissolveWeightsAsync(nonExistentPath);

        // Assert
        result.IsSuccess.Should().BeTrue("should succeed even if file doesn't exist");
    }

    [Fact]
    public async Task DissolveWeightsAsync_PreservesFileContent()
    {
        // Arrange
        var id = "test-preserve";
        var weights = new byte[] { 10, 20, 30, 40 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.6, "Forgetting", DateTime.UtcNow, false, weights.Length);
        var storeResult = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Act
        await _sut.DissolveWeightsAsync(storeResult.Value);

        // Assert
        var dissolvedPath = storeResult.Value + ".dissolved";
        File.Exists(dissolvedPath).Should().BeTrue();
        File.ReadAllBytes(dissolvedPath).Should().Equal(weights, "content should be preserved");
    }

    [Fact]
    public async Task DissolveWeightsAsync_CanOverwriteExistingDissolvedFile()
    {
        // Arrange
        var id = "test-overwrite";
        var weights = new byte[] { 1, 2, 3 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights.Length);
        var storeResult = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Act - dissolve twice
        await _sut.DissolveWeightsAsync(storeResult.Value);
        
        // Store again and dissolve again
        await _sut.StoreWeightsAsync(id, weights, metadata);
        var storeResult2 = await _sut.StoreWeightsAsync(id, weights, metadata);
        var result = await _sut.DissolveWeightsAsync(storeResult2.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(storeResult2.Value + ".dissolved").Should().BeTrue();
    }

    #endregion

    #region GetTotalStorageSizeAsync Tests

    [Fact]
    public async Task GetTotalStorageSizeAsync_WithNoFiles_ReturnsZero()
    {
        // Act
        var result = await _sut.GetTotalStorageSizeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalStorageSizeAsync_CalculatesTotalSize()
    {
        // Arrange
        var weights1 = new byte[100];
        var weights2 = new byte[200];
        var metadata1 = new DistinctionWeightMetadata(
            "id1", string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights1.Length);
        var metadata2 = new DistinctionWeightMetadata(
            "id2", string.Empty, 0.8, "Recognition", DateTime.UtcNow, false, weights2.Length);

        await _sut.StoreWeightsAsync("id1", weights1, metadata1);
        await _sut.StoreWeightsAsync("id2", weights2, metadata2);

        // Act
        var result = await _sut.GetTotalStorageSizeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(300); // 100 + 200
    }

    [Fact]
    public async Task GetTotalStorageSizeAsync_ExcludesDissolvedFiles()
    {
        // Arrange
        var weights = new byte[150];
        var metadata = new DistinctionWeightMetadata(
            "test", string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights.Length);
        var storeResult = await _sut.StoreWeightsAsync("test", weights, metadata);

        await _sut.DissolveWeightsAsync(storeResult.Value);

        // Act
        var result = await _sut.GetTotalStorageSizeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0, "dissolved files should not count toward total size");
    }

    [Fact]
    public async Task GetTotalStorageSizeAsync_WithMultipleFiles_SumsCorrectly()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var weights = new byte[50];
            var metadata = new DistinctionWeightMetadata(
                $"id{i}", string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights.Length);
            await _sut.StoreWeightsAsync($"id{i}", weights, metadata);
        }

        // Act
        var result = await _sut.GetTotalStorageSizeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(250); // 5 * 50
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new FileSystemDistinctionWeightStorage(null!));
    }

    [Fact]
    public void Constructor_CreatesStorageDirectory()
    {
        // Arrange
        var testPath = Path.Combine(Path.GetTempPath(), $"test-auto-create-{Guid.NewGuid()}");
        var config = new DistinctionStorageConfig(
            testPath, 
            1024 * 1024, 
            TimeSpan.FromDays(30));

        try
        {
            // Act
            _ = new FileSystemDistinctionWeightStorage(config);

            // Assert
            Directory.Exists(testPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(testPath))
            {
                Directory.Delete(testPath, true);
            }
        }
    }

    [Fact]
    public async Task StoreWeightsAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var id = "test-cancel";
        var weights = new byte[] { 1, 2, 3 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights.Length);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _sut.StoreWeightsAsync(id, weights, metadata, cts.Token));
    }

    [Fact]
    public async Task LoadWeightsAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _sut.LoadWeightsAsync("test", cts.Token));
    }

    [Fact]
    public async Task ListWeightsAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _sut.ListWeightsAsync(cts.Token));
    }

    [Fact]
    public async Task DissolveWeightsAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _sut.DissolveWeightsAsync("test.weights", cts.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("test-with-dash")]
    [InlineData("test_with_underscore")]
    [InlineData("test123")]
    [InlineData("UPPERCASE")]
    public async Task StoreWeightsAsync_WithVariousIds_Succeeds(string id)
    {
        // Arrange
        var weights = new byte[] { 1, 2, 3 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.5, "Distinction", DateTime.UtcNow, false, weights.Length);

        // Act
        var result = await _sut.StoreWeightsAsync(id, weights, metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RoundTrip_StoreLoadDissolve_WorksCorrectly()
    {
        // Arrange
        var id = "roundtrip-test";
        var weights = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var metadata = new DistinctionWeightMetadata(
            id, string.Empty, 0.85, "Recognition", DateTime.UtcNow, false, weights.Length);

        // Act & Assert - Store
        var storeResult = await _sut.StoreWeightsAsync(id, weights, metadata);
        storeResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Load
        var loadResult = await _sut.LoadWeightsAsync(id);
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Should().Equal(weights);

        // Act & Assert - List
        var listResult = await _sut.ListWeightsAsync();
        listResult.IsSuccess.Should().BeTrue();
        listResult.Value.Should().ContainSingle();

        // Act & Assert - Dissolve
        var dissolveResult = await _sut.DissolveWeightsAsync(storeResult.Value);
        dissolveResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Verify dissolved
        var listResult2 = await _sut.ListWeightsAsync();
        listResult2.Value[0].IsDissolved.Should().BeTrue();
    }

    #endregion
}
