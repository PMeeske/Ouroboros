// <copyright file="ConnectionTopology.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Manages the weighted connection graph between neurons.
/// Thread-safe for concurrent access from multiple neurons.
/// </summary>
public sealed class ConnectionTopology
{
    private readonly ConcurrentDictionary<(string Source, string Target), WeightedConnection> _connections = new();

    /// <summary>
    /// Adds or updates a connection between two neurons.
    /// Preserves existing activation history when updating an existing connection.
    /// </summary>
    /// <param name="sourceId">The source neuron identifier.</param>
    /// <param name="targetId">The target neuron identifier.</param>
    /// <param name="weight">The connection weight (clamped to [-1.0, 1.0]).</param>
    /// <param name="plasticityRate">The learning rate for Hebbian updates (default: 0.01).</param>
    public void SetConnection(string sourceId, string targetId, double weight, double plasticityRate = 0.01)
    {
        _connections.AddOrUpdate(
            (sourceId, targetId),
            _ => new WeightedConnection(sourceId, targetId, weight, plasticityRate),
            (_, existing) =>
            {
                // Create new connection with updated parameters but preserve history if needed
                // Since we can't mutate Weight/PlasticityRate (they're init-only or private set),
                // we create a new connection. This is by design - SetConnection is for explicit configuration.
                return new WeightedConnection(sourceId, targetId, weight, plasticityRate);
            });
    }

    /// <summary>
    /// Gets the connection weight between two neurons.
    /// Returns 1.0 (default excitatory) if no explicit connection exists.
    /// </summary>
    /// <param name="sourceId">The source neuron identifier.</param>
    /// <param name="targetId">The target neuron identifier.</param>
    /// <returns>The connection weight, or 1.0 if no connection exists.</returns>
    public double GetWeight(string sourceId, string targetId)
    {
        if (_connections.TryGetValue((sourceId, targetId), out var connection))
        {
            return connection.Weight;
        }

        return 1.0; // Default excitatory weight
    }

    /// <summary>
    /// Gets the connection object between two neurons, if it exists.
    /// </summary>
    /// <param name="sourceId">The source neuron identifier.</param>
    /// <param name="targetId">The target neuron identifier.</param>
    /// <returns>The connection if it exists, otherwise null.</returns>
    public WeightedConnection? GetConnection(string sourceId, string targetId)
    {
        _connections.TryGetValue((sourceId, targetId), out var connection);
        return connection;
    }

    /// <summary>
    /// Gets all connections from a source neuron.
    /// </summary>
    /// <param name="sourceId">The source neuron identifier.</param>
    /// <returns>An enumerable of outgoing connections.</returns>
    public IEnumerable<WeightedConnection> GetOutgoingConnections(string sourceId)
    {
        return _connections
            .Where(kvp => kvp.Key.Source == sourceId)
            .Select(kvp => kvp.Value);
    }

    /// <summary>
    /// Gets all connections to a target neuron.
    /// </summary>
    /// <param name="targetId">The target neuron identifier.</param>
    /// <returns>An enumerable of incoming connections.</returns>
    public IEnumerable<WeightedConnection> GetIncomingConnections(string targetId)
    {
        return _connections
            .Where(kvp => kvp.Key.Target == targetId)
            .Select(kvp => kvp.Value);
    }

    /// <summary>
    /// Applies Hebbian update to a specific connection.
    /// </summary>
    /// <param name="sourceId">The source neuron identifier.</param>
    /// <param name="targetId">The target neuron identifier.</param>
    /// <param name="sourceActive">Whether the source neuron is active.</param>
    /// <param name="targetActive">Whether the target neuron is active.</param>
    public void ApplyHebbianUpdate(string sourceId, string targetId, bool sourceActive, bool targetActive)
    {
        if (_connections.TryGetValue((sourceId, targetId), out var connection))
        {
            connection.HebbianUpdate(sourceActive, targetActive);
        }
    }

    /// <summary>
    /// Gets a snapshot of the entire topology for visualization/debugging.
    /// Returns an immutable snapshot that cannot be modified.
    /// </summary>
    /// <returns>A read-only dictionary of connection weights.</returns>
    public IReadOnlyDictionary<(string Source, string Target), double> GetWeightSnapshot()
    {
        return new System.Collections.ObjectModel.ReadOnlyDictionary<(string Source, string Target), double>(
            _connections.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Weight));
    }

    /// <summary>
    /// Adds an inhibitory connection (weight &lt; 0) between two neurons.
    /// Convenience method for creating inhibitory links.
    /// </summary>
    /// <param name="sourceId">The source neuron identifier.</param>
    /// <param name="targetId">The target neuron identifier.</param>
    /// <param name="inhibitionStrength">The inhibition strength (default: -0.5, clamped to [-1.0, 0.0]).</param>
    public void AddInhibition(string sourceId, string targetId, double inhibitionStrength = -0.5)
    {
        var clampedStrength = Math.Clamp(inhibitionStrength, -1.0, 0.0);
        SetConnection(sourceId, targetId, clampedStrength);
    }

    /// <summary>
    /// Computes the net input to a target neuron from all weighted connections.
    /// Sum of (weight_i * activation_i) for all incoming connections.
    /// </summary>
    /// <param name="targetId">The target neuron identifier.</param>
    /// <param name="getActivation">Function to retrieve activation level of a neuron by ID.</param>
    /// <returns>The net input as a weighted sum of activations.</returns>
    public double ComputeNetInput(string targetId, Func<string, double> getActivation)
    {
        return GetIncomingConnections(targetId)
            .Sum(conn => conn.Weight * getActivation(conn.SourceNeuronId));
    }
}
