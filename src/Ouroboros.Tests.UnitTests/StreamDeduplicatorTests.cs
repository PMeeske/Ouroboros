// <copyright file="StreamDeduplicatorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using System.Runtime.CompilerServices;
using FluentAssertions;
using Ouroboros.Infrastructure.FeatureEngineering;
using Xunit;

/// <summary>
/// Tests for StreamDeduplicator functionality.
/// </summary>
[Trait("Category", "Unit")]
public class StreamDeduplicatorTests
{
    /// <summary>
    /// Tests that the constructor accepts valid parameters.
    /// </summary>
    [Fact]
    public void Constructor_WithValidParameters_CreatesDeduplicator()
    {
        // Act
        var deduplicator = new StreamDeduplicator(0.9f, 100);

        // Assert
        deduplicator.Should().NotBeNull();
        deduplicator.CacheSize.Should().Be(0);
    }

    /// <summary>
    /// Tests that constructor throws when similarity threshold is out of range.
    /// </summary>
    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(2.0f)]
    public void Constructor_WithInvalidSimilarityThreshold_ThrowsArgumentOutOfRangeException(float threshold)
    {
        // Act
        Action act = () => new StreamDeduplicator(threshold, 100);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("similarityThreshold");
    }

    /// <summary>
    /// Tests that constructor throws when max cache size is invalid.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidMaxCacheSize_ThrowsArgumentOutOfRangeException(int maxSize)
    {
        // Act
        Action act = () => new StreamDeduplicator(0.9f, maxSize);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxCacheSize");
    }

    /// <summary>
    /// Tests that first vector is not considered a duplicate.
    /// </summary>
    [Fact]
    public void IsDuplicate_WithFirstVector_ReturnsFalse()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vector = new float[] { 1f, 0f, 0f, 0f };

        // Act
        var isDuplicate = deduplicator.IsDuplicate(vector);

        // Assert
        isDuplicate.Should().BeFalse();
        deduplicator.CacheSize.Should().Be(1);
    }

    /// <summary>
    /// Tests that identical vectors are detected as duplicates.
    /// </summary>
    [Fact]
    public void IsDuplicate_WithIdenticalVector_ReturnsTrue()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vector = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };

        // Act
        deduplicator.IsDuplicate(vector);
        var isDuplicate = deduplicator.IsDuplicate(vector);

        // Assert
        isDuplicate.Should().BeTrue();
        deduplicator.CacheSize.Should().Be(1);
    }

    /// <summary>
    /// Tests that similar vectors are detected as duplicates based on threshold.
    /// </summary>
    [Fact]
    public void IsDuplicate_WithSimilarVector_ReturnsTrue()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vector1 = new float[] { 1f, 0f, 0f, 0f };
        var vector2 = new float[] { 0.99f, 0.01f, 0f, 0f };

        // Normalize vector2
        var norm = MathF.Sqrt(vector2.Sum(v => v * v));
        for (int i = 0; i < vector2.Length; i++)
        {
            vector2[i] /= norm;
        }

        // Act
        deduplicator.IsDuplicate(vector1);
        var isDuplicate = deduplicator.IsDuplicate(vector2);

        // Assert
        isDuplicate.Should().BeTrue();
    }

    /// <summary>
    /// Tests that different vectors are not detected as duplicates.
    /// </summary>
    [Fact]
    public void IsDuplicate_WithDifferentVector_ReturnsFalse()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vector1 = new float[] { 1f, 0f, 0f, 0f };
        var vector2 = new float[] { 0f, 1f, 0f, 0f };

        // Act
        deduplicator.IsDuplicate(vector1);
        var isDuplicate = deduplicator.IsDuplicate(vector2);

        // Assert
        isDuplicate.Should().BeFalse();
        deduplicator.CacheSize.Should().Be(2);
    }

    /// <summary>
    /// Tests that IsDuplicate throws when vector is null.
    /// </summary>
    [Fact]
    public void IsDuplicate_WithNullVector_ThrowsArgumentNullException()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);

        // Act
        Action act = () => deduplicator.IsDuplicate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that cache evicts least recently used items when full.
    /// </summary>
    [Fact]
    public void IsDuplicate_WhenCacheFull_EvictsLeastRecentlyUsed()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 3);
        var vectors = new[]
        {
            new float[] { 1f, 0f, 0f, 0f },
            new float[] { 0f, 1f, 0f, 0f },
            new float[] { 0f, 0f, 1f, 0f },
            new float[] { 0f, 0f, 0f, 1f },
        };

        // Act
        foreach (var vector in vectors)
        {
            deduplicator.IsDuplicate(vector);
        }

        // Assert
        deduplicator.CacheSize.Should().Be(3);

        // The first vector should have been evicted
        var firstVectorIsDuplicate = deduplicator.IsDuplicate(vectors[0]);
        firstVectorIsDuplicate.Should().BeFalse();
    }

    /// <summary>
    /// Tests that FilterBatch removes duplicates correctly.
    /// </summary>
    [Fact]
    public void FilterBatch_WithDuplicates_RemovesDuplicates()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vectors = new[]
        {
            new float[] { 1f, 0f, 0f, 0f },
            new float[] { 1f, 0f, 0f, 0f }, // Duplicate
            new float[] { 0f, 1f, 0f, 0f },
            new float[] { 0f, 1f, 0f, 0f },  // Duplicate
        };

        // Act
        var filtered = deduplicator.FilterBatch(vectors);

        // Assert
        filtered.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests that FilterBatch with null throws exception.
    /// </summary>
    [Fact]
    public void FilterBatch_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);

        // Act
        Action act = () => deduplicator.FilterBatch(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests that FilterBatch preserves order.
    /// </summary>
    [Fact]
    public void FilterBatch_PreservesOrder()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vectors = new[]
        {
            new float[] { 1f, 0f, 0f, 0f },
            new float[] { 0f, 1f, 0f, 0f },
            new float[] { 0f, 0f, 1f, 0f },
        };

        // Act
        var filtered = deduplicator.FilterBatch(vectors);

        // Assert
        filtered.Should().HaveCount(3);
        filtered[0].Should().Equal(vectors[0]);
        filtered[1].Should().Equal(vectors[1]);
        filtered[2].Should().Equal(vectors[2]);
    }

    /// <summary>
    /// Tests that ClearCache removes all cached vectors.
    /// </summary>
    [Fact]
    public void ClearCache_RemovesAllCachedVectors()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vectors = new[]
        {
            new float[] { 1f, 0f, 0f, 0f },
            new float[] { 0f, 1f, 0f, 0f },
        };

        foreach (var vector in vectors)
        {
            deduplicator.IsDuplicate(vector);
        }

        // Act
        deduplicator.ClearCache();

        // Assert
        deduplicator.CacheSize.Should().Be(0);
    }

    /// <summary>
    /// Tests that GetStatistics returns correct information.
    /// </summary>
    [Fact]
    public void GetStatistics_ReturnsCorrectInformation()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.92f, 50);
        var vector = new float[] { 1f, 0f, 0f, 0f };
        deduplicator.IsDuplicate(vector);

        // Act
        var stats = deduplicator.GetStatistics();

        // Assert
        stats.CacheSize.Should().Be(1);
        stats.MaxCacheSize.Should().Be(50);
        stats.SimilarityThreshold.Should().Be(0.92f);
    }

    /// <summary>
    /// Tests async stream filtering.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task FilterStreamAsync_WithDuplicates_RemovesDuplicates()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vectors = new[]
        {
            new float[] { 1f, 0f, 0f, 0f },
            new float[] { 1f, 0f, 0f, 0f }, // Duplicate
            new float[] { 0f, 1f, 0f, 0f },
            new float[] { 0f, 1f, 0f, 0f },  // Duplicate
        };

        async IAsyncEnumerable<float[]> GetVectorsAsync()
        {
            foreach (var vector in vectors)
            {
                await Task.Delay(10);
                yield return vector;
            }
        }

        // Act
        var filtered = new List<float[]>();
        await foreach (var vector in deduplicator.FilterStreamAsync(GetVectorsAsync()))
        {
            filtered.Add(vector);
        }

        // Assert
        filtered.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests async stream filtering with null throws exception.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task FilterStreamAsync_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);

        // Act
        var act = async () =>
        {
            await foreach (var _ in deduplicator.FilterStreamAsync(null!))
            {
                // Should not reach here
            }
        };

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Tests async stream filtering respects cancellation token.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task FilterStreamAsync_WithCancellation_RespectsCancellationToken()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var cts = new CancellationTokenSource();

        async IAsyncEnumerable<float[]> GetVectorsAsync([EnumeratorCancellation] CancellationToken ct)
        {
            for (int i = 0; i < 100; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(20, ct);
                yield return new float[] { i, 0f, 0f, 0f };
            }
        }

        // Act
        var filtered = new List<float[]>();
        cts.CancelAfter(150);

        var act = async () =>
        {
            await foreach (var vector in deduplicator.FilterStreamAsync(GetVectorsAsync(cts.Token), cts.Token))
            {
                filtered.Add(vector);
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        filtered.Should().HaveCountLessThan(100);
    }

    /// <summary>
    /// Tests extension method Deduplicate for IEnumerable.
    /// </summary>
    [Fact]
    public void DeduplicateExtension_WithEnumerable_RemovesDuplicates()
    {
        // Arrange
        var vectors = new[]
        {
            new float[] { 1f, 0f, 0f, 0f },
            new float[] { 1f, 0f, 0f, 0f }, // Duplicate
            new float[] { 0f, 1f, 0f, 0f },
        };

        // Act
        var filtered = vectors.Deduplicate(0.95f, 100);

        // Assert
        filtered.Should().HaveCount(2);
    }

    /// <summary>
    /// Tests extension method Deduplicate with null throws exception.
    /// </summary>
    [Fact]
    public void DeduplicateExtension_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        IEnumerable<float[]> vectors = null!;

        // Act
        Action act = () => vectors.Deduplicate(0.95f, 100);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests extension method Deduplicate for IAsyncEnumerable.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task DeduplicateExtension_WithAsyncEnumerable_RemovesDuplicates()
    {
        // Arrange
        var deduplicator = new StreamDeduplicator(0.95f, 100);
        var vectors = new[]
        {
            new float[] { 1f, 0f, 0f, 0f },
            new float[] { 1f, 0f, 0f, 0f }, // Duplicate
            new float[] { 0f, 1f, 0f, 0f },
        };

        async IAsyncEnumerable<float[]> GetVectorsAsync()
        {
            foreach (var vector in vectors)
            {
                await Task.Delay(10);
                yield return vector;
            }
        }

        // Act
        var filtered = new List<float[]>();
        await foreach (var vector in GetVectorsAsync().Deduplicate(deduplicator))
        {
            filtered.Add(vector);
        }

        // Assert
        filtered.Should().HaveCount(2);
    }
}
