// <copyright file="DistinctionConsolidationServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Tests;

using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Application.Services;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the DistinctionConsolidationService.
/// Validates periodic consolidation, dissolution, and cleanup operations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DistinctionConsolidationServiceTests : IDisposable
{
    private readonly Mock<IDistinctionLearner> _mockLearner;
    private readonly Mock<IDistinctionWeightStorage> _mockStorage;
    private readonly Mock<ILogger<DistinctionConsolidationService>> _mockLogger;
    private readonly DistinctionStorageConfig _config;
    private readonly CancellationTokenSource _cts;

    public DistinctionConsolidationServiceTests()
    {
        _mockLearner = new Mock<IDistinctionLearner>();
        _mockStorage = new Mock<IDistinctionWeightStorage>();
        _mockLogger = new Mock<ILogger<DistinctionConsolidationService>>();
        _config = new DistinctionStorageConfig(
            StoragePath: Path.Combine(Path.GetTempPath(), "test-distinctions"),
            MaxTotalStorageBytes: 1024,
            DissolvedRetentionPeriod: TimeSpan.FromDays(7));
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_RunsConsolidationPeriodically()
    {
        // Arrange
        var consolidationInterval = TimeSpan.FromMilliseconds(100);
        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            consolidationInterval);

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(
                new List<DistinctionWeightMetadata>()));

        var cts = new CancellationTokenSource();

        // Act
        var serviceTask = service.StartAsync(cts.Token);
        await Task.Delay(250); // Allow time for at least 2 cycles
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockStorage.Verify(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()),
            Times.AtLeast(2), "should run consolidation at least twice");
    }

    [Fact]
    public async Task ExecuteAsync_StopsWhenCancellationRequested()
    {
        // Arrange
        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(50));

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(
                new List<DistinctionWeightMetadata>()));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(10); // Brief delay
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - service should complete without hanging
        cts.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region Consolidation Tests

    [Fact]
    public async Task Consolidation_DissolvesLowFitnessDistinctions()
    {
        // Arrange
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.2, "Distinction", DateTime.UtcNow, false, 100),
            new("id2", "path2.weights", 0.8, "Recognition", DateTime.UtcNow, false, 100),
            new("id3", "path3.weights", 0.1, "Void", DateTime.UtcNow, false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));
        _mockStorage.Setup(s => s.GetTotalStorageSizeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long, string>.Success(300L));

        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(100));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150); // Wait for one cycle
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path1.weights", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce, "should dissolve low fitness (0.2)");
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path3.weights", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce, "should dissolve low fitness (0.1)");
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path2.weights", It.IsAny<CancellationToken>()),
            Times.Never, "should not dissolve high fitness (0.8)");
    }

    [Fact]
    public async Task Consolidation_CleansUpOldDissolvedDistinctions()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-distinctions-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var oldDissolvedPath = Path.Combine(tempDir, "old.weights.dissolved");
        File.WriteAllText(oldDissolvedPath, "test");

        try
        {
            var config = new DistinctionStorageConfig(
                StoragePath: tempDir,
                MaxTotalStorageBytes: 1024,
                DissolvedRetentionPeriod: TimeSpan.FromDays(7));

            var metadata = new List<DistinctionWeightMetadata>
            {
                new("id1", oldDissolvedPath, 0.2, "Distinction", DateTime.UtcNow.AddDays(-10), true, 100),
                new("id2", "path2.weights", 0.8, "Recognition", DateTime.UtcNow, false, 100)
            };

            _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
            _mockStorage.Setup(s => s.GetTotalStorageSizeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<long, string>.Success(200L));

            var service = new DistinctionConsolidationService(
                _mockLearner.Object,
                _mockStorage.Object,
                config,
                _mockLogger.Object,
                TimeSpan.FromMilliseconds(100));

            var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(150);
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert
            File.Exists(oldDissolvedPath).Should().BeFalse("old dissolved file should be deleted");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task Consolidation_SkipsRecentlyDissolvedDistinctions()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-distinctions-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var recentDissolvedPath = Path.Combine(tempDir, "recent.weights.dissolved");
        File.WriteAllText(recentDissolvedPath, "test");

        try
        {
            var config = new DistinctionStorageConfig(
                StoragePath: tempDir,
                MaxTotalStorageBytes: 1024,
                DissolvedRetentionPeriod: TimeSpan.FromDays(7));

            var metadata = new List<DistinctionWeightMetadata>
            {
                new("id1", recentDissolvedPath, 0.2, "Distinction", DateTime.UtcNow.AddDays(-3), true, 100)
            };

            _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
            _mockStorage.Setup(s => s.GetTotalStorageSizeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<long, string>.Success(100L));

            var service = new DistinctionConsolidationService(
                _mockLearner.Object,
                _mockStorage.Object,
                config,
                _mockLogger.Object,
                TimeSpan.FromMilliseconds(100));

            var cts = new CancellationTokenSource();

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(150);
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert
            File.Exists(recentDissolvedPath).Should().BeTrue(
                "recently dissolved file should not be deleted");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task Consolidation_EnforcesStorageLimits()
    {
        // Arrange
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.3, "Distinction", DateTime.UtcNow.AddDays(-5), false, 100),
            new("id2", "path2.weights", 0.8, "Recognition", DateTime.UtcNow, false, 100),
            new("id3", "path3.weights", 0.5, "Void", DateTime.UtcNow.AddDays(-2), false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        // Storage exceeds limit
        _mockStorage.Setup(s => s.GetTotalStorageSizeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long, string>.Success(2000L)); // Exceeds 1024 limit

        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(100));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockStorage.Verify(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(1), "should dissolve distinctions to free space");
    }

    [Fact]
    public async Task Consolidation_WhenStorageLimitExceeded_DissolvesLowestFitnessFirst()
    {
        // Arrange
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.4, "Distinction", DateTime.UtcNow.AddDays(-5), false, 100),
            new("id2", "path2.weights", 0.9, "Recognition", DateTime.UtcNow, false, 100),
            new("id3", "path3.weights", 0.3, "Void", DateTime.UtcNow.AddDays(-2), false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));
        _mockStorage.Setup(s => s.GetTotalStorageSizeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long, string>.Success(2000L));

        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(100));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path3.weights", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce, "should dissolve lowest fitness (0.3)");
    }

    [Fact]
    public async Task Consolidation_HandlesStorageListFailureGracefully()
    {
        // Arrange
        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Failure("Storage error"));

        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(100));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - should not throw, service should continue
        _mockStorage.Verify(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Consolidation_HandlesDissolveFailureGracefully()
    {
        // Arrange
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights", 0.2, "Distinction", DateTime.UtcNow, false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Failure("Dissolve failed")));
        _mockStorage.Setup(s => s.GetTotalStorageSizeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long, string>.Success(100L));

        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(100));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - should continue despite failure
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path1.weights", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Consolidation_SkipsAlreadyDissolvedDistinctions()
    {
        // Arrange
        var metadata = new List<DistinctionWeightMetadata>
        {
            new("id1", "path1.weights.dissolved", 0.2, "Distinction", DateTime.UtcNow, true, 100),
            new("id2", "path2.weights", 0.1, "Void", DateTime.UtcNow, false, 100)
        };

        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<DistinctionWeightMetadata>, string>.Success(metadata));
        _mockStorage.Setup(s => s.DissolveWeightsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));
        _mockStorage.Setup(s => s.GetTotalStorageSizeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<long, string>.Success(200L));

        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(100));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path1.weights.dissolved", It.IsAny<CancellationToken>()),
            Times.Never, "should not dissolve already dissolved distinctions");
        _mockStorage.Verify(s => s.DissolveWeightsAsync("path2.weights", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Consolidation_RecoversFromExceptionAndContinues()
    {
        // Arrange
        var callCount = 0;
        _mockStorage.Setup(s => s.ListWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Transient error");
                }

                return Result<List<DistinctionWeightMetadata>, string>.Success(
                    new List<DistinctionWeightMetadata>());
            });

        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(50));

        var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(250); // Allow multiple cycles
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert
        callCount.Should().BeGreaterThan(1, "service should recover and continue after exception");
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLearner_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DistinctionConsolidationService(
                null!,
                _mockStorage.Object,
                _config,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullStorage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DistinctionConsolidationService(
                _mockLearner.Object,
                null!,
                _config,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DistinctionConsolidationService(
                _mockLearner.Object,
                _mockStorage.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DistinctionConsolidationService(
                _mockLearner.Object,
                _mockStorage.Object,
                _config,
                null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        var service = new DistinctionConsolidationService(
            _mockLearner.Object,
            _mockStorage.Object,
            _config,
            _mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion
}
