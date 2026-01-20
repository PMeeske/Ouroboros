// ==========================================================
// Orchestration Cache Tests
// Tests for request caching layer
// ==========================================================

using FluentAssertions;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Tools;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for the InMemoryOrchestrationCache class.
/// </summary>
[Trait("Category", "Unit")]
public class OrchestrationCacheTests : IDisposable
{
    private readonly InMemoryOrchestrationCache _cache;

    public OrchestrationCacheTests()
    {
        _cache = new InMemoryOrchestrationCache(maxEntries: 100, cleanupIntervalSeconds: 60);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public async Task GetCachedDecisionAsync_WithEmptyHash_ShouldReturnNone()
    {
        // Act
        var result = await _cache.GetCachedDecisionAsync("");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task GetCachedDecisionAsync_WithNonExistentKey_ShouldReturnNone()
    {
        // Act
        var result = await _cache.GetCachedDecisionAsync("nonexistent");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task CacheDecisionAsync_ThenGet_ShouldReturnCachedValue()
    {
        // Arrange
        var decision = CreateTestDecision("model1");
        var hash = "test-hash-1";

        // Act
        await _cache.CacheDecisionAsync(hash, decision, TimeSpan.FromMinutes(5));
        var result = await _cache.GetCachedDecisionAsync(hash);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.ModelName.Should().Be("model1");
    }

    [Fact]
    public async Task CacheDecisionAsync_WithExpiredTtl_ShouldNotReturnValue()
    {
        // Arrange
        var decision = CreateTestDecision("model1");
        var hash = "test-hash-expired";

        // Act
        await _cache.CacheDecisionAsync(hash, decision, TimeSpan.FromMilliseconds(1));
        await Task.Delay(10); // Wait for expiry
        var result = await _cache.GetCachedDecisionAsync(hash);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateAsync_ShouldRemoveEntry()
    {
        // Arrange
        var decision = CreateTestDecision("model1");
        var hash = "test-hash-invalidate";
        await _cache.CacheDecisionAsync(hash, decision, TimeSpan.FromMinutes(5));

        // Act
        await _cache.InvalidateAsync(hash);
        var result = await _cache.GetCachedDecisionAsync(hash);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllEntries()
    {
        // Arrange
        await _cache.CacheDecisionAsync("hash1", CreateTestDecision("model1"), TimeSpan.FromMinutes(5));
        await _cache.CacheDecisionAsync("hash2", CreateTestDecision("model2"), TimeSpan.FromMinutes(5));

        // Act
        await _cache.ClearAsync();
        var result1 = await _cache.GetCachedDecisionAsync("hash1");
        var result2 = await _cache.GetCachedDecisionAsync("hash2");

        // Assert
        result1.HasValue.Should().BeFalse();
        result2.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatistics_ShouldTrackHitsAndMisses()
    {
        // Arrange
        await _cache.CacheDecisionAsync("hash1", CreateTestDecision("model1"), TimeSpan.FromMinutes(5));

        // Act
        await _cache.GetCachedDecisionAsync("hash1"); // Hit
        await _cache.GetCachedDecisionAsync("hash1"); // Hit
        await _cache.GetCachedDecisionAsync("nonexistent"); // Miss

        var stats = _cache.GetStatistics();

        // Assert
        stats.HitCount.Should().BeGreaterThanOrEqualTo(2);
        stats.MissCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetStatistics_ShouldReportEntryCount()
    {
        // Arrange
        _cache.CacheDecisionAsync("hash1", CreateTestDecision("model1"), TimeSpan.FromMinutes(5)).Wait();
        _cache.CacheDecisionAsync("hash2", CreateTestDecision("model2"), TimeSpan.FromMinutes(5)).Wait();

        // Act
        var stats = _cache.GetStatistics();

        // Assert
        stats.TotalEntries.Should().Be(2);
        stats.MaxEntries.Should().Be(100);
    }

    [Fact]
    public void GeneratePromptHash_WithSamePrompt_ShouldReturnSameHash()
    {
        // Act
        var hash1 = InMemoryOrchestrationCache.GeneratePromptHash("test prompt");
        var hash2 = InMemoryOrchestrationCache.GeneratePromptHash("test prompt");

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GeneratePromptHash_WithDifferentPrompts_ShouldReturnDifferentHashes()
    {
        // Act
        var hash1 = InMemoryOrchestrationCache.GeneratePromptHash("prompt 1");
        var hash2 = InMemoryOrchestrationCache.GeneratePromptHash("prompt 2");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GeneratePromptHash_WithContext_ShouldIncludeContextInHash()
    {
        // Arrange
        var context = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var hashWithoutContext = InMemoryOrchestrationCache.GeneratePromptHash("prompt");
        var hashWithContext = InMemoryOrchestrationCache.GeneratePromptHash("prompt", context);

        // Assert
        hashWithoutContext.Should().NotBe(hashWithContext);
    }

    [Fact]
    public void CacheStatistics_UtilizationPercent_ShouldBeCalculated()
    {
        // Arrange
        var stats = new CacheStatistics(
            TotalEntries: 50,
            MaxEntries: 100,
            HitCount: 0,
            MissCount: 0,
            HitRate: 0,
            MemoryEstimateBytes: 0);

        // Assert
        stats.UtilizationPercent.Should().Be(50);
    }

    [Fact]
    public void CacheStatistics_IsHealthy_WithHighHitRate_ShouldBeTrue()
    {
        // Arrange
        var stats = new CacheStatistics(
            TotalEntries: 50,
            MaxEntries: 100,
            HitCount: 80,
            MissCount: 20,
            HitRate: 0.8,
            MemoryEstimateBytes: 0);

        // Assert
        stats.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void CacheStatistics_IsHealthy_WhenWarmingUp_ShouldBeTrue()
    {
        // Arrange (low total requests = warming up)
        var stats = new CacheStatistics(
            TotalEntries: 10,
            MaxEntries: 100,
            HitCount: 5,
            MissCount: 45,
            HitRate: 0.1,
            MemoryEstimateBytes: 0);

        // Assert
        stats.IsHealthy.Should().BeTrue();
    }

    private static OrchestratorDecision CreateTestDecision(string modelName)
    {
        return new OrchestratorDecision(
            SelectedModel: null!,
            ModelName: modelName,
            Reason: "Test decision",
            RecommendedTools: new ToolRegistry(),
            ConfidenceScore: 0.9);
    }
}

/// <summary>
/// Tests for the CachingModelOrchestrator decorator.
/// </summary>
[Trait("Category", "Unit")]
public class CachingModelOrchestratorTests : IDisposable
{
    private readonly InMemoryOrchestrationCache _cache;
    private readonly MockOrchestrator _innerOrchestrator;
    private readonly CachingModelOrchestrator _cachingOrchestrator;

    public CachingModelOrchestratorTests()
    {
        _cache = new InMemoryOrchestrationCache(maxEntries: 100);
        _innerOrchestrator = new MockOrchestrator();
        _cachingOrchestrator = new CachingModelOrchestrator(
            _innerOrchestrator, _cache, TimeSpan.FromMinutes(5));
    }

    public void Dispose()
    {
        _cache.Dispose();
    }

    [Fact]
    public async Task SelectModelAsync_FirstCall_ShouldCallInnerOrchestrator()
    {
        // Act
        await _cachingOrchestrator.SelectModelAsync("test prompt");

        // Assert
        _innerOrchestrator.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task SelectModelAsync_SecondCall_ShouldUseCache()
    {
        // Act
        await _cachingOrchestrator.SelectModelAsync("test prompt");
        await _cachingOrchestrator.SelectModelAsync("test prompt");

        // Assert
        _innerOrchestrator.CallCount.Should().Be(1, "second call should use cache");
    }

    [Fact]
    public async Task SelectModelAsync_DifferentPrompts_ShouldCallInnerForEach()
    {
        // Act
        await _cachingOrchestrator.SelectModelAsync("prompt 1");
        await _cachingOrchestrator.SelectModelAsync("prompt 2");

        // Assert
        _innerOrchestrator.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCacheStatistics_ShouldReflectUsage()
    {
        // Act
        await _cachingOrchestrator.SelectModelAsync("prompt 1");
        await _cachingOrchestrator.SelectModelAsync("prompt 1"); // Cache hit

        var stats = _cachingOrchestrator.GetCacheStatistics();

        // Assert
        stats.HitCount.Should().Be(1);
        stats.TotalEntries.Should().Be(1);
    }

    [Fact]
    public async Task ClearCacheAsync_ShouldInvalidateAllEntries()
    {
        // Arrange
        await _cachingOrchestrator.SelectModelAsync("prompt 1");
        await _cachingOrchestrator.SelectModelAsync("prompt 2");

        // Act
        await _cachingOrchestrator.ClearCacheAsync();

        // Call again - should not use cache
        await _cachingOrchestrator.SelectModelAsync("prompt 1");
        await _cachingOrchestrator.SelectModelAsync("prompt 2");

        // Assert
        _innerOrchestrator.CallCount.Should().Be(4, "all prompts should be re-fetched after clear");
    }

    [Fact]
    public void ClassifyUseCase_ShouldDelegateToInner()
    {
        // Act
        var result = _cachingOrchestrator.ClassifyUseCase("test");

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(UseCaseType.Reasoning);
    }

    [Fact]
    public void GetMetrics_ShouldDelegateToInner()
    {
        // Act
        var metrics = _cachingOrchestrator.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
    }

    /// <summary>
    /// Simple mock orchestrator for testing.
    /// </summary>
    private sealed class MockOrchestrator : IModelOrchestrator
    {
        public int CallCount { get; private set; }

        public Task<Result<OrchestratorDecision, string>> SelectModelAsync(
            string prompt,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            CallCount++;

            var decision = new OrchestratorDecision(
                SelectedModel: null!,
                ModelName: "mock-model",
                Reason: "Mock selection",
                RecommendedTools: new ToolRegistry(),
                ConfidenceScore: 0.9);

            return Task.FromResult(Result<OrchestratorDecision, string>.Success(decision));
        }

        public UseCase ClassifyUseCase(string prompt)
        {
            return new UseCase(UseCaseType.Reasoning, 5, Array.Empty<string>(), 0.5, 0.5);
        }

        public void RegisterModel(ModelCapability capability) { }

        public void RecordMetric(string resourceName, double latencyMs, bool success) { }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }
}
