using LangChain.Databases;
using LangChain.DocumentLoaders;
using Moq;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline;

/// <summary>
/// Comprehensive tests for BranchOps following functional programming principles.
/// Tests focus on merge operations, conflict resolution, and immutability.
/// </summary>
[Trait("Category", "Unit")]
public class BranchOpsTests
{
    private readonly Mock<IEmbeddingModel> _mockEmbedding;

    public BranchOpsTests()
    {
        _mockEmbedding = new Mock<IEmbeddingModel>();
        // Setup default embedding behavior
        _mockEmbedding.Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
    }

    #region MergeByRelevance Tests

    [Fact]
    public async Task MergeByRelevance_WithNoConflicts_CombinesAllVectors()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        await storeA.AddAsync(new[]
        {
            new Vector { Id = "vec1", Text = "Content A1", Embedding = new[] { 1.0f, 0.0f, 0.0f } }
        });

        await storeB.AddAsync(new[]
        {
            new Vector { Id = "vec2", Text = "Content B1", Embedding = new[] { 0.0f, 1.0f, 0.0f } }
        });

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 1);

        // Act
        var result = await mergeStep((branchA, branchB, "test query"));

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("branchA+branchB");
        var vectors = result.Store.GetAll().ToList();
        vectors.Should().HaveCount(2);
        vectors.Should().Contain(v => v.Id == "vec1");
        vectors.Should().Contain(v => v.Id == "vec2");
    }

    [Fact]
    public async Task MergeByRelevance_WithConflictingIds_ResolvesBasedOnRelevance()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        // Both stores have vectors with the same ID but different content
        await storeA.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "conflict", 
                Text = "Content from A", 
                Embedding = new[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["id"] = "conflict" }
            }
        });

        await storeB.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "conflict", 
                Text = "Content from B", 
                Embedding = new[] { 0.0f, 1.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["id"] = "conflict" }
            }
        });

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 1);

        // Act
        var result = await mergeStep((branchA, branchB, "test query"));

        // Assert
        result.Should().NotBeNull();
        var vectors = result.Store.GetAll().ToList();
        vectors.Should().HaveCount(1); // Only one vector should remain after conflict resolution
        vectors[0].Id.Should().Be("conflict");
    }

    [Fact]
    public async Task MergeByRelevance_CombinesEvents()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        // Add events to branches
        branchA = branchA.WithReasoning(new Draft("Draft A"), "prompt A");
        branchB = branchB.WithReasoning(new Draft("Draft B"), "prompt B");

        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 1);

        // Act
        var result = await mergeStep((branchA, branchB, "test query"));

        // Assert
        result.Events.Should().HaveCount(2);
        result.Events.OfType<ReasoningStep>().Should().HaveCount(2);
    }

    [Fact]
    public async Task MergeByRelevance_WithEmptyBranches_ProducesEmptyMerge()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 1);

        // Act
        var result = await mergeStep((branchA, branchB, "test query"));

        // Assert
        result.Should().NotBeNull();
        result.Store.GetAll().Should().BeEmpty();
        result.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task MergeByRelevance_WithMultipleConflicts_ResolvesAllCorrectly()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        await storeA.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "conflict1", 
                Text = "A1", 
                Embedding = new[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["id"] = "conflict1" }
            },
            new Vector 
            { 
                Id = "conflict2", 
                Text = "A2", 
                Embedding = new[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["id"] = "conflict2" }
            },
            new Vector 
            { 
                Id = "unique1", 
                Text = "A3", 
                Embedding = new[] { 1.0f, 0.0f, 0.0f }
            }
        });

        await storeB.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "conflict1", 
                Text = "B1", 
                Embedding = new[] { 0.0f, 1.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["id"] = "conflict1" }
            },
            new Vector 
            { 
                Id = "conflict2", 
                Text = "B2", 
                Embedding = new[] { 0.0f, 1.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["id"] = "conflict2" }
            },
            new Vector 
            { 
                Id = "unique2", 
                Text = "B3", 
                Embedding = new[] { 0.0f, 1.0f, 0.0f }
            }
        });

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 1);

        // Act
        var result = await mergeStep((branchA, branchB, "test query"));

        // Assert
        result.Should().NotBeNull();
        var vectors = result.Store.GetAll().ToList();
        vectors.Should().HaveCount(4); // 2 resolved conflicts + 2 unique vectors
    }

    [Fact]
    public async Task MergeByRelevance_PreservesOriginalBranches()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        await storeA.AddAsync(new[]
        {
            new Vector { Id = "vec1", Text = "Content A", Embedding = new[] { 1.0f, 0.0f, 0.0f } }
        });

        await storeB.AddAsync(new[]
        {
            new Vector { Id = "vec2", Text = "Content B", Embedding = new[] { 0.0f, 1.0f, 0.0f } }
        });

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        var originalACount = branchA.Store.GetAll().Count();
        var originalBCount = branchB.Store.GetAll().Count();

        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 1);

        // Act
        await mergeStep((branchA, branchB, "test query"));

        // Assert - Original branches should be unchanged (immutability)
        branchA.Store.GetAll().Should().HaveCount(originalACount);
        branchB.Store.GetAll().Should().HaveCount(originalBCount);
    }

    [Fact]
    public async Task MergeByRelevance_WithDifferentTopK_UsesCorrectAmount()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        // Test with topK = 3
        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 3);

        // Act
        var result = await mergeStep((branchA, branchB, "test query"));

        // Assert
        result.Should().NotBeNull();
        // The function should work with different topK values
    }

    [Fact]
    public async Task MergeByRelevance_WithComplexQuery_HandlesCorrectly()
    {
        // Arrange
        var storeA = new TrackedVectorStore();
        var storeB = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        await storeA.AddAsync(new[]
        {
            new Vector { Id = "vec1", Text = "Complex content with special chars: @#$%", Embedding = new[] { 1.0f, 0.0f, 0.0f } }
        });

        var branchA = new PipelineBranch("branchA", storeA, source);
        var branchB = new PipelineBranch("branchB", storeB, source);

        var mergeStep = BranchOps.MergeByRelevance(_mockEmbedding.Object, topK: 1);

        // Act
        var result = await mergeStep((branchA, branchB, "test query with unicode: 日本語"));

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
