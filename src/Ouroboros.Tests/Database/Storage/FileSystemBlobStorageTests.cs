// <copyright file="FileSystemBlobStorageTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.Storage;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;
using Xunit;

/// <summary>
/// Unit tests for FileSystemBlobStorage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class FileSystemBlobStorageTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSystemBlobStorage _storage;

    public FileSystemBlobStorageTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ouroboros_blob_tests", Guid.NewGuid().ToString());
        _storage = new FileSystemBlobStorage(_testDirectory, NullLogger<FileSystemBlobStorage>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDirectory_CreatesDirectory()
    {
        // Assert
        Directory.Exists(_testDirectory).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidDirectory_ThrowsArgumentException(string? invalidDir)
    {
        // Act & Assert
        var action = () => new FileSystemBlobStorage(invalidDir!, null);
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region StoreWeightsAsync Tests

    [Fact]
    public async Task StoreWeightsAsync_WithValidWeights_ReturnsSuccessWithPath()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var weights = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var result = await _storage.StoreWeightsAsync(adapterId, weights);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(adapterId.ToString());
        result.Value.Should().EndWith(".bin");
        File.Exists(result.Value).Should().BeTrue();
    }

    [Fact]
    public async Task StoreWeightsAsync_WithLargeWeights_StoresSuccessfully()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var weights = new byte[1024 * 1024]; // 1MB
        new Random(42).NextBytes(weights);

        // Act
        var result = await _storage.StoreWeightsAsync(adapterId, weights);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var storedSize = new FileInfo(result.Value).Length;
        storedSize.Should().Be(weights.Length);
    }

    [Fact]
    public async Task StoreWeightsAsync_WithEmptyWeights_ReturnsFailure()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var emptyWeights = Array.Empty<byte>();

        // Act
        var result = await _storage.StoreWeightsAsync(adapterId, emptyWeights);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task StoreWeightsAsync_WithNullWeights_ReturnsFailure()
    {
        // Arrange
        var adapterId = AdapterId.NewId();

        // Act
        var result = await _storage.StoreWeightsAsync(adapterId, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task StoreWeightsAsync_OverwritesExisting_UpdatesFile()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var originalWeights = new byte[] { 0x01, 0x02, 0x03 };
        var updatedWeights = new byte[] { 0x04, 0x05, 0x06, 0x07, 0x08 };

        // Act
        await _storage.StoreWeightsAsync(adapterId, originalWeights);
        var result = await _storage.StoreWeightsAsync(adapterId, updatedWeights);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var storedWeights = await File.ReadAllBytesAsync(result.Value);
        storedWeights.Should().BeEquivalentTo(updatedWeights);
    }

    [Fact]
    public async Task StoreWeightsAsync_WithCancellation_ReturnsFailure()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var weights = new byte[1024 * 1024]; // 1MB
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _storage.StoreWeightsAsync(adapterId, weights, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    #endregion

    #region GetWeightsAsync Tests

    [Fact]
    public async Task GetWeightsAsync_WithExistingPath_ReturnsWeights()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var originalWeights = new byte[] { 0xAB, 0xCD, 0xEF };
        var storeResult = await _storage.StoreWeightsAsync(adapterId, originalWeights);

        // Act
        var result = await _storage.GetWeightsAsync(storeResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(originalWeights);
    }

    [Fact]
    public async Task GetWeightsAsync_WithNonExistentPath_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.bin");

        // Act
        var result = await _storage.GetWeightsAsync(nonExistentPath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetWeightsAsync_WithInvalidPath_ReturnsFailure(string? invalidPath)
    {
        // Act
        var result = await _storage.GetWeightsAsync(invalidPath!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task GetWeightsAsync_RoundTrip_PreservesData()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var originalWeights = new byte[256];
        new Random(123).NextBytes(originalWeights);
        var storeResult = await _storage.StoreWeightsAsync(adapterId, originalWeights);

        // Act
        var result = await _storage.GetWeightsAsync(storeResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(originalWeights);
    }

    #endregion

    #region DeleteWeightsAsync Tests

    [Fact]
    public async Task DeleteWeightsAsync_WithExistingFile_DeletesSuccessfully()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var weights = new byte[] { 0x01, 0x02 };
        var storeResult = await _storage.StoreWeightsAsync(adapterId, weights);
        var filePath = storeResult.Value;

        // Act
        var result = await _storage.DeleteWeightsAsync(filePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteWeightsAsync_WithNonExistentFile_ReturnsSuccess()
    {
        // Arrange - idempotent delete
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.bin");

        // Act
        var result = await _storage.DeleteWeightsAsync(nonExistentPath);

        // Assert
        result.IsSuccess.Should().BeTrue("delete should be idempotent");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteWeightsAsync_WithInvalidPath_ReturnsFailure(string? invalidPath)
    {
        // Act
        var result = await _storage.DeleteWeightsAsync(invalidPath!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region GetWeightsSizeAsync Tests

    [Fact]
    public async Task GetWeightsSizeAsync_WithExistingFile_ReturnsCorrectSize()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var weights = new byte[512];
        new Random().NextBytes(weights);
        var storeResult = await _storage.StoreWeightsAsync(adapterId, weights);

        // Act
        var result = await _storage.GetWeightsSizeAsync(storeResult.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(512);
    }

    [Fact]
    public async Task GetWeightsSizeAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.bin");

        // Act
        var result = await _storage.GetWeightsSizeAsync(nonExistentPath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetWeightsSizeAsync_WithInvalidPath_ReturnsFailure(string? invalidPath)
    {
        // Act
        var result = await _storage.GetWeightsSizeAsync(invalidPath!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullLifecycle_StoreGetDeleteSize_WorksCorrectly()
    {
        // Arrange
        var adapterId = AdapterId.NewId();
        var weights = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55 };

        // Store
        var storeResult = await _storage.StoreWeightsAsync(adapterId, weights);
        storeResult.IsSuccess.Should().BeTrue();

        // Get
        var getResult = await _storage.GetWeightsAsync(storeResult.Value);
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.Should().BeEquivalentTo(weights);

        // Size
        var sizeResult = await _storage.GetWeightsSizeAsync(storeResult.Value);
        sizeResult.IsSuccess.Should().BeTrue();
        sizeResult.Value.Should().Be(5);

        // Delete
        var deleteResult = await _storage.DeleteWeightsAsync(storeResult.Value);
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify deleted
        var verifyResult = await _storage.GetWeightsAsync(storeResult.Value);
        verifyResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleAdapters_StoredIndependently()
    {
        // Arrange
        var adapter1 = AdapterId.NewId();
        var adapter2 = AdapterId.NewId();
        var weights1 = new byte[] { 0x01 };
        var weights2 = new byte[] { 0x02 };

        // Act
        var store1 = await _storage.StoreWeightsAsync(adapter1, weights1);
        var store2 = await _storage.StoreWeightsAsync(adapter2, weights2);

        // Assert
        store1.Value.Should().NotBe(store2.Value);

        var get1 = await _storage.GetWeightsAsync(store1.Value);
        var get2 = await _storage.GetWeightsAsync(store2.Value);

        get1.Value.Should().BeEquivalentTo(weights1);
        get2.Value.Should().BeEquivalentTo(weights2);
    }

    #endregion
}
