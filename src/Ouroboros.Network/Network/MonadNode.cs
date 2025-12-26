// <copyright file="MonadNode.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Ouroboros.Network;

/// <summary>
/// Represents a reified monadic value as a data object in the Merkle-DAG.
/// Can represent ReasoningState, Result, Option, or any arbitrary payload.
/// </summary>
public sealed record MonadNode
{
    /// <summary>
    /// Gets the unique identifier for this node.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the type name of the monad or state (e.g., "Draft", "FinalSpec", "Result", "Option").
    /// </summary>
    public string TypeName { get; init; }

    /// <summary>
    /// Gets the serialized JSON payload of the monadic value.
    /// </summary>
    public string PayloadJson { get; init; }

    /// <summary>
    /// Gets the timestamp when this node was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the IDs of parent nodes in the DAG (empty for root nodes).
    /// </summary>
    public ImmutableArray<Guid> ParentIds { get; init; }

    /// <summary>
    /// Gets the Merkle hash of this node.
    /// Computed from: Hash(TypeName + PayloadJson + ParentIds + CreatedAt).
    /// </summary>
    public string Hash { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MonadNode"/> class.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="typeName">The type name of the monad/state.</param>
    /// <param name="payloadJson">The serialized JSON payload.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="parentIds">The parent node IDs.</param>
    public MonadNode(
        Guid id,
        string typeName,
        string payloadJson,
        DateTimeOffset createdAt,
        ImmutableArray<Guid> parentIds)
    {
        this.Id = id;
        this.TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        this.PayloadJson = payloadJson ?? throw new ArgumentNullException(nameof(payloadJson));
        this.CreatedAt = createdAt;
        this.ParentIds = parentIds;
        this.Hash = this.ComputeHash();
    }

    /// <summary>
    /// Creates a new MonadNode from a ReasoningState.
    /// </summary>
    /// <param name="state">The reasoning state to reify.</param>
    /// <param name="parentIds">Optional parent node IDs.</param>
    /// <returns>A new MonadNode representing the reasoning state.</returns>
    public static MonadNode FromReasoningState(
        ReasoningState state,
        ImmutableArray<Guid> parentIds = default)
    {
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        return new MonadNode(
            Guid.NewGuid(),
            state.Kind,
            json,
            DateTimeOffset.UtcNow,
            parentIds.IsDefault ? ImmutableArray<Guid>.Empty : parentIds);
    }

    /// <summary>
    /// Creates a new MonadNode from a generic payload.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    /// <param name="typeName">The type name for the node.</param>
    /// <param name="payload">The payload to serialize.</param>
    /// <param name="parentIds">Optional parent node IDs.</param>
    /// <returns>A new MonadNode representing the payload.</returns>
    public static MonadNode FromPayload<T>(
        string typeName,
        T payload,
        ImmutableArray<Guid> parentIds = default)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        return new MonadNode(
            Guid.NewGuid(),
            typeName,
            json,
            DateTimeOffset.UtcNow,
            parentIds.IsDefault ? ImmutableArray<Guid>.Empty : parentIds);
    }

    /// <summary>
    /// Deserializes the payload as a specific type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized payload or None if deserialization fails.</returns>
    public Option<T> DeserializePayload<T>()
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(this.PayloadJson);
            return result is not null ? Option<T>.Some(result) : Option<T>.None();
        }
        catch
        {
            return Option<T>.None();
        }
    }

    /// <summary>
    /// Computes the Merkle hash for this node.
    /// </summary>
    /// <returns>The computed hash as a hexadecimal string.</returns>
    private string ComputeHash()
    {
        var hashInput = new StringBuilder();
        hashInput.Append(this.TypeName);
        hashInput.Append('|');
        hashInput.Append(this.PayloadJson);
        hashInput.Append('|');
        hashInput.Append(string.Join(",", this.ParentIds.Select(p => p.ToString())));
        hashInput.Append('|');
        hashInput.Append(this.CreatedAt.ToUnixTimeMilliseconds());

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashInput.ToString()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that the stored hash matches the computed hash.
    /// </summary>
    /// <returns>True if the hash is valid, false otherwise.</returns>
    public bool VerifyHash()
    {
        return this.Hash == this.ComputeHash();
    }
}
