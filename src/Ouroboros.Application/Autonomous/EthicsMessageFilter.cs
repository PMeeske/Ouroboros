// <copyright file="EthicsMessageFilter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Ethics;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.Application.Autonomous;

/// <summary>
/// Message filter that evaluates neuron messages through the ethics framework.
/// Safe topics bypass ethics evaluation for performance, while potentially dangerous
/// topics are evaluated before routing.
/// </summary>
public sealed class EthicsMessageFilter : IMessageFilter
{
    private readonly IEthicsFramework _ethicsFramework;
    private readonly ILogger<EthicsMessageFilter> _logger;

    // Safe topics that don't require ethics evaluation
    private static readonly HashSet<string> SafeTopics = new(StringComparer.OrdinalIgnoreCase)
    {
        "reflection.request",
        "reflection.request.response",
        "health.check",
        "health.check.response",
        "notification.send",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="EthicsMessageFilter"/> class.
    /// </summary>
    /// <param name="ethicsFramework">The ethics framework for evaluation.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public EthicsMessageFilter(IEthicsFramework ethicsFramework, ILogger<EthicsMessageFilter> logger)
    {
        ArgumentNullException.ThrowIfNull(ethicsFramework);
        ArgumentNullException.ThrowIfNull(logger);

        _ethicsFramework = ethicsFramework;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldRouteAsync(NeuronMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Safe topics bypass ethics evaluation
        if (IsSafeTopic(message.Topic))
        {
            return true;
        }

        try
        {
            // Construct a proposed action from the message
            var proposedAction = new ProposedAction
            {
                ActionType = "neuron_message",
                Description = $"Route message '{message.Topic}' from {message.SourceNeuron}" +
                             (message.TargetNeuron != null ? $" to {message.TargetNeuron}" : " (broadcast)"),
                Parameters = BuildParameters(message),
                TargetEntity = message.TargetNeuron,
                PotentialEffects = new[] { $"Deliver message with topic '{message.Topic}'" },
                Metadata = new Dictionary<string, object>
                {
                    ["MessageId"] = message.Id,
                    ["Topic"] = message.Topic,
                    ["Priority"] = message.Priority.ToString(),
                }
            };

            var actionContext = new ActionContext
            {
                AgentId = message.SourceNeuron,
                UserId = null, // Neuron messages don't have a specific user context
                Environment = "neural_network",
                State = new Dictionary<string, object>
                {
                    ["Topic"] = message.Topic,
                    ["CreatedAt"] = message.CreatedAt,
                }
            };

            // Evaluate the action through the ethics framework
            var result = await _ethicsFramework.EvaluateActionAsync(proposedAction, actionContext, ct);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Ethics evaluation failed for message {MessageId} with topic '{Topic}': {Error}",
                    message.Id,
                    message.Topic,
                    result.Error);
                return false;
            }

            var clearance = result.Value;

            if (!clearance.IsPermitted)
            {
                _logger.LogWarning(
                    "Message {MessageId} with topic '{Topic}' blocked by ethics framework. " +
                    "Level: {Level}, Reason: {Reasoning}",
                    message.Id,
                    message.Topic,
                    clearance.Level,
                    clearance.Reasoning);

                if (clearance.Violations.Count > 0)
                {
                    _logger.LogWarning(
                        "Ethics violations detected: {Violations}",
                        string.Join(", ", clearance.Violations.Select(v => v.Description)));
                }

                return false;
            }

            // Log concerns if present, but allow routing
            if (clearance.Concerns.Count > 0)
            {
                _logger.LogInformation(
                    "Message {MessageId} with topic '{Topic}' permitted with concerns: {Concerns}",
                    message.Id,
                    message.Topic,
                    string.Join(", ", clearance.Concerns.Select(c => c.Description)));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during ethics evaluation for message {MessageId} with topic '{Topic}'",
                message.Id,
                message.Topic);

            // Fail-safe: block messages that cannot be evaluated
            return false;
        }
    }

    private static bool IsSafeTopic(string topic)
    {
        // Check explicit safe topics
        if (SafeTopics.Contains(topic))
        {
            return true;
        }

        // Any topic ending in .response is considered safe (responses to already-evaluated requests)
        if (topic.EndsWith(".response", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, object> BuildParameters(NeuronMessage message)
    {
        var parameters = new Dictionary<string, object>
        {
            ["topic"] = message.Topic,
            ["source"] = message.SourceNeuron,
            ["priority"] = message.Priority.ToString(),
            ["created_at"] = message.CreatedAt,
        };

        if (message.TargetNeuron != null)
        {
            parameters["target"] = message.TargetNeuron;
        }

        if (message.CorrelationId.HasValue)
        {
            parameters["correlation_id"] = message.CorrelationId.Value;
        }

        if (message.ExpectsResponse)
        {
            parameters["expects_response"] = true;
        }

        return parameters;
    }
}
