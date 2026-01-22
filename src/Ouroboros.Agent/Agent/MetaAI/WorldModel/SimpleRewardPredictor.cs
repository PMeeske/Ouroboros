// <copyright file="SimpleRewardPredictor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Simple reward predictor using linear regression on state-action features.
/// Follows immutable and functional programming principles.
/// </summary>
public sealed class SimpleRewardPredictor : IRewardPredictor
{
    private readonly float[] weights;
    private readonly float bias;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleRewardPredictor"/> class.
    /// </summary>
    /// <param name="weights">Weight vector for linear prediction.</param>
    /// <param name="bias">Bias term.</param>
    public SimpleRewardPredictor(float[] weights, float bias)
    {
        this.weights = weights;
        this.bias = bias;
    }

    /// <inheritdoc/>
    public Task<double> PredictAsync(State current, Action action, State next, CancellationToken ct = default)
    {
        // Combine embeddings for feature vector
        var features = this.CombineFeatures(current.Embedding, action, next.Embedding);

        // Linear prediction
        float prediction = this.bias;
        for (int i = 0; i < Math.Min(features.Length, this.weights.Length); i++)
        {
            prediction += features[i] * this.weights[i];
        }

        return Task.FromResult((double)prediction);
    }

    /// <summary>
    /// Creates a randomly initialized reward predictor.
    /// </summary>
    /// <param name="featureSize">Size of the feature vector.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A new reward predictor.</returns>
    public static SimpleRewardPredictor CreateRandom(int featureSize, int seed = 42)
    {
        var random = new Random(seed);
        var weights = new float[featureSize];
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = (float)(random.NextDouble() * 2 - 1) * 0.1f;
        }

        return new SimpleRewardPredictor(weights, 0.0f);
    }

    private float[] CombineFeatures(float[] stateEmb, Action action, float[] nextEmb)
    {
        // Simple concatenation - could use more sophisticated feature engineering
        var actionHash = (float)action.Name.GetHashCode() / int.MaxValue;
        var features = new float[stateEmb.Length + nextEmb.Length + 1];

        Array.Copy(stateEmb, 0, features, 0, stateEmb.Length);
        features[stateEmb.Length] = actionHash;
        Array.Copy(nextEmb, 0, features, stateEmb.Length + 1, nextEmb.Length);

        return features;
    }
}
