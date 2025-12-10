// <copyright file="BranchHash.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LangChainPipeline.Pipeline.Branches;

/// <summary>
/// Provides hash integrity checking for pipeline branch snapshots.
/// Uses SHA-256 for deterministic content hashing.
/// </summary>
public static class BranchHash
{
    /// <summary>
    /// Computes a deterministic SHA-256 hash of a branch snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot to hash.</param>
    /// <returns>A hex-encoded SHA-256 hash string.</returns>
    public static string ComputeHash(BranchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Serialize to JSON with consistent formatting for deterministic hashing
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        string json = JsonSerializer.Serialize(snapshot, options);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(bytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that a snapshot's hash matches the expected hash.
    /// </summary>
    /// <param name="snapshot">The snapshot to verify.</param>
    /// <param name="expectedHash">The expected hash value.</param>
    /// <returns>True if the hash matches, false otherwise.</returns>
    public static bool VerifyHash(BranchSnapshot snapshot, string expectedHash)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(expectedHash);

        string computedHash = ComputeHash(snapshot);
        return string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a snapshot with an embedded hash for integrity verification.
    /// </summary>
    /// <param name="snapshot">The snapshot to hash.</param>
    /// <returns>A tuple containing the snapshot and its hash.</returns>
    public static (BranchSnapshot Snapshot, string Hash) WithHash(BranchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string hash = ComputeHash(snapshot);
        return (snapshot, hash);
    }
}
