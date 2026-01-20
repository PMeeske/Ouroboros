using FluentAssertions;
using LangChain.DocumentLoaders;
using Ouroboros.Pipeline.Branches;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Comprehensive tests for BranchHash following functional programming principles.
/// Tests focus on deterministic hashing, integrity verification, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BranchHashTests
{
    #region ComputeHash Tests

    [Fact]
    public void ComputeHash_WithSameSnapshot_ShouldReturnSameHash()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test-branch");

        // Act
        var hash1 = BranchHash.ComputeHash(snapshot);
        var hash2 = BranchHash.ComputeHash(snapshot);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_WithDifferentSnapshots_ShouldReturnDifferentHashes()
    {
        // Arrange
        var snapshot1 = CreateTestSnapshot("branch1");
        var snapshot2 = CreateTestSnapshot("branch2");

        // Act
        var hash1 = BranchHash.ComputeHash(snapshot1);
        var hash2 = BranchHash.ComputeHash(snapshot2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ShouldReturnLowercaseHexString()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test");

        // Act
        var hash = BranchHash.ComputeHash(snapshot);

        // Assert
        hash.Should().MatchRegex("^[0-9a-f]+$");
        hash.Length.Should().Be(64); // SHA-256 produces 64 hex characters
    }

    [Fact]
    public void ComputeHash_WithNullSnapshot_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => BranchHash.ComputeHash(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeHash_WithEmptySnapshot_ShouldProduceValidHash()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "",
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>()
        };

        // Act
        var hash = BranchHash.ComputeHash(snapshot);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(64);
    }

    [Fact]
    public void ComputeHash_IsDeterministic_ForComplexSnapshot()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "complex-branch",
            Events = new List<PipelineEvent>
            {
                new ReasoningStep(
                    Guid.Parse("12345678-1234-1234-1234-123456789012"),
                    "Draft",
                    new Draft("test content"),
                    DateTime.Parse("2025-01-01T00:00:00Z").ToUniversalTime(),
                    "test prompt",
                    null)
            },
            Vectors = new List<SerializableVector>
            {
                new SerializableVector
                {
                    Id = "vec1",
                    Text = "test text",
                    Metadata = new Dictionary<string, object> { ["key"] = "value" },
                    Embedding = new float[] { 0.1f, 0.2f, 0.3f }
                }
            }
        };

        // Act
        var hash1 = BranchHash.ComputeHash(snapshot);
        var hash2 = BranchHash.ComputeHash(snapshot);

        // Assert
        hash1.Should().Be(hash2);
    }

    #endregion

    #region VerifyHash Tests

    [Fact]
    public void VerifyHash_WithMatchingHash_ShouldReturnTrue()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test");
        var expectedHash = BranchHash.ComputeHash(snapshot);

        // Act
        var result = BranchHash.VerifyHash(snapshot, expectedHash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_WithNonMatchingHash_ShouldReturnFalse()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test");
        var wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = BranchHash.VerifyHash(snapshot, wrongHash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyHash_IsCaseInsensitive()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test");
        var hash = BranchHash.ComputeHash(snapshot);
        var uppercaseHash = hash.ToUpperInvariant();

        // Act
        var result = BranchHash.VerifyHash(snapshot, uppercaseHash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_WithNullSnapshot_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => BranchHash.VerifyHash(null!, "somehash");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void VerifyHash_WithNullHash_ShouldThrowArgumentNullException()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test");

        // Act
        Action act = () => BranchHash.VerifyHash(snapshot, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithHash Tests

    [Fact]
    public void WithHash_ShouldReturnSnapshotAndHash()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test");

        // Act
        var (returnedSnapshot, hash) = BranchHash.WithHash(snapshot);

        // Assert
        returnedSnapshot.Should().BeSameAs(snapshot);
        hash.Should().NotBeNullOrEmpty();
        hash.Length.Should().Be(64);
    }

    [Fact]
    public void WithHash_HashShouldMatchComputedHash()
    {
        // Arrange
        var snapshot = CreateTestSnapshot("test");

        // Act
        var (_, hash) = BranchHash.WithHash(snapshot);
        var computedHash = BranchHash.ComputeHash(snapshot);

        // Assert
        hash.Should().Be(computedHash);
    }

    [Fact]
    public void WithHash_WithNullSnapshot_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => BranchHash.WithHash(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Helper Methods

    private static BranchSnapshot CreateTestSnapshot(string name)
    {
        return new BranchSnapshot
        {
            Name = name,
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>()
        };
    }

    #endregion
}
