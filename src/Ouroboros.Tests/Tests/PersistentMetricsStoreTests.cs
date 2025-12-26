// ==========================================================
// Persistent Metrics Store Tests
// Tests for the metrics persistence layer
// ==========================================================

using FluentAssertions;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests;

public sealed class PersistentMetricsStoreTests : IDisposable
{
    private readonly string _testStoragePath;

    public PersistentMetricsStoreTests()
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), $"metrics_test_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testStoragePath))
        {
            Directory.Delete(_testStoragePath, recursive: true);
        }
    }

    [Fact]
    public async Task StoreMetricsAsync_WithValidMetrics_ShouldPersist()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        var metrics = new PerformanceMetrics(
            ResourceName: "test-model",
            ExecutionCount: 10,
            AverageLatencyMs: 150.5,
            SuccessRate: 0.95,
            LastUsed: DateTime.UtcNow,
            CustomMetrics: new Dictionary<string, double> { ["accuracy"] = 0.92 });

        // Act
        await store.StoreMetricsAsync(metrics);
        await store.SaveMetricsAsync();

        // Assert
        var retrieved = await store.GetMetricsAsync("test-model");
        retrieved.Should().NotBeNull();
        retrieved!.ExecutionCount.Should().Be(10);
        retrieved.AverageLatencyMs.Should().Be(150.5);
        retrieved.SuccessRate.Should().Be(0.95);
    }

    [Fact]
    public async Task GetAllMetricsAsync_WithMultipleMetrics_ShouldReturnAll()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        var metrics1 = new PerformanceMetrics("model-1", 5, 100, 0.9, DateTime.UtcNow, new());
        var metrics2 = new PerformanceMetrics("model-2", 10, 200, 0.8, DateTime.UtcNow, new());
        var metrics3 = new PerformanceMetrics("model-3", 15, 300, 0.7, DateTime.UtcNow, new());

        await store.StoreMetricsAsync(metrics1);
        await store.StoreMetricsAsync(metrics2);
        await store.StoreMetricsAsync(metrics3);

        // Act
        var allMetrics = await store.GetAllMetricsAsync();

        // Assert
        allMetrics.Should().HaveCount(3);
        allMetrics.Should().ContainKey("model-1");
        allMetrics.Should().ContainKey("model-2");
        allMetrics.Should().ContainKey("model-3");
    }

    [Fact]
    public async Task RemoveMetricsAsync_WithExistingMetrics_ShouldRemove()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        var metrics = new PerformanceMetrics("to-remove", 1, 100, 1.0, DateTime.UtcNow, new());
        await store.StoreMetricsAsync(metrics);

        // Act
        bool removed = await store.RemoveMetricsAsync("to-remove");

        // Assert
        removed.Should().BeTrue();
        var retrieved = await store.GetMetricsAsync("to-remove");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task RemoveMetricsAsync_WithNonExistent_ShouldReturnFalse()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        // Act
        bool removed = await store.RemoveMetricsAsync("non-existent");

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllMetrics()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        await store.StoreMetricsAsync(new PerformanceMetrics("m1", 1, 100, 1.0, DateTime.UtcNow, new()));
        await store.StoreMetricsAsync(new PerformanceMetrics("m2", 2, 200, 0.9, DateTime.UtcNow, new()));

        // Act
        await store.ClearAsync();

        // Assert
        var allMetrics = await store.GetAllMetricsAsync();
        allMetrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatisticsAsync_WithMetrics_ShouldReturnCorrectStats()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        await store.StoreMetricsAsync(new PerformanceMetrics("m1", 10, 100, 0.9, DateTime.UtcNow, new()));
        await store.StoreMetricsAsync(new PerformanceMetrics("m2", 20, 200, 0.8, DateTime.UtcNow, new()));

        // Act
        var stats = await store.GetStatisticsAsync();

        // Assert
        stats.TotalResources.Should().Be(2);
        stats.TotalExecutions.Should().Be(30);
        stats.OverallSuccessRate.Should().BeApproximately(0.85, 0.001);
        stats.AverageLatencyMs.Should().BeApproximately(150, 0.001);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithNoMetrics_ShouldReturnEmptyStats()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        // Act
        var stats = await store.GetStatisticsAsync();

        // Assert
        stats.TotalResources.Should().Be(0);
        stats.TotalExecutions.Should().Be(0);
        stats.OldestMetric.Should().BeNull();
        stats.NewestMetric.Should().BeNull();
    }

    [Fact]
    public async Task Persistence_MetricsShouldSurviveRestart()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        var metrics = new PerformanceMetrics(
            ResourceName: "persistent-model",
            ExecutionCount: 42,
            AverageLatencyMs: 123.45,
            SuccessRate: 0.87,
            LastUsed: DateTime.UtcNow,
            CustomMetrics: new Dictionary<string, double>());

        // Store and save with first instance
        using (var store1 = new PersistentMetricsStore(config))
        {
            await store1.StoreMetricsAsync(metrics);
            await store1.SaveMetricsAsync();
        }

        // Act - Create new instance (simulating restart)
        using var store2 = new PersistentMetricsStore(config);

        // Assert
        var retrieved = await store2.GetMetricsAsync("persistent-model");
        retrieved.Should().NotBeNull();
        retrieved!.ExecutionCount.Should().Be(42);
        retrieved.AverageLatencyMs.Should().Be(123.45);
        retrieved.SuccessRate.Should().Be(0.87);
    }

    [Fact]
    public async Task OldMetrics_ShouldBeRemovedOnLoad()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false,
            MaxMetricsAge: 30); // 30 days max

        var oldMetrics = new PerformanceMetrics(
            ResourceName: "old-model",
            ExecutionCount: 10,
            AverageLatencyMs: 100,
            SuccessRate: 0.9,
            LastUsed: DateTime.UtcNow.AddDays(-60), // 60 days old
            CustomMetrics: new());

        var recentMetrics = new PerformanceMetrics(
            ResourceName: "recent-model",
            ExecutionCount: 5,
            AverageLatencyMs: 50,
            SuccessRate: 0.95,
            LastUsed: DateTime.UtcNow.AddDays(-10), // 10 days old
            CustomMetrics: new());

        // Store both with first instance
        using (var store1 = new PersistentMetricsStore(config))
        {
            await store1.StoreMetricsAsync(oldMetrics);
            await store1.StoreMetricsAsync(recentMetrics);
            await store1.SaveMetricsAsync();
        }

        // Act - Create new instance (triggers cleanup)
        using var store2 = new PersistentMetricsStore(config);
        var allMetrics = await store2.GetAllMetricsAsync();

        // Assert
        allMetrics.Should().HaveCount(1);
        allMetrics.Should().ContainKey("recent-model");
        allMetrics.Should().NotContainKey("old-model");
    }

    [Fact]
    public void Constructor_ShouldCreateStorageDirectory()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        // Act
        using var store = new PersistentMetricsStore(config);

        // Assert
        Directory.Exists(_testStoragePath).Should().BeTrue();
    }

    [Fact]
    public async Task StoreMetricsAsync_WithNullMetrics_ShouldThrow()
    {
        // Arrange
        var config = new PersistentMetricsConfig(
            StoragePath: _testStoragePath,
            AutoSave: false);

        using var store = new PersistentMetricsStore(config);

        // Act & Assert
        Func<Task> act = async () => await store.StoreMetricsAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

public sealed class InMemoryMetricsStoreTests
{
    [Fact]
    public async Task StoreAndRetrieve_ShouldWork()
    {
        // Arrange
        var store = new InMemoryMetricsStore();
        var metrics = new PerformanceMetrics("test", 1, 100, 1.0, DateTime.UtcNow, new());

        // Act
        await store.StoreMetricsAsync(metrics);
        var retrieved = await store.GetMetricsAsync("test");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ResourceName.Should().Be("test");
    }

    [Fact]
    public async Task Clear_ShouldRemoveAll()
    {
        // Arrange
        var store = new InMemoryMetricsStore();
        await store.StoreMetricsAsync(new PerformanceMetrics("m1", 1, 100, 1.0, DateTime.UtcNow, new()));
        await store.StoreMetricsAsync(new PerformanceMetrics("m2", 2, 200, 0.9, DateTime.UtcNow, new()));

        // Act
        await store.ClearAsync();

        // Assert
        var all = await store.GetAllMetricsAsync();
        all.Should().BeEmpty();
    }
}

public sealed class SmartModelOrchestratorPersistenceTests
{
    [Fact]
    public async Task OrchestratorWithMetricsStore_ShouldPersistMetrics()
    {
        // Arrange
        var metricsStore = new InMemoryMetricsStore();
        var tools = new ToolRegistry();

        using var orchestrator = new SmartModelOrchestrator(tools, metricsStore);

        // Act
        await orchestrator.RecordMetricAsync("test-model", 100.0, true);

        // Assert
        var storedMetrics = await metricsStore.GetMetricsAsync("test-model");
        storedMetrics.Should().NotBeNull();
        storedMetrics!.ExecutionCount.Should().Be(1);
        storedMetrics.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task OrchestratorWithMetricsStore_ShouldLoadExistingMetrics()
    {
        // Arrange
        var metricsStore = new InMemoryMetricsStore();

        // Pre-populate store
        await metricsStore.StoreMetricsAsync(new PerformanceMetrics(
            "preloaded-model", 100, 50.0, 0.95, DateTime.UtcNow, new()));

        var tools = new ToolRegistry();

        // Act
        using var orchestrator = new SmartModelOrchestrator(tools, metricsStore);
        var metrics = orchestrator.GetMetrics();

        // Assert
        metrics.Should().ContainKey("preloaded-model");
        metrics["preloaded-model"].ExecutionCount.Should().Be(100);
    }

    [Fact]
    public void OrchestratorWithoutMetricsStore_ShouldWorkWithInMemoryOnly()
    {
        // Arrange
        var tools = new ToolRegistry();

        // Act
        using var orchestrator = new SmartModelOrchestrator(tools);
        orchestrator.RecordMetric("test", 100, true);

        // Assert
        var metrics = orchestrator.GetMetrics();
        metrics.Should().ContainKey("test");
    }

    [Fact]
    public async Task GetMetricsStoreStatisticsAsync_WithStore_ShouldReturnStats()
    {
        // Arrange
        var metricsStore = new InMemoryMetricsStore();
        await metricsStore.StoreMetricsAsync(new PerformanceMetrics(
            "model", 10, 100, 0.9, DateTime.UtcNow, new()));

        var tools = new ToolRegistry();
        using var orchestrator = new SmartModelOrchestrator(tools, metricsStore);

        // Act
        var stats = await orchestrator.GetMetricsStoreStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats!.TotalResources.Should().Be(1);
        stats.TotalExecutions.Should().Be(10);
    }

    [Fact]
    public async Task GetMetricsStoreStatisticsAsync_WithoutStore_ShouldReturnNull()
    {
        // Arrange
        var tools = new ToolRegistry();
        using var orchestrator = new SmartModelOrchestrator(tools);

        // Act
        var stats = await orchestrator.GetMetricsStoreStatisticsAsync();

        // Assert
        stats.Should().BeNull();
    }
}
