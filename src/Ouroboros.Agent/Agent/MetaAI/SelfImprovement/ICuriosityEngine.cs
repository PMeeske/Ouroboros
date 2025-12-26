#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Curiosity Engine Interface
// Intrinsic motivation and curiosity-driven exploration
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a novel exploration opportunity.
/// </summary>
public sealed record ExplorationOpportunity(
    string Description,
    double NoveltyScore,
    double InformationGainEstimate,
    List<string> Prerequisites,
    DateTime IdentifiedAt);

/// <summary>
/// Configuration for curiosity-driven behavior.
/// </summary>
public sealed record CuriosityEngineConfig(
    double ExplorationThreshold = 0.6,
    double ExploitationBias = 0.7,
    int MaxExplorationPerSession = 5,
    bool EnableSafeExploration = true,
    double MinSafetyScore = 0.8);

/// <summary>
/// Interface for curiosity-driven exploration capabilities.
/// Enables autonomous learning through intrinsic motivation.
/// </summary>
public interface ICuriosityEngine
{
    /// <summary>
    /// Computes the novelty score for a potential action or plan.
    /// </summary>
    /// <param name="plan">The plan to evaluate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Novelty score (0-1, higher = more novel)</returns>
    Task<double> ComputeNoveltyAsync(
        Plan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Generates an exploratory plan to learn something new.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Exploratory plan</returns>
    Task<Result<Plan, string>> GenerateExploratoryPlanAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Decides whether to explore or exploit based on current state.
    /// </summary>
    /// <param name="currentGoal">The current goal being considered</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if should explore, false if should exploit</returns>
    Task<bool> ShouldExploreAsync(
        string? currentGoal = null,
        CancellationToken ct = default);

    /// <summary>
    /// Identifies novel exploration opportunities.
    /// </summary>
    /// <param name="maxOpportunities">Maximum number of opportunities to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of exploration opportunities</returns>
    Task<List<ExplorationOpportunity>> IdentifyExplorationOpportunitiesAsync(
        int maxOpportunities = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates the information gain from exploring a particular area.
    /// </summary>
    /// <param name="explorationDescription">Description of what to explore</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Estimated information gain (0-1)</returns>
    Task<double> EstimateInformationGainAsync(
        string explorationDescription,
        CancellationToken ct = default);

    /// <summary>
    /// Records the outcome of an exploration attempt.
    /// </summary>
    /// <param name="plan">The exploratory plan that was executed</param>
    /// <param name="execution">The execution result</param>
    /// <param name="actualNovelty">The actual novelty discovered</param>
    void RecordExploration(Plan plan, ExecutionResult execution, double actualNovelty);

    /// <summary>
    /// Gets exploration statistics.
    /// </summary>
    /// <returns>Dictionary of exploration metrics</returns>
    Dictionary<string, double> GetExplorationStats();
}
