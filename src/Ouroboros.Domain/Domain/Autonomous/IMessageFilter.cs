// <copyright file="IMessageFilter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Interface for filtering neuron messages before routing.
/// Enables ethical evaluation and other checks on inter-neuron messaging.
/// </summary>
public interface IMessageFilter
{
    /// <summary>
    /// Evaluates whether a message should be routed.
    /// </summary>
    /// <param name="message">The message to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the message is permitted, false if it should be blocked.</returns>
    Task<bool> ShouldRouteAsync(NeuronMessage message, CancellationToken ct = default);
}
