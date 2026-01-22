// <copyright file="MetaModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents a meta-trained model that can rapidly adapt to new tasks.
/// Encapsulates the model, its meta-learned parameters, and training configuration.
/// </summary>
/// <param name="Id">Unique identifier for this meta-model.</param>
/// <param name="InnerModel">The underlying model that was meta-trained.</param>
/// <param name="Config">Configuration used for meta-training.</param>
/// <param name="MetaParameters">Meta-learned parameters (e.g., learned learning rates for Meta-SGD).</param>
/// <param name="TrainedAt">Timestamp when meta-training completed.</param>
public sealed record MetaModel(
    Guid Id,
    IModel InnerModel,
    MetaLearningConfig Config,
    Dictionary<string, object> MetaParameters,
    DateTime TrainedAt)
{
    /// <summary>
    /// Creates a new meta-model with the current timestamp.
    /// </summary>
    /// <param name="innerModel">The meta-trained model.</param>
    /// <param name="config">Meta-learning configuration.</param>
    /// <param name="metaParameters">Meta-learned parameters.</param>
    /// <returns>A new MetaModel instance.</returns>
    public static MetaModel Create(
        IModel innerModel,
        MetaLearningConfig config,
        Dictionary<string, object> metaParameters) =>
        new(Guid.NewGuid(), innerModel, config, metaParameters, DateTime.UtcNow);

    /// <summary>
    /// Gets a metadata value by key.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The parameter value if present, null otherwise.</returns>
    public object? GetMetaParameter(string key) =>
        MetaParameters.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Updates a single meta-parameter.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <param name="value">The new value.</param>
    /// <returns>A new MetaModel with the updated parameter.</returns>
    public MetaModel WithMetaParameter(string key, object value)
    {
        var newParams = new Dictionary<string, object>(MetaParameters) { [key] = value };
        return this with { MetaParameters = newParams };
    }

    /// <summary>
    /// Gets the age of this meta-model (time since training).
    /// </summary>
    public TimeSpan Age => DateTime.UtcNow - TrainedAt;
}
