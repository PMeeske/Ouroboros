// <copyright file="FeedbackSignal.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Represents feedback for continual learning from user interactions.
/// </summary>
/// <param name="Type">The type of feedback signal.</param>
/// <param name="Score">Numerical score for the feedback (0.0 to 1.0 for success, -1.0 to 0.0 for failure).</param>
/// <param name="Correction">Optional correction text for user corrections.</param>
public sealed record FeedbackSignal(
    FeedbackType Type,
    double Score,
    string? Correction = null)
{
    /// <summary>
    /// Creates a user correction feedback signal.
    /// </summary>
    /// <param name="correction">The corrected output text.</param>
    /// <returns>A feedback signal representing user correction.</returns>
    public static FeedbackSignal UserCorrection(string correction) =>
        new(FeedbackType.UserCorrection, 1.0, correction);

    /// <summary>
    /// Creates a success signal.
    /// </summary>
    /// <param name="score">Success score (0.0 to 1.0).</param>
    /// <returns>A feedback signal representing success.</returns>
    public static FeedbackSignal Success(double score = 1.0) =>
        new(FeedbackType.SuccessSignal, Math.Clamp(score, 0.0, 1.0));

    /// <summary>
    /// Creates a failure signal.
    /// </summary>
    /// <param name="score">Failure score (-1.0 to 0.0).</param>
    /// <returns>A feedback signal representing failure.</returns>
    public static FeedbackSignal Failure(double score = -1.0) =>
        new(FeedbackType.FailureSignal, Math.Clamp(score, -1.0, 0.0));

    /// <summary>
    /// Creates a preference ranking feedback.
    /// </summary>
    /// <param name="score">Preference score (0.0 to 1.0).</param>
    /// <returns>A feedback signal representing preference ranking.</returns>
    public static FeedbackSignal Preference(double score) =>
        new(FeedbackType.PreferenceRanking, Math.Clamp(score, 0.0, 1.0));

    /// <summary>
    /// Validates the feedback signal.
    /// </summary>
    /// <returns>Success if valid, Failure with error message otherwise.</returns>
    public Monads.Result<FeedbackSignal, string> Validate()
    {
        if (this.Score < -1.0 || this.Score > 1.0)
        {
            return Monads.Result<FeedbackSignal, string>.Failure("Score must be between -1.0 and 1.0");
        }

        if (this.Type == FeedbackType.UserCorrection && string.IsNullOrWhiteSpace(this.Correction))
        {
            return Monads.Result<FeedbackSignal, string>.Failure("User correction requires correction text");
        }

        return Monads.Result<FeedbackSignal, string>.Success(this);
    }
}

/// <summary>
/// Types of feedback signals for continual learning.
/// </summary>
public enum FeedbackType
{
    /// <summary>
    /// User provided a correction to the generated output.
    /// </summary>
    UserCorrection,

    /// <summary>
    /// Signal indicating successful task completion.
    /// </summary>
    SuccessSignal,

    /// <summary>
    /// Signal indicating task failure.
    /// </summary>
    FailureSignal,

    /// <summary>
    /// Preference ranking between multiple outputs.
    /// </summary>
    PreferenceRanking,
}
