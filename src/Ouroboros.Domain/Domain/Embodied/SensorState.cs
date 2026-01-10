// <copyright file="SensorState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Represents the complete sensor state of an embodied agent.
/// Includes position, rotation, velocity, visual observations, and proprioceptive state.
/// </summary>
/// <param name="Position">3D position in world space</param>
/// <param name="Rotation">Rotation as quaternion</param>
/// <param name="Velocity">Velocity vector</param>
/// <param name="VisualObservation">Raw visual sensor data (flattened pixels or features)</param>
/// <param name="ProprioceptiveState">Internal state (joint positions, forces, etc.)</param>
/// <param name="CustomSensors">Additional custom sensor readings</param>
/// <param name="Timestamp">Time of observation</param>
public sealed record SensorState(
    Vector3 Position,
    Quaternion Rotation,
    Vector3 Velocity,
    float[] VisualObservation,
    float[] ProprioceptiveState,
    IReadOnlyDictionary<string, float> CustomSensors,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a default sensor state at origin with no motion.
    /// </summary>
    /// <returns>Default sensor state</returns>
    public static SensorState Default() => new(
        Vector3.Zero,
        Quaternion.Identity,
        Vector3.Zero,
        Array.Empty<float>(),
        Array.Empty<float>(),
        new Dictionary<string, float>(),
        DateTime.UtcNow);
}
