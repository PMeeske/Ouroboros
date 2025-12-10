// <copyright file="TransitionEdge.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace LangChainPipeline.Network;

/// <summary>
/// Represents a first-class transition between monad nodes in the DAG.
/// Captures transformation metadata including operation details and metrics.
/// </summary>
public sealed record TransitionEdge
{
    /// <summary>
    /// Gets the unique identifier for this transition.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the IDs of input nodes for this transition (one or more).
    /// </summary>
    public ImmutableArray<Guid> InputIds { get; init; }

    /// <summary>
    /// Gets the ID of the resulting output node.
    /// </summary>
    public Guid OutputId { get; init; }

    /// <summary>
    /// Gets the name of the operation that produced this transition (e.g., "UseCritique", "UseImprove").
    /// </summary>
    public string OperationName { get; init; }

    /// <summary>
    /// Gets the serialized JSON specification of the operation (parameters, prompts, config).
    /// </summary>
    public string OperationSpecJson { get; init; }

    /// <summary>
    /// Gets the optional confidence metric for this transition.
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// Gets the optional duration in milliseconds for this transition.
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Gets the Merkle hash of this edge.
    /// </summary>
    public string Hash { get; init; }

    /// <summary>
    /// Gets the timestamp when this transition was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionEdge"/> class.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="inputIds">The input node IDs.</param>
    /// <param name="outputId">The output node ID.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationSpecJson">The operation specification JSON.</param>
    /// <param name="createdAt">The creation timestamp.</param>
    /// <param name="confidence">Optional confidence metric.</param>
    /// <param name="durationMs">Optional duration in milliseconds.</param>
    public TransitionEdge(
        Guid id,
        ImmutableArray<Guid> inputIds,
        Guid outputId,
        string operationName,
        string operationSpecJson,
        DateTimeOffset createdAt,
        double? confidence = null,
        long? durationMs = null)
    {
        if (inputIds.IsDefaultOrEmpty)
        {
            throw new ArgumentException("InputIds cannot be empty", nameof(inputIds));
        }

        this.Id = id;
        this.InputIds = inputIds;
        this.OutputId = outputId;
        this.OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
        this.OperationSpecJson = operationSpecJson ?? throw new ArgumentNullException(nameof(operationSpecJson));
        this.CreatedAt = createdAt;
        this.Confidence = confidence;
        this.DurationMs = durationMs;
        this.Hash = this.ComputeHash();
    }

    /// <summary>
    /// Creates a new TransitionEdge with the specified parameters.
    /// </summary>
    /// <param name="inputIds">The input node IDs.</param>
    /// <param name="outputId">The output node ID.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationSpec">The operation specification object.</param>
    /// <param name="confidence">Optional confidence metric.</param>
    /// <param name="durationMs">Optional duration in milliseconds.</param>
    /// <returns>A new TransitionEdge.</returns>
    public static TransitionEdge Create(
        ImmutableArray<Guid> inputIds,
        Guid outputId,
        string operationName,
        object operationSpec,
        double? confidence = null,
        long? durationMs = null)
    {
        var specJson = JsonSerializer.Serialize(operationSpec, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        return new TransitionEdge(
            Guid.NewGuid(),
            inputIds,
            outputId,
            operationName,
            specJson,
            DateTimeOffset.UtcNow,
            confidence,
            durationMs);
    }

    /// <summary>
    /// Creates a simple transition from a single input to an output.
    /// </summary>
    /// <param name="inputId">The input node ID.</param>
    /// <param name="outputId">The output node ID.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="operationSpec">The operation specification object.</param>
    /// <param name="confidence">Optional confidence metric.</param>
    /// <param name="durationMs">Optional duration in milliseconds.</param>
    /// <returns>A new TransitionEdge.</returns>
    public static TransitionEdge CreateSimple(
        Guid inputId,
        Guid outputId,
        string operationName,
        object operationSpec,
        double? confidence = null,
        long? durationMs = null)
    {
        return Create(
            ImmutableArray.Create(inputId),
            outputId,
            operationName,
            operationSpec,
            confidence,
            durationMs);
    }

    /// <summary>
    /// Deserializes the operation specification as a specific type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized specification or None if deserialization fails.</returns>
    public Option<T> DeserializeOperationSpec<T>()
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(this.OperationSpecJson);
            return result is not null ? Option<T>.Some(result) : Option<T>.None();
        }
        catch
        {
            return Option<T>.None();
        }
    }

    /// <summary>
    /// Computes the Merkle hash for this edge.
    /// </summary>
    /// <returns>The computed hash as a hexadecimal string.</returns>
    private string ComputeHash()
    {
        var hashInput = new StringBuilder();
        hashInput.Append(string.Join(",", this.InputIds.Select(i => i.ToString())));
        hashInput.Append('|');
        hashInput.Append(this.OutputId);
        hashInput.Append('|');
        hashInput.Append(this.OperationName);
        hashInput.Append('|');
        hashInput.Append(this.OperationSpecJson);
        hashInput.Append('|');
        hashInput.Append(this.CreatedAt.ToUnixTimeMilliseconds());
        
        if (this.Confidence.HasValue)
        {
            hashInput.Append('|');
            hashInput.Append(this.Confidence.Value);
        }

        if (this.DurationMs.HasValue)
        {
            hashInput.Append('|');
            hashInput.Append(this.DurationMs.Value);
        }

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
