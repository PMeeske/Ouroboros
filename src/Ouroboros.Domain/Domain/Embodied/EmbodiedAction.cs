// <copyright file="EmbodiedAction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Represents an action to be taken by an embodied agent.
/// Includes movement, rotation, and custom action parameters.
/// </summary>
/// <param name="Movement">Movement vector in local or world space</param>
/// <param name="Rotation">Rotation vector (euler angles or angular velocity)</param>
/// <param name="CustomActions">Additional action parameters (e.g., gripper force, jump)</param>
/// <param name="ActionName">Optional human-readable action name</param>
public sealed record EmbodiedAction(
    Vector3 Movement,
    Vector3 Rotation,
    IReadOnlyDictionary<string, float> CustomActions,
    string? ActionName = null)
{
    /// <summary>
    /// Creates a no-op action (no movement or rotation).
    /// </summary>
    /// <returns>No-op action</returns>
    public static EmbodiedAction NoOp() => new(
        Vector3.Zero,
        Vector3.Zero,
        new Dictionary<string, float>(),
        "NoOp");

    /// <summary>
    /// Creates a movement-only action.
    /// </summary>
    /// <param name="movement">Movement vector</param>
    /// <param name="actionName">Optional action name</param>
    /// <returns>Movement action</returns>
    public static EmbodiedAction Move(Vector3 movement, string? actionName = null) => new(
        movement,
        Vector3.Zero,
        new Dictionary<string, float>(),
        actionName ?? "Move");

    /// <summary>
    /// Creates a rotation-only action.
    /// </summary>
    /// <param name="rotation">Rotation vector</param>
    /// <param name="actionName">Optional action name</param>
    /// <returns>Rotation action</returns>
    public static EmbodiedAction Rotate(Vector3 rotation, string? actionName = null) => new(
        Vector3.Zero,
        rotation,
        new Dictionary<string, float>(),
        actionName ?? "Rotate");
}
