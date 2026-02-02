// <copyright file="InMemoryAdapterStorageTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.Storage;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Xunit;

/// <summary>
/// Unit tests for InMemoryAdapterStorage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class InMemoryAdapterStorageTests
{
    private readonly InMemoryAdapterStorage _storage;

    public InMemoryAdapterStorageTests()
    {
        _storage = new InMemoryAdapterStorage(NullLogger<InMemoryAdapterStorage>.Instance);
    }

    #region Helper Methods

    private static AdapterMetadata CreateTestMetadata(
        AdapterId? id = null,
        string taskName = "test-task",
        int exampleCount = 100,
        double? performanceScore = 0.85)
    {
        var adapterId = id ?? AdapterId.NewId();
        return AdapterMetadata.Create(
            adapterId,
            taskName,
            AdapterConfig.Default(),
            $"/blobs/{adapterId}.bin")
            .WithTraining(exampleCount, performanceScore);
    }

    #endregion

    #region StoreMetadataAsync Tests

    [Fact]
    public async Task StoreMetadataAsync_WithValidMetadata_ReturnsSuccess()
    {
        // Arrange
        var metadata = CreateTestMetadata();

        // Act
        var result = await _storage.StoreMetadataAsync(metadata);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _storage.Count.Should().Be(1);
    }

    [Fact]
    public async Task StoreMetadataAsync_WithNullMetadata_ReturnsFailure()
    {
        // Act
        var result = await _storage.StoreMetadataAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task StoreMetadataAsync_WithSameId_OverwritesExisting()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var original = CreateTestMetadata(adapterId, taskName: "original-task");
        var updated = CreateTestMetadata(adapterId, taskName: "updated-task");

        // Act
        await _storage.StoreMetadataAsync(original);
        var result = await _storage.StoreMetadataAsync(updated);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _storage.Count.Should().Be(1);

        var retrieved = await _storage.GetMetadataAsync(adapterId);
        retrieved.Value.TaskName.Should().Be("updated-task");
    }

    [Fact]
    public async Task StoreMetadataAsync_MultipleDifferentIds_StoresAll()
    {
        // Arrange
        var metadata1 = CreateTestMetadata(taskName: "task1");
        var metadata2 = CreateTestMetadata(taskName: "task2");
        var metadata3 = CreateTestMetadata(taskName: "task3");

        // Act
        await _storage.StoreMetadataAsync(metadata1);
        await _storage.StoreMetadataAsync(metadata2);
        await _storage.StoreMetadataAsync(metadata3);

        // Assert
        _storage.Count.Should().Be(3);
    }

    #endregion

    #region GetMetadataAsync Tests

    [Fact]
    public async Task GetMetadataAsync_WithExistingId_ReturnsMetadata()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var metadata = CreateTestMetadata(adapterId, taskName: "test-retrieval");
        await _storage.StoreMetadataAsync(metadata);

        // Act
        var result = await _storage.GetMetadataAsync(adapterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(adapterId);
        result.Value.TaskName.Should().Be("test-retrieval");
    }

    [Fact]
    public async Task GetMetadataAsync_WithNonExistentId_ReturnsFailure()
    {
        // Arrange
        var nonExistentId = AdapterId.NewId();

        // Act
        var result = await _storage.GetMetadataAsync(nonExistentId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetMetadataAsync_PreservesAllProperties()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var config = AdapterConfig.HighRank();
        var metadata = new AdapterMetadata(
            adapterId,
            "complex-task",
            config,
            "/storage/path.bin",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            500,
            0.95);
        await _storage.StoreMetadataAsync(metadata);

        // Act
        var result = await _storage.GetMetadataAsync(adapterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TaskName.Should().Be("complex-task");
        result.Value.Config.Rank.Should().Be(config.Rank);
        result.Value.BlobStoragePath.Should().Be("/storage/path.bin");
        result.Value.TrainingExampleCount.Should().Be(500);
        result.Value.PerformanceScore.Should().Be(0.95);
    }

    #endregion

    #region GetAdaptersByTaskAsync Tests

    [Fact]
    public async Task GetAdaptersByTaskAsync_WithMatchingTask_ReturnsAdapters()
    {
        // Arrange
        var metadata1 = CreateTestMetadata(taskName: "summarization");
        var metadata2 = CreateTestMetadata(taskName: "summarization");
        var metadata3 = CreateTestMetadata(taskName: "translation");
        await _storage.StoreMetadataAsync(metadata1);
        await _storage.StoreMetadataAsync(metadata2);
        await _storage.StoreMetadataAsync(metadata3);

        // Act
        var result = await _storage.GetAdaptersByTaskAsync("summarization");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(m => m.TaskName == "summarization");
    }

    [Fact]
    public async Task GetAdaptersByTaskAsync_CaseInsensitive_ReturnsMatches()
    {
        // Arrange
        var metadata = CreateTestMetadata(taskName: "Summarization");
        await _storage.StoreMetadataAsync(metadata);

        // Act
        var result = await _storage.GetAdaptersByTaskAsync("SUMMARIZATION");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAdaptersByTaskAsync_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var metadata = CreateTestMetadata(taskName: "existing-task");
        await _storage.StoreMetadataAsync(metadata);

        // Act
        var result = await _storage.GetAdaptersByTaskAsync("nonexistent-task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAdaptersByTaskAsync_WithInvalidTaskName_ReturnsFailure(string? invalidTask)
    {
        // Act
        var result = await _storage.GetAdaptersByTaskAsync(invalidTask!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    #endregion

    #region UpdateMetadataAsync Tests

    [Fact]
    public async Task UpdateMetadataAsync_WithExistingAdapter_UpdatesSuccessfully()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var original = CreateTestMetadata(adapterId, exampleCount: 100, performanceScore: 0.5);
        await _storage.StoreMetadataAsync(original);

        var updated = original.WithTraining(50, 0.9);

        // Act
        var result = await _storage.UpdateMetadataAsync(updated);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var retrieved = await _storage.GetMetadataAsync(adapterId);
        retrieved.Value.TrainingExampleCount.Should().Be(150);
        retrieved.Value.PerformanceScore.Should().Be(0.9);
    }

    [Fact]
    public async Task UpdateMetadataAsync_WithNonExistentAdapter_ReturnsFailure()
    {
        // Arrange
        var nonExistent = CreateTestMetadata();

        // Act
        var result = await _storage.UpdateMetadataAsync(nonExistent);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateMetadataAsync_WithNullMetadata_ReturnsFailure()
    {
        // Act
        var result = await _storage.UpdateMetadataAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    #endregion

    #region DeleteMetadataAsync Tests

    [Fact]
    public async Task DeleteMetadataAsync_WithExistingAdapter_DeletesSuccessfully()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var metadata = CreateTestMetadata(adapterId);
        await _storage.StoreMetadataAsync(metadata);

        // Act
        var result = await _storage.DeleteMetadataAsync(adapterId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteMetadataAsync_WithNonExistentAdapter_ReturnsSuccess()
    {
        // Arrange - idempotent delete
        var nonExistentId = AdapterId.NewId();

        // Act
        var result = await _storage.DeleteMetadataAsync(nonExistentId);

        // Assert
        result.IsSuccess.Should().BeTrue("delete should be idempotent");
    }

    [Fact]
    public async Task DeleteMetadataAsync_VerifyRemoved()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var metadata = CreateTestMetadata(adapterId);
        await _storage.StoreMetadataAsync(metadata);
        await _storage.DeleteMetadataAsync(adapterId);

        // Act
        var result = await _storage.GetMetadataAsync(adapterId);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Count and Clear Tests

    [Fact]
    public void Count_InitiallyZero()
    {
        // Assert
        _storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task Count_ReflectsStoredItems()
    {
        // Arrange & Act
        await _storage.StoreMetadataAsync(CreateTestMetadata());
        await _storage.StoreMetadataAsync(CreateTestMetadata());

        // Assert
        _storage.Count.Should().Be(2);
    }

    [Fact]
    public async Task Clear_RemovesAllItems()
    {
        // Arrange
        await _storage.StoreMetadataAsync(CreateTestMetadata());
        await _storage.StoreMetadataAsync(CreateTestMetadata());
        await _storage.StoreMetadataAsync(CreateTestMetadata());

        // Act
        _storage.Clear();

        // Assert
        _storage.Count.Should().Be(0);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentStores_AllSucceed()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _storage.StoreMetadataAsync(CreateTestMetadata()));

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(r => r.IsSuccess);
        _storage.Count.Should().Be(100);
    }

    [Fact]
    public async Task ConcurrentReadWrite_NoExceptions()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var metadata = CreateTestMetadata(adapterId);
        await _storage.StoreMetadataAsync(metadata);

        // Act - concurrent reads and updates
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(_storage.GetMetadataAsync(adapterId));
            tasks.Add(_storage.UpdateMetadataAsync(metadata.WithTraining(1)));
        }

        // Assert - no exceptions
        var action = async () => await Task.WhenAll(tasks);
        await action.Should().NotThrowAsync();
    }

    #endregion

    #region Full Lifecycle Tests

    [Fact]
    public async Task FullLifecycle_CRUD_WorksCorrectly()
    {
        // Create
        var adapterId = AdapterId.NewId();
        var metadata = CreateTestMetadata(adapterId, taskName: "lifecycle-test");
        var createResult = await _storage.StoreMetadataAsync(metadata);
        createResult.IsSuccess.Should().BeTrue();

        // Read
        var readResult = await _storage.GetMetadataAsync(adapterId);
        readResult.IsSuccess.Should().BeTrue();
        readResult.Value.TaskName.Should().Be("lifecycle-test");

        // Update
        var updated = readResult.Value.WithTraining(25, 0.92);
        var updateResult = await _storage.UpdateMetadataAsync(updated);
        updateResult.IsSuccess.Should().BeTrue();

        // Verify update
        var verifyResult = await _storage.GetMetadataAsync(adapterId);
        verifyResult.Value.PerformanceScore.Should().Be(0.92);

        // Delete
        var deleteResult = await _storage.DeleteMetadataAsync(adapterId);
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify deleted
        var finalResult = await _storage.GetMetadataAsync(adapterId);
        finalResult.IsFailure.Should().BeTrue();
    }

    #endregion
}
