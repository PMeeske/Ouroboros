// <copyright file="SimpleModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Agent.MetaLearning;

/// <summary>
/// Simple implementation of IModel for testing and demonstration.
/// Wraps a basic prediction function with parameter management.
/// </summary>
public class SimpleModel : IModel
{
    private Dictionary<string, object> parameters;
    private readonly Func<string, Dictionary<string, object>, string> predictionFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleModel"/> class.
    /// </summary>
    /// <param name="predictionFunc">Function that makes predictions given input and parameters.</param>
    /// <param name="initialParameters">Initial model parameters.</param>
    public SimpleModel(
        Func<string, Dictionary<string, object>, string> predictionFunc,
        Dictionary<string, object>? initialParameters = null)
    {
        this.predictionFunc = predictionFunc ?? throw new ArgumentNullException(nameof(predictionFunc));
        this.parameters = initialParameters ?? new Dictionary<string, object>();
    }

    /// <inheritdoc/>
    public Task<string> PredictAsync(string input, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = this.predictionFunc(input, this.parameters);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<IModel> CloneAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var clonedParams = new Dictionary<string, object>(this.parameters);
        IModel cloned = new SimpleModel(this.predictionFunc, clonedParams);
        return Task.FromResult(cloned);
    }

    /// <inheritdoc/>
    public Task UpdateParametersAsync(
        Dictionary<string, object> gradients,
        double learningRate,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var (key, gradient) in gradients)
        {
            if (this.parameters.ContainsKey(key))
            {
                // Simple gradient descent: param = param - learningRate * gradient
                if (this.parameters[key] is double paramValue && gradient is double gradValue)
                {
                    this.parameters[key] = paramValue - (learningRate * gradValue);
                }
                else if (this.parameters[key] is double[] paramArray && gradient is double[] gradArray)
                {
                    var updated = new double[paramArray.Length];
                    for (var i = 0; i < paramArray.Length; i++)
                    {
                        updated[i] = paramArray[i] - (learningRate * gradArray[i]);
                    }

                    this.parameters[key] = updated;
                }
            }
            else
            {
                this.parameters[key] = gradient;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, object>> GetParametersAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new Dictionary<string, object>(this.parameters));
    }

    /// <inheritdoc/>
    public Task SetParametersAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        this.parameters = new Dictionary<string, object>(parameters);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, object>> ComputeGradientsAsync(
        List<Example> examples,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Simple mock gradient computation
        // In a real implementation, this would compute actual gradients via backpropagation
        var gradients = new Dictionary<string, object>();

        foreach (var (key, value) in this.parameters)
        {
            if (value is double doubleValue)
            {
                // Mock gradient: random small value
                gradients[key] = Random.Shared.NextDouble() * 0.01;
            }
            else if (value is double[] arrayValue)
            {
                var gradArray = new double[arrayValue.Length];
                for (var i = 0; i < arrayValue.Length; i++)
                {
                    gradArray[i] = Random.Shared.NextDouble() * 0.01;
                }

                gradients[key] = gradArray;
            }
        }

        return Task.FromResult(gradients);
    }
}
