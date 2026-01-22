// <copyright file="Vector3.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Embodied;

/// <summary>
/// Represents a 3D vector for position, movement, or velocity in embodied simulation.
/// Immutable record for thread-safe operations.
/// </summary>
/// <param name="X">X coordinate</param>
/// <param name="Y">Y coordinate</param>
/// <param name="Z">Z coordinate</param>
public sealed record Vector3(float X, float Y, float Z)
{
    /// <summary>
    /// Gets a zero vector (0, 0, 0).
    /// </summary>
    public static Vector3 Zero => new(0f, 0f, 0f);

    /// <summary>
    /// Gets a unit vector along the X axis (1, 0, 0).
    /// </summary>
    public static Vector3 UnitX => new(1f, 0f, 0f);

    /// <summary>
    /// Gets a unit vector along the Y axis (0, 1, 0).
    /// </summary>
    public static Vector3 UnitY => new(0f, 1f, 0f);

    /// <summary>
    /// Gets a unit vector along the Z axis (0, 0, 1).
    /// </summary>
    public static Vector3 UnitZ => new(0f, 0f, 1f);

    /// <summary>
    /// Calculates the magnitude (length) of the vector.
    /// </summary>
    /// <returns>The magnitude of the vector</returns>
    public float Magnitude() => MathF.Sqrt((this.X * this.X) + (this.Y * this.Y) + (this.Z * this.Z));

    /// <summary>
    /// Returns a normalized version of the vector (unit length).
    /// </summary>
    /// <returns>Normalized vector</returns>
    public Vector3 Normalized()
    {
        var mag = this.Magnitude();
        return mag > 0 ? new Vector3(this.X / mag, this.Y / mag, this.Z / mag) : Zero;
    }

    /// <summary>
    /// Adds two vectors component-wise.
    /// </summary>
    public static Vector3 operator +(Vector3 a, Vector3 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>
    /// Subtracts two vectors component-wise.
    /// </summary>
    public static Vector3 operator -(Vector3 a, Vector3 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>
    /// Multiplies a vector by a scalar.
    /// </summary>
    public static Vector3 operator *(Vector3 v, float scalar) =>
        new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    /// <summary>
    /// Multiplies a vector by a scalar.
    /// </summary>
    public static Vector3 operator *(float scalar, Vector3 v) =>
        new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    /// <summary>
    /// Calculates the dot product of two vectors.
    /// </summary>
    public static float Dot(Vector3 a, Vector3 b) =>
        (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    /// <summary>
    /// Calculates the cross product of two vectors.
    /// </summary>
    public static Vector3 Cross(Vector3 a, Vector3 b) =>
        new(
            (a.Y * b.Z) - (a.Z * b.Y),
            (a.Z * b.X) - (a.X * b.Z),
            (a.X * b.Y) - (a.Y * b.X));
}
