// <copyright file="SymbolicRetrievalStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Retrieval;

using LangChain.DocumentLoaders;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Result of a hybrid retrieval operation.
/// </summary>
/// <param name="Query">The original query.</param>
/// <param name="SymbolicMatches">Documents matched through symbolic (exact) reasoning.</param>
/// <param name="SemanticMatches">Documents matched through vector similarity.</param>
public sealed record HybridRetrievalResult(
    string Query,
    IReadOnlyList<string> SymbolicMatches,
    IReadOnlyList<Document> SemanticMatches)
{
    /// <summary>
    /// Gets all unique document IDs from both symbolic and semantic matches.
    /// Symbolic matches are prioritized.
    /// </summary>
    public IEnumerable<string> AllDocumentIds =>
        this.SymbolicMatches
            .Concat(this.SemanticMatches.Select(d => 
                d.Metadata.TryGetValue("id", out var id) ? id?.ToString() ?? d.PageContent[..Math.Min(50, d.PageContent.Length)] 
                    : d.PageContent[..Math.Min(50, d.PageContent.Length)]))
            .Distinct();
}

/// <summary>
/// Pipeline step that performs hybrid retrieval using both symbolic (MeTTa) and semantic (vector) search.
/// Symbolic queries provide exact matches for state-based queries, while vectors handle semantic similarity.
/// </summary>
public sealed class SymbolicRetrievalStep
{
    private readonly IMeTTaEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicRetrievalStep"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine for symbolic retrieval.</param>
    public SymbolicRetrievalStep(IMeTTaEngine engine)
    {
        this._engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Performs symbolic retrieval based on document status.
    /// </summary>
    /// <param name="status">The status to filter by (e.g., "Outdated", "Current").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching document identifiers.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> RetrieveByStatusAsync(
        string status,
        CancellationToken ct = default)
    {
        string query = $"!(match &self (Status $doc (State \"{status}\")) $doc)";
        return await this.ExecuteSymbolicQueryAsync(query, ct);
    }

    /// <summary>
    /// Performs symbolic retrieval based on document topic.
    /// </summary>
    /// <param name="topic">The topic to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching document identifiers.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> RetrieveByTopicAsync(
        string topic,
        CancellationToken ct = default)
    {
        string query = $"!(match &self (Topic $doc (Concept \"{topic}\")) $doc)";
        return await this.ExecuteSymbolicQueryAsync(query, ct);
    }

    /// <summary>
    /// Performs compound symbolic retrieval filtering by both status and topic.
    /// This is where symbolic reasoning excels over pure vector search.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <param name="topic">The topic to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching document identifiers.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> RetrieveByStatusAndTopicAsync(
        string status,
        string topic,
        CancellationToken ct = default)
    {
        // MeTTa compound query - exact match on both conditions
        string query = $@"!(match &self 
            (, 
              (Status $doc (State ""{status}""))
              (Topic $doc (Concept ""{topic}""))
            ) 
            $doc)";
        
        return await this.ExecuteSymbolicQueryAsync(query, ct);
    }

    /// <summary>
    /// Performs hybrid retrieval combining symbolic and semantic search.
    /// </summary>
    /// <param name="branch">The pipeline branch containing the vector store.</param>
    /// <param name="embedModel">The embedding model.</param>
    /// <param name="query">The natural language query.</param>
    /// <param name="status">Optional status filter for symbolic search.</param>
    /// <param name="topic">Optional topic filter for symbolic search.</param>
    /// <param name="semanticK">Number of semantic results to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A hybrid retrieval result.</returns>
    public async Task<Result<HybridRetrievalResult, string>> HybridRetrieveAsync(
        PipelineBranch branch,
        IEmbeddingModel embedModel,
        string query,
        string? status = null,
        string? topic = null,
        int semanticK = 5,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(embedModel);
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            // Step 1: Symbolic retrieval (exact matches)
            List<string> symbolicMatches = new();

            if (!string.IsNullOrEmpty(status) && !string.IsNullOrEmpty(topic))
            {
                Result<IReadOnlyList<string>, string> result = 
                    await this.RetrieveByStatusAndTopicAsync(status, topic, ct);
                
                if (result.IsSuccess)
                {
                    symbolicMatches.AddRange(result.Value);
                }
            }
            else if (!string.IsNullOrEmpty(status))
            {
                Result<IReadOnlyList<string>, string> result = 
                    await this.RetrieveByStatusAsync(status, ct);
                
                if (result.IsSuccess)
                {
                    symbolicMatches.AddRange(result.Value);
                }
            }
            else if (!string.IsNullOrEmpty(topic))
            {
                Result<IReadOnlyList<string>, string> result = 
                    await this.RetrieveByTopicAsync(topic, ct);
                
                if (result.IsSuccess)
                {
                    symbolicMatches.AddRange(result.Value);
                }
            }

            // Step 2: Semantic retrieval (vector similarity)
            IReadOnlyCollection<Document> semanticMatches = 
                await branch.Store.GetSimilarDocuments(embedModel, query, amount: semanticK);

            HybridRetrievalResult hybridResult = new(
                query,
                symbolicMatches,
                semanticMatches.ToList());

            return Result<HybridRetrievalResult, string>.Success(hybridResult);
        }
        catch (Exception ex)
        {
            return Result<HybridRetrievalResult, string>.Failure(
                $"Hybrid retrieval failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a Kleisli arrow for hybrid retrieval.
    /// </summary>
    /// <param name="embedModel">The embedding model.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="topic">Optional topic filter.</param>
    /// <param name="semanticK">Number of semantic results.</param>
    /// <returns>A step that performs hybrid retrieval.</returns>
    public Step<(PipelineBranch Branch, string Query), Result<HybridRetrievalResult, string>> AsArrow(
        IEmbeddingModel embedModel,
        string? status = null,
        string? topic = null,
        int semanticK = 5)
        => input => this.HybridRetrieveAsync(input.Branch, embedModel, input.Query, status, topic, semanticK);

    /// <summary>
    /// Executes a symbolic query and parses the results.
    /// </summary>
    private async Task<Result<IReadOnlyList<string>, string>> ExecuteSymbolicQueryAsync(
        string query,
        CancellationToken ct)
    {
        Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct);

        return result.Match(
            success => Result<IReadOnlyList<string>, string>.Success(this.ParseDocumentIds(success)),
            error => Result<IReadOnlyList<string>, string>.Failure(error));
    }

    /// <summary>
    /// Parses document IDs from MeTTa query results.
    /// </summary>
    private IReadOnlyList<string> ParseDocumentIds(string mettaResult)
    {
        List<string> ids = new();

        // MeTTa returns results in various formats like:
        // [(Doc "deployment.md")] or (Doc "deployment.md") or just document identifiers
        System.Text.RegularExpressions.Regex docPattern = 
            new(@"\(Doc\s+""([^""]+)""\)", System.Text.RegularExpressions.RegexOptions.Compiled);

        foreach (System.Text.RegularExpressions.Match match in docPattern.Matches(mettaResult))
        {
            if (match.Groups.Count > 1)
            {
                ids.Add(match.Groups[1].Value);
            }
        }

        // Also try simple string extraction for direct matches
        if (ids.Count == 0)
        {
            // Try to extract quoted strings
            System.Text.RegularExpressions.Regex quotedPattern = 
                new(@"""([^""]+)""", System.Text.RegularExpressions.RegexOptions.Compiled);
            
            foreach (System.Text.RegularExpressions.Match match in quotedPattern.Matches(mettaResult))
            {
                if (match.Groups.Count > 1)
                {
                    ids.Add(match.Groups[1].Value);
                }
            }
        }

        return ids;
    }
}
