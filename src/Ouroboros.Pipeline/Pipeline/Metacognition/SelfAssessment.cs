// <copyright file="SelfAssessment.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Metacognition;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;

/// <summary>
/// Represents the dimensions of performance that can be assessed.
/// Each dimension captures a distinct aspect of system capabilities.
/// </summary>
public enum PerformanceDimension
{
    /// <summary>
    /// Correctness and precision of outputs relative to ground truth.
    /// </summary>
    Accuracy,

    /// <summary>
    /// Response time and throughput efficiency.
    /// </summary>
    Speed,

    /// <summary>
    /// Novelty, originality, and divergent thinking in solutions.
    /// </summary>
    Creativity,

    /// <summary>
    /// Reliability and reproducibility of outputs across similar inputs.
    /// </summary>
    Consistency,

    /// <summary>
    /// Ability to handle new situations and transfer learning.
    /// </summary>
    Adaptability,

    /// <summary>
    /// Clarity, coherence, and effectiveness of communication.
    /// </summary>
    Communication,
}

/// <summary>
/// Represents the trend direction of a performance metric over time.
/// </summary>
public enum Trend
{
    /// <summary>
    /// Performance is getting better over time.
    /// </summary>
    Improving,

    /// <summary>
    /// Performance is remaining constant.
    /// </summary>
    Stable,

    /// <summary>
    /// Performance is getting worse over time.
    /// </summary>
    Declining,

    /// <summary>
    /// Performance shows high variance with no clear direction.
    /// </summary>
    Volatile,

    /// <summary>
    /// Insufficient data to determine trend.
    /// </summary>
    Unknown,
}

/// <summary>
/// Represents a score for a single performance dimension with confidence and evidence.
/// Follows Bayesian principles where scores represent posterior beliefs given evidence.
/// </summary>
/// <param name="Dimension">The performance dimension being scored.</param>
/// <param name="Score">The estimated score in [0, 1] where 1 is optimal performance.</param>
/// <param name="Confidence">The confidence in the score estimate in [0, 1].</param>
/// <param name="Evidence">Supporting evidence for the score (observations, metrics, etc.).</param>
/// <param name="Trend">The direction of change in this dimension over time.</param>
public sealed record DimensionScore(
    PerformanceDimension Dimension,
    double Score,
    double Confidence,
    ImmutableList<string> Evidence,
    Trend Trend)
{
    /// <summary>
    /// Creates an unknown dimension score with no evidence.
    /// Represents maximum entropy (complete uncertainty) about the dimension.
    /// </summary>
    /// <param name="dimension">The dimension to create an unknown score for.</param>
    /// <returns>A DimensionScore with 0.5 score, 0 confidence, and unknown trend.</returns>
    public static DimensionScore Unknown(PerformanceDimension dimension) => new(
        Dimension: dimension,
        Score: 0.5,  // Maximum entropy prior
        Confidence: 0.0,
        Evidence: ImmutableList<string>.Empty,
        Trend: Trend.Unknown);

    /// <summary>
    /// Creates a dimension score with initial evidence.
    /// </summary>
    /// <param name="dimension">The dimension being scored.</param>
    /// <param name="score">The initial score estimate.</param>
    /// <param name="confidence">The confidence in the estimate.</param>
    /// <param name="evidence">Initial evidence supporting the score.</param>
    /// <returns>A new DimensionScore with unknown trend.</returns>
    public static DimensionScore Create(
        PerformanceDimension dimension,
        double score,
        double confidence,
        IEnumerable<string> evidence) => new(
            Dimension: dimension,
            Score: Math.Clamp(score, 0.0, 1.0),
            Confidence: Math.Clamp(confidence, 0.0, 1.0),
            Evidence: evidence.ToImmutableList(),
            Trend: Trend.Unknown);

    /// <summary>
    /// Updates the score with new evidence using Bayesian update rules.
    /// </summary>
    /// <param name="newScore">The new observed score.</param>
    /// <param name="observationWeight">Weight of the new observation (0-1).</param>
    /// <param name="newEvidence">New evidence to add.</param>
    /// <returns>Updated DimensionScore with combined beliefs.</returns>
    public DimensionScore WithBayesianUpdate(
        double newScore,
        double observationWeight,
        string newEvidence)
    {
        // Bayesian update: weighted combination of prior and likelihood
        var priorWeight = Confidence;
        var totalWeight = priorWeight + observationWeight;

        var updatedScore = totalWeight > 0
            ? ((Score * priorWeight) + (newScore * observationWeight)) / totalWeight
            : newScore;

        // Confidence increases with more evidence but with diminishing returns
        var updatedConfidence = Math.Min(1.0, Confidence + (observationWeight * (1.0 - Confidence) * 0.1));

        // Determine trend from score change
        var scoreDelta = updatedScore - Score;
        var updatedTrend = DetermineTrend(scoreDelta, Trend);

        return this with
        {
            Score = Math.Clamp(updatedScore, 0.0, 1.0),
            Confidence = updatedConfidence,
            Evidence = Evidence.Add(newEvidence),
            Trend = updatedTrend,
        };
    }

    /// <summary>
    /// Validates the dimension score values.
    /// </summary>
    /// <returns>A Result indicating validity or validation error.</returns>
    public Result<Unit, string> Validate()
    {
        if (Score < 0.0 || Score > 1.0)
        {
            return Result<Unit, string>.Failure($"Score must be in [0, 1], got {Score}.");
        }

        if (Confidence < 0.0 || Confidence > 1.0)
        {
            return Result<Unit, string>.Failure($"Confidence must be in [0, 1], got {Confidence}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private static Trend DetermineTrend(double scoreDelta, Trend currentTrend)
    {
        const double threshold = 0.02;

        if (Math.Abs(scoreDelta) < threshold)
        {
            return currentTrend == Trend.Unknown ? Trend.Stable : currentTrend;
        }

        return scoreDelta > 0 ? Trend.Improving : Trend.Declining;
    }
}

/// <summary>
/// Represents a complete self-assessment result across all performance dimensions.
/// Aggregates individual dimension scores into a holistic evaluation.
/// </summary>
/// <param name="Id">Unique identifier for this assessment.</param>
/// <param name="AssessmentTime">When the assessment was performed.</param>
/// <param name="DimensionScores">Scores for each performance dimension.</param>
/// <param name="OverallScore">Weighted aggregate score across all dimensions.</param>
/// <param name="OverallConfidence">Aggregate confidence in the overall assessment.</param>
/// <param name="Strengths">Identified areas of strong performance.</param>
/// <param name="Weaknesses">Identified areas needing improvement.</param>
/// <param name="ImprovementAreas">Prioritized list of areas for development.</param>
public sealed record SelfAssessmentResult(
    Guid Id,
    DateTime AssessmentTime,
    ImmutableDictionary<PerformanceDimension, DimensionScore> DimensionScores,
    double OverallScore,
    double OverallConfidence,
    ImmutableList<string> Strengths,
    ImmutableList<string> Weaknesses,
    ImmutableList<string> ImprovementAreas)
{
    /// <summary>
    /// Creates an empty assessment with no data.
    /// </summary>
    /// <returns>An empty SelfAssessmentResult.</returns>
    public static SelfAssessmentResult Empty() => new(
        Id: Guid.NewGuid(),
        AssessmentTime: DateTime.UtcNow,
        DimensionScores: ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty,
        OverallScore: 0.5,
        OverallConfidence: 0.0,
        Strengths: ImmutableList<string>.Empty,
        Weaknesses: ImmutableList<string>.Empty,
        ImprovementAreas: ImmutableList<string>.Empty);

    /// <summary>
    /// Creates a new assessment from dimension scores.
    /// Automatically computes overall scores and identifies strengths/weaknesses.
    /// </summary>
    /// <param name="dimensionScores">The scores for each dimension.</param>
    /// <returns>A complete SelfAssessmentResult.</returns>
    public static SelfAssessmentResult FromDimensionScores(
        ImmutableDictionary<PerformanceDimension, DimensionScore> dimensionScores)
    {
        if (dimensionScores.IsEmpty)
        {
            return Empty();
        }

        // Compute weighted overall score (confidence-weighted mean)
        var totalWeight = dimensionScores.Values.Sum(s => s.Confidence);
        var overallScore = totalWeight > 0
            ? dimensionScores.Values.Sum(s => s.Score * s.Confidence) / totalWeight
            : 0.5;

        // Overall confidence is geometric mean of individual confidences
        var confidenceProduct = dimensionScores.Values.Aggregate(1.0, (acc, s) => acc * Math.Max(s.Confidence, 0.01));
        var overallConfidence = Math.Pow(confidenceProduct, 1.0 / dimensionScores.Count);

        // Identify strengths (high score with reasonable confidence)
        var strengths = dimensionScores.Values
            .Where(s => s.Score >= 0.7 && s.Confidence >= 0.3)
            .OrderByDescending(s => s.Score * s.Confidence)
            .Select(s => $"{s.Dimension}: {s.Score:P0} (confidence: {s.Confidence:P0})")
            .ToImmutableList();

        // Identify weaknesses (low score with reasonable confidence)
        var weaknesses = dimensionScores.Values
            .Where(s => s.Score < 0.5 && s.Confidence >= 0.3)
            .OrderBy(s => s.Score)
            .Select(s => $"{s.Dimension}: {s.Score:P0} (confidence: {s.Confidence:P0})")
            .ToImmutableList();

        // Prioritize improvement areas (declining trends or low scores with high impact potential)
        var improvementAreas = dimensionScores.Values
            .Where(s => s.Trend == Trend.Declining || s.Score < 0.6)
            .OrderBy(s => s.Score)
            .ThenBy(s => s.Trend == Trend.Declining ? 0 : 1)
            .Select(s => $"{s.Dimension} ({s.Trend})")
            .ToImmutableList();

        return new SelfAssessmentResult(
            Id: Guid.NewGuid(),
            AssessmentTime: DateTime.UtcNow,
            DimensionScores: dimensionScores,
            OverallScore: overallScore,
            OverallConfidence: overallConfidence,
            Strengths: strengths,
            Weaknesses: weaknesses,
            ImprovementAreas: improvementAreas);
    }

    /// <summary>
    /// Gets the score for a specific dimension if available.
    /// </summary>
    /// <param name="dimension">The dimension to query.</param>
    /// <returns>Some(score) if available, None otherwise.</returns>
    public Option<DimensionScore> GetDimensionScore(PerformanceDimension dimension)
        => DimensionScores.TryGetValue(dimension, out var score)
            ? Option<DimensionScore>.Some(score)
            : Option<DimensionScore>.None();

    /// <summary>
    /// Creates a copy with an updated dimension score.
    /// </summary>
    /// <param name="score">The new or updated dimension score.</param>
    /// <returns>A new SelfAssessmentResult with the updated score.</returns>
    public SelfAssessmentResult WithDimensionScore(DimensionScore score)
        => FromDimensionScores(DimensionScores.SetItem(score.Dimension, score));
}

/// <summary>
/// Represents a belief about a specific capability.
/// Uses Bayesian inference to maintain and update capability estimates.
/// </summary>
/// <param name="CapabilityName">The name identifying this capability.</param>
/// <param name="Proficiency">Estimated proficiency level in [0, 1].</param>
/// <param name="Uncertainty">Uncertainty in the estimate in [0, 1] (epistemic uncertainty).</param>
/// <param name="LastValidated">When this belief was last validated against evidence.</param>
/// <param name="ValidationCount">Number of times this belief has been validated.</param>
public sealed record CapabilityBelief(
    string CapabilityName,
    double Proficiency,
    double Uncertainty,
    DateTime LastValidated,
    int ValidationCount)
{
    /// <summary>
    /// Creates a new capability belief with maximum uncertainty (uninformative prior).
    /// </summary>
    /// <param name="capabilityName">The capability name.</param>
    /// <returns>A new CapabilityBelief with 0.5 proficiency and high uncertainty.</returns>
    public static CapabilityBelief Uninformative(string capabilityName) => new(
        CapabilityName: capabilityName,
        Proficiency: 0.5,
        Uncertainty: 1.0,
        LastValidated: DateTime.MinValue,
        ValidationCount: 0);

    /// <summary>
    /// Creates a capability belief with initial estimates.
    /// </summary>
    /// <param name="capabilityName">The capability name.</param>
    /// <param name="proficiency">Initial proficiency estimate.</param>
    /// <param name="uncertainty">Initial uncertainty.</param>
    /// <returns>A new CapabilityBelief.</returns>
    public static CapabilityBelief Create(
        string capabilityName,
        double proficiency,
        double uncertainty) => new(
            CapabilityName: capabilityName,
            Proficiency: Math.Clamp(proficiency, 0.0, 1.0),
            Uncertainty: Math.Clamp(uncertainty, 0.0, 1.0),
            LastValidated: DateTime.UtcNow,
            ValidationCount: 1);

    /// <summary>
    /// Updates the belief with new evidence using Bayesian inference.
    /// Uses a beta distribution model for proficiency estimation.
    /// </summary>
    /// <param name="observedSuccess">The observed success rate (0-1) from evidence.</param>
    /// <param name="sampleSize">Number of observations in the evidence.</param>
    /// <returns>Updated CapabilityBelief with posterior estimates.</returns>
    public CapabilityBelief WithBayesianUpdate(double observedSuccess, int sampleSize = 1)
    {
        // Beta distribution update: prior α, β mapped from proficiency and uncertainty
        // Prior pseudo-counts based on current belief strength
        var beliefStrength = Math.Max(1, ValidationCount);
        var priorAlpha = Proficiency * beliefStrength;
        var priorBeta = (1.0 - Proficiency) * beliefStrength;

        // Likelihood: binomial model
        var likelihoodAlpha = observedSuccess * sampleSize;
        var likelihoodBeta = (1.0 - observedSuccess) * sampleSize;

        // Posterior
        var posteriorAlpha = priorAlpha + likelihoodAlpha;
        var posteriorBeta = priorBeta + likelihoodBeta;
        var totalPseudoCount = posteriorAlpha + posteriorBeta;

        // Posterior mean
        var posteriorProficiency = posteriorAlpha / totalPseudoCount;

        // Posterior variance → uncertainty (scaled)
        var posteriorVariance = (posteriorAlpha * posteriorBeta) /
            (totalPseudoCount * totalPseudoCount * (totalPseudoCount + 1));
        var posteriorUncertainty = Math.Sqrt(posteriorVariance) * 3.46; // Scale to [0,1] range approximately

        return this with
        {
            Proficiency = Math.Clamp(posteriorProficiency, 0.0, 1.0),
            Uncertainty = Math.Clamp(posteriorUncertainty, 0.0, 1.0),
            LastValidated = DateTime.UtcNow,
            ValidationCount = ValidationCount + sampleSize,
        };
    }

    /// <summary>
    /// Computes the expected value with uncertainty bounds.
    /// Returns the proficiency with a credible interval.
    /// </summary>
    /// <param name="credibleLevel">The credible interval level (e.g., 0.95 for 95%).</param>
    /// <returns>Tuple of (lower bound, expected, upper bound).</returns>
    public (double Lower, double Expected, double Upper) GetCredibleInterval(double credibleLevel = 0.95)
    {
        // Approximate credible interval using normal approximation
        var z = credibleLevel switch
        {
            >= 0.99 => 2.576,
            >= 0.95 => 1.96,
            >= 0.90 => 1.645,
            _ => 1.0,
        };

        var margin = z * Uncertainty * 0.5;
        return (
            Lower: Math.Max(0.0, Proficiency - margin),
            Expected: Proficiency,
            Upper: Math.Min(1.0, Proficiency + margin));
    }

    /// <summary>
    /// Validates the capability belief values.
    /// </summary>
    /// <returns>A Result indicating validity or validation error.</returns>
    public Result<Unit, string> Validate()
    {
        if (string.IsNullOrWhiteSpace(CapabilityName))
        {
            return Result<Unit, string>.Failure("Capability name cannot be empty.");
        }

        if (Proficiency < 0.0 || Proficiency > 1.0)
        {
            return Result<Unit, string>.Failure($"Proficiency must be in [0, 1], got {Proficiency}.");
        }

        if (Uncertainty < 0.0 || Uncertainty > 1.0)
        {
            return Result<Unit, string>.Failure($"Uncertainty must be in [0, 1], got {Uncertainty}.");
        }

        if (ValidationCount < 0)
        {
            return Result<Unit, string>.Failure($"ValidationCount must be non-negative, got {ValidationCount}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}

/// <summary>
/// Interface for performing self-assessment of system capabilities and performance.
/// Implements introspective evaluation with Bayesian belief updates.
/// </summary>
public interface ISelfAssessor
{
    /// <summary>
    /// Performs a comprehensive self-assessment across all dimensions.
    /// </summary>
    /// <returns>A complete self-assessment result.</returns>
    Task<Result<SelfAssessmentResult, string>> AssessAsync();

    /// <summary>
    /// Assesses a single performance dimension.
    /// </summary>
    /// <param name="dimension">The dimension to assess.</param>
    /// <returns>The score for the specified dimension.</returns>
    Task<Result<DimensionScore, string>> AssessDimensionAsync(PerformanceDimension dimension);

    /// <summary>
    /// Queries the current belief about a specific capability.
    /// </summary>
    /// <param name="capability">The capability name to query.</param>
    /// <returns>The current capability belief if known.</returns>
    Option<CapabilityBelief> GetCapabilityBelief(string capability);

    /// <summary>
    /// Updates a capability belief with new evidence using Bayesian inference.
    /// </summary>
    /// <param name="capability">The capability to update.</param>
    /// <param name="evidence">The observed evidence (success rate 0-1).</param>
    /// <returns>The updated capability belief.</returns>
    Result<CapabilityBelief, string> UpdateBelief(string capability, double evidence);

    /// <summary>
    /// Returns all current capability beliefs.
    /// </summary>
    /// <returns>An immutable dictionary of all capability beliefs.</returns>
    ImmutableDictionary<string, CapabilityBelief> GetAllBeliefs();

    /// <summary>
    /// Calibrates confidence estimates using historical accuracy data.
    /// Adjusts confidence calibration based on predicted vs actual outcomes.
    /// </summary>
    /// <param name="samples">Pairs of (predicted confidence, actual success rate).</param>
    /// <returns>Unit on success, error message on failure.</returns>
    Result<Unit, string> CalibrateConfidence(IEnumerable<(double Predicted, double Actual)> samples);
}

/// <summary>
/// Bayesian self-assessor implementing ISelfAssessor with probabilistic belief updates.
/// Maintains and updates beliefs about capabilities using Bayesian inference.
/// </summary>
public sealed class BayesianSelfAssessor : ISelfAssessor
{
    private readonly ConcurrentDictionary<string, CapabilityBelief> capabilityBeliefs;
    private readonly ConcurrentDictionary<PerformanceDimension, DimensionScore> dimensionScores;
    private double confidenceCalibrationFactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="BayesianSelfAssessor"/> class.
    /// </summary>
    public BayesianSelfAssessor()
    {
        this.capabilityBeliefs = new ConcurrentDictionary<string, CapabilityBelief>();
        this.dimensionScores = new ConcurrentDictionary<PerformanceDimension, DimensionScore>();
        this.confidenceCalibrationFactor = 1.0;

        // Initialize dimension scores with uninformative priors
        foreach (var dimension in Enum.GetValues<PerformanceDimension>())
        {
            this.dimensionScores[dimension] = DimensionScore.Unknown(dimension);
        }
    }

    /// <summary>
    /// Initializes a new instance with pre-existing beliefs.
    /// </summary>
    /// <param name="initialBeliefs">Initial capability beliefs.</param>
    /// <param name="initialScores">Initial dimension scores.</param>
    public BayesianSelfAssessor(
        IEnumerable<CapabilityBelief> initialBeliefs,
        IEnumerable<DimensionScore> initialScores)
        : this()
    {
        foreach (var belief in initialBeliefs)
        {
            this.capabilityBeliefs[belief.CapabilityName] = belief;
        }

        foreach (var score in initialScores)
        {
            this.dimensionScores[score.Dimension] = score;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<SelfAssessmentResult, string>> AssessAsync()
    {
        try
        {
            var tasks = Enum.GetValues<PerformanceDimension>()
                .Select(d => AssessDimensionAsync(d))
                .ToList();

            var results = await Task.WhenAll(tasks);

            var scores = ImmutableDictionary.CreateBuilder<PerformanceDimension, DimensionScore>();
            foreach (var result in results)
            {
                if (result.IsSuccess)
                {
                    scores[result.Value.Dimension] = result.Value;
                }
            }

            var assessment = SelfAssessmentResult.FromDimensionScores(scores.ToImmutable());
            return Result<SelfAssessmentResult, string>.Success(assessment);
        }
        catch (Exception ex)
        {
            return Result<SelfAssessmentResult, string>.Failure($"Assessment failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<Result<DimensionScore, string>> AssessDimensionAsync(PerformanceDimension dimension)
    {
        try
        {
            var currentScore = this.dimensionScores.GetOrAdd(
                dimension,
                d => DimensionScore.Unknown(d));

            // Apply confidence calibration
            var calibratedScore = currentScore with
            {
                Confidence = Math.Min(1.0, currentScore.Confidence * this.confidenceCalibrationFactor),
            };

            return Task.FromResult(Result<DimensionScore, string>.Success(calibratedScore));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                Result<DimensionScore, string>.Failure($"Dimension assessment failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Option<CapabilityBelief> GetCapabilityBelief(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability))
        {
            return Option<CapabilityBelief>.None();
        }

        return this.capabilityBeliefs.TryGetValue(capability, out var belief)
            ? Option<CapabilityBelief>.Some(belief)
            : Option<CapabilityBelief>.None();
    }

    /// <inheritdoc/>
    public Result<CapabilityBelief, string> UpdateBelief(string capability, double evidence)
    {
        if (string.IsNullOrWhiteSpace(capability))
        {
            return Result<CapabilityBelief, string>.Failure("Capability name cannot be empty.");
        }

        if (evidence < 0.0 || evidence > 1.0)
        {
            return Result<CapabilityBelief, string>.Failure($"Evidence must be in [0, 1], got {evidence}.");
        }

        try
        {
            var updatedBelief = this.capabilityBeliefs.AddOrUpdate(
                capability,
                _ => CapabilityBelief.Uninformative(capability).WithBayesianUpdate(evidence),
                (_, existing) => existing.WithBayesianUpdate(evidence));

            return Result<CapabilityBelief, string>.Success(updatedBelief);
        }
        catch (Exception ex)
        {
            return Result<CapabilityBelief, string>.Failure($"Belief update failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public ImmutableDictionary<string, CapabilityBelief> GetAllBeliefs()
        => this.capabilityBeliefs.ToImmutableDictionary();

    /// <inheritdoc/>
    public Result<Unit, string> CalibrateConfidence(IEnumerable<(double Predicted, double Actual)> samples)
    {
        try
        {
            var sampleList = samples.ToList();
            if (sampleList.Count == 0)
            {
                return Result<Unit, string>.Success(Unit.Value);
            }

            // Compute calibration error (Expected Calibration Error approach)
            // If predictions are overconfident, reduce calibration factor
            // If underconfident, increase it

            var totalError = 0.0;
            var totalBias = 0.0;

            foreach (var (predicted, actual) in sampleList)
            {
                var clampedPredicted = Math.Clamp(predicted, 0.0, 1.0);
                var clampedActual = Math.Clamp(actual, 0.0, 1.0);

                totalError += Math.Abs(clampedPredicted - clampedActual);
                totalBias += clampedPredicted - clampedActual; // Positive = overconfident
            }

            var meanBias = totalBias / sampleList.Count;

            // Adjust calibration factor based on bias
            // Overconfident → reduce factor, Underconfident → increase factor
            var adjustment = 1.0 - (meanBias * 0.5); // Gradual adjustment
            this.confidenceCalibrationFactor = Math.Clamp(
                this.confidenceCalibrationFactor * adjustment,
                0.1,
                2.0);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Calibration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates a dimension score with new evidence.
    /// </summary>
    /// <param name="dimension">The dimension to update.</param>
    /// <param name="observedScore">The observed score value.</param>
    /// <param name="weight">The weight of the observation.</param>
    /// <param name="evidence">Evidence description.</param>
    /// <returns>The updated dimension score.</returns>
    public Result<DimensionScore, string> UpdateDimensionScore(
        PerformanceDimension dimension,
        double observedScore,
        double weight,
        string evidence)
    {
        try
        {
            var updatedScore = this.dimensionScores.AddOrUpdate(
                dimension,
                d => DimensionScore.Unknown(d).WithBayesianUpdate(observedScore, weight, evidence),
                (_, existing) => existing.WithBayesianUpdate(observedScore, weight, evidence));

            return Result<DimensionScore, string>.Success(updatedScore);
        }
        catch (Exception ex)
        {
            return Result<DimensionScore, string>.Failure($"Dimension score update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current confidence calibration factor.
    /// </summary>
    /// <returns>The calibration factor applied to confidence values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetCalibrationFactor() => this.confidenceCalibrationFactor;
}

/// <summary>
/// Provides Kleisli arrows for self-assessment operations in the pipeline.
/// Enables functional composition of self-assessment with other pipeline operations.
/// </summary>
public static class SelfAssessmentArrow
{
    /// <summary>
    /// Creates a Kleisli arrow that performs a complete self-assessment.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from Unit to SelfAssessmentResult.</returns>
    public static KleisliResult<Unit, SelfAssessmentResult, string> AssessArrow(ISelfAssessor assessor)
        => async _ => await assessor.AssessAsync();

    /// <summary>
    /// Creates a Kleisli arrow that assesses a specific dimension.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from PerformanceDimension to DimensionScore.</returns>
    public static KleisliResult<PerformanceDimension, DimensionScore, string> AssessDimensionArrow(
        ISelfAssessor assessor)
        => async dimension => await assessor.AssessDimensionAsync(dimension);

    /// <summary>
    /// Creates a Kleisli arrow that updates a capability belief with evidence.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from (capability, evidence) to CapabilityBelief.</returns>
    public static KleisliResult<(string Capability, double Evidence), CapabilityBelief, string> UpdateBeliefArrow(
        ISelfAssessor assessor)
        => input => Task.FromResult(assessor.UpdateBelief(input.Capability, input.Evidence));

    /// <summary>
    /// Creates a Kleisli arrow that queries a capability belief.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from capability name to optional CapabilityBelief.</returns>
    public static KleisliOption<string, CapabilityBelief> GetBeliefArrow(ISelfAssessor assessor)
        => capability => Task.FromResult(assessor.GetCapabilityBelief(capability));

    /// <summary>
    /// Creates a Kleisli arrow that calibrates confidence with sample data.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <returns>A Kleisli arrow from calibration samples to Unit.</returns>
    public static KleisliResult<IEnumerable<(double Predicted, double Actual)>, Unit, string> CalibrateArrow(
        ISelfAssessor assessor)
        => samples => Task.FromResult(assessor.CalibrateConfidence(samples));

    /// <summary>
    /// Creates a composed arrow that assesses and then updates beliefs based on results.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <param name="capabilityExtractor">Function to extract capability updates from assessment.</param>
    /// <returns>A composed Kleisli arrow.</returns>
    public static KleisliResult<Unit, SelfAssessmentResult, string> AssessAndUpdateArrow(
        ISelfAssessor assessor,
        Func<SelfAssessmentResult, IEnumerable<(string Capability, double Evidence)>> capabilityExtractor)
        => async _ =>
        {
            var assessmentResult = await assessor.AssessAsync();

            if (assessmentResult.IsFailure)
            {
                return assessmentResult;
            }

            var assessment = assessmentResult.Value;
            var updates = capabilityExtractor(assessment);

            foreach (var (capability, evidence) in updates)
            {
                var updateResult = assessor.UpdateBelief(capability, evidence);
                if (updateResult.IsFailure)
                {
                    // Log but continue - partial updates are acceptable
                }
            }

            return Result<SelfAssessmentResult, string>.Success(assessment);
        };

    /// <summary>
    /// Creates an arrow that performs assessment with automatic confidence calibration.
    /// </summary>
    /// <param name="assessor">The self-assessor to use.</param>
    /// <param name="historicalData">Historical prediction/actual pairs for calibration.</param>
    /// <returns>A Kleisli arrow that calibrates then assesses.</returns>
    public static KleisliResult<Unit, SelfAssessmentResult, string> CalibratedAssessArrow(
        ISelfAssessor assessor,
        IEnumerable<(double Predicted, double Actual)> historicalData)
        => async _ =>
        {
            // First calibrate
            var calibrationResult = assessor.CalibrateConfidence(historicalData);
            if (calibrationResult.IsFailure)
            {
                return Result<SelfAssessmentResult, string>.Failure(calibrationResult.Error);
            }

            // Then assess with calibrated confidence
            return await assessor.AssessAsync();
        };
}

/// <summary>
/// Represents a unit type (void equivalent in functional programming).
/// Used to indicate successful completion without a meaningful return value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The singleton instance of Unit.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    /// Determines equality with another Unit instance.
    /// All Unit instances are equal.
    /// </summary>
    /// <param name="other">The other Unit instance.</param>
    /// <returns>Always true.</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if obj is a Unit instance.</returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Gets the hash code for Unit.
    /// </summary>
    /// <returns>Always returns 0.</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns a string representation of Unit.
    /// </summary>
    /// <returns>The string "()".</returns>
    public override string ToString() => "()";

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always true.</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always false.</returns>
    public static bool operator !=(Unit left, Unit right) => false;
}
