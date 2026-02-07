// <copyright file="WeightedConnection.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Represents a weighted connection between two neurons.
/// Positive weights are excitatory, negative weights are inhibitory.
/// Supports Hebbian learning for adaptive connection strength.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class WeightedConnection
{
    private readonly object _lock = new();
    private long _activationCount;
    private DateTimeOffset _lastActivation;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeightedConnection"/> class.
    /// </summary>
    /// <param name="sourceNeuronId">Source neuron identifier.</param>
    /// <param name="targetNeuronId">Target neuron identifier.</param>
    /// <param name="initialWeight">Initial connection weight (clamped to [-1.0, 1.0]).</param>
    /// <param name="plasticityRate">Learning rate for Hebbian updates.</param>
    public WeightedConnection(
        string sourceNeuronId,
        string targetNeuronId,
        double initialWeight = 1.0,
        double plasticityRate = 0.01)
    {
        SourceNeuronId = sourceNeuronId;
        TargetNeuronId = targetNeuronId;
        Weight = Math.Clamp(initialWeight, -1.0, 1.0);
        PlasticityRate = plasticityRate;
    }

    /// <summary>
    /// Gets the source neuron identifier.
    /// </summary>
    public string SourceNeuronId { get; init; }

    /// <summary>
    /// Gets the target neuron identifier.
    /// </summary>
    public string TargetNeuronId { get; init; }

    /// <summary>
    /// Gets the connection weight. Range: [-1.0, 1.0].
    /// Positive = excitatory, Negative = inhibitory, Zero = no connection.
    /// </summary>
    public double Weight { get; private set; }

    /// <summary>
    /// Gets the learning rate for Hebbian updates.
    /// </summary>
    public double PlasticityRate { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this connection is frozen (no further learning).
    /// </summary>
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Gets the number of times this connection has been activated.
    /// </summary>
    public long ActivationCount
    {
        get
        {
            lock (_lock)
            {
                return _activationCount;
            }
        }
    }

    /// <summary>
    /// Gets the last time this connection was activated.
    /// </summary>
    public DateTimeOffset LastActivation
    {
        get
        {
            lock (_lock)
            {
                return _lastActivation;
            }
        }
    }

    /// <summary>
    /// Hebbian update: strengthens connection when source and target co-activate.
    /// ΔW = η * pre * post (simplified Hebb rule).
    /// With decay toward zero for anti-Hebbian dynamics.
    /// Thread-safe for concurrent updates.
    /// </summary>
    /// <param name="sourceActive">Whether the source neuron is active.</param>
    /// <param name="targetActive">Whether the target neuron is active.</param>
    public void HebbianUpdate(bool sourceActive, bool targetActive)
    {
        lock (_lock)
        {
            if (IsFrozen)
            {
                return;
            }

            if (sourceActive && targetActive)
            {
                // Strengthen: neurons that fire together wire together
                Weight += PlasticityRate * (1.0 - Math.Abs(Weight));
            }
            else if (sourceActive && !targetActive)
            {
                // Weaken: source fires but target doesn't respond
                Weight -= PlasticityRate * Math.Abs(Weight) * 0.5;
            }

            // If neither fires, no update (stability)
            Weight = Math.Clamp(Weight, -1.0, 1.0);
        }
    }

    /// <summary>
    /// Records an activation event.
    /// Thread-safe for concurrent calls.
    /// </summary>
    public void RecordActivation()
    {
        lock (_lock)
        {
            _activationCount++;
            _lastActivation = DateTimeOffset.UtcNow;
        }
    }
}
