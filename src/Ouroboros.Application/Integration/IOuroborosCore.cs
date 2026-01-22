// <copyright file="IOuroborosCore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.WorldModel;
using Ouroboros.Core.Learning;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Reasoning;
using Ouroboros.Core.Synthesis;
using Ouroboros.Domain.Benchmarks;
using Ouroboros.Domain.Embodied;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.MetaLearning;
using Ouroboros.Domain.MultiAgent;
using Ouroboros.Domain.Reflection;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Memory;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using Episode = Ouroboros.Pipeline.Memory.Episode;

/// <summary>
/// Core interface for the unified Ouroboros AGI system.
/// Provides access to all feature engines and unified operations.
/// </summary>
public interface IOuroborosCore
{
    // Core engines - Tier 1
    /// <summary>Gets the episodic memory engine for long-term memory storage.</summary>
    IEpisodicMemoryEngine EpisodicMemory { get; }

    /// <summary>Gets the adapter learning engine for LoRA/PEFT adaptation.</summary>
    IAdapterLearningEngine AdapterLearning { get; }

    /// <summary>Gets the advanced MeTTa reasoning engine for symbolic AI.</summary>
    IAdvancedMeTTaEngine MeTTaReasoning { get; }

    /// <summary>Gets the hierarchical planner for multi-level planning.</summary>
    IHierarchicalPlanner HierarchicalPlanner { get; }

    /// <summary>Gets the reflection engine for meta-cognitive analysis.</summary>
    IReflectionEngine Reflection { get; }

    /// <summary>Gets the benchmark suite for evaluation.</summary>
    IBenchmarkSuite Benchmarks { get; }

    // Tier 2 engines
    /// <summary>Gets the program synthesis engine for code generation.</summary>
    IProgramSynthesisEngine ProgramSynthesis { get; }

    /// <summary>Gets the world model engine for model-based RL.</summary>
    IWorldModelEngine WorldModel { get; }

    /// <summary>Gets the multi-agent coordinator for collaborative intelligence.</summary>
    IMultiAgentCoordinator MultiAgent { get; }

    /// <summary>Gets the causal reasoning engine implementing Pearl's framework.</summary>
    ICausalReasoningEngine CausalReasoning { get; }

    // Tier 3 engines
    /// <summary>Gets the meta-learning engine for fast task adaptation.</summary>
    IMetaLearningEngine MetaLearning { get; }

    /// <summary>Gets the embodied agent for grounded cognition.</summary>
    IEmbodiedAgent EmbodiedAgent { get; }

    /// <summary>Gets the consciousness scaffold for global workspace integration.</summary>
    IConsciousnessScaffold Consciousness { get; }

    // Unified operations
    /// <summary>
    /// Executes a goal using the configured cognitive pipeline.
    /// Integrates hierarchical planning, episodic memory, and causal reasoning.
    /// </summary>
    /// <param name="goal">The goal to execute.</param>
    /// <param name="config">Configuration for execution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing execution result or error message.</returns>
    Task<Result<ExecutionResult, string>> ExecuteGoalAsync(
        string goal,
        ExecutionConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Learns from past experiences by consolidating memories and updating adapters.
    /// </summary>
    /// <param name="experiences">Episodes to learn from.</param>
    /// <param name="config">Configuration for learning.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing learning result or error message.</returns>
    Task<Result<LearningResult, string>> LearnFromExperienceAsync(
        List<Episode> experiences,
        LearningConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Performs unified reasoning combining symbolic, causal, and abductive methods.
    /// </summary>
    /// <param name="query">The query to reason about.</param>
    /// <param name="config">Configuration for reasoning.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing reasoning result or error message.</returns>
    Task<Result<ReasoningResult, string>> ReasonAboutAsync(
        string query,
        ReasoningConfig config,
        CancellationToken ct = default);
}

/// <summary>
/// Configuration for goal execution.
/// </summary>
public sealed record ExecutionConfig(
    bool UseEpisodicMemory = true,
    bool UseCausalReasoning = true,
    bool UseHierarchicalPlanning = true,
    bool UseWorldModel = false,
    int MaxPlanningDepth = 10,
    TimeSpan Timeout = default)
{
    /// <summary>Gets the default execution configuration.</summary>
    public static ExecutionConfig Default => new();
}

/// <summary>
/// Configuration for learning from experience.
/// </summary>
public sealed record LearningConfig(
    bool ConsolidateMemories = true,
    bool UpdateAdapters = true,
    bool ExtractRules = true,
    ConsolidationStrategy ConsolidationStrategy = ConsolidationStrategy.Abstract)
{
    /// <summary>Gets the default learning configuration.</summary>
    public static LearningConfig Default => new();
}

/// <summary>
/// Configuration for reasoning operations.
/// </summary>
public sealed record ReasoningConfig(
    bool UseSymbolicReasoning = true,
    bool UseCausalInference = true,
    bool UseAbduction = true,
    int MaxInferenceSteps = 100)
{
    /// <summary>Gets the default reasoning configuration.</summary>
    public static ReasoningConfig Default => new();
}

/// <summary>
/// Result of goal execution.
/// </summary>
public sealed record ExecutionResult(
    bool Success,
    string Output,
    PipelineBranch ReasoningTrace,
    Plan? ExecutedPlan,
    List<Episode> GeneratedEpisodes,
    TimeSpan Duration);

/// <summary>
/// Result of learning from experience.
/// </summary>
public sealed record LearningResult(
    int EpisodesProcessed,
    int RulesLearned,
    int AdaptersUpdated,
    double PerformanceImprovement,
    List<Insight> Insights);

/// <summary>
/// Represents an insight learned from experience.
/// </summary>
public sealed record Insight(
    string Description,
    double Confidence,
    List<Episode> SupportingEpisodes);

/// <summary>
/// Result of reasoning operations.
/// </summary>
public sealed record ReasoningResult(
    string Answer,
    Form Certainty,
    List<Fact> SupportingFacts,
    ProofTrace? Proof,
    CausalGraph? RelevantCauses);
