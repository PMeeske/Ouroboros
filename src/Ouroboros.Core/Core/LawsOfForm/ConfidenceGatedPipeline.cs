// <copyright file="ConfidenceGatedPipeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;

/// <summary>
/// Pipeline steps for gating LLM responses based on confidence levels.
/// Uses Forms to represent confidence as three-valued certainty.
/// </summary>
public static class ConfidenceGatedPipeline
{
    /// <summary>
    /// Gates an LLM response: only proceeds if confidence is marked (certain).
    /// </summary>
    /// <param name="threshold">The confidence threshold (default 0.8).</param>
    /// <returns>A step that returns the response if confident, None otherwise.</returns>
    public static Step<LlmResponse, Option<LlmResponse>> GateByConfidence(double threshold = 0.8)
    {
        return async response =>
        {
            var form = response.Confidence.ToForm(highThreshold: threshold, lowThreshold: threshold * 0.5);

            if (form.IsMark())
            {
                return Option<LlmResponse>.Some(response);
            }

            return Option<LlmResponse>.None();
        };
    }

    /// <summary>
    /// Routes an LLM response to different handlers based on confidence level.
    /// </summary>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="onHighConfidence">Handler for high confidence responses (Mark).</param>
    /// <param name="onLowConfidence">Handler for low confidence responses (Void).</param>
    /// <param name="onUncertain">Handler for uncertain responses (Imaginary).</param>
    /// <param name="highThreshold">Threshold for high confidence (default 0.8).</param>
    /// <param name="lowThreshold">Threshold for low confidence (default 0.3).</param>
    /// <returns>A step that routes the response based on confidence.</returns>
    public static Step<LlmResponse, TOutput> RouteByConfidence<TOutput>(
        Func<LlmResponse, TOutput> onHighConfidence,
        Func<LlmResponse, TOutput> onLowConfidence,
        Func<LlmResponse, TOutput> onUncertain,
        double highThreshold = 0.8,
        double lowThreshold = 0.3)
    {
        return async response =>
        {
            var form = response.Confidence.ToForm(highThreshold, lowThreshold);

            return form.Match(
                onMark: () => onHighConfidence(response),
                onVoid: () => onLowConfidence(response),
                onImaginary: () => onUncertain(response));
        };
    }

    /// <summary>
    /// Combines multiple LLM responses/opinions using Laws of Form superposition.
    /// Useful for ensemble models or multi-agent consensus.
    /// </summary>
    /// <param name="opinions">Weighted opinions as (form, weight) tuples.</param>
    /// <returns>The superposed form representing consensus.</returns>
    public static Form CombineOpinions(params (Form opinion, double weight)[] opinions)
    {
        return FormExtensions.Superposition(opinions);
    }

    /// <summary>
    /// Creates a confidence-gated pipeline step with Result monad.
    /// </summary>
    /// <param name="threshold">The confidence threshold.</param>
    /// <returns>A step that returns Success if confident, Failure otherwise.</returns>
    public static Step<LlmResponse, Result<LlmResponse, string>> GateByConfidenceResult(double threshold = 0.8)
    {
        return async response =>
        {
            var form = response.Confidence.ToForm(highThreshold: threshold, lowThreshold: threshold * 0.5);

            return form.ToResult(
                response,
                $"Response confidence {response.Confidence:F2} is below threshold {threshold:F2}");
        };
    }

    /// <summary>
    /// Filters a batch of responses, keeping only those above the confidence threshold.
    /// </summary>
    /// <param name="threshold">The confidence threshold.</param>
    /// <returns>A step that filters responses by confidence.</returns>
    public static Step<IEnumerable<LlmResponse>, IEnumerable<LlmResponse>> FilterByConfidence(double threshold = 0.8)
    {
        return async responses =>
        {
            return responses.Where(r => r.Confidence >= threshold).ToList();
        };
    }

    /// <summary>
    /// Aggregates multiple LLM responses with different confidence levels.
    /// Returns a consolidated response based on weighted consensus.
    /// </summary>
    /// <param name="responses">The responses to aggregate.</param>
    /// <param name="highThreshold">Threshold for Mark state (default 0.8).</param>
    /// <param name="lowThreshold">Threshold for Void state (default 0.3).</param>
    /// <returns>A Result containing the consensus if reached, or an error.</returns>
    public static Result<LlmResponse, string> AggregateResponses(
        IEnumerable<LlmResponse> responses,
        double highThreshold = 0.8,
        double lowThreshold = 0.3)
    {
        var responseList = responses.ToList();

        if (responseList.Count == 0)
        {
            return Result<LlmResponse, string>.Failure("No responses to aggregate");
        }

        // Convert each response confidence to a form with weight
        var opinions = responseList
            .Select(r => (r.Confidence.ToForm(highThreshold, lowThreshold), 1.0))
            .ToArray();

        var consensus = FormExtensions.Superposition(opinions);

        if (consensus.IsImaginary())
        {
            return Result<LlmResponse, string>.Failure(
                $"No clear consensus among {responseList.Count} responses");
        }

        // Return the response with highest confidence
        var bestResponse = responseList.OrderByDescending(r => r.Confidence).First();

        return Result<LlmResponse, string>.Success(bestResponse);
    }
}

/// <summary>
/// Represents an LLM response with confidence information.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// Gets the response text from the LLM.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets the confidence score (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets optional tool calls in the response.
    /// </summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; init; }

    /// <summary>
    /// Gets optional metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; }

    /// <summary>
    /// Gets the model that generated this response.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// Gets the timestamp of the response.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmResponse"/> class.
    /// </summary>
    /// <param name="text">The response text.</param>
    /// <param name="confidence">The confidence score.</param>
    /// <param name="toolCalls">Optional tool calls.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="modelName">Optional model name.</param>
    /// <param name="timestamp">Optional timestamp.</param>
    public LlmResponse(
        string text,
        double confidence = 1.0,
        IReadOnlyList<ToolCall>? toolCalls = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        string? modelName = null,
        DateTime? timestamp = null)
    {
        this.Text = text;
        this.Confidence = Math.Clamp(confidence, 0.0, 1.0);
        this.ToolCalls = toolCalls ?? Array.Empty<ToolCall>();
        this.Metadata = metadata ?? new Dictionary<string, object>();
        this.ModelName = modelName;
        this.Timestamp = timestamp ?? DateTime.UtcNow;
    }
}
