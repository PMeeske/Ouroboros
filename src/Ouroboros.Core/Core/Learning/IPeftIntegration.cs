// <copyright file="IPeftIntegration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for HuggingFace PEFT (Parameter-Efficient Fine-Tuning) integration.
/// Abstracts the communication with Python PEFT library via Python.NET or REST API.
/// </summary>
public interface IPeftIntegration
{
    /// <summary>
    /// Initializes a new PEFT adapter with the specified configuration.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="config">Adapter configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing adapter weights or error message.</returns>
    Task<Result<byte[], string>> InitializeAdapterAsync(
        string modelName,
        AdapterConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Trains an adapter on the provided examples.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="adapterWeights">Current adapter weights.</param>
    /// <param name="examples">Training examples.</param>
    /// <param name="config">Training configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing updated adapter weights or error message.</returns>
    Task<Result<byte[], string>> TrainAdapterAsync(
        string modelName,
        byte[] adapterWeights,
        List<TrainingExample> examples,
        TrainingConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Generates text using the base model with an adapter.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="adapterWeights">Adapter weights to use (null for base model).</param>
    /// <param name="prompt">Input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing generated text or error message.</returns>
    Task<Result<string, string>> GenerateAsync(
        string modelName,
        byte[]? adapterWeights,
        string prompt,
        CancellationToken ct = default);

    /// <summary>
    /// Merges multiple adapters using the specified strategy.
    /// </summary>
    /// <param name="modelName">Base model name/path.</param>
    /// <param name="adapterWeights">List of adapter weights to merge.</param>
    /// <param name="strategy">Merge strategy.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing merged adapter weights or error message.</returns>
    Task<Result<byte[], string>> MergeAdaptersAsync(
        string modelName,
        List<byte[]> adapterWeights,
        MergeStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Validates adapter weights and checks their size.
    /// </summary>
    /// <param name="weights">Adapter weights to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing size in bytes or error message.</returns>
    Task<Result<long, string>> ValidateAdapterAsync(byte[] weights, CancellationToken ct = default);
}
