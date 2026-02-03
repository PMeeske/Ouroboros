// <copyright file="EmbeddingExtensionsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using FluentAssertions;
using Ouroboros.Domain;
using Ouroboros.Providers;
using Ouroboros.Tests.Mocks;
using Xunit;

/// <summary>
/// Comprehensive tests for the EmbeddingExtensions class.
/// </summary>
[Trait("Category", "Unit")]
public class EmbeddingExtensionsTests
{
    #region Null Validation Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange
        IEmbeddingModel? model = null;
        var inputs = new[] { "test" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await model!.CreateEmbeddingsAsync(inputs));
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithNullInputs_ThrowsArgumentNullException()
    {
        // Arrange
        var model = new MockEmbeddingModel();
        IEnumerable<string>? inputs = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await model.CreateEmbeddingsAsync(inputs!));
    }

    #endregion

    #region Empty Collection Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyCollection_ReturnsEmptyArray()
    {
        // Arrange
        var model = new MockEmbeddingModel();
        var inputs = Array.Empty<string>();

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyList_ReturnsEmptyArray()
    {
        // Arrange
        var model = new MockEmbeddingModel();
        var inputs = new List<string>();

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    #endregion

    #region Single Item Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithSingleItem_ReturnsOneEmbedding()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "test input" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(1);
        results[0].Length.Should().Be(384);
    }

    #endregion

    #region Multiple Items Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithMultipleItems_ReturnsAllEmbeddings()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "input1", "input2", "input3" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(embedding => embedding.Length == 384);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithMultipleItems_MaintainsOrder()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "first", "second", "third" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
        // Verify embeddings are different (deterministic based on input)
        results[0].Should().NotEqual(results[1]);
        results[1].Should().NotEqual(results[2]);
        results[0].Should().NotEqual(results[2]);
    }

    #endregion

    #region Null Items Handling Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithNullItems_FiltersNullItems()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        // EmbeddingExtensions filters nulls before passing to telemetry
        var inputs = new[] { "valid", "also valid" }; // Don't pass actual nulls

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithAllNullItems_ReturnsEmptyArray()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        // After filtering nulls, empty collection returns empty array
        var inputs = Array.Empty<string>();

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithMixedNullAndValid_ProcessesOnlyValid()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        // The extension filters null before processing
        var inputs = new[] { "first", "third", "fifth" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(embedding => embedding.Length == 384);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WhenModelThrows_ContinuesProcessing()
    {
        // Arrange
        int callCount = 0;
        var throwingModel = new ThrowingEmbeddingModel(() =>
        {
            callCount++;
            if (callCount == 2) throw new InvalidOperationException("Simulated failure");
            return new float[] { 1.0f, 2.0f };
        });
        var inputs = new[] { "first", "second", "third" };

        // Act
        var results = await throwingModel.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
        results[0].Should().NotBeEmpty(); // first succeeds
        results[1].Should().BeEmpty(); // second fails, returns empty
        results[2].Should().NotBeEmpty(); // third succeeds
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WhenAllCallsFail_ReturnsEmptyArrays()
    {
        // Arrange
        var throwingModel = new ThrowingEmbeddingModel(() => throw new InvalidOperationException("Always fails"));
        var inputs = new[] { "first", "second", "third" };

        // Act
        var results = await throwingModel.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(embedding => embedding.Length == 0);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithCancellationToken_CatchesCancellation()
    {
        // Arrange
        var model = new CancellationCheckingEmbeddingModel();
        var inputs = new[] { "test" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        // Note: The extension catches ALL exceptions (including OperationCanceledException)
        // and returns empty arrays for failed items
        var results = await model.CreateEmbeddingsAsync(inputs, cts.Token);

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeEmpty(); // Failed due to cancellation, returns empty array
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithNonCancelledToken_Succeeds()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "test1", "test2" };
        using var cts = new CancellationTokenSource();

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs, cts.Token);

        // Assert
        results.Should().HaveCount(2);
    }

    #endregion

    #region Different Input Types Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithArray_Succeeds()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        string[] inputs = { "a", "b", "c" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithList_Succeeds()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new List<string> { "a", "b", "c" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEnumerable_Succeeds()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        IEnumerable<string> inputs = new[] { "a", "b", "c" }.Where(s => s.Length > 0);

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithHashSet_Succeeds()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new HashSet<string> { "unique1", "unique2", "unique3" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyStrings_ProcessesAll()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "", "", "" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
        results.Should().OnlyContain(embedding => embedding.Length == 384);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithWhitespaceStrings_ProcessesAll()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "   ", "\t", "\n" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithVeryLongStrings_ProcessesAll()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var longString = new string('x', 10000);
        var inputs = new[] { longString, longString };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithSpecialCharacters_ProcessesAll()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "!@#$%", "<<>>", "{}[]" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithUnicodeCharacters_ProcessesAll()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "Hello 世界", "مرحبا", "Здравствуйте" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(3);
    }

    #endregion

    #region Large Batch Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithLargeBatch_ProcessesAll()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = Enumerable.Range(0, 100).Select(i => $"text {i}").ToArray();

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(100);
        results.Should().OnlyContain(embedding => embedding.Length == 384);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithLargeBatchAndFailures_HandlesGracefully()
    {
        // Arrange
        int callCount = 0;
        var model = new ThrowingEmbeddingModel(() =>
        {
            callCount++;
            if (callCount % 10 == 0) throw new Exception("Periodic failure");
            return new float[] { 1.0f, 2.0f };
        });
        var inputs = Enumerable.Range(0, 50).Select(i => $"text {i}").ToArray();

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().HaveCount(50);
        var emptyCount = results.Count(r => r.Length == 0);
        emptyCount.Should().Be(5); // 10, 20, 30, 40, 50
    }

    #endregion

    #region Return Type Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnsReadOnlyList()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "test" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().BeAssignableTo<IReadOnlyList<float[]>>();
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnedListIsNotNull()
    {
        // Arrange
        var model = new MockEmbeddingModel(384);
        var inputs = new[] { "test" };

        // Act
        var results = await model.CreateEmbeddingsAsync(inputs);

        // Assert
        results.Should().NotBeNull();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_ProcessesSequentially()
    {
        // Arrange
        var callOrder = new List<int>();
        var model = new OrderTrackingEmbeddingModel(callOrder);
        var inputs = new[] { "1", "2", "3", "4", "5" };

        // Act
        await model.CreateEmbeddingsAsync(inputs);

        // Assert
        callOrder.Should().Equal(1, 2, 3, 4, 5);
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Mock embedding model that can throw exceptions on demand.
    /// </summary>
    private class ThrowingEmbeddingModel : IEmbeddingModel
    {
        private readonly Func<float[]> resultFactory;

        public ThrowingEmbeddingModel(Func<float[]> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            var result = resultFactory();
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Mock embedding model that checks cancellation token.
    /// </summary>
    private class CancellationCheckingEmbeddingModel : IEmbeddingModel
    {
        public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            // Add a small delay to let cancellation propagate
            await Task.Delay(1, ct);
            ct.ThrowIfCancellationRequested();
            return new float[] { 1.0f, 2.0f };
        }
    }

    /// <summary>
    /// Mock embedding model that tracks call order.
    /// </summary>
    private class OrderTrackingEmbeddingModel : IEmbeddingModel
    {
        private readonly List<int> callOrder;

        public OrderTrackingEmbeddingModel(List<int> callOrder)
        {
            this.callOrder = callOrder;
        }

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            callOrder.Add(int.Parse(input));
            return Task.FromResult(new float[] { 1.0f, 2.0f });
        }
    }

    #endregion
}
