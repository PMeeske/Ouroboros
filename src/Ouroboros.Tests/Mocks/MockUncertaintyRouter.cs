// <copyright file="MockUncertaintyRouter.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Mock implementation of IUncertaintyRouter for testing purposes.
/// Provides configurable routing behavior with confidence scores.
/// </summary>
public sealed class MockUncertaintyRouter : IUncertaintyRouter
{
    private readonly Func<string, Dictionary<string, object>?, RoutingDecision>? routingFactory;
    private readonly double minimumConfidence;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockUncertaintyRouter"/> class.
    /// </summary>
    /// <param name="minimumConfidence">Minimum confidence threshold (default: 0.7).</param>
    public MockUncertaintyRouter(double minimumConfidence = 0.7)
    {
        this.minimumConfidence = minimumConfidence;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockUncertaintyRouter"/> class with custom routing logic.
    /// </summary>
    /// <param name="routingFactory">Factory function for routing decisions.</param>
    /// <param name="minimumConfidence">Minimum confidence threshold.</param>
    public MockUncertaintyRouter(
        Func<string, Dictionary<string, object>?, RoutingDecision> routingFactory,
        double minimumConfidence = 0.7)
    {
        this.routingFactory = routingFactory;
        this.minimumConfidence = minimumConfidence;
    }

    /// <summary>
    /// Gets the minimum confidence threshold.
    /// </summary>
    public double MinimumConfidenceThreshold => this.minimumConfidence;

    /// <summary>
    /// Gets the number of times RouteAsync was called.
    /// </summary>
    public int RouteCallCount { get; private set; }

    /// <summary>
    /// Gets the last routing decision made.
    /// </summary>
    public RoutingDecision? LastDecision { get; private set; }

    /// <summary>
    /// Routes a task based on confidence analysis.
    /// </summary>
    public Task<Result<RoutingDecision, string>> RouteAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        this.RouteCallCount++;

        if (string.IsNullOrWhiteSpace(task))
        {
            return Task.FromResult(Result<RoutingDecision, string>.Failure("Task cannot be empty"));
        }

        RoutingDecision decision;
        if (this.routingFactory != null)
        {
            decision = this.routingFactory(task, context);
        }
        else
        {
            // Default: route to "default" with high confidence
            decision = new RoutingDecision(
                Route: "default",
                Reason: "Default routing for testing",
                Confidence: 0.9,
                Metadata: new Dictionary<string, object>());
        }

        this.LastDecision = decision;
        return Task.FromResult(Result<RoutingDecision, string>.Success(decision));
    }

    /// <summary>
    /// Determines fallback strategy for low-confidence scenarios.
    /// </summary>
    public FallbackStrategy DetermineFallback(string task, double confidence)
    {
        if (confidence >= this.minimumConfidence)
        {
            return FallbackStrategy.UseDefault;
        }

        if (confidence >= 0.5)
        {
            return FallbackStrategy.GatherMoreContext;
        }

        if (confidence >= 0.3)
        {
            return FallbackStrategy.DecomposeTask;
        }

        return FallbackStrategy.RequestClarification;
    }

    /// <summary>
    /// Calculates confidence for a given decision.
    /// </summary>
    public Task<double> CalculateConfidenceAsync(
        string task,
        string route,
        Dictionary<string, object>? context = null)
    {
        // Default: return high confidence
        return Task.FromResult(0.85);
    }

    /// <summary>
    /// Records routing outcome for metrics.
    /// </summary>
    public void RecordRoutingOutcome(RoutingDecision decision, bool success)
    {
        // No-op for mock
    }
}
