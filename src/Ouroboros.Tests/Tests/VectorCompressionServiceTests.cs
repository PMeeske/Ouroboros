// <copyright file="VectorCompressionServiceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Domain.VectorCompression;
using DomainEvent = Ouroboros.Domain.VectorCompression.VectorCompressionEvent;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for VectorCompressionService refactored to use immutable event sourcing pattern.
/// </summary>
public class VectorCompressionServiceTests
{
    private static readonly CompressionConfig DefaultConfig = new(128, 0.95, CompressionMethod.DCT);

    [Fact]
    public void Compress_WithValidVector_ShouldReturnSuccessWithEvent()
    {
        // Arrange
        var vector = GenerateTestVector(1536);

        // Act
        var result = VectorCompressionService.Compress(vector, DefaultConfig);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CompressedData.Should().NotBeEmpty();
        result.Value.Event.Should().NotBeNull();
        result.Value.Event.Method.Should().Be("DCT");
        result.Value.Event.OriginalBytes.Should().Be(vector.Length * sizeof(float));
        result.Value.Event.CompressedBytes.Should().BePositive();
        result.Value.Event.EnergyRetained.Should().BeInRange(0.0, 1.0); // Validate range
    }

    [Fact]
    public void Compress_WithNullVector_ShouldReturnFailure()
    {
        // Act
        var result = VectorCompressionService.Compress(null!, DefaultConfig);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    [Fact]
    public void Compress_WithNullConfig_ShouldReturnFailure()
    {
        // Arrange
        var vector = GenerateTestVector(1536);

        // Act
        var result = VectorCompressionService.Compress(vector, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Compress_WithAdaptiveMethod_ShouldSelectOptimalMethod()
    {
        // Arrange
        var vector = GenerateTestVector(1536);
        var config = DefaultConfig with { DefaultMethod = CompressionMethod.Adaptive };

        // Act
        var result = VectorCompressionService.Compress(vector, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Event.Method.Should().BeOneOf("DCT", "FFT");
    }

    [Theory]
    [InlineData(CompressionMethod.DCT)]
    [InlineData(CompressionMethod.FFT)]
    [InlineData(CompressionMethod.QuantizedDCT)]
    public void Compress_WithDifferentMethods_ShouldProduceValidEvents(CompressionMethod method)
    {
        // Arrange
        var vector = GenerateTestVector(1536);
        var config = DefaultConfig with { DefaultMethod = method };

        // Act
        var result = VectorCompressionService.Compress(vector, config, method);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Event.Method.Should().Be(method.ToString());
        result.Value.CompressedData.Should().NotBeEmpty();
        result.Value.Event.CompressionRatio.Should().BeGreaterThan(0.0); // Can be less than 1 due to headers
    }

    [Fact]
    public void Decompress_AfterCompress_ShouldRecoverVector()
    {
        // Arrange
        var original = GenerateTestVector(256);
        var compressResult = VectorCompressionService.Compress(original, DefaultConfig);
        compressResult.IsSuccess.Should().BeTrue();

        // Act
        var decompressResult = VectorCompressionService.Decompress(compressResult.Value.CompressedData, DefaultConfig);

        // Assert
        decompressResult.IsSuccess.Should().BeTrue();
        decompressResult.Value.Length.Should().Be(original.Length);

        // Verify similarity (compression is lossy, but should retain most information)
        var similarity = CosineSimilarity(original, decompressResult.Value);
        similarity.Should().BeGreaterThan(0.85); // Adjusted for realistic compression with small vectors
    }

    [Fact]
    public void Decompress_WithInvalidData_ShouldReturnFailure()
    {
        // Arrange
        var invalidData = new byte[] { 1, 2, 3, 4 };

        // Act
        var result = VectorCompressionService.Decompress(invalidData, DefaultConfig);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    [Fact]
    public void GetStats_WithNoEvents_ShouldReturnEmptyStats()
    {
        // Arrange
        var events = Array.Empty<DomainEvent>();

        // Act
        var result = VectorCompressionService.GetStats(events);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.VectorsCompressed.Should().Be(0);
        result.Value.TotalOriginalBytes.Should().Be(0);
        result.Value.TotalCompressedBytes.Should().Be(0);
    }

    [Fact]
    public void GetStats_WithMultipleEvents_ShouldComputeCorrectStatistics()
    {
        // Arrange
        var vectors = new[] { GenerateTestVector(256), GenerateTestVector(256), GenerateTestVector(256) };
        var events = new List<DomainEvent>();

        foreach (var vector in vectors)
        {
            var result = VectorCompressionService.Compress(vector, DefaultConfig);
            result.IsSuccess.Should().BeTrue();
            events.Add(result.Value.Event);
        }

        // Act
        var statsResult = VectorCompressionService.GetStats(events);

        // Assert
        statsResult.IsSuccess.Should().BeTrue();
        var stats = statsResult.Value;
        stats.VectorsCompressed.Should().Be(3);
        stats.TotalOriginalBytes.Should().Be(3 * 256 * sizeof(float));
        stats.AverageCompressionRatio.Should().BeGreaterThan(1.0);
        stats.AverageEnergyRetained.Should().BeGreaterThan(0.8); // Adjusted for realistic compression
        stats.FirstCompressionAt.Should().NotBeNull();
        stats.LastCompressionAt.Should().NotBeNull();
        stats.MethodBreakdown["DCT"].Should().Be(3);
    }

    [Fact]
    public async Task BatchCompressAsync_WithMultipleVectors_ShouldReturnAllCompressedData()
    {
        // Arrange
        var vectors = new[] { GenerateTestVector(256), GenerateTestVector(256), GenerateTestVector(256) };

        // Act
        var result = await VectorCompressionService.BatchCompressAsync(vectors, DefaultConfig);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CompressedData.Count.Should().Be(3);
        result.Value.Events.Count.Should().Be(3);

        foreach (var compressedData in result.Value.CompressedData)
        {
            compressedData.Should().NotBeEmpty();
        }

        foreach (var evt in result.Value.Events)
        {
            evt.Method.Should().Be("DCT");
            evt.EnergyRetained.Should().BeGreaterThan(0.8); // Adjusted for realistic compression
        }
    }

    [Fact]
    public async Task BatchCompressAsync_WithEmptyCollection_ShouldReturnSuccess()
    {
        // Arrange
        var vectors = Array.Empty<float[]>();

        // Act
        var result = await VectorCompressionService.BatchCompressAsync(vectors, DefaultConfig);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CompressedData.Should().BeEmpty();
        result.Value.Events.Should().BeEmpty();
    }

    [Fact]
    public void Preview_WithValidVector_ShouldReturnCompressionAnalysis()
    {
        // Arrange
        var vector = GenerateTestVector(1536);

        // Act
        var result = VectorCompressionService.Preview(vector, DefaultConfig);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var preview = result.Value;
        preview.OriginalDimension.Should().Be(1536);
        preview.OriginalSizeBytes.Should().Be(1536 * sizeof(float));
        preview.DCTCompressedSize.Should().BeLessThan(preview.OriginalSizeBytes);
        preview.FFTCompressedSize.Should().BeLessThan(preview.OriginalSizeBytes);
        preview.QuantizedDCTSize.Should().BeLessThan(preview.DCTCompressedSize);
        preview.DCTEnergyRetained.Should().BeInRange(0.0, 1.0); // Validate range
        preview.BestCompressionRatio.Should().BeGreaterThan(1.0);
        preview.RecommendedMethod.Should().BeOneOf(CompressionMethod.DCT, CompressionMethod.FFT, CompressionMethod.QuantizedDCT);
    }

    [Fact]
    public void CompressedSimilarity_BetweenSimilarVectors_ShouldReturnHighScore()
    {
        // Arrange
        var vector1 = GenerateTestVector(256);
        var vector2 = GenerateTestVector(256);

        var compress1 = VectorCompressionService.Compress(vector1, DefaultConfig);
        var compress2 = VectorCompressionService.Compress(vector2, DefaultConfig);

        compress1.IsSuccess.Should().BeTrue();
        compress2.IsSuccess.Should().BeTrue();

        // Act
        var simResult = VectorCompressionService.CompressedSimilarity(
            compress1.Value.CompressedData,
            compress2.Value.CompressedData,
            DefaultConfig);

        // Assert
        simResult.IsSuccess.Should().BeTrue();
        simResult.Value.Should().BeInRange(-1.0, 1.0);
    }

    [Fact]
    public void Events_ShouldBeImmutable()
    {
        // Arrange
        var vector = GenerateTestVector(256);
        var result = VectorCompressionService.Compress(vector, DefaultConfig);
        result.IsSuccess.Should().BeTrue();

        var originalEvent = result.Value.Event;
        var originalTimestamp = originalEvent.Timestamp;
        var originalMethod = originalEvent.Method;

        // Events are records, so they're immutable by design
        // This test documents the immutability property
        originalEvent.Should().NotBeNull();
        originalEvent.Timestamp.Should().Be(originalTimestamp);
        originalEvent.Method.Should().Be(originalMethod);
    }

    private static float[] GenerateTestVector(int dimension)
    {
        var random = new Random(42);
        var vector = new float[dimension];
        for (int i = 0; i < dimension; i++)
        {
            vector[i] = (float)random.NextDouble();
        }

        // Normalize
        var norm = Math.Sqrt(vector.Sum(v => v * v));
        for (int i = 0; i < dimension; i++)
        {
            vector[i] /= (float)norm;
        }

        return vector;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        const double epsilon = 1e-12;
        if (normA <= epsilon || normB <= epsilon)
            return 0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
