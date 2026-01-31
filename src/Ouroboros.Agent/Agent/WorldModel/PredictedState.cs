// <copyright file="PredictedState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.WorldModel;

using Ouroboros.Domain.Embodied;

/// <summary>
/// Represents a predicted future state with associated metadata.
/// </summary>
/// <param name="State">Predicted sensor state</param>
/// <param name="Reward">Predicted reward</param>
/// <param name="Terminal">Whether state is terminal</param>
/// <param name="Confidence">Prediction confidence (0-1)</param>
/// <param name="Metadata">Additional prediction metadata</param>
public sealed record PredictedState(
    SensorState State,
    double Reward,
    bool Terminal,
    double Confidence,
    IReadOnlyDictionary<string, object> Metadata);
