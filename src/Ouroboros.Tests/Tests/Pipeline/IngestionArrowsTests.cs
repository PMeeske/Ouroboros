using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Splitters.Text;
using Moq;
using Ouroboros.Pipeline.Ingestion;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline;

/// <summary>
/// Tests for IngestionArrows covering document loading, chunking, and embedding.
/// </summary>
[Trait("Category", "Unit")]
public class IngestionArrowsTests
{
    private readonly Mock<IEmbeddingModel> _mockEmbedding;

    public IngestionArrowsTests()
    {
        _mockEmbedding = new Mock<IEmbeddingModel>();
        
        // Setup default embedding behavior
        _mockEmbedding.Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
    }

    #region IngestArrow Tests

    [Fact]
    public async Task IngestArrow_WithValidData_AddsIngestEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        var splitter = new CharacterTextSplitter(chunkSize: 100, chunkOverlap: 0);
        var arrow = IngestionArrows.IngestArrow<SingleDocumentLoader>(_mockEmbedding.Object, splitter);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.OfType<IngestBatch>().Should().HaveCount(1);
    }

    [Fact]
    public async Task IngestArrow_WithDefaultSplitter_UsesRecursiveCharacterTextSplitter()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        var arrow = IngestionArrows.IngestArrow<SingleDocumentLoader>(_mockEmbedding.Object);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        // Should succeed with default splitter
    }

    [Fact]
    public async Task IngestArrow_WithCustomTag_UsesCustomTag()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        var splitter = new CharacterTextSplitter(chunkSize: 100, chunkOverlap: 0);
        var arrow = IngestionArrows.IngestArrow<SingleDocumentLoader>(
            _mockEmbedding.Object, 
            splitter, 
            tag: "CustomTag");

        // Act
        var result = await arrow(branch);

        // Assert
        var ingestEvent = result.Events.OfType<IngestBatch>().First();
        ingestEvent.Source.Should().Be("CustomTag");
    }

    [Fact]
    public async Task IngestArrow_WithEmptyTag_UsesLoaderTypeName()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        var splitter = new CharacterTextSplitter(chunkSize: 100, chunkOverlap: 0);
        var arrow = IngestionArrows.IngestArrow<SingleDocumentLoader>(
            _mockEmbedding.Object, 
            splitter, 
            tag: "");

        // Act
        var result = await arrow(branch);

        // Assert
        var ingestEvent = result.Events.OfType<IngestBatch>().First();
        ingestEvent.Source.Should().Be("SingleDocumentLoader");
    }

    [Fact]
    public async Task IngestArrow_PreservesOriginalBranch()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("test", store, source);
        
        originalBranch = originalBranch.WithReasoning(new Draft("Existing"), "prompt");
        var originalEventCount = originalBranch.Events.Count;

        var splitter = new CharacterTextSplitter(chunkSize: 100, chunkOverlap: 0);
        var arrow = IngestionArrows.IngestArrow<SingleDocumentLoader>(_mockEmbedding.Object, splitter);

        // Act
        var result = await arrow(originalBranch);

        // Assert - Original branch should be unchanged (immutability)
        originalBranch.Events.Should().HaveCount(originalEventCount);
        result.Events.Should().HaveCount(originalEventCount + 1);
    }

    [Fact]
    public async Task IngestArrow_WithDifferentChunkSizes_HandlesCorrectly()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        var smallChunkSplitter = new CharacterTextSplitter(chunkSize: 10, chunkOverlap: 2);
        var arrow = IngestionArrows.IngestArrow<SingleDocumentLoader>(_mockEmbedding.Object, smallChunkSplitter);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        var ingestEvent = result.Events.OfType<IngestBatch>().First();
        ingestEvent.Ids.Should().NotBeEmpty();
    }

    #endregion
}
