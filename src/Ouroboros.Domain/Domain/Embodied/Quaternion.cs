// <copyright file="Quaternion.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Represents a quaternion for rotation in 3D space.
/// Immutable record for thread-safe operations.
/// </summary>
/// <param name="X">X component</param>
/// <param name="Y">Y component</param>
/// <param name="Z">Z component</param>
/// <param name="W">W component (scalar part)</param>
public sealed record Quaternion(float X, float Y, float Z, float W)
{
    /// <summary>
    /// Gets the identity quaternion (no rotation).
    /// </summary>
    public static Quaternion Identity => new(0f, 0f, 0f, 1f);

    /// <summary>
    /// Calculates the magnitude of the quaternion.
    /// </summary>
    /// <returns>The magnitude</returns>
    public float Magnitude() => MathF.Sqrt((this.X * this.X) + (this.Y * this.Y) + (this.Z * this.Z) + (this.W * this.W));

    /// <summary>
    /// Returns a normalized version of the quaternion.
    /// </summary>
    /// <returns>Normalized quaternion</returns>
    public Quaternion Normalized()
    {
        var mag = this.Magnitude();
        return mag > 0
            ? new Quaternion(this.X / mag, this.Y / mag, this.Z / mag, this.W / mag)
            : Identity;
    }

    /// <summary>
    /// Returns the conjugate of the quaternion.
    /// </summary>
    /// <returns>Conjugate quaternion</returns>
    public Quaternion Conjugate() => new(-this.X, -this.Y, -this.Z, this.W);

    /// <summary>
    /// Multiplies two quaternions (combines rotations).
    /// </summary>
    public static Quaternion operator *(Quaternion a, Quaternion b) =>
        new(
            (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
            (a.W * b.Y) - (a.X * b.Z) + (a.Y * b.W) + (a.Z * b.X),
            (a.W * b.Z) + (a.X * b.Y) - (a.Y * b.X) + (a.Z * b.W),
            (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));
}
