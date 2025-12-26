#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Cost-Aware Router - Balance quality vs. cost
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents cost information for a model or operation.
/// </summary>
public sealed record CostInfo(
    string ResourceId,
    double CostPerToken,
    double CostPerRequest,
    double EstimatedQuality);

/// <summary>
/// Represents a cost-benefit analysis result.
/// </summary>
public sealed record CostBenefitAnalysis(
    string RecommendedRoute,
    double EstimatedCost,
    double EstimatedQuality,
    double ValueScore,
    string Rationale);

/// <summary>
/// Configuration for cost-aware routing.
/// </summary>
public sealed record CostAwareRoutingConfig(
    double MaxCostPerPlan = 1.0,
    double MinAcceptableQuality = 0.7,
    CostOptimizationStrategy Strategy = CostOptimizationStrategy.Balanced);

/// <summary>
/// Cost optimization strategy.
/// </summary>
public enum CostOptimizationStrategy
{
    MinimizeCost,
    MaximizeQuality,
    Balanced,
    MaximizeValue // Best quality per cost
}

/// <summary>
/// Interface for cost-aware routing capabilities.
/// </summary>
public interface ICostAwareRouter
{
    /// <summary>
    /// Routes a task considering cost and quality tradeoffs.
    /// </summary>
    Task<Result<CostBenefitAnalysis, string>> RouteWithCostAwarenessAsync(
        string task,
        Dictionary<string, object>? context = null,
        CostAwareRoutingConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates the cost of executing a plan.
    /// </summary>
    Task<double> EstimatePlanCostAsync(Plan plan, CancellationToken ct = default);

    /// <summary>
    /// Optimizes a plan to reduce cost while maintaining quality.
    /// </summary>
    Task<Result<Plan, string>> OptimizePlanForCostAsync(
        Plan plan,
        CostAwareRoutingConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Registers cost information for a resource.
    /// </summary>
    void RegisterCostInfo(CostInfo costInfo);
}

/// <summary>
/// Implementation of cost-aware routing for balancing quality vs. cost.
/// </summary>
public sealed class CostAwareRouter : ICostAwareRouter
{
    private readonly IUncertaintyRouter _uncertaintyRouter;
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private readonly Dictionary<string, CostInfo> _costRegistry = new();

    public CostAwareRouter(
        IUncertaintyRouter uncertaintyRouter,
        IMetaAIPlannerOrchestrator orchestrator)
    {
        _uncertaintyRouter = uncertaintyRouter ?? throw new ArgumentNullException(nameof(uncertaintyRouter));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));

        // Register default costs
        RegisterDefaultCosts();
    }

    /// <summary>
    /// Routes a task considering cost and quality tradeoffs.
    /// </summary>
    public async Task<Result<CostBenefitAnalysis, string>> RouteWithCostAwarenessAsync(
        string task,
        Dictionary<string, object>? context = null,
        CostAwareRoutingConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new CostAwareRoutingConfig();

        try
        {
            // Get uncertainty-based routing
            Result<RoutingDecision, string> routingResult = await _uncertaintyRouter.RouteAsync(task, context, ct);

            if (!routingResult.IsSuccess)
            {
                return Result<CostBenefitAnalysis, string>.Failure(routingResult.Error);
            }

            RoutingDecision decision = routingResult.Value;

            // Get cost info for the route
            CostInfo costInfo = GetCostInfoForRoute(decision.Route);

            // Estimate cost and quality
            double estimatedCost = CalculateEstimatedCost(task, costInfo);
            double estimatedQuality = decision.Confidence * costInfo.EstimatedQuality;

            // Check constraints
            if (estimatedCost > config.MaxCostPerPlan)
            {
                // Try to find cheaper alternative
                CostInfo? cheaperRoute = FindCheaperRoute(config.MaxCostPerPlan, config.MinAcceptableQuality);
                if (cheaperRoute != null)
                {
                    costInfo = cheaperRoute;
                    estimatedCost = CalculateEstimatedCost(task, costInfo);
                    estimatedQuality = costInfo.EstimatedQuality;
                }
                else
                {
                    return Result<CostBenefitAnalysis, string>.Failure(
                        $"Cannot meet cost constraint of {config.MaxCostPerPlan}");
                }
            }

            if (estimatedQuality < config.MinAcceptableQuality)
            {
                return Result<CostBenefitAnalysis, string>.Failure(
                    $"Cannot meet quality requirement of {config.MinAcceptableQuality}");
            }

            // Calculate value score based on strategy
            double valueScore = CalculateValueScore(estimatedCost, estimatedQuality, config.Strategy);

            CostBenefitAnalysis analysis = new CostBenefitAnalysis(
                costInfo.ResourceId,
                estimatedCost,
                estimatedQuality,
                valueScore,
                $"Selected {costInfo.ResourceId} using {config.Strategy} strategy");

            return Result<CostBenefitAnalysis, string>.Success(analysis);
        }
        catch (Exception ex)
        {
            return Result<CostBenefitAnalysis, string>.Failure($"Cost-aware routing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Estimates the cost of executing a plan.
    /// </summary>
    public async Task<double> EstimatePlanCostAsync(Plan plan, CancellationToken ct = default)
    {
        double totalCost = 0;

        foreach (PlanStep step in plan.Steps)
        {
            CostInfo costInfo = GetCostInfoForRoute(step.Action);

            // Estimate tokens (simplified)
            int estimatedTokens = EstimateTokenCount(step);

            totalCost += costInfo.CostPerRequest;
            totalCost += costInfo.CostPerToken * estimatedTokens;
        }

        return await Task.FromResult(totalCost);
    }

    /// <summary>
    /// Optimizes a plan to reduce cost while maintaining quality.
    /// </summary>
    public async Task<Result<Plan, string>> OptimizePlanForCostAsync(
        Plan plan,
        CostAwareRoutingConfig config,
        CancellationToken ct = default)
    {
        try
        {
            double currentCost = await EstimatePlanCostAsync(plan, ct);

            if (currentCost <= config.MaxCostPerPlan)
            {
                // Already within budget
                return Result<Plan, string>.Success(plan);
            }

            // Try to optimize steps
            List<PlanStep> optimizedSteps = new List<PlanStep>();

            foreach (PlanStep step in plan.Steps)
            {
                // Check if we can use a cheaper alternative
                CostInfo currentCostInfo = GetCostInfoForRoute(step.Action);
                CostInfo? cheaperInfo = FindCheaperRoute(
                    currentCostInfo.CostPerRequest * 0.8, // 20% cheaper
                    config.MinAcceptableQuality);

                if (cheaperInfo != null)
                {
                    // Replace with cheaper alternative
                    optimizedSteps.Add(step with
                    {
                        Action = cheaperInfo.ResourceId,
                        ConfidenceScore = step.ConfidenceScore * 0.9 // Slight confidence reduction
                    });
                }
                else
                {
                    optimizedSteps.Add(step);
                }
            }

            Plan optimizedPlan = new Plan(
                plan.Goal,
                optimizedSteps,
                plan.ConfidenceScores,
                DateTime.UtcNow);

            double newCost = await EstimatePlanCostAsync(optimizedPlan, ct);

            if (newCost > config.MaxCostPerPlan)
            {
                return Result<Plan, string>.Failure(
                    $"Cannot optimize plan to meet cost constraint. Current: {newCost}, Max: {config.MaxCostPerPlan}");
            }

            return Result<Plan, string>.Success(optimizedPlan);
        }
        catch (Exception ex)
        {
            return Result<Plan, string>.Failure($"Plan optimization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Registers cost information for a resource.
    /// </summary>
    public void RegisterCostInfo(CostInfo costInfo)
    {
        ArgumentNullException.ThrowIfNull(costInfo);
        _costRegistry[costInfo.ResourceId] = costInfo;
    }

    private void RegisterDefaultCosts()
    {
        // Default cost information (example values)
        RegisterCostInfo(new CostInfo("gpt-4", 0.00003, 0.01, 0.95));
        RegisterCostInfo(new CostInfo("gpt-3.5-turbo", 0.000001, 0.001, 0.80));
        RegisterCostInfo(new CostInfo("llama3", 0.0000005, 0.0001, 0.75));
        RegisterCostInfo(new CostInfo("codellama", 0.0000005, 0.0001, 0.85));
        RegisterCostInfo(new CostInfo("default", 0.000001, 0.001, 0.70));
    }

    private CostInfo GetCostInfoForRoute(string route)
    {
        return _costRegistry.TryGetValue(route, out CostInfo? info)
            ? info
            : _costRegistry["default"];
    }

    private CostInfo? FindCheaperRoute(double maxCost, double minQuality)
    {
        return _costRegistry.Values
            .Where(ci => ci.CostPerRequest <= maxCost && ci.EstimatedQuality >= minQuality)
            .OrderBy(ci => ci.CostPerRequest)
            .FirstOrDefault();
    }

    private double CalculateEstimatedCost(string task, CostInfo costInfo)
    {
        // Estimate based on task length
        int estimatedTokens = task.Length / 4; // Rough approximation
        return costInfo.CostPerRequest + (costInfo.CostPerToken * estimatedTokens);
    }

    private int EstimateTokenCount(PlanStep step)
    {
        // Simple token estimation
        int actionLength = step.Action.Length;
        int paramsLength = System.Text.Json.JsonSerializer.Serialize(step.Parameters).Length;
        int outcomeLength = step.ExpectedOutcome.Length;

        return (actionLength + paramsLength + outcomeLength) / 4;
    }

    private double CalculateValueScore(double cost, double quality, CostOptimizationStrategy strategy)
    {
        return strategy switch
        {
            CostOptimizationStrategy.MinimizeCost => 1.0 / (cost + 0.001), // Lower cost = higher score
            CostOptimizationStrategy.MaximizeQuality => quality,
            CostOptimizationStrategy.MaximizeValue => quality / (cost + 0.001), // Quality per cost
            CostOptimizationStrategy.Balanced => (quality + (1.0 / (cost + 0.001))) / 2.0,
            _ => quality / (cost + 0.001)
        };
    }
}
