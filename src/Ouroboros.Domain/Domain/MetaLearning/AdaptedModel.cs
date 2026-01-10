// <copyright file="AdaptedModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents a model that has been adapted to a specific task through few-shot learning.
/// Contains the adapted model along with performance metrics.
/// </summary>
/// <param name="Model">The adapted model instance.</param>
/// <param name="AdaptationSteps">Number of gradient steps used for adaptation.</param>
/// <param name="ValidationPerformance">Performance on validation set (e.g., accuracy, loss).</param>
/// <param name="AdaptationTime">Time taken for the adaptation process.</param>
public sealed record AdaptedModel(
    IModel Model,
    int AdaptationSteps,
    double ValidationPerformance,
    TimeSpan AdaptationTime)
{
    /// <summary>
    /// Creates an adapted model from a base model.
    /// </summary>
    /// <param name="model">The adapted model.</param>
    /// <param name="adaptationSteps">Number of adaptation steps performed.</param>
    /// <param name="validationPerformance">Performance metric on validation set.</param>
    /// <param name="adaptationTime">Time elapsed during adaptation.</param>
    /// <returns>A new AdaptedModel instance.</returns>
    public static AdaptedModel Create(
        IModel model,
        int adaptationSteps,
        double validationPerformance,
        TimeSpan adaptationTime) =>
        new(model, adaptationSteps, validationPerformance, adaptationTime);

    /// <summary>
    /// Gets the adaptation speed in steps per second.
    /// </summary>
    public double StepsPerSecond =>
        AdaptationTime.TotalSeconds > 0 ? AdaptationSteps / AdaptationTime.TotalSeconds : 0;

    /// <summary>
    /// Indicates whether the adaptation was successful based on a performance threshold.
    /// </summary>
    /// <param name="threshold">Minimum acceptable performance.</param>
    /// <returns>True if validation performance meets or exceeds threshold.</returns>
    public bool IsSuccessful(double threshold) => ValidationPerformance >= threshold;
}
