// <copyright file="ReflectiveReasoning.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Metacognition;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;

/// <summary>
/// Represents the type of a reasoning step in a logical chain.
/// Each type captures a distinct cognitive operation in the reasoning process.
/// </summary>
public enum ReasoningStepType
{
    /// <summary>
    /// Direct perception or data gathering from available information.
    /// </summary>
    Observation,

    /// <summary>
    /// Logical derivation from observations or prior inferences.
    /// </summary>
    Inference,

    /// <summary>
    /// A tentative explanation or prediction to be validated.
    /// </summary>
    Hypothesis,

    /// <summary>
    /// Testing or verification of a hypothesis or inference.
    /// </summary>
    Validation,

    /// <summary>
    /// Modification of prior reasoning based on new evidence.
    /// </summary>
    Revision,

    /// <summary>
    /// An accepted premise without direct evidence.
    /// </summary>
    Assumption,

    /// <summary>
    /// A final determination reached through reasoning.
    /// </summary>
    Conclusion,

    /// <summary>
    /// Detection of inconsistency between reasoning steps.
    /// </summary>
    Contradiction,
}

/// <summary>
/// Represents a single step in a reasoning process.
/// Captures the content, justification, and dependencies of a logical step.
/// </summary>
/// <param name="StepNumber">Sequential number of this step in the trace.</param>
/// <param name="StepType">The type of reasoning operation performed.</param>
/// <param name="Content">The actual content or claim of this reasoning step.</param>
/// <param name="Justification">The rationale or supporting argument for this step.</param>
/// <param name="Timestamp">When this step was performed.</param>
/// <param name="Dependencies">References to earlier step numbers this step depends on.</param>
public sealed record ReasoningStep(
    int StepNumber,
    ReasoningStepType StepType,
    string Content,
    string Justification,
    DateTime Timestamp,
    ImmutableList<int> Dependencies)
{
    /// <summary>
    /// Creates an observation step with no dependencies.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The observed content.</param>
    /// <param name="justification">Why this observation is relevant.</param>
    /// <returns>A new observation reasoning step.</returns>
    public static ReasoningStep Observation(int stepNumber, string content, string justification) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Observation,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: ImmutableList<int>.Empty);

    /// <summary>
    /// Creates an inference step from prior steps.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The inferred content.</param>
    /// <param name="justification">The logical justification for the inference.</param>
    /// <param name="dependencies">Step numbers this inference depends on.</param>
    /// <returns>A new inference reasoning step.</returns>
    public static ReasoningStep Inference(int stepNumber, string content, string justification, params int[] dependencies) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Inference,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: dependencies.ToImmutableList());

    /// <summary>
    /// Creates a hypothesis step.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The hypothesized content.</param>
    /// <param name="justification">Why this hypothesis is worth considering.</param>
    /// <param name="dependencies">Step numbers that motivated this hypothesis.</param>
    /// <returns>A new hypothesis reasoning step.</returns>
    public static ReasoningStep Hypothesis(int stepNumber, string content, string justification, params int[] dependencies) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Hypothesis,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: dependencies.ToImmutableList());

    /// <summary>
    /// Creates a conclusion step from prior reasoning.
    /// </summary>
    /// <param name="stepNumber">The step number in the trace.</param>
    /// <param name="content">The concluded content.</param>
    /// <param name="justification">How this conclusion was reached.</param>
    /// <param name="dependencies">Step numbers supporting this conclusion.</param>
    /// <returns>A new conclusion reasoning step.</returns>
    public static ReasoningStep Conclusion(int stepNumber, string content, string justification, params int[] dependencies) => new(
        StepNumber: stepNumber,
        StepType: ReasoningStepType.Conclusion,
        Content: content,
        Justification: justification,
        Timestamp: DateTime.UtcNow,
        Dependencies: dependencies.ToImmutableList());

    /// <summary>
    /// Adds a dependency to this step.
    /// </summary>
    /// <param name="stepNumber">The step number to depend on.</param>
    /// <returns>A new ReasoningStep with the added dependency.</returns>
    public ReasoningStep WithDependency(int stepNumber)
        => this with { Dependencies = Dependencies.Add(stepNumber) };

    /// <summary>
    /// Validates that all dependencies reference earlier steps.
    /// </summary>
    /// <returns>True if all dependencies are valid (reference earlier steps).</returns>
    public bool HasValidDependencies()
        => Dependencies.All(d => d > 0 && d < StepNumber);
}

/// <summary>
/// Represents a complete trace of a reasoning process from start to conclusion.
/// Immutable record capturing the full logical chain for analysis and reflection.
/// </summary>
/// <param name="Id">Unique identifier for this reasoning trace.</param>
/// <param name="StartTime">When the reasoning process began.</param>
/// <param name="EndTime">When the reasoning process concluded (null if still active).</param>
/// <param name="Steps">The ordered list of reasoning steps.</param>
/// <param name="FinalConclusion">The final conclusion reached (null if incomplete or failed).</param>
/// <param name="Confidence">The confidence level in the conclusion (0.0 to 1.0).</param>
/// <param name="WasSuccessful">Whether the reasoning process reached a valid conclusion.</param>
public sealed record ReasoningTrace(
    Guid Id,
    DateTime StartTime,
    DateTime? EndTime,
    ImmutableList<ReasoningStep> Steps,
    string? FinalConclusion,
    double Confidence,
    bool WasSuccessful)
{
    /// <summary>
    /// Creates a new reasoning trace ready to begin recording.
    /// </summary>
    /// <returns>A new empty ReasoningTrace.</returns>
    public static ReasoningTrace Start() => new(
        Id: Guid.NewGuid(),
        StartTime: DateTime.UtcNow,
        EndTime: null,
        Steps: ImmutableList<ReasoningStep>.Empty,
        FinalConclusion: null,
        Confidence: 0.0,
        WasSuccessful: false);

    /// <summary>
    /// Creates a reasoning trace with an existing identifier.
    /// </summary>
    /// <param name="id">The identifier to use.</param>
    /// <returns>A new empty ReasoningTrace with the specified ID.</returns>
    public static ReasoningTrace StartWithId(Guid id) => new(
        Id: id,
        StartTime: DateTime.UtcNow,
        EndTime: null,
        Steps: ImmutableList<ReasoningStep>.Empty,
        FinalConclusion: null,
        Confidence: 0.0,
        WasSuccessful: false);

    /// <summary>
    /// Gets the next step number for this trace.
    /// </summary>
    public int NextStepNumber => Steps.Count + 1;

    /// <summary>
    /// Gets the duration of the reasoning process if completed.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Gets whether this trace is still active (not yet concluded).
    /// </summary>
    public bool IsActive => !EndTime.HasValue;

    /// <summary>
    /// Adds a reasoning step to the trace.
    /// </summary>
    /// <param name="step">The step to add.</param>
    /// <returns>A new ReasoningTrace with the added step.</returns>
    public ReasoningTrace WithStep(ReasoningStep step)
        => this with { Steps = Steps.Add(step) };

    /// <summary>
    /// Adds an observation step to the trace.
    /// </summary>
    /// <param name="content">The observed content.</param>
    /// <param name="justification">Why this observation is relevant.</param>
    /// <returns>A new ReasoningTrace with the added observation.</returns>
    public ReasoningTrace AddObservation(string content, string justification)
        => WithStep(ReasoningStep.Observation(NextStepNumber, content, justification));

    /// <summary>
    /// Adds an inference step to the trace.
    /// </summary>
    /// <param name="content">The inferred content.</param>
    /// <param name="justification">The logical justification.</param>
    /// <param name="dependencies">Step numbers this inference depends on.</param>
    /// <returns>A new ReasoningTrace with the added inference.</returns>
    public ReasoningTrace AddInference(string content, string justification, params int[] dependencies)
        => WithStep(ReasoningStep.Inference(NextStepNumber, content, justification, dependencies));

    /// <summary>
    /// Adds a hypothesis step to the trace.
    /// </summary>
    /// <param name="content">The hypothesized content.</param>
    /// <param name="justification">Why this hypothesis is worth considering.</param>
    /// <param name="dependencies">Step numbers that motivated this hypothesis.</param>
    /// <returns>A new ReasoningTrace with the added hypothesis.</returns>
    public ReasoningTrace AddHypothesis(string content, string justification, params int[] dependencies)
        => WithStep(ReasoningStep.Hypothesis(NextStepNumber, content, justification, dependencies));

    /// <summary>
    /// Completes the reasoning trace with a conclusion.
    /// </summary>
    /// <param name="conclusion">The final conclusion.</param>
    /// <param name="confidence">Confidence in the conclusion (0.0 to 1.0).</param>
    /// <param name="success">Whether the reasoning was successful.</param>
    /// <returns>A completed ReasoningTrace.</returns>
    public ReasoningTrace Complete(string conclusion, double confidence, bool success = true)
    {
        var conclusionStep = ReasoningStep.Conclusion(
            NextStepNumber,
            conclusion,
            $"Concluded with {confidence:P0} confidence",
            Steps.Select(s => s.StepNumber).ToArray());

        return this with
        {
            EndTime = DateTime.UtcNow,
            Steps = Steps.Add(conclusionStep),
            FinalConclusion = conclusion,
            Confidence = Math.Clamp(confidence, 0.0, 1.0),
            WasSuccessful = success,
        };
    }

    /// <summary>
    /// Marks the reasoning trace as failed without a conclusion.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <returns>A failed ReasoningTrace.</returns>
    public ReasoningTrace Fail(string reason) => this with
    {
        EndTime = DateTime.UtcNow,
        FinalConclusion = $"Failed: {reason}",
        Confidence = 0.0,
        WasSuccessful = false,
    };

    /// <summary>
    /// Gets all steps of a specific type.
    /// </summary>
    /// <param name="stepType">The type of steps to retrieve.</param>
    /// <returns>An enumerable of matching steps.</returns>
    public IEnumerable<ReasoningStep> GetStepsByType(ReasoningStepType stepType)
        => Steps.Where(s => s.StepType == stepType);

    /// <summary>
    /// Validates the logical consistency of the trace.
    /// </summary>
    /// <returns>True if all dependencies are valid and the trace is logically consistent.</returns>
    public bool IsLogicallyConsistent()
        => Steps.All(s => s.HasValidDependencies());
}

/// <summary>
/// Represents the result of reflecting on a reasoning trace.
/// Captures quality metrics, identified issues, and improvement suggestions.
/// </summary>
/// <param name="OriginalTrace">The reasoning trace that was analyzed.</param>
/// <param name="QualityScore">Overall quality score (0.0 to 1.0).</param>
/// <param name="LogicalSoundness">Score for logical consistency and validity (0.0 to 1.0).</param>
/// <param name="EvidenceSupport">Score for how well conclusions are supported by evidence (0.0 to 1.0).</param>
/// <param name="IdentifiedFallacies">List of logical fallacies detected in the reasoning.</param>
/// <param name="MissedConsiderations">Factors that should have been considered but weren't.</param>
/// <param name="AlternativeConclusions">Other valid conclusions that could have been drawn.</param>
/// <param name="Improvements">Specific suggestions for improving the reasoning.</param>
public sealed record ReflectionResult(
    ReasoningTrace OriginalTrace,
    double QualityScore,
    double LogicalSoundness,
    double EvidenceSupport,
    ImmutableList<string> IdentifiedFallacies,
    ImmutableList<string> MissedConsiderations,
    ImmutableList<string> AlternativeConclusions,
    ImmutableList<string> Improvements)
{
    /// <summary>
    /// Creates a reflection result indicating high-quality reasoning.
    /// </summary>
    /// <param name="trace">The original trace.</param>
    /// <returns>A positive reflection result.</returns>
    public static ReflectionResult HighQuality(ReasoningTrace trace) => new(
        OriginalTrace: trace,
        QualityScore: 0.9,
        LogicalSoundness: 0.95,
        EvidenceSupport: 0.85,
        IdentifiedFallacies: ImmutableList<string>.Empty,
        MissedConsiderations: ImmutableList<string>.Empty,
        AlternativeConclusions: ImmutableList<string>.Empty,
        Improvements: ImmutableList<string>.Empty);

    /// <summary>
    /// Creates a reflection result for an empty or invalid trace.
    /// </summary>
    /// <param name="trace">The original trace.</param>
    /// <returns>A reflection result indicating invalid reasoning.</returns>
    public static ReflectionResult Invalid(ReasoningTrace trace) => new(
        OriginalTrace: trace,
        QualityScore: 0.0,
        LogicalSoundness: 0.0,
        EvidenceSupport: 0.0,
        IdentifiedFallacies: ImmutableList.Create("Invalid or empty reasoning trace"),
        MissedConsiderations: ImmutableList<string>.Empty,
        AlternativeConclusions: ImmutableList<string>.Empty,
        Improvements: ImmutableList.Create("Provide a complete reasoning trace with observations, inferences, and a conclusion"));

    /// <summary>
    /// Gets whether the reasoning quality meets a minimum threshold.
    /// </summary>
    /// <param name="threshold">The minimum acceptable quality score.</param>
    /// <returns>True if quality meets or exceeds the threshold.</returns>
    public bool MeetsQualityThreshold(double threshold = 0.7)
        => QualityScore >= threshold;

    /// <summary>
    /// Gets whether there are any identified issues with the reasoning.
    /// </summary>
    public bool HasIssues => IdentifiedFallacies.Count > 0 || MissedConsiderations.Count > 0;

    /// <summary>
    /// Adds an identified fallacy to the result.
    /// </summary>
    /// <param name="fallacy">The fallacy to add.</param>
    /// <returns>A new ReflectionResult with the added fallacy.</returns>
    public ReflectionResult WithFallacy(string fallacy)
        => this with { IdentifiedFallacies = IdentifiedFallacies.Add(fallacy) };

    /// <summary>
    /// Adds a missed consideration to the result.
    /// </summary>
    /// <param name="consideration">The consideration to add.</param>
    /// <returns>A new ReflectionResult with the added consideration.</returns>
    public ReflectionResult WithMissedConsideration(string consideration)
        => this with { MissedConsiderations = MissedConsiderations.Add(consideration) };

    /// <summary>
    /// Adds an improvement suggestion to the result.
    /// </summary>
    /// <param name="improvement">The improvement to add.</param>
    /// <returns>A new ReflectionResult with the added improvement.</returns>
    public ReflectionResult WithImprovement(string improvement)
        => this with { Improvements = Improvements.Add(improvement) };
}

/// <summary>
/// Represents a characterization of thinking style based on reasoning patterns.
/// Captures the balance between different cognitive approaches.
/// </summary>
/// <param name="StyleName">Descriptive name for this thinking style profile.</param>
/// <param name="AnalyticalScore">Score for systematic, logical analysis (0.0 to 1.0).</param>
/// <param name="CreativeScore">Score for novel, divergent thinking (0.0 to 1.0).</param>
/// <param name="SystematicScore">Score for structured, methodical approach (0.0 to 1.0).</param>
/// <param name="IntuitiveScore">Score for quick, pattern-based judgments (0.0 to 1.0).</param>
/// <param name="BiasProfile">Map of identified biases to their strength (0.0 to 1.0).</param>
public sealed record ThinkingStyle(
    string StyleName,
    double AnalyticalScore,
    double CreativeScore,
    double SystematicScore,
    double IntuitiveScore,
    ImmutableDictionary<string, double> BiasProfile)
{
    /// <summary>
    /// Creates a balanced thinking style with no detected biases.
    /// </summary>
    /// <returns>A balanced ThinkingStyle.</returns>
    public static ThinkingStyle Balanced() => new(
        StyleName: "Balanced",
        AnalyticalScore: 0.5,
        CreativeScore: 0.5,
        SystematicScore: 0.5,
        IntuitiveScore: 0.5,
        BiasProfile: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Creates an analytical thinking style profile.
    /// </summary>
    /// <returns>An analytically-oriented ThinkingStyle.</returns>
    public static ThinkingStyle Analytical() => new(
        StyleName: "Analytical",
        AnalyticalScore: 0.85,
        CreativeScore: 0.35,
        SystematicScore: 0.75,
        IntuitiveScore: 0.25,
        BiasProfile: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Creates a creative thinking style profile.
    /// </summary>
    /// <returns>A creatively-oriented ThinkingStyle.</returns>
    public static ThinkingStyle Creative() => new(
        StyleName: "Creative",
        AnalyticalScore: 0.4,
        CreativeScore: 0.9,
        SystematicScore: 0.3,
        IntuitiveScore: 0.7,
        BiasProfile: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Gets the dominant cognitive dimension.
    /// </summary>
    public string DominantDimension
    {
        get
        {
            var scores = new (string Name, double Score)[]
            {
                ("Analytical", AnalyticalScore),
                ("Creative", CreativeScore),
                ("Systematic", SystematicScore),
                ("Intuitive", IntuitiveScore),
            };
            return scores.MaxBy(s => s.Score).Name;
        }
    }

    /// <summary>
    /// Gets whether there are significant detected biases.
    /// </summary>
    /// <param name="threshold">The threshold above which a bias is considered significant.</param>
    /// <returns>True if any bias exceeds the threshold.</returns>
    public bool HasSignificantBiases(double threshold = 0.5)
        => BiasProfile.Values.Any(v => v > threshold);

    /// <summary>
    /// Gets the most significant biases in the profile.
    /// </summary>
    /// <param name="threshold">The minimum bias strength to include.</param>
    /// <returns>Biases that exceed the threshold, ordered by strength.</returns>
    public IEnumerable<(string Bias, double Strength)> GetSignificantBiases(double threshold = 0.3)
        => BiasProfile
            .Where(kv => kv.Value > threshold)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => (kv.Key, kv.Value));

    /// <summary>
    /// Adds or updates a bias in the profile.
    /// </summary>
    /// <param name="biasName">The name of the bias.</param>
    /// <param name="strength">The strength of the bias (0.0 to 1.0).</param>
    /// <returns>A new ThinkingStyle with the updated bias.</returns>
    public ThinkingStyle WithBias(string biasName, double strength)
        => this with { BiasProfile = BiasProfile.SetItem(biasName, Math.Clamp(strength, 0.0, 1.0)) };
}

/// <summary>
/// Interface for a system capable of reflecting on its own reasoning processes.
/// Defines the contract for metacognitive reasoning capabilities.
/// </summary>
public interface IReflectiveReasoner
{
    /// <summary>
    /// Begins recording a new reasoning trace.
    /// </summary>
    /// <returns>The ID of the newly started trace.</returns>
    Guid StartTrace();

    /// <summary>
    /// Adds a step to the currently active reasoning trace.
    /// </summary>
    /// <param name="stepType">The type of reasoning step.</param>
    /// <param name="content">The content of the step.</param>
    /// <param name="justification">The justification for this step.</param>
    /// <param name="dependencies">Step numbers this step depends on.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Result<int, string> AddStep(ReasoningStepType stepType, string content, string justification, params int[] dependencies);

    /// <summary>
    /// Completes the active reasoning trace with a conclusion.
    /// </summary>
    /// <param name="conclusion">The final conclusion.</param>
    /// <param name="success">Whether the reasoning was successful.</param>
    /// <returns>The completed reasoning trace, or an error.</returns>
    Result<ReasoningTrace, string> EndTrace(string conclusion, bool success);

    /// <summary>
    /// Reflects on a completed reasoning trace, analyzing its quality.
    /// </summary>
    /// <param name="trace">The trace to analyze.</param>
    /// <returns>The reflection result.</returns>
    ReflectionResult ReflectOn(ReasoningTrace trace);

    /// <summary>
    /// Analyzes reasoning history to determine thinking style characteristics.
    /// </summary>
    /// <returns>The analyzed thinking style profile.</returns>
    ThinkingStyle GetThinkingStyle();

    /// <summary>
    /// Identifies potential biases from a history of reasoning traces.
    /// </summary>
    /// <param name="history">Collection of past reasoning traces to analyze.</param>
    /// <returns>Map of identified biases to their estimated strength.</returns>
    ImmutableDictionary<string, double> IdentifyBiases(IEnumerable<ReasoningTrace> history);

    /// <summary>
    /// Suggests specific improvements for a reasoning trace.
    /// </summary>
    /// <param name="trace">The trace to improve.</param>
    /// <returns>List of improvement suggestions.</returns>
    ImmutableList<string> SuggestImprovement(ReasoningTrace trace);

    /// <summary>
    /// Gets the currently active trace, if any.
    /// </summary>
    /// <returns>The active trace or None if no trace is active.</returns>
    Option<ReasoningTrace> GetActiveTrace();

    /// <summary>
    /// Gets all completed traces in the reasoner's history.
    /// </summary>
    /// <returns>Enumerable of completed traces.</returns>
    IEnumerable<ReasoningTrace> GetHistory();
}

/// <summary>
/// Thread-safe implementation of reflective reasoning with bias detection and style analysis.
/// Manages active reasoning traces and provides post-hoc analysis capabilities.
/// </summary>
public sealed class MetacognitiveReasoner : IReflectiveReasoner
{
    private readonly ConcurrentDictionary<Guid, ReasoningTrace> activeTraces = new();
    private readonly ConcurrentQueue<ReasoningTrace> completedTraces = new();
    private readonly object traceLock = new();
    private Guid? currentTraceId;

    /// <summary>
    /// Known logical fallacies and their detection patterns.
    /// </summary>
    private static readonly ImmutableDictionary<string, Func<ReasoningTrace, bool>> FallacyDetectors =
        ImmutableDictionary<string, Func<ReasoningTrace, bool>>.Empty
            .Add("Circular Reasoning", HasCircularDependencies)
            .Add("Unsupported Conclusion", HasUnsupportedConclusion)
            .Add("Missing Evidence", HasInsufficientObservations)
            .Add("Confirmation Bias Pattern", HasConfirmationBiasPattern)
            .Add("Hasty Generalization", HasHastyGeneralization);

    /// <summary>
    /// Known cognitive biases and their detection heuristics.
    /// </summary>
    private static readonly ImmutableList<string> KnownBiases = ImmutableList.Create(
        "Confirmation Bias",
        "Anchoring Bias",
        "Availability Heuristic",
        "Dunning-Kruger Effect",
        "Hindsight Bias",
        "Status Quo Bias",
        "Sunk Cost Fallacy",
        "Bandwagon Effect");

    /// <inheritdoc/>
    public Guid StartTrace()
    {
        var trace = ReasoningTrace.Start();
        activeTraces[trace.Id] = trace;

        lock (traceLock)
        {
            currentTraceId = trace.Id;
        }

        return trace.Id;
    }

    /// <inheritdoc/>
    public Result<int, string> AddStep(ReasoningStepType stepType, string content, string justification, params int[] dependencies)
    {
        lock (traceLock)
        {
            if (!currentTraceId.HasValue || !activeTraces.TryGetValue(currentTraceId.Value, out var trace))
            {
                return Result<int, string>.Failure("No active reasoning trace. Call StartTrace() first.");
            }

            var stepNumber = trace.NextStepNumber;
            var step = new ReasoningStep(
                StepNumber: stepNumber,
                StepType: stepType,
                Content: content,
                Justification: justification,
                Timestamp: DateTime.UtcNow,
                Dependencies: dependencies.ToImmutableList());

            if (!step.HasValidDependencies() && dependencies.Length > 0)
            {
                return Result<int, string>.Failure($"Invalid dependencies: all referenced steps must exist and precede step {stepNumber}.");
            }

            var updatedTrace = trace.WithStep(step);
            activeTraces[currentTraceId.Value] = updatedTrace;

            return Result<int, string>.Success(stepNumber);
        }
    }

    /// <inheritdoc/>
    public Result<ReasoningTrace, string> EndTrace(string conclusion, bool success)
    {
        lock (traceLock)
        {
            if (!currentTraceId.HasValue || !activeTraces.TryRemove(currentTraceId.Value, out var trace))
            {
                return Result<ReasoningTrace, string>.Failure("No active reasoning trace to complete.");
            }

            var confidence = CalculateConfidence(trace, success);
            var completedTrace = trace.Complete(conclusion, confidence, success);

            completedTraces.Enqueue(completedTrace);
            currentTraceId = null;

            return Result<ReasoningTrace, string>.Success(completedTrace);
        }
    }

    /// <inheritdoc/>
    public ReflectionResult ReflectOn(ReasoningTrace trace)
    {
        if (trace.Steps.Count == 0)
        {
            return ReflectionResult.Invalid(trace);
        }

        var fallacies = DetectFallacies(trace);
        var logicalSoundness = CalculateLogicalSoundness(trace, fallacies);
        var evidenceSupport = CalculateEvidenceSupport(trace);
        var qualityScore = CalculateQualityScore(logicalSoundness, evidenceSupport, trace);
        var missedConsiderations = IdentifyMissedConsiderations(trace);
        var alternativeConclusions = GenerateAlternativeConclusions(trace);
        var improvements = SuggestImprovement(trace);

        return new ReflectionResult(
            OriginalTrace: trace,
            QualityScore: qualityScore,
            LogicalSoundness: logicalSoundness,
            EvidenceSupport: evidenceSupport,
            IdentifiedFallacies: fallacies,
            MissedConsiderations: missedConsiderations,
            AlternativeConclusions: alternativeConclusions,
            Improvements: improvements);
    }

    /// <inheritdoc/>
    public ThinkingStyle GetThinkingStyle()
    {
        var history = GetHistory().ToList();
        if (history.Count == 0)
        {
            return ThinkingStyle.Balanced();
        }

        var analyticalScore = CalculateAnalyticalScore(history);
        var creativeScore = CalculateCreativeScore(history);
        var systematicScore = CalculateSystematicScore(history);
        var intuitiveScore = CalculateIntuitiveScore(history);
        var biases = IdentifyBiases(history);

        var styleName = DetermineStyleName(analyticalScore, creativeScore, systematicScore, intuitiveScore);

        return new ThinkingStyle(
            StyleName: styleName,
            AnalyticalScore: analyticalScore,
            CreativeScore: creativeScore,
            SystematicScore: systematicScore,
            IntuitiveScore: intuitiveScore,
            BiasProfile: biases);
    }

    /// <inheritdoc/>
    public ImmutableDictionary<string, double> IdentifyBiases(IEnumerable<ReasoningTrace> history)
    {
        var traces = history.ToList();
        if (traces.Count == 0)
        {
            return ImmutableDictionary<string, double>.Empty;
        }

        var biases = ImmutableDictionary<string, double>.Empty;

        // Check for confirmation bias - tendency to only add supporting evidence
        var confirmationBiasScore = CalculateConfirmationBiasStrength(traces);
        if (confirmationBiasScore > 0.2)
        {
            biases = biases.Add("Confirmation Bias", confirmationBiasScore);
        }

        // Check for anchoring - over-reliance on initial observations
        var anchoringScore = CalculateAnchoringStrength(traces);
        if (anchoringScore > 0.2)
        {
            biases = biases.Add("Anchoring Bias", anchoringScore);
        }

        // Check for hasty generalization - jumping to conclusions with little evidence
        var hastyGenScore = CalculateHastyGeneralizationStrength(traces);
        if (hastyGenScore > 0.2)
        {
            biases = biases.Add("Hasty Generalization", hastyGenScore);
        }

        // Check for status quo bias - avoiding revisions
        var statusQuoScore = CalculateStatusQuoBiasStrength(traces);
        if (statusQuoScore > 0.2)
        {
            biases = biases.Add("Status Quo Bias", statusQuoScore);
        }

        return biases;
    }

    /// <inheritdoc/>
    public ImmutableList<string> SuggestImprovement(ReasoningTrace trace)
    {
        var suggestions = new List<string>();

        // Check observation count
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        if (observations < 2)
        {
            suggestions.Add("Gather more observations before drawing inferences.");
        }

        // Check for hypothesis testing
        var hypotheses = trace.GetStepsByType(ReasoningStepType.Hypothesis).Count();
        var validations = trace.GetStepsByType(ReasoningStepType.Validation).Count();
        if (hypotheses > 0 && validations == 0)
        {
            suggestions.Add("Validate hypotheses before accepting them as premises.");
        }

        // Check for revisions
        var revisions = trace.GetStepsByType(ReasoningStepType.Revision).Count();
        if (trace.Steps.Count > 5 && revisions == 0)
        {
            suggestions.Add("Consider revising initial assumptions as new information emerges.");
        }

        // Check dependency chains
        var isolatedSteps = trace.Steps.Where(s =>
            s.StepType != ReasoningStepType.Observation &&
            s.Dependencies.Count == 0).Count();
        if (isolatedSteps > 1)
        {
            suggestions.Add("Ensure non-observation steps reference their supporting evidence.");
        }

        // Check conclusion support
        var conclusions = trace.GetStepsByType(ReasoningStepType.Conclusion).ToList();
        if (conclusions.Count > 0)
        {
            var lastConclusion = conclusions.Last();
            if (lastConclusion.Dependencies.Count < 2)
            {
                suggestions.Add("Strengthen conclusions by explicitly referencing multiple supporting steps.");
            }
        }

        // Check for contradictions
        var contradictions = trace.GetStepsByType(ReasoningStepType.Contradiction).Count();
        if (contradictions > 0)
        {
            suggestions.Add("Resolve identified contradictions before concluding.");
        }

        // Encourage deeper analysis
        if (trace.Steps.Count < 4)
        {
            suggestions.Add("Develop reasoning more thoroughly before reaching conclusions.");
        }

        return suggestions.ToImmutableList();
    }

    /// <inheritdoc/>
    public Option<ReasoningTrace> GetActiveTrace()
    {
        lock (traceLock)
        {
            if (currentTraceId.HasValue && activeTraces.TryGetValue(currentTraceId.Value, out var trace))
            {
                return Option<ReasoningTrace>.Some(trace);
            }

            return Option<ReasoningTrace>.None();
        }
    }

    /// <inheritdoc/>
    public IEnumerable<ReasoningTrace> GetHistory() => completedTraces.ToArray();

    #region Private Helper Methods

    private static bool HasCircularDependencies(ReasoningTrace trace)
    {
        // Check for any step that eventually depends on itself
        foreach (var step in trace.Steps)
        {
            var visited = new HashSet<int>();
            var toCheck = new Queue<int>(step.Dependencies);

            while (toCheck.Count > 0)
            {
                var dep = toCheck.Dequeue();
                if (dep == step.StepNumber)
                {
                    return true;
                }

                if (visited.Add(dep))
                {
                    var depStep = trace.Steps.FirstOrDefault(s => s.StepNumber == dep);
                    if (depStep != null)
                    {
                        foreach (var nestedDep in depStep.Dependencies)
                        {
                            toCheck.Enqueue(nestedDep);
                        }
                    }
                }
            }
        }

        return false;
    }

    private static bool HasUnsupportedConclusion(ReasoningTrace trace)
    {
        var conclusions = trace.GetStepsByType(ReasoningStepType.Conclusion).ToList();
        return conclusions.Any(c => c.Dependencies.Count == 0);
    }

    private static bool HasInsufficientObservations(ReasoningTrace trace)
    {
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var inferences = trace.GetStepsByType(ReasoningStepType.Inference).Count();
        return observations < 1 && inferences > 0;
    }

    private static bool HasConfirmationBiasPattern(ReasoningTrace trace)
    {
        // Check if there are hypotheses but no contradicting evidence or revisions
        var hypotheses = trace.GetStepsByType(ReasoningStepType.Hypothesis).Count();
        var contradictions = trace.GetStepsByType(ReasoningStepType.Contradiction).Count();
        var revisions = trace.GetStepsByType(ReasoningStepType.Revision).Count();

        return hypotheses > 0 && contradictions == 0 && revisions == 0;
    }

    private static bool HasHastyGeneralization(ReasoningTrace trace)
    {
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var conclusions = trace.GetStepsByType(ReasoningStepType.Conclusion).Count();
        return observations <= 1 && conclusions > 0;
    }

    private ImmutableList<string> DetectFallacies(ReasoningTrace trace)
    {
        var detected = new List<string>();

        foreach (var (fallacyName, detector) in FallacyDetectors)
        {
            if (detector(trace))
            {
                detected.Add(fallacyName);
            }
        }

        return detected.ToImmutableList();
    }

    private static double CalculateLogicalSoundness(ReasoningTrace trace, ImmutableList<string> fallacies)
    {
        if (!trace.IsLogicallyConsistent())
        {
            return 0.3;
        }

        var baseScore = 0.9;
        var penaltyPerFallacy = 0.15;
        var score = baseScore - (fallacies.Count * penaltyPerFallacy);

        return Math.Max(0.0, Math.Min(1.0, score));
    }

    private static double CalculateEvidenceSupport(ReasoningTrace trace)
    {
        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var inferences = trace.GetStepsByType(ReasoningStepType.Inference).Count();
        var validations = trace.GetStepsByType(ReasoningStepType.Validation).Count();

        if (observations == 0)
        {
            return 0.2;
        }

        var ratio = (double)(observations + validations) / Math.Max(1, inferences + 1);
        return Math.Min(1.0, ratio * 0.5 + 0.3);
    }

    private static double CalculateQualityScore(double logicalSoundness, double evidenceSupport, ReasoningTrace trace)
    {
        var completenessBonus = trace.WasSuccessful ? 0.1 : 0.0;
        var stepCountBonus = Math.Min(0.1, trace.Steps.Count * 0.02);

        var weightedScore = (logicalSoundness * 0.4) + (evidenceSupport * 0.4) + completenessBonus + stepCountBonus;
        return Math.Min(1.0, weightedScore);
    }

    private static double CalculateConfidence(ReasoningTrace trace, bool success)
    {
        if (!success)
        {
            return 0.1;
        }

        var observations = trace.GetStepsByType(ReasoningStepType.Observation).Count();
        var validations = trace.GetStepsByType(ReasoningStepType.Validation).Count();

        var baseConfidence = 0.5;
        var observationBonus = Math.Min(0.2, observations * 0.05);
        var validationBonus = Math.Min(0.2, validations * 0.1);

        return Math.Min(0.95, baseConfidence + observationBonus + validationBonus);
    }

    private static ImmutableList<string> IdentifyMissedConsiderations(ReasoningTrace trace)
    {
        var missed = new List<string>();

        var hasAssumptions = trace.GetStepsByType(ReasoningStepType.Assumption).Any();
        if (!hasAssumptions && trace.Steps.Count > 3)
        {
            missed.Add("Consider explicitly stating underlying assumptions.");
        }

        var hasValidation = trace.GetStepsByType(ReasoningStepType.Validation).Any();
        if (!hasValidation && trace.GetStepsByType(ReasoningStepType.Hypothesis).Any())
        {
            missed.Add("Hypotheses should be validated before being used in further reasoning.");
        }

        return missed.ToImmutableList();
    }

    private static ImmutableList<string> GenerateAlternativeConclusions(ReasoningTrace trace)
    {
        // In a real implementation, this would analyze the evidence to suggest alternatives
        // For now, we provide generic guidance
        if (trace.Steps.Count < 3)
        {
            return ImmutableList<string>.Empty;
        }

        return ImmutableList.Create(
            "Consider alternative interpretations of the observations.",
            "Explore whether different assumptions would lead to different conclusions.");
    }

    private static double CalculateAnalyticalScore(IReadOnlyList<ReasoningTrace> history)
    {
        var totalInferences = history.Sum(t => t.GetStepsByType(ReasoningStepType.Inference).Count());
        var totalSteps = history.Sum(t => t.Steps.Count);
        return totalSteps > 0 ? Math.Min(1.0, (double)totalInferences / totalSteps * 2.5) : 0.5;
    }

    private static double CalculateCreativeScore(IReadOnlyList<ReasoningTrace> history)
    {
        var totalHypotheses = history.Sum(t => t.GetStepsByType(ReasoningStepType.Hypothesis).Count());
        var totalTraces = history.Count;
        return totalTraces > 0 ? Math.Min(1.0, (double)totalHypotheses / totalTraces * 0.5) : 0.5;
    }

    private static double CalculateSystematicScore(IReadOnlyList<ReasoningTrace> history)
    {
        var validTraces = history.Count(t => t.IsLogicallyConsistent());
        return history.Count > 0 ? (double)validTraces / history.Count : 0.5;
    }

    private static double CalculateIntuitiveScore(IReadOnlyList<ReasoningTrace> history)
    {
        // Lower observation-to-conclusion ratio suggests more intuitive reasoning
        var avgObservations = history.Count > 0
            ? history.Average(t => t.GetStepsByType(ReasoningStepType.Observation).Count())
            : 2.0;
        return Math.Max(0.0, Math.Min(1.0, 1.0 - (avgObservations * 0.2)));
    }

    private static double CalculateConfirmationBiasStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        var biasedTraces = traces.Count(HasConfirmationBiasPattern);
        return traces.Count > 0 ? (double)biasedTraces / traces.Count : 0.0;
    }

    private static double CalculateAnchoringStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        // Check if first observations are disproportionately referenced
        var anchoredCount = 0;
        foreach (var trace in traces)
        {
            if (trace.Steps.Count < 3)
            {
                continue;
            }

            var firstStepRefs = trace.Steps.Sum(s => s.Dependencies.Count(d => d == 1));
            var totalRefs = trace.Steps.Sum(s => s.Dependencies.Count);
            if (totalRefs > 0 && (double)firstStepRefs / totalRefs > 0.5)
            {
                anchoredCount++;
            }
        }

        return traces.Count > 0 ? (double)anchoredCount / traces.Count : 0.0;
    }

    private static double CalculateHastyGeneralizationStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        var hastyCount = traces.Count(HasHastyGeneralization);
        return traces.Count > 0 ? (double)hastyCount / traces.Count : 0.0;
    }

    private static double CalculateStatusQuoBiasStrength(IReadOnlyList<ReasoningTrace> traces)
    {
        var noRevisionCount = traces.Count(t =>
            t.Steps.Count > 5 &&
            t.GetStepsByType(ReasoningStepType.Revision).Count() == 0);
        return traces.Count > 0 ? (double)noRevisionCount / traces.Count : 0.0;
    }

    private static string DetermineStyleName(double analytical, double creative, double systematic, double intuitive)
    {
        var scores = new (string Name, double Score)[]
        {
            ("Analytical", analytical),
            ("Creative", creative),
            ("Systematic", systematic),
            ("Intuitive", intuitive),
        };

        var dominant = scores.MaxBy(s => s.Score);
        var secondary = scores.Where(s => s.Name != dominant.Name).MaxBy(s => s.Score);

        if (Math.Abs(dominant.Score - secondary.Score) < 0.15)
        {
            return $"Balanced ({dominant.Name}/{secondary.Name})";
        }

        return dominant.Name;
    }

    #endregion
}

/// <summary>
/// Provides Kleisli arrows for reflective reasoning operations.
/// Enables functional composition of metacognitive analysis steps.
/// </summary>
public static class ReflectiveReasoningArrow
{
    /// <summary>
    /// Creates a step that reflects on a reasoning trace to produce a reflection result.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that transforms a reasoning trace into a reflection result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<ReasoningTrace, Result<ReflectionResult, string>> Reflect(IReflectiveReasoner reasoner) =>
        trace => Task.FromResult(
            trace.Steps.Count > 0
                ? Result<ReflectionResult, string>.Success(reasoner.ReflectOn(trace))
                : Result<ReflectionResult, string>.Failure("Cannot reflect on empty reasoning trace."));

    /// <summary>
    /// Creates a step that analyzes multiple reasoning traces to determine thinking style.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that transforms reasoning traces into a thinking style profile.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<IEnumerable<ReasoningTrace>, Result<ThinkingStyle, string>> AnalyzeStyle(IReflectiveReasoner reasoner) =>
        traces =>
        {
            var traceList = traces.ToList();
            if (traceList.Count == 0)
            {
                return Task.FromResult(Result<ThinkingStyle, string>.Failure("Cannot analyze style without reasoning history."));
            }

            // Temporarily use the provided traces for analysis
            var biases = reasoner.IdentifyBiases(traceList);
            var style = reasoner.GetThinkingStyle();

            // Merge biases from the specific traces
            var mergedStyle = biases.Aggregate(style, (s, kv) => s.WithBias(kv.Key, kv.Value));

            return Task.FromResult(Result<ThinkingStyle, string>.Success(mergedStyle));
        };

    /// <summary>
    /// Creates a step that identifies biases from reasoning history.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that identifies biases from reasoning traces.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<IEnumerable<ReasoningTrace>, Result<ImmutableDictionary<string, double>, string>> IdentifyBiases(IReflectiveReasoner reasoner) =>
        traces =>
        {
            var traceList = traces.ToList();
            if (traceList.Count < 3)
            {
                return Task.FromResult(Result<ImmutableDictionary<string, double>, string>.Failure(
                    "At least 3 reasoning traces are needed for reliable bias detection."));
            }

            var biases = reasoner.IdentifyBiases(traceList);
            return Task.FromResult(Result<ImmutableDictionary<string, double>, string>.Success(biases));
        };

    /// <summary>
    /// Creates a step that suggests improvements for a reasoning trace.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use for analysis.</param>
    /// <returns>A step that generates improvement suggestions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<ReasoningTrace, Result<ImmutableList<string>, string>> SuggestImprovements(IReflectiveReasoner reasoner) =>
        trace => Task.FromResult(
            trace.Steps.Count > 0
                ? Result<ImmutableList<string>, string>.Success(reasoner.SuggestImprovement(trace))
                : Result<ImmutableList<string>, string>.Failure("Cannot suggest improvements for empty trace."));

    /// <summary>
    /// Creates a composed step that performs full metacognitive analysis on a trace.
    /// Combines reflection, bias detection, and improvement suggestions.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use.</param>
    /// <returns>A step that performs comprehensive metacognitive analysis.</returns>
    public static Step<ReasoningTrace, Result<MetacognitiveAnalysis, string>> FullAnalysis(IReflectiveReasoner reasoner) =>
        async trace =>
        {
            if (trace.Steps.Count == 0)
            {
                return Result<MetacognitiveAnalysis, string>.Failure("Cannot analyze empty reasoning trace.");
            }

            var reflection = reasoner.ReflectOn(trace);
            var improvements = reasoner.SuggestImprovement(trace);
            var style = reasoner.GetThinkingStyle();

            var analysis = new MetacognitiveAnalysis(
                Trace: trace,
                Reflection: reflection,
                Style: style,
                Improvements: improvements,
                AnalyzedAt: DateTime.UtcNow);

            return Result<MetacognitiveAnalysis, string>.Success(analysis);
        };

    /// <summary>
    /// Creates a step that validates reasoning quality against a threshold.
    /// </summary>
    /// <param name="reasoner">The reflective reasoner to use.</param>
    /// <param name="qualityThreshold">Minimum acceptable quality score.</param>
    /// <returns>A step that validates reasoning quality.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Step<ReasoningTrace, Result<bool, string>> ValidateQuality(IReflectiveReasoner reasoner, double qualityThreshold = 0.7) =>
        trace =>
        {
            if (trace.Steps.Count == 0)
            {
                return Task.FromResult(Result<bool, string>.Failure("Cannot validate empty trace."));
            }

            var reflection = reasoner.ReflectOn(trace);
            var meetsThreshold = reflection.MeetsQualityThreshold(qualityThreshold);

            return Task.FromResult(Result<bool, string>.Success(meetsThreshold));
        };
}

/// <summary>
/// Represents a comprehensive metacognitive analysis of a reasoning trace.
/// Combines reflection results with style analysis and improvement suggestions.
/// </summary>
/// <param name="Trace">The original reasoning trace that was analyzed.</param>
/// <param name="Reflection">The reflection result with quality metrics.</param>
/// <param name="Style">The thinking style profile.</param>
/// <param name="Improvements">List of improvement suggestions.</param>
/// <param name="AnalyzedAt">When the analysis was performed.</param>
public sealed record MetacognitiveAnalysis(
    ReasoningTrace Trace,
    ReflectionResult Reflection,
    ThinkingStyle Style,
    ImmutableList<string> Improvements,
    DateTime AnalyzedAt)
{
    /// <summary>
    /// Gets a summary of the analysis quality.
    /// </summary>
    public string QualitySummary => Reflection.QualityScore switch
    {
        >= 0.9 => "Excellent reasoning quality",
        >= 0.7 => "Good reasoning quality",
        >= 0.5 => "Moderate reasoning quality - improvements recommended",
        >= 0.3 => "Poor reasoning quality - significant improvements needed",
        _ => "Very poor reasoning quality - fundamental issues detected",
    };

    /// <summary>
    /// Gets whether this analysis indicates acceptable reasoning.
    /// </summary>
    public bool IsAcceptable => Reflection.MeetsQualityThreshold(0.6);

    /// <summary>
    /// Gets the primary areas needing improvement.
    /// </summary>
    public IEnumerable<string> PriorityImprovements => Improvements.Take(3);
}
