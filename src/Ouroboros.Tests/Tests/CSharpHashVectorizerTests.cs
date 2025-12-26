// <copyright file="CSharpHashVectorizerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Infrastructure.FeatureEngineering;
using Xunit;

/// <summary>
/// Tests for CSharpHashVectorizer functionality.
/// </summary>
public class CSharpHashVectorizerTests
{
    /// <summary>
    /// Tests that the vectorizer creates vectors of the correct dimension.
    /// </summary>
    [Theory]
    [InlineData(256)]
    [InlineData(4096)]
    [InlineData(65536)]
    public void Constructor_WithValidDimension_CreatesVectorizer(int dimension)
    {
        // Act
        var vectorizer = new CSharpHashVectorizer(dimension);

        // Assert
        vectorizer.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that the constructor throws when dimension is not a power of 2.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(65535)]
    public void Constructor_WithInvalidDimension_ThrowsArgumentException(int dimension)
    {
        // Act
        Action act = () => new CSharpHashVectorizer(dimension);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("dimension");
    }

    /// <summary>
    /// Tests that TransformCode returns a vector with correct dimension.
    /// </summary>
    [Fact]
    public void TransformCode_WithValidCode_ReturnsVectorOfCorrectDimension()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var code = @"
            public class MyClass
            {
                public int GetValue() => 42;
            }";

        // Act
        var vector = vectorizer.TransformCode(code);

        // Assert
        vector.Should().NotBeNull();
        vector.Length.Should().Be(4096);
    }

    /// <summary>
    /// Tests that TransformCode returns normalized vectors (L2 norm = 1).
    /// </summary>
    [Fact]
    public void TransformCode_WithValidCode_ReturnsNormalizedVector()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var code = "public class Test { }";

        // Act
        var vector = vectorizer.TransformCode(code);

        // Assert
        var norm = Math.Sqrt(vector.Sum(v => v * v));
        norm.Should().BeApproximately(1.0f, 0.001f);
    }

    /// <summary>
    /// Tests that identical code produces identical vectors.
    /// </summary>
    [Fact]
    public void TransformCode_WithIdenticalCode_ProducesSameVector()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096, lowercase: true);
        var code = @"
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }";

        // Act
        var vector1 = vectorizer.TransformCode(code);
        var vector2 = vectorizer.TransformCode(code);

        // Assert
        vector1.Should().Equal(vector2);
    }

    /// <summary>
    /// Tests that similar code produces similar vectors.
    /// </summary>
    [Fact]
    public void TransformCode_WithSimilarCode_ProducesSimilarVectors()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var code1 = @"
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }";
        var code2 = @"
            public class Calculator
            {
                public int Add(int x, int y) => x + y;
            }";

        // Act
        var vector1 = vectorizer.TransformCode(code1);
        var vector2 = vectorizer.TransformCode(code2);
        var similarity = CSharpHashVectorizer.CosineSimilarity(vector1, vector2);

        // Assert - Should be similar (variable names changed but structure same)
        similarity.Should().BeGreaterThan(0.6f);
    }

    /// <summary>
    /// Tests that different code produces different vectors.
    /// </summary>
    [Fact]
    public void TransformCode_WithDifferentCode_ProducesDifferentVectors()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var code1 = "public class Calculator { public int Add(int a, int b) => a + b; }";
        var code2 = "public interface ILogger { void Log(string message); }";

        // Act
        var vector1 = vectorizer.TransformCode(code1);
        var vector2 = vectorizer.TransformCode(code2);
        var similarity = CSharpHashVectorizer.CosineSimilarity(vector1, vector2);

        // Assert
        similarity.Should().BeLessThan(0.6f);
    }

    /// <summary>
    /// Tests that empty code returns a zero vector.
    /// </summary>
    [Fact]
    public void TransformCode_WithEmptyCode_ReturnsZeroVector()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);

        // Act
        var vector = vectorizer.TransformCode(string.Empty);

        // Assert
        vector.All(v => v == 0f).Should().BeTrue();
    }

    /// <summary>
    /// Tests that null code is handled gracefully.
    /// </summary>
    [Fact]
    public void TransformCode_WithNullCode_ReturnsZeroVector()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);

        // Act
        var vector = vectorizer.TransformCode(null!);

        // Assert
        vector.All(v => v == 0f).Should().BeTrue();
    }

    /// <summary>
    /// Tests that lowercase option affects vectorization.
    /// </summary>
    [Fact]
    public void TransformCode_WithLowercaseOption_AffectsVectorization()
    {
        // Arrange
        var code = "public class MyClass { }";
        var vectorizer1 = new CSharpHashVectorizer(4096, lowercase: true);
        var vectorizer2 = new CSharpHashVectorizer(4096, lowercase: false);

        // Act
        var vector1 = vectorizer1.TransformCode(code);
        var vector2 = vectorizer2.TransformCode(code);

        // Assert - Keywords should be same, identifiers different
        vector1.Should().NotEqual(vector2);
    }

    /// <summary>
    /// Tests TransformFile with a temporary file.
    /// </summary>
    [Fact]
    public void TransformFile_WithValidFile_ReturnsVector()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var tempFile = Path.GetTempFileName();
        var code = "public class Test { }";
        File.WriteAllText(tempFile, code);

        try
        {
            // Act
            var vector = vectorizer.TransformFile(tempFile);

            // Assert
            vector.Should().NotBeNull();
            vector.Length.Should().Be(4096);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Tests TransformFile with non-existent file throws exception.
    /// </summary>
    [Fact]
    public void TransformFile_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);

        // Act
        Action act = () => vectorizer.TransformFile("/nonexistent/file.cs");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    /// <summary>
    /// Tests TransformFiles with multiple files.
    /// </summary>
    [Fact]
    public void TransformFiles_WithMultipleFiles_ReturnsMultipleVectors()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var tempFiles = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, $"public class Test{i} {{ }}");
            tempFiles.Add(tempFile);
        }

        try
        {
            // Act
            var vectors = vectorizer.TransformFiles(tempFiles);

            // Assert
            vectors.Should().HaveCount(3);
            vectors.All(v => v.Length == 4096).Should().BeTrue();
        }
        finally
        {
            tempFiles.ForEach(File.Delete);
        }
    }

    /// <summary>
    /// Tests TransformFiles with null throws exception.
    /// </summary>
    [Fact]
    public void TransformFiles_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);

        // Act
        Action act = () => vectorizer.TransformFiles(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests CosineSimilarity with identical vectors returns 1.
    /// </summary>
    [Fact]
    public void CosineSimilarity_WithIdenticalVectors_ReturnsOne()
    {
        // Arrange
        var v1 = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };
        var v2 = new float[] { 0.5f, 0.5f, 0.5f, 0.5f };

        // Act
        var similarity = CSharpHashVectorizer.CosineSimilarity(v1, v2);

        // Assert
        similarity.Should().BeApproximately(1.0f, 0.001f);
    }

    /// <summary>
    /// Tests CosineSimilarity with orthogonal vectors returns 0.
    /// </summary>
    [Fact]
    public void CosineSimilarity_WithOrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var v1 = new float[] { 1f, 0f, 0f, 0f };
        var v2 = new float[] { 0f, 1f, 0f, 0f };

        // Act
        var similarity = CSharpHashVectorizer.CosineSimilarity(v1, v2);

        // Assert
        similarity.Should().BeApproximately(0.0f, 0.001f);
    }

    /// <summary>
    /// Tests CosineSimilarity with mismatched vector lengths throws exception.
    /// </summary>
    [Fact]
    public void CosineSimilarity_WithMismatchedLengths_ThrowsArgumentException()
    {
        // Arrange
        var v1 = new float[] { 1f, 0f };
        var v2 = new float[] { 0f, 1f, 0f };

        // Act
        Action act = () => CSharpHashVectorizer.CosineSimilarity(v1, v2);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Tests async methods work correctly.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task TransformCodeAsync_WithValidCode_ReturnsVector()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var code = "public class Test { }";

        // Act
        var vector = await vectorizer.TransformCodeAsync(code);

        // Assert
        vector.Should().NotBeNull();
        vector.Length.Should().Be(4096);
    }

    /// <summary>
    /// Tests async file transformation.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task TransformFilesAsync_WithValidFiles_ReturnsVectors()
    {
        // Arrange
        var vectorizer = new CSharpHashVectorizer(4096);
        var tempFiles = new List<string>();

        for (int i = 0; i < 2; i++)
        {
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, $"public class Test{i} {{ }}");
            tempFiles.Add(tempFile);
        }

        try
        {
            // Act
            var vectors = await vectorizer.TransformFilesAsync(tempFiles);

            // Assert
            vectors.Should().HaveCount(2);
            vectors.All(v => v.Length == 4096).Should().BeTrue();
        }
        finally
        {
            tempFiles.ForEach(File.Delete);
        }
    }
}
