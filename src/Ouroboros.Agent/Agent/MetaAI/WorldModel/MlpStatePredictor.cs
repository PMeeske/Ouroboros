// <copyright file="MlpStatePredictor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Multi-layer perceptron based state predictor.
/// Simple feed-forward neural network for state transition prediction.
/// Follows immutable and functional programming principles.
/// </summary>
public sealed class MlpStatePredictor : IStatePredictor
{
    private readonly int inputSize;
    private readonly int hiddenSize;
    private readonly int outputSize;
    private readonly float[][] weightsInputHidden;
    private readonly float[] biasHidden;
    private readonly float[][] weightsHiddenOutput;
    private readonly float[] biasOutput;

    /// <summary>
    /// Initializes a new instance of the <see cref="MlpStatePredictor"/> class.
    /// </summary>
    /// <param name="inputSize">Size of input layer (state + action embedding).</param>
    /// <param name="hiddenSize">Size of hidden layer.</param>
    /// <param name="outputSize">Size of output layer (next state embedding).</param>
    /// <param name="weightsInputHidden">Weights from input to hidden layer.</param>
    /// <param name="biasHidden">Bias for hidden layer.</param>
    /// <param name="weightsHiddenOutput">Weights from hidden to output layer.</param>
    /// <param name="biasOutput">Bias for output layer.</param>
    public MlpStatePredictor(
        int inputSize,
        int hiddenSize,
        int outputSize,
        float[][] weightsInputHidden,
        float[] biasHidden,
        float[][] weightsHiddenOutput,
        float[] biasOutput)
    {
        this.inputSize = inputSize;
        this.hiddenSize = hiddenSize;
        this.outputSize = outputSize;
        this.weightsInputHidden = weightsInputHidden;
        this.biasHidden = biasHidden;
        this.weightsHiddenOutput = weightsHiddenOutput;
        this.biasOutput = biasOutput;
    }

    /// <inheritdoc/>
    public Task<State> PredictAsync(State current, Action action, CancellationToken ct = default)
    {
        // Concatenate state embedding with action embedding
        var actionEmbedding = this.EncodeAction(action);
        var input = this.ConcatenateEmbeddings(current.Embedding, actionEmbedding);

        // Forward pass through network
        var hidden = this.ForwardLayer(input, this.weightsInputHidden, this.biasHidden, relu: true);
        var output = this.ForwardLayer(hidden, this.weightsHiddenOutput, this.biasOutput, relu: false);

        // Create new state with predicted embedding
        var predictedState = new State(
            Features: new Dictionary<string, object>(current.Features),
            Embedding: output);

        return Task.FromResult(predictedState);
    }

    /// <summary>
    /// Creates a randomly initialized MLP predictor.
    /// </summary>
    /// <param name="stateSize">Size of state embeddings.</param>
    /// <param name="actionSize">Size of action embeddings.</param>
    /// <param name="hiddenSize">Size of hidden layer.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A new MLP state predictor.</returns>
    public static MlpStatePredictor CreateRandom(int stateSize, int actionSize, int hiddenSize, int seed = 42)
    {
        var random = new Random(seed);
        int inputSize = stateSize + actionSize;

        // Xavier initialization
        float inputScale = (float)Math.Sqrt(2.0 / inputSize);
        float hiddenScale = (float)Math.Sqrt(2.0 / hiddenSize);

        var weightsInputHidden = InitializeWeights(random, inputSize, hiddenSize, inputScale);
        var biasHidden = new float[hiddenSize];
        var weightsHiddenOutput = InitializeWeights(random, hiddenSize, stateSize, hiddenScale);
        var biasOutput = new float[stateSize];

        return new MlpStatePredictor(
            inputSize,
            hiddenSize,
            stateSize,
            weightsInputHidden,
            biasHidden,
            weightsHiddenOutput,
            biasOutput);
    }

    private static float[][] InitializeWeights(Random random, int rows, int cols, float scale)
    {
        var weights = new float[rows][];
        for (int i = 0; i < rows; i++)
        {
            weights[i] = new float[cols];
            for (int j = 0; j < cols; j++)
            {
                weights[i][j] = (float)(random.NextDouble() * 2 - 1) * scale;
            }
        }

        return weights;
    }

    private float[] EncodeAction(Action action)
    {
        // Simple hash-based encoding - in practice would use learned embeddings
        var hash = action.Name.GetHashCode();
        var embedding = new float[10];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)Math.Sin(hash * (i + 1) * 0.1);
        }

        return embedding;
    }

    private float[] ConcatenateEmbeddings(float[] a, float[] b)
    {
        var result = new float[a.Length + b.Length];
        Array.Copy(a, 0, result, 0, a.Length);
        Array.Copy(b, 0, result, a.Length, b.Length);
        return result;
    }

    private float[] ForwardLayer(float[] input, float[][] weights, float[] bias, bool relu)
    {
        var output = new float[weights[0].Length];

        // Matrix multiplication: input * weights + bias
        for (int j = 0; j < output.Length; j++)
        {
            float sum = bias[j];
            for (int i = 0; i < input.Length; i++)
            {
                sum += input[i] * weights[i][j];
            }

            output[j] = relu ? Math.Max(0, sum) : sum;
        }

        return output;
    }
}
