// <copyright file="IModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents a machine learning model that can be adapted through meta-learning.
/// Provides interfaces for prediction, cloning, and parameter updates.
/// </summary>
public interface IModel
{
    /// <summary>
    /// Makes a prediction given an input.
    /// </summary>
    /// <param name="input">The input text or prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The model's prediction or output.</returns>
    Task<string> PredictAsync(string input, CancellationToken ct = default);

    /// <summary>
    /// Creates a deep copy of this model with independent parameters.
    /// Used for task-specific adaptation in meta-learning.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A cloned model with the same architecture and current parameters.</returns>
    Task<IModel> CloneAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates model parameters using computed gradients.
    /// Implements gradient descent or similar optimization.
    /// </summary>
    /// <param name="gradients">Dictionary of parameter names to gradient values.</param>
    /// <param name="learningRate">Learning rate for the update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the update operation.</returns>
    Task UpdateParametersAsync(
        Dictionary<string, object> gradients,
        double learningRate,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the current model parameters.
    /// Used for computing meta-gradients and saving state.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of parameter names to parameter values.</returns>
    Task<Dictionary<string, object>> GetParametersAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets model parameters directly.
    /// Used for restoring state or applying meta-updates.
    /// </summary>
    /// <param name="parameters">Dictionary of parameter names to parameter values.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task representing the operation.</returns>
    Task SetParametersAsync(Dictionary<string, object> parameters, CancellationToken ct = default);

    /// <summary>
    /// Computes gradients for the model given input-output pairs.
    /// Used in meta-learning algorithms for adaptation.
    /// </summary>
    /// <param name="examples">Input-output pairs for gradient computation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of parameter names to gradient values.</returns>
    Task<Dictionary<string, object>> ComputeGradientsAsync(
        List<Example> examples,
        CancellationToken ct = default);
}
