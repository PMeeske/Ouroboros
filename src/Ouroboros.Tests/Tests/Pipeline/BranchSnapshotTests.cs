using LangChain.Databases;
using LangChain.DocumentLoaders;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline;

/// <summary>
/// Tests for BranchSnapshot serialization and restoration.
/// Validates snapshot capture, restoration, and data integrity.
/// </summary>
[Trait("Category", "Unit")]
public class BranchSnapshotTests
{
    #region Capture Tests

    [Fact]
    public async Task Capture_WithEmptyBranch_CreatesEmptySnapshot()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);

        // Act
        var snapshot = await BranchSnapshot.Capture(branch);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Name.Should().Be("test-branch");
        snapshot.Events.Should().BeEmpty();
        snapshot.Vectors.Should().BeEmpty();
    }

    [Fact]
    public async Task Capture_WithVectors_CapturesAllVectors()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);

        await store.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "vec1", 
                Text = "Content 1", 
                Embedding = new[] { 1.0f, 0.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["key"] = "value1" }
            },
            new Vector 
            { 
                Id = "vec2", 
                Text = "Content 2", 
                Embedding = new[] { 0.0f, 1.0f, 0.0f },
                Metadata = new Dictionary<string, object> { ["key"] = "value2" }
            }
        });

        // Act
        var snapshot = await BranchSnapshot.Capture(branch);

        // Assert
        snapshot.Vectors.Should().HaveCount(2);
        snapshot.Vectors.Should().Contain(v => v.Id == "vec1" && v.Text == "Content 1");
        snapshot.Vectors.Should().Contain(v => v.Id == "vec2" && v.Text == "Content 2");
    }

    [Fact]
    public async Task Capture_WithEvents_CapturesAllEvents()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);

        branch = branch.WithReasoning(new Draft("Draft content"), "Draft prompt");
        branch = branch.WithReasoning(new Critique("Critique content"), "Critique prompt");
        branch = branch.WithIngestEvent("test-source", new[] { "doc1", "doc2" });

        // Act
        var snapshot = await BranchSnapshot.Capture(branch);

        // Assert
        snapshot.Events.Should().HaveCount(3);
        snapshot.Events.OfType<ReasoningStep>().Should().HaveCount(2);
        snapshot.Events.OfType<IngestBatch>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Capture_WithComplexBranch_CapturesAllData()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("complex-branch", store, source);

        await store.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "vec1", 
                Text = "Vector content", 
                Embedding = new[] { 1.0f, 2.0f, 3.0f },
                Metadata = new Dictionary<string, object> 
                { 
                    ["type"] = "test",
                    ["index"] = 0
                }
            }
        });

        branch = branch.WithReasoning(new Draft("Draft"), "prompt");
        branch = branch.WithIngestEvent("source", new[] { "id1", "id2", "id3" });

        // Act
        var snapshot = await BranchSnapshot.Capture(branch);

        // Assert
        snapshot.Name.Should().Be("complex-branch");
        snapshot.Events.Should().HaveCount(2);
        snapshot.Vectors.Should().HaveCount(1);
        snapshot.Vectors[0].Metadata.Should().ContainKey("type");
        snapshot.Vectors[0].Metadata["type"].Should().Be("test");
    }

    [Fact]
    public async Task Capture_WithNullEmbedding_HandlesGracefully()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);

        await store.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "vec1", 
                Text = "Content", 
                Embedding = Array.Empty<float>() // Empty embedding instead of null
            }
        });

        // Act
        var snapshot = await BranchSnapshot.Capture(branch);

        // Assert
        snapshot.Vectors.Should().HaveCount(1);
        snapshot.Vectors[0].Embedding.Should().NotBeNull();
    }

    [Fact]
    public async Task Capture_WithNullMetadata_HandlesGracefully()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);

        await store.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "vec1", 
                Text = "Content", 
                Embedding = new[] { 1.0f },
                Metadata = null // Null metadata
            }
        });

        // Act
        var snapshot = await BranchSnapshot.Capture(branch);

        // Assert
        snapshot.Vectors.Should().HaveCount(1);
        snapshot.Vectors[0].Metadata.Should().NotBeNull();
        snapshot.Vectors[0].Metadata.Should().BeEmpty();
    }

    #endregion

    #region Restore Tests

    [Fact]
    public async Task Restore_WithEmptySnapshot_RestoresEmptyBranch()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "restored-branch",
            Events = new List<PipelineEvent>(),
            Vectors = new List<SerializableVector>()
        };

        // Act
        var branch = await snapshot.Restore();

        // Assert
        branch.Should().NotBeNull();
        branch.Name.Should().Be("restored-branch");
        branch.Events.Should().BeEmpty();
        branch.Store.GetAll().Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_WithVectors_RestoresAllVectors()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "restored-branch",
            Vectors = new List<SerializableVector>
            {
                new SerializableVector
                {
                    Id = "vec1",
                    Text = "Content 1",
                    Embedding = new[] { 1.0f, 0.0f, 0.0f },
                    Metadata = new Dictionary<string, object> { ["key"] = "value1" }
                },
                new SerializableVector
                {
                    Id = "vec2",
                    Text = "Content 2",
                    Embedding = new[] { 0.0f, 1.0f, 0.0f },
                    Metadata = new Dictionary<string, object> { ["key"] = "value2" }
                }
            }
        };

        // Act
        var branch = await snapshot.Restore();

        // Assert
        var vectors = branch.Store.GetAll().ToList();
        vectors.Should().HaveCount(2);
        vectors.Should().Contain(v => v.Id == "vec1" && v.Text == "Content 1");
        vectors.Should().Contain(v => v.Id == "vec2" && v.Text == "Content 2");
    }

    [Fact]
    public async Task Restore_WithEvents_RestoresAllEvents()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "restored-branch",
            Events = new List<PipelineEvent>
            {
                new ReasoningStep(
                    Guid.NewGuid(),
                    "Draft",
                    new Draft("Draft content"),
                    DateTime.UtcNow,
                    "Draft prompt",
                    null
                ),
                new IngestBatch(
                    Guid.NewGuid(),
                    "test-source",
                    new List<string> { "doc1", "doc2" },
                    DateTime.UtcNow
                )
            }
        };

        // Act
        var branch = await snapshot.Restore();

        // Assert
        branch.Events.Should().HaveCount(2);
        branch.Events.OfType<ReasoningStep>().Should().HaveCount(1);
        branch.Events.OfType<IngestBatch>().Should().HaveCount(1);
    }

    [Fact]
    public async Task Restore_PreservesMetadata()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "restored-branch",
            Vectors = new List<SerializableVector>
            {
                new SerializableVector
                {
                    Id = "vec1",
                    Text = "Content",
                    Embedding = new[] { 1.0f, 2.0f, 3.0f },
                    Metadata = new Dictionary<string, object> 
                    { 
                        ["type"] = "document",
                        ["index"] = 5,
                        ["nested"] = new Dictionary<string, object> { ["key"] = "value" }
                    }
                }
            }
        };

        // Act
        var branch = await snapshot.Restore();

        // Assert
        var vector = branch.Store.GetAll().First();
        vector.Metadata.Should().ContainKey("type");
        vector.Metadata["type"].Should().Be("document");
        vector.Metadata.Should().ContainKey("index");
        vector.Metadata["index"].Should().Be(5);
    }

    [Fact]
    public async Task Restore_WithNullEmbedding_RestoresWithEmptyArray()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "restored-branch",
            Vectors = new List<SerializableVector>
            {
                new SerializableVector
                {
                    Id = "vec1",
                    Text = "Content",
                    Embedding = null
                }
            }
        };

        // Act
        var branch = await snapshot.Restore();

        // Assert
        var vector = branch.Store.GetAll().First();
        vector.Embedding.Should().NotBeNull();
        vector.Embedding.Should().BeEmpty();
    }

    [Fact]
    public async Task Restore_WithNullMetadata_RestoresWithEmptyDictionary()
    {
        // Arrange
        var snapshot = new BranchSnapshot
        {
            Name = "restored-branch",
            Vectors = new List<SerializableVector>
            {
                new SerializableVector
                {
                    Id = "vec1",
                    Text = "Content",
                    Embedding = new[] { 1.0f },
                    Metadata = null
                }
            }
        };

        // Act
        var branch = await snapshot.Restore();

        // Assert
        var vector = branch.Store.GetAll().First();
        vector.Metadata.Should().NotBeNull();
        vector.Metadata.Should().BeEmpty();
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public async Task CaptureAndRestore_PreservesAllData()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("original-branch", store, source);

        await store.AddAsync(new[]
        {
            new Vector 
            { 
                Id = "vec1", 
                Text = "Test content", 
                Embedding = new[] { 1.0f, 2.0f, 3.0f },
                Metadata = new Dictionary<string, object> { ["key"] = "value" }
            }
        });

        originalBranch = originalBranch.WithReasoning(new Draft("Draft"), "prompt");
        originalBranch = originalBranch.WithIngestEvent("source", new[] { "id1" });

        // Act
        var snapshot = await BranchSnapshot.Capture(originalBranch);
        var restoredBranch = await snapshot.Restore();

        // Assert
        restoredBranch.Name.Should().Be(originalBranch.Name);
        restoredBranch.Events.Should().HaveCount(originalBranch.Events.Count);
        restoredBranch.Store.GetAll().Should().HaveCount(originalBranch.Store.GetAll().Count());

        var originalVector = originalBranch.Store.GetAll().First();
        var restoredVector = restoredBranch.Store.GetAll().First();
        restoredVector.Id.Should().Be(originalVector.Id);
        restoredVector.Text.Should().Be(originalVector.Text);
        restoredVector.Embedding.Should().BeEquivalentTo(originalVector.Embedding);
    }

    [Fact]
    public async Task CaptureAndRestore_PreservesEventOrder()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("original-branch", store, source);

        originalBranch = originalBranch.WithReasoning(new Draft("Draft 1"), "prompt 1");
        originalBranch = originalBranch.WithReasoning(new Critique("Critique 1"), "prompt 2");
        originalBranch = originalBranch.WithReasoning(new FinalSpec("Final 1"), "prompt 3");

        // Act
        var snapshot = await BranchSnapshot.Capture(originalBranch);
        var restoredBranch = await snapshot.Restore();

        // Assert
        var originalSteps = originalBranch.Events.OfType<ReasoningStep>().ToList();
        var restoredSteps = restoredBranch.Events.OfType<ReasoningStep>().ToList();

        restoredSteps.Should().HaveCount(3);
        for (int i = 0; i < 3; i++)
        {
            restoredSteps[i].StepKind.Should().Be(originalSteps[i].StepKind);
            restoredSteps[i].Prompt.Should().Be(originalSteps[i].Prompt);
        }
    }

    [Fact]
    public async Task CaptureAndRestore_WithLargeBranch_HandlesCorrectly()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("large-branch", store, source);

        // Add many vectors
        var vectors = Enumerable.Range(0, 100).Select(i => new Vector
        {
            Id = $"vec{i}",
            Text = $"Content {i}",
            Embedding = new[] { (float)i, (float)i * 2, (float)i * 3 },
            Metadata = new Dictionary<string, object> { ["index"] = i }
        }).ToArray();

        await store.AddAsync(vectors);

        // Add many events
        for (int i = 0; i < 50; i++)
        {
            originalBranch = originalBranch.WithReasoning(new Draft($"Draft {i}"), $"prompt {i}");
        }

        // Act
        var snapshot = await BranchSnapshot.Capture(originalBranch);
        var restoredBranch = await snapshot.Restore();

        // Assert
        restoredBranch.Store.GetAll().Should().HaveCount(100);
        restoredBranch.Events.Should().HaveCount(50);
    }

    #endregion
}
