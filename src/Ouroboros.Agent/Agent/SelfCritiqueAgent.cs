// <copyright file="SelfCritiqueAgent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent;
/// <summary>
/// Agent that wraps LLM calls with self-critique and iterative improvement.
/// Implements Draft → Critique → Improve loop with configurable iterations.
/// </summary>
public sealed class SelfCritiqueAgent
{
    /// <summary>
    /// Maximum number of critique-improve cycles allowed.
    /// Set to 5 to balance thoroughness with performance and cost.
    /// </summary>
    private const int MaxIterations = 5;

    /// <summary>
    /// Default timeout per iteration to prevent long-running operations.
    /// Set to 30 seconds as a reasonable limit for LLM generation.
    /// </summary>
    private static readonly TimeSpan DefaultIterationTimeout = TimeSpan.FromSeconds(30);

    private readonly ToolAwareChatModel llm;
    private readonly ToolRegistry tools;
    private readonly IEmbeddingModel embed;
    private readonly TimeSpan iterationTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfCritiqueAgent"/> class.
    /// </summary>
    /// <param name="llm">The language model for generation</param>
    /// <param name="tools">Registry of available tools</param>
    /// <param name="embed">Embedding model for context retrieval</param>
    /// <param name="iterationTimeout">Optional timeout per iteration</param>
    public SelfCritiqueAgent(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        IEmbeddingModel embed,
        TimeSpan? iterationTimeout = null)
    {
        this.llm = llm;
        this.tools = tools;
        this.embed = embed;
        this.iterationTimeout = iterationTimeout ?? DefaultIterationTimeout;
    }

    /// <summary>
    /// Generates a response with self-critique and iterative improvement.
    /// </summary>
    /// <param name="branch">The initial pipeline branch</param>
    /// <param name="topic">The topic for reasoning</param>
    /// <param name="query">The query for RAG retrieval</param>
    /// <param name="iterations">Number of critique-improve cycles (capped at 5)</param>
    /// <param name="k">Number of documents to retrieve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the improved response with confidence rating</returns>
    public async Task<Result<SelfCritiqueResult, string>> GenerateWithCritiqueAsync(
        PipelineBranch branch,
        string topic,
        string query,
        int iterations = 1,
        int k = 8,
        CancellationToken ct = default)
    {
        try
        {
            // Validate and cap iterations
            int safeIterations = Math.Min(Math.Max(1, iterations), MaxIterations);

            // Generate initial draft
            Result<PipelineBranch, string> draftResult = await ExecuteWithTimeoutAsync(
                () => ReasoningArrows.SafeDraftArrow(this.llm, this.tools, this.embed, topic, query, k)(branch),
                this.iterationTimeout,
                ct);

            if (!draftResult.IsSuccess)
            {
                return Result<SelfCritiqueResult, string>.Failure(draftResult.Error);
            }

            PipelineBranch currentBranch = draftResult.Value;
            Draft? draft = currentBranch.Events.OfType<ReasoningStep>()
                .Select(e => e.State)
                .OfType<Draft>()
                .LastOrDefault();

            if (draft is null)
            {
                return Result<SelfCritiqueResult, string>.Failure("Failed to generate initial draft");
            }

            string lastCritique = string.Empty;
            string lastImprovement = draft.DraftText;

            // Perform critique-improve cycles
            for (int i = 0; i < safeIterations; i++)
            {
                // Generate critique
                Result<PipelineBranch, string> critiqueResult = await ExecuteWithTimeoutAsync(
                    () => ReasoningArrows.SafeCritiqueArrow(this.llm, this.tools, this.embed, topic, query, k)(currentBranch),
                    this.iterationTimeout,
                    ct);

                if (!critiqueResult.IsSuccess)
                {
                    return Result<SelfCritiqueResult, string>.Failure($"Critique failed at iteration {i + 1}: {critiqueResult.Error}");
                }

                currentBranch = critiqueResult.Value;
                Critique? critique = currentBranch.Events.OfType<ReasoningStep>()
                    .Select(e => e.State)
                    .OfType<Critique>()
                    .LastOrDefault();

                if (critique is null)
                {
                    return Result<SelfCritiqueResult, string>.Failure($"Failed to generate critique at iteration {i + 1}");
                }

                lastCritique = critique.CritiqueText;

                // Generate improvement
                Result<PipelineBranch, string> improveResult = await ExecuteWithTimeoutAsync(
                    () => ReasoningArrows.SafeImproveArrow(this.llm, this.tools, this.embed, topic, query, k)(currentBranch),
                    this.iterationTimeout,
                    ct);

                if (!improveResult.IsSuccess)
                {
                    return Result<SelfCritiqueResult, string>.Failure($"Improvement failed at iteration {i + 1}: {improveResult.Error}");
                }

                currentBranch = improveResult.Value;
                FinalSpec? finalSpec = currentBranch.Events.OfType<ReasoningStep>()
                    .Select(e => e.State)
                    .OfType<FinalSpec>()
                    .LastOrDefault();

                if (finalSpec is null)
                {
                    return Result<SelfCritiqueResult, string>.Failure($"Failed to generate improvement at iteration {i + 1}");
                }

                lastImprovement = finalSpec.Text;
            }

            // Compute confidence rating based on iterations and result quality
            ConfidenceRating confidence = ComputeConfidence(safeIterations, lastCritique);

            SelfCritiqueResult result = new SelfCritiqueResult(
                Draft: draft.DraftText,
                Critique: lastCritique,
                ImprovedResponse: lastImprovement,
                Confidence: confidence,
                IterationsPerformed: safeIterations,
                Branch: currentBranch);

            return Result<SelfCritiqueResult, string>.Success(result);
        }
        catch (OperationCanceledException)
        {
            return Result<SelfCritiqueResult, string>.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return Result<SelfCritiqueResult, string>.Failure($"Self-critique failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Quality indicators that suggest high confidence in the response.
    /// </summary>
    private static readonly string[] HighQualityIndicators =
    [
        "excellent",
        "well done",
        "high quality",
        "no major issues"
    ];

    /// <summary>
    /// Quality indicators that suggest low confidence in the response.
    /// </summary>
    private static readonly string[] LowQualityIndicators =
    [
        "needs work",
        "significant issues",
        "poor quality"
    ];

    /// <summary>
    /// Computes confidence rating based on iterations performed and critique content.
    /// </summary>
    private static ConfidenceRating ComputeConfidence(int iterations, string critique)
    {
        // Check if critique indicates high or low quality
        bool indicatesHighQuality = HighQualityIndicators.Any(indicator =>
            critique.Contains(indicator, StringComparison.OrdinalIgnoreCase));

        bool indicatesLowQuality = LowQualityIndicators.Any(indicator =>
            critique.Contains(indicator, StringComparison.OrdinalIgnoreCase));

        if (indicatesHighQuality && iterations >= 2)
        {
            return ConfidenceRating.High;
        }

        if (indicatesLowQuality)
        {
            return ConfidenceRating.Low;
        }

        // Default to medium for reasonable iterations
        return iterations >= 2 ? ConfidenceRating.Medium : ConfidenceRating.Low;
    }

    /// <summary>
    /// Executes an async operation with timeout support.
    /// </summary>
    private static async Task<Result<T, string>> ExecuteWithTimeoutAsync<T>(
        Func<Task<Result<T, string>>> operation,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result<T, string>.Failure($"Operation timed out after {timeout.TotalSeconds:F1} seconds");
        }
    }
}
