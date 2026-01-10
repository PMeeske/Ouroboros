// <copyright file="SimpleTerminalPredictor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Simple terminal state predictor using logistic regression on state features.
/// Follows immutable and functional programming principles.
/// </summary>
public sealed class SimpleTerminalPredictor : ITerminalPredictor
{
    private readonly float[] weights;
    private readonly float bias;
    private readonly float threshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleTerminalPredictor"/> class.
    /// </summary>
    /// <param name="weights">Weight vector for logistic prediction.</param>
    /// <param name="bias">Bias term.</param>
    /// <param name="threshold">Classification threshold (default 0.5).</param>
    public SimpleTerminalPredictor(float[] weights, float bias, float threshold = 0.5f)
    {
        this.weights = weights;
        this.bias = bias;
        this.threshold = threshold;
    }

    /// <inheritdoc/>
    public Task<bool> PredictAsync(State state, CancellationToken ct = default)
    {
        // Logistic regression
        float logit = this.bias;
        for (int i = 0; i < Math.Min(state.Embedding.Length, this.weights.Length); i++)
        {
            logit += state.Embedding[i] * this.weights[i];
        }

        // Sigmoid activation
        float probability = 1.0f / (1.0f + (float)Math.Exp(-logit));

        bool isTerminal = probability >= this.threshold;
        return Task.FromResult(isTerminal);
    }

    /// <summary>
    /// Creates a randomly initialized terminal predictor.
    /// </summary>
    /// <param name="featureSize">Size of the state embedding.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A new terminal predictor.</returns>
    public static SimpleTerminalPredictor CreateRandom(int featureSize, int seed = 42)
    {
        var random = new Random(seed);
        var weights = new float[featureSize];
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = (float)(random.NextDouble() * 2 - 1) * 0.1f;
        }

        return new SimpleTerminalPredictor(weights, -2.0f); // Bias towards non-terminal
    }
}
