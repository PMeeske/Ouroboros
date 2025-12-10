// <copyright file="SymbolicIngestionStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Ingestion;

using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChainPipeline.Pipeline.Extraction;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Result of symbolic ingestion containing both vector and symbolic representations.
/// </summary>
/// <param name="DocumentId">The document identifier.</param>
/// <param name="VectorIds">The IDs of vectors stored in the vector store.</param>
/// <param name="Triples">The semantic triples stored in the AtomSpace.</param>
public sealed record SymbolicIngestionResult(
    string DocumentId,
    IReadOnlyList<string> VectorIds,
    IReadOnlyList<SemanticTriple> Triples);

/// <summary>
/// Pipeline step that ingests documents into both vector store and MeTTa AtomSpace.
/// Combines semantic embeddings with symbolic triple extraction for hybrid retrieval.
/// </summary>
public sealed class SymbolicIngestionStep
{
    private readonly IMeTTaEngine _engine;
    private readonly TripleExtractionStep _extractor;
    private readonly IEmbeddingModel _embedModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicIngestionStep"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine for symbolic storage.</param>
    /// <param name="extractor">The triple extraction step.</param>
    /// <param name="embedModel">The embedding model for vectorization.</param>
    public SymbolicIngestionStep(
        IMeTTaEngine engine,
        TripleExtractionStep extractor,
        IEmbeddingModel embedModel)
    {
        this._engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this._extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        this._embedModel = embedModel ?? throw new ArgumentNullException(nameof(embedModel));
    }

    /// <summary>
    /// Ingests a document into the pipeline branch with symbolic triple extraction.
    /// </summary>
    /// <param name="branch">The pipeline branch to update.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="content">The document content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the updated branch and ingestion result.</returns>
    public async Task<Result<(PipelineBranch Branch, SymbolicIngestionResult Result), string>> IngestAsync(
        PipelineBranch branch,
        string documentId,
        string content,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            // Step 1: Extract semantic triples from the document
            Result<ExtractionResult, string> extractionResult = await this._extractor.ExtractAsync(
                documentId,
                content,
                ct);

            if (extractionResult.IsFailure)
            {
                return Result<(PipelineBranch, SymbolicIngestionResult), string>.Failure(extractionResult.Error);
            }

            List<SemanticTriple> triples = extractionResult.Value.Triples.ToList();

            // Step 2: Add triples to MeTTa AtomSpace
            foreach (SemanticTriple triple in triples)
            {
                string fact = triple.ToMeTTaFact();
                Result<Unit, string> addResult = await this._engine.AddFactAsync(fact, ct);
                
                if (addResult.IsFailure)
                {
                    // Continue processing - individual fact failures shouldn't fail entire ingestion
                    // Errors are tracked but not blocking
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to add fact: {addResult.Error}");
                }
            }

            // Step 3: Add to vector store for semantic search
            List<string> vectorIds = new();
            Vector vector = new()
            {
                Id = documentId,
                Text = content,
                Embedding = await this._embedModel.CreateEmbeddingsAsync(content, ct),
            };
            
            await branch.Store.AddAsync(new[] { vector });
            vectorIds.Add(documentId);

            // Step 4: Update branch with ingest event
            PipelineBranch updatedBranch = branch.WithIngestEvent($"symbolic:{documentId}", vectorIds);

            SymbolicIngestionResult result = new(documentId, vectorIds, triples);
            return Result<(PipelineBranch, SymbolicIngestionResult), string>.Success((updatedBranch, result));
        }
        catch (Exception ex)
        {
            return Result<(PipelineBranch, SymbolicIngestionResult), string>.Failure(
                $"Symbolic ingestion failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates an arrow for symbolic ingestion.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="content">The document content.</param>
    /// <returns>A step that ingests documents symbolically.</returns>
    public Step<PipelineBranch, Result<(PipelineBranch, SymbolicIngestionResult), string>> AsArrow(
        string documentId,
        string content)
        => branch => this.IngestAsync(branch, documentId, content);
}
