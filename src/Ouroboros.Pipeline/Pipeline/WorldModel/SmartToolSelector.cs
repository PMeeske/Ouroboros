// <copyright file="SmartToolSelector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Domain.States;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Tools;

/// <summary>
/// Defines the optimization strategy for tool selection.
/// </summary>
public enum OptimizationStrategy
{
    /// <summary>
    /// Optimize for lowest cost (prefer cheaper tools).
    /// </summary>
    Cost,

    /// <summary>
    /// Optimize for fastest execution speed.
    /// </summary>
    Speed,

    /// <summary>
    /// Optimize for highest quality output.
    /// </summary>
    Quality,

    /// <summary>
    /// Balanced optimization considering all factors.
    /// </summary>
    Balanced,
}

/// <summary>
/// Configuration options for smart tool selection.
/// </summary>
/// <param name="MaxTools">Maximum number of tools to select (default: 5).</param>
/// <param name="MinConfidence">Minimum confidence threshold for selection (0.0 to 1.0, default: 0.3).</param>
/// <param name="OptimizeFor">The optimization strategy to use (default: Balanced).</param>
/// <param name="AllowParallelExecution">Whether selected tools can be executed in parallel (default: true).</param>
public sealed record SelectionConfig(
    int MaxTools = 5,
    double MinConfidence = 0.3,
    OptimizationStrategy OptimizeFor = OptimizationStrategy.Balanced,
    bool AllowParallelExecution = true)
{
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static SelectionConfig Default { get; } = new();

    /// <summary>
    /// Creates a configuration optimized for cost.
    /// </summary>
    /// <returns>A cost-optimized configuration.</returns>
    public static SelectionConfig ForCost() => new(
        MaxTools: 3,
        MinConfidence: 0.4,
        OptimizeFor: OptimizationStrategy.Cost,
        AllowParallelExecution: false);

    /// <summary>
    /// Creates a configuration optimized for speed.
    /// </summary>
    /// <returns>A speed-optimized configuration.</returns>
    public static SelectionConfig ForSpeed() => new(
        MaxTools: 2,
        MinConfidence: 0.5,
        OptimizeFor: OptimizationStrategy.Speed,
        AllowParallelExecution: true);

    /// <summary>
    /// Creates a configuration optimized for quality.
    /// </summary>
    /// <returns>A quality-optimized configuration.</returns>
    public static SelectionConfig ForQuality() => new(
        MaxTools: 10,
        MinConfidence: 0.2,
        OptimizeFor: OptimizationStrategy.Quality,
        AllowParallelExecution: true);

    /// <summary>
    /// Creates a new configuration with the specified max tools.
    /// </summary>
    /// <param name="maxTools">The maximum number of tools.</param>
    /// <returns>A new configuration with updated max tools.</returns>
    public SelectionConfig WithMaxTools(int maxTools) =>
        this with { MaxTools = Math.Max(1, maxTools) };

    /// <summary>
    /// Creates a new configuration with the specified minimum confidence.
    /// </summary>
    /// <param name="minConfidence">The minimum confidence threshold.</param>
    /// <returns>A new configuration with updated minimum confidence.</returns>
    public SelectionConfig WithMinConfidence(double minConfidence) =>
        this with { MinConfidence = Math.Clamp(minConfidence, 0.0, 1.0) };
}

/// <summary>
/// Represents a scored candidate tool for selection.
/// </summary>
/// <param name="Tool">The candidate tool.</param>
/// <param name="FitScore">How well the tool fits the goal (0.0 to 1.0).</param>
/// <param name="CostScore">Normalized cost score (lower is better, 0.0 to 1.0).</param>
/// <param name="SpeedScore">Normalized speed score (higher is faster, 0.0 to 1.0).</param>
/// <param name="QualityScore">Expected quality score (0.0 to 1.0).</param>
/// <param name="MatchedCapabilities">Capabilities that matched the goal requirements.</param>
public sealed record ToolCandidate(
    ITool Tool,
    double FitScore,
    double CostScore,
    double SpeedScore,
    double QualityScore,
    IReadOnlyList<string> MatchedCapabilities)
{
    /// <summary>
    /// Calculates the combined score based on optimization strategy.
    /// </summary>
    /// <param name="strategy">The optimization strategy.</param>
    /// <returns>The combined weighted score.</returns>
    public double GetWeightedScore(OptimizationStrategy strategy)
    {
        return strategy switch
        {
            OptimizationStrategy.Cost => (FitScore * 0.4) + ((1.0 - CostScore) * 0.5) + (QualityScore * 0.1),
            OptimizationStrategy.Speed => (FitScore * 0.3) + (SpeedScore * 0.5) + (QualityScore * 0.2),
            OptimizationStrategy.Quality => (FitScore * 0.3) + (QualityScore * 0.6) + (SpeedScore * 0.1),
            OptimizationStrategy.Balanced => (FitScore * 0.4) + (QualityScore * 0.3) + (SpeedScore * 0.2) + ((1.0 - CostScore) * 0.1),
            _ => FitScore,
        };
    }

    /// <summary>
    /// Creates a candidate with default scores from a tool match.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="match">The tool match containing fit information.</param>
    /// <returns>A new tool candidate.</returns>
    public static ToolCandidate FromMatch(ITool tool, ToolMatch match)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(match);

        return new ToolCandidate(
            Tool: tool,
            FitScore: match.RelevanceScore,
            CostScore: 0.5, // Default normalized cost
            SpeedScore: 0.5, // Default speed
            QualityScore: match.RelevanceScore, // Use relevance as quality proxy
            MatchedCapabilities: match.MatchedCapabilities);
    }
}

/// <summary>
/// Represents the result of a tool selection process.
/// </summary>
/// <param name="SelectedTools">The tools selected for the goal.</param>
/// <param name="Reasoning">Human-readable explanation of the selection.</param>
/// <param name="ConfidenceScore">Overall confidence in the selection (0.0 to 1.0).</param>
/// <param name="AllCandidates">All evaluated candidates before filtering.</param>
/// <param name="AppliedConstraints">Constraints that were applied during selection.</param>
public sealed record ToolSelection(
    IReadOnlyList<ITool> SelectedTools,
    string Reasoning,
    double ConfidenceScore,
    IReadOnlyList<ToolCandidate> AllCandidates,
    IReadOnlyList<Constraint> AppliedConstraints)
{
    /// <summary>
    /// Gets an empty selection with no tools.
    /// </summary>
    public static ToolSelection Empty { get; } = new(
        SelectedTools: [],
        Reasoning: "No tools selected.",
        ConfidenceScore: 0.0,
        AllCandidates: [],
        AppliedConstraints: []);

    /// <summary>
    /// Creates a failed selection result.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <returns>A failed selection result.</returns>
    public static ToolSelection Failed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return new ToolSelection(
            SelectedTools: [],
            Reasoning: reason,
            ConfidenceScore: 0.0,
            AllCandidates: [],
            AppliedConstraints: []);
    }

    /// <summary>
    /// Checks if any tools were selected.
    /// </summary>
    public bool HasTools => SelectedTools.Count > 0;

    /// <summary>
    /// Gets the tool names as an immutable set.
    /// </summary>
    public IReadOnlySet<string> ToolNames =>
        SelectedTools.Select(t => t.Name).ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new selection with an additional constraint recorded.
    /// </summary>
    /// <param name="constraint">The constraint to add.</param>
    /// <returns>A new selection with the constraint added.</returns>
    public ToolSelection WithAppliedConstraint(Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        return this with { AppliedConstraints = AppliedConstraints.Append(constraint).ToList() };
    }
}

/// <summary>
/// Selects optimal tools based on goals, world state, and configuration.
/// Uses capability matching and constraint filtering to find the best tools for a task.
/// Follows functional programming principles with immutable operations and monadic error handling.
/// </summary>
public sealed class SmartToolSelector
{
    private readonly WorldState worldState;
    private readonly ToolRegistry toolRegistry;
    private readonly ToolCapabilityMatcher capabilityMatcher;
    private readonly SelectionConfig config;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartToolSelector"/> class.
    /// </summary>
    /// <param name="worldState">The current world state.</param>
    /// <param name="toolRegistry">The registry of available tools.</param>
    /// <param name="capabilityMatcher">The capability matcher for scoring tools.</param>
    public SmartToolSelector(
        WorldState worldState,
        ToolRegistry toolRegistry,
        ToolCapabilityMatcher capabilityMatcher)
        : this(worldState, toolRegistry, capabilityMatcher, SelectionConfig.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartToolSelector"/> class with custom configuration.
    /// </summary>
    /// <param name="worldState">The current world state.</param>
    /// <param name="toolRegistry">The registry of available tools.</param>
    /// <param name="capabilityMatcher">The capability matcher for scoring tools.</param>
    /// <param name="config">The selection configuration.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public SmartToolSelector(
        WorldState worldState,
        ToolRegistry toolRegistry,
        ToolCapabilityMatcher capabilityMatcher,
        SelectionConfig config)
    {
        ArgumentNullException.ThrowIfNull(worldState);
        ArgumentNullException.ThrowIfNull(toolRegistry);
        ArgumentNullException.ThrowIfNull(capabilityMatcher);
        ArgumentNullException.ThrowIfNull(config);

        this.worldState = worldState;
        this.toolRegistry = toolRegistry;
        this.capabilityMatcher = capabilityMatcher;
        this.config = config;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public SelectionConfig Configuration => config;

    /// <summary>
    /// Creates a new selector with updated world state.
    /// </summary>
    /// <param name="newWorldState">The new world state.</param>
    /// <returns>A new selector with the updated world state.</returns>
    public SmartToolSelector WithWorldState(WorldState newWorldState)
    {
        ArgumentNullException.ThrowIfNull(newWorldState);

        return new SmartToolSelector(newWorldState, toolRegistry, capabilityMatcher, config);
    }

    /// <summary>
    /// Creates a new selector with updated configuration.
    /// </summary>
    /// <param name="newConfig">The new configuration.</param>
    /// <returns>A new selector with the updated configuration.</returns>
    public SmartToolSelector WithConfig(SelectionConfig newConfig)
    {
        ArgumentNullException.ThrowIfNull(newConfig);

        return new SmartToolSelector(worldState, toolRegistry, capabilityMatcher, newConfig);
    }

    /// <summary>
    /// Selects tools asynchronously for the given goal.
    /// </summary>
    /// <param name="goal">The goal to select tools for.</param>
    /// <returns>A Result containing the tool selection or an error message.</returns>
    public Task<Result<ToolSelection, string>> SelectForGoalAsync(Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return SelectForGoalAsync(goal, CancellationToken.None);
    }

    /// <summary>
    /// Selects tools asynchronously for the given goal with cancellation support.
    /// </summary>
    /// <param name="goal">The goal to select tools for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A Result containing the tool selection or an error message.</returns>
    public Task<Result<ToolSelection, string>> SelectForGoalAsync(Goal goal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(goal);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<ToolSelection, string>.Failure("Operation was cancelled."));
        }

        // Match tools using capability matcher
        Result<IReadOnlyList<ToolMatch>, string> matchResult =
            capabilityMatcher.MatchToolsForGoal(goal, config.MinConfidence);

        if (matchResult.IsFailure)
        {
            return Task.FromResult(Result<ToolSelection, string>.Failure(matchResult.Error));
        }

        IReadOnlyList<ToolMatch> matches = matchResult.Value;

        if (matches.Count == 0)
        {
            return Task.FromResult(Result<ToolSelection, string>.Success(
                ToolSelection.Failed($"No tools found matching goal '{goal.Description}' with minimum confidence {config.MinConfidence}.")));
        }

        // Convert matches to candidates with full scoring
        List<ToolCandidate> candidates = new();
        foreach (ToolMatch match in matches)
        {
            Option<ITool> toolOption = toolRegistry.GetTool(match.ToolName);
            if (toolOption.HasValue && toolOption.Value is ITool tool)
            {
                ToolCandidate candidate = EvaluateToolFit(tool, goal, worldState, match);
                candidates.Add(candidate);
            }
        }

        // Apply world state constraints
        IReadOnlyList<Constraint> activeConstraints = worldState.Constraints;
        List<ToolCandidate> filteredCandidates = ApplyConstraints(candidates, activeConstraints);

        // Rank by optimization strategy
        List<ToolCandidate> rankedCandidates = filteredCandidates
            .OrderByDescending(c => c.GetWeightedScore(config.OptimizeFor))
            .ToList();

        // Select top tools up to max
        IReadOnlyList<ToolCandidate> selectedCandidates = rankedCandidates
            .Take(config.MaxTools)
            .ToList();

        IReadOnlyList<ITool> selectedTools = selectedCandidates
            .Select(c => c.Tool)
            .ToList();

        // Calculate overall confidence
        double overallConfidence = selectedCandidates.Count > 0
            ? selectedCandidates.Average(c => c.FitScore)
            : 0.0;

        // Build reasoning explanation
        string reasoning = BuildReasoning(goal, selectedCandidates, activeConstraints);

        ToolSelection selection = new(
            SelectedTools: selectedTools,
            Reasoning: reasoning,
            ConfidenceScore: overallConfidence,
            AllCandidates: candidates,
            AppliedConstraints: activeConstraints);

        return Task.FromResult(Result<ToolSelection, string>.Success(selection));
    }

    /// <summary>
    /// Creates a pipeline step that selects tools for a goal.
    /// </summary>
    /// <param name="goal">The goal to select tools for.</param>
    /// <returns>A step that transforms a pipeline branch with tool selection.</returns>
    public Step<PipelineBranch, Result<PipelineBranch, string>> CreateStepForGoal(Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return async branch =>
        {
            ArgumentNullException.ThrowIfNull(branch);

            Result<ToolSelection, string> selectionResult = await SelectForGoalAsync(goal);

            if (selectionResult.IsFailure)
            {
                return Result<PipelineBranch, string>.Failure(selectionResult.Error);
            }

            ToolSelection selection = selectionResult.Value;

            // Record the selection as a reasoning event
            ToolSelectionState state = new(
                Goal: goal,
                Selection: selection,
                Timestamp: DateTime.UtcNow);

            PipelineBranch updatedBranch = branch.WithReasoning(
                state,
                $"Selected {selection.SelectedTools.Count} tools for goal: {goal.Description}",
                tools: null);

            return Result<PipelineBranch, string>.Success(updatedBranch);
        };
    }

    /// <summary>
    /// Creates a parameterized step that extracts the goal from the branch.
    /// </summary>
    /// <param name="goalExtractor">Function to extract goal from branch.</param>
    /// <returns>A step that transforms a pipeline branch with dynamic tool selection.</returns>
    public Step<PipelineBranch, Result<PipelineBranch, string>> CreateDynamicStep(
        Func<PipelineBranch, Option<Goal>> goalExtractor)
    {
        ArgumentNullException.ThrowIfNull(goalExtractor);

        return async branch =>
        {
            ArgumentNullException.ThrowIfNull(branch);

            Option<Goal> goalOption = goalExtractor(branch);

            if (!goalOption.HasValue || goalOption.Value is not Goal extractedGoal)
            {
                return Result<PipelineBranch, string>.Failure("Could not extract goal from pipeline branch.");
            }

            Result<ToolSelection, string> selectionResult = await SelectForGoalAsync(extractedGoal);

            if (selectionResult.IsFailure)
            {
                return Result<PipelineBranch, string>.Failure(selectionResult.Error);
            }

            ToolSelection selection = selectionResult.Value;

            ToolSelectionState state = new(
                Goal: extractedGoal,
                Selection: selection,
                Timestamp: DateTime.UtcNow);

            PipelineBranch updatedBranch = branch.WithReasoning(
                state,
                $"Dynamically selected {selection.SelectedTools.Count} tools for: {extractedGoal.Description}",
                tools: null);

            return Result<PipelineBranch, string>.Success(updatedBranch);
        };
    }

    /// <summary>
    /// Evaluates how well a tool fits a goal given the current world state.
    /// </summary>
    /// <param name="tool">The tool to evaluate.</param>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="currentWorldState">The current world state.</param>
    /// <returns>A tool candidate with computed fit scores.</returns>
    public ToolCandidate EvaluateToolFit(ITool tool, Goal goal, WorldState currentWorldState)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(currentWorldState);

        // Get match from capability matcher
        Option<ToolMatch> matchOption = capabilityMatcher.GetBestMatch(goal, 0.0);

        if (matchOption.HasValue && matchOption.Value is ToolMatch match && match.ToolName.Equals(tool.Name, StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateToolFit(tool, goal, currentWorldState, match);
        }

        // Fallback: compute basic fit score
        double fitScore = capabilityMatcher.ScoreToolRelevance(tool, goal.Description);

        return new ToolCandidate(
            Tool: tool,
            FitScore: fitScore,
            CostScore: EstimateToolCost(tool, currentWorldState),
            SpeedScore: EstimateToolSpeed(tool, currentWorldState),
            QualityScore: EstimateToolQuality(tool, fitScore),
            MatchedCapabilities: []);
    }

    /// <summary>
    /// Evaluates tool fit with an existing match result.
    /// </summary>
    /// <param name="tool">The tool to evaluate.</param>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="currentWorldState">The current world state.</param>
    /// <param name="match">The pre-computed match result.</param>
    /// <returns>A tool candidate with computed fit scores.</returns>
    public ToolCandidate EvaluateToolFit(
        ITool tool,
        Goal goal,
        WorldState currentWorldState,
        ToolMatch match)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(currentWorldState);
        ArgumentNullException.ThrowIfNull(match);

        double fitScore = match.RelevanceScore;
        double costScore = EstimateToolCost(tool, currentWorldState);
        double speedScore = EstimateToolSpeed(tool, currentWorldState);
        double qualityScore = EstimateToolQuality(tool, fitScore);

        // Adjust scores based on world state observations
        if (currentWorldState.Observations.TryGetValue($"tool.{tool.Name}.performance", out Observation? perfObs))
        {
            if (perfObs.Value is double performanceMultiplier)
            {
                speedScore = Math.Clamp(speedScore * performanceMultiplier, 0.0, 1.0);
            }
        }

        if (currentWorldState.Observations.TryGetValue($"tool.{tool.Name}.reliability", out Observation? reliabilityObs))
        {
            if (reliabilityObs.Value is double reliabilityMultiplier)
            {
                qualityScore = Math.Clamp(qualityScore * reliabilityMultiplier, 0.0, 1.0);
            }
        }

        return new ToolCandidate(
            Tool: tool,
            FitScore: fitScore,
            CostScore: costScore,
            SpeedScore: speedScore,
            QualityScore: qualityScore,
            MatchedCapabilities: match.MatchedCapabilities);
    }

    /// <summary>
    /// Applies constraints to filter tool candidates.
    /// </summary>
    /// <param name="candidates">The list of candidates to filter.</param>
    /// <param name="constraints">The constraints to apply.</param>
    /// <returns>A filtered list of candidates that pass all constraints.</returns>
    public List<ToolCandidate> ApplyConstraints(
        IReadOnlyList<ToolCandidate> candidates,
        IReadOnlyList<Constraint> constraints)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(constraints);

        if (constraints.Count == 0)
        {
            return candidates.ToList();
        }

        List<ToolCandidate> filtered = new(candidates);

        foreach (Constraint constraint in constraints.OrderByDescending(c => c.Priority))
        {
            filtered = ApplySingleConstraint(filtered, constraint);

            // If we've filtered out all candidates, stop and return empty
            if (filtered.Count == 0)
            {
                break;
            }
        }

        return filtered;
    }

    /// <summary>
    /// Gets all tools from the registry as candidates without scoring.
    /// </summary>
    /// <returns>List of all tools as candidates with default scores.</returns>
    public IReadOnlyList<ToolCandidate> GetAllCandidates()
    {
        return toolRegistry.All
            .Select(tool => new ToolCandidate(
                Tool: tool,
                FitScore: 0.5,
                CostScore: 0.5,
                SpeedScore: 0.5,
                QualityScore: 0.5,
                MatchedCapabilities: []))
            .ToList();
    }

    /// <summary>
    /// Checks if a specific tool is available and passes current constraints.
    /// </summary>
    /// <param name="toolName">The name of the tool to check.</param>
    /// <returns>True if the tool is available and unconstrained.</returns>
    public bool IsToolAvailable(string toolName)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        Option<ITool> toolOption = toolRegistry.GetTool(toolName);

        if (!toolOption.HasValue || toolOption.Value is not ITool tool)
        {
            return false;
        }

        // Check if tool is blocked by any constraint
        foreach (Constraint constraint in worldState.Constraints)
        {
            if (IsToolBlockedByConstraint(tool, constraint))
            {
                return false;
            }
        }

        return true;
    }

    private static double EstimateToolCost(ITool tool, WorldState state)
    {
        // Check if cost information is available in world state
        if (state.Observations.TryGetValue($"tool.{tool.Name}.cost", out Observation? costObs))
        {
            if (costObs.Value is double cost)
            {
                return Math.Clamp(cost, 0.0, 1.0);
            }
        }

        // Default cost estimation based on tool complexity (schema presence indicates complexity)
        return tool.JsonSchema != null ? 0.6 : 0.3;
    }

    private static double EstimateToolSpeed(ITool tool, WorldState state)
    {
        // Check if speed information is available in world state
        if (state.Observations.TryGetValue($"tool.{tool.Name}.speed", out Observation? speedObs))
        {
            if (speedObs.Value is double speed)
            {
                return Math.Clamp(speed, 0.0, 1.0);
            }
        }

        // Default speed estimation
        return 0.5;
    }

    private static double EstimateToolQuality(ITool tool, double fitScore)
    {
        // Quality is primarily derived from fit score with some base quality
        double baseQuality = 0.3;
        return Math.Clamp(baseQuality + (fitScore * 0.7), 0.0, 1.0);
    }

    private static string BuildReasoning(
        Goal goal,
        IReadOnlyList<ToolCandidate> selectedCandidates,
        IReadOnlyList<Constraint> constraints)
    {
        if (selectedCandidates.Count == 0)
        {
            return $"No suitable tools found for goal: {goal.Description}";
        }

        List<string> reasoningParts = new()
        {
            $"Selected {selectedCandidates.Count} tool(s) for goal: \"{goal.Description}\".",
        };

        foreach (ToolCandidate candidate in selectedCandidates)
        {
            string capabilities = candidate.MatchedCapabilities.Count > 0
                ? $" (matched: {string.Join(", ", candidate.MatchedCapabilities)})"
                : string.Empty;

            reasoningParts.Add($"- {candidate.Tool.Name}: fit={candidate.FitScore:F2}{capabilities}");
        }

        if (constraints.Count > 0)
        {
            reasoningParts.Add($"Applied {constraints.Count} constraint(s): {string.Join(", ", constraints.Select(c => c.Name))}.");
        }

        return string.Join(" ", reasoningParts);
    }

    private static List<ToolCandidate> ApplySingleConstraint(
        List<ToolCandidate> candidates,
        Constraint constraint)
    {
        // Parse constraint rules and filter candidates
        // Supported constraint formats:
        // - "exclude:tool_name" - excludes a specific tool
        // - "require:capability" - requires a specific capability
        // - "max_cost:0.5" - maximum cost threshold
        // - "min_quality:0.7" - minimum quality threshold

        string rule = constraint.Rule.Trim().ToLowerInvariant();

        if (rule.StartsWith("exclude:", StringComparison.Ordinal))
        {
            string toolName = rule.Substring("exclude:".Length).Trim();
            return candidates
                .Where(c => !c.Tool.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (rule.StartsWith("require:", StringComparison.Ordinal))
        {
            string capability = rule.Substring("require:".Length).Trim();
            return candidates
                .Where(c => c.MatchedCapabilities.Any(cap =>
                    cap.Contains(capability, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (rule.StartsWith("max_cost:", StringComparison.Ordinal))
        {
            string costStr = rule.Substring("max_cost:".Length).Trim();
            if (double.TryParse(costStr, out double maxCost))
            {
                return candidates
                    .Where(c => c.CostScore <= maxCost)
                    .ToList();
            }
        }

        if (rule.StartsWith("min_quality:", StringComparison.Ordinal))
        {
            string qualityStr = rule.Substring("min_quality:".Length).Trim();
            if (double.TryParse(qualityStr, out double minQuality))
            {
                return candidates
                    .Where(c => c.QualityScore >= minQuality)
                    .ToList();
            }
        }

        // Unknown constraint format - return candidates unchanged
        return candidates;
    }

    private static bool IsToolBlockedByConstraint(ITool tool, Constraint constraint)
    {
        string rule = constraint.Rule.Trim().ToLowerInvariant();

        if (rule.StartsWith("exclude:", StringComparison.Ordinal))
        {
            string toolName = rule.Substring("exclude:".Length).Trim();
            return tool.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

/// <summary>
/// Represents the state of a tool selection reasoning step.
/// </summary>
/// <param name="Goal">The goal that tools were selected for.</param>
/// <param name="Selection">The resulting tool selection.</param>
/// <param name="Timestamp">When the selection was made.</param>
public sealed record ToolSelectionState(
    Goal Goal,
    ToolSelection Selection,
    DateTime Timestamp) : ReasoningState(
        Kind: "ToolSelection",
        Text: $"Selected {Selection.SelectedTools.Count} tools for '{Goal.Description}' (confidence: {Selection.ConfidenceScore:P0})")
{
    /// <summary>
    /// Gets a summary of this selection state.
    /// </summary>
    /// <returns>A human-readable summary.</returns>
    public string GetSummary() =>
        $"Selected {Selection.SelectedTools.Count} tools for '{Goal.Description}' (confidence: {Selection.ConfidenceScore:P0})";
}
