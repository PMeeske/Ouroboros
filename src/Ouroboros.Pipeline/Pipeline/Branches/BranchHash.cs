// <copyright file="BranchHash.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;

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

        // Build a deterministic string representation for hashing
        // This avoids complex JSON serialization issues with polymorphic types
        var sb = new StringBuilder();
        sb.Append("NAME:").Append(snapshot.Name).Append('|');
        sb.Append("EVENTS:").Append(snapshot.Events.Count).Append('|');
        
        foreach (var evt in snapshot.Events)
        {
            sb.Append("E{");
            sb.Append(evt.Id).Append(',');
            sb.Append(evt.Timestamp.Ticks).Append(',');
            sb.Append(evt.GetType().Name);
            sb.Append("}|");
        }
        
        sb.Append("VECTORS:").Append(snapshot.Vectors.Count).Append('|');
        foreach (var vec in snapshot.Vectors)
        {
            sb.Append("V{");
            sb.Append(vec.Id).Append(',');
            sb.Append(vec.Text).Append(',');
            if (vec.Embedding != null)
            {
                sb.Append(string.Join(",", vec.Embedding.Select(f => f.ToString("F6"))));
            }
            sb.Append("}|");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

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
