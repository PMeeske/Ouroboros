// <copyright file="ICaptchaResolverStrategy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.CaptchaResolver;

/// <summary>
/// Represents the result of a CAPTCHA detection check.
/// </summary>
public record CaptchaDetectionResult(
    bool IsCaptcha,
    string CaptchaType,
    string? ChallengeText = null);

/// <summary>
/// Represents the result of a CAPTCHA resolution attempt.
/// </summary>
public record CaptchaResolutionResult(
    bool Success,
    string? ResolvedContent = null,
    string? ErrorMessage = null);

/// <summary>
/// Strategy interface for detecting and resolving CAPTCHAs encountered during web searches.
/// </summary>
public interface ICaptchaResolverStrategy
{
    /// <summary>
    /// Gets the name of this resolver strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority of this strategy (higher = tried first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Detects if the given content contains a CAPTCHA challenge.
    /// </summary>
    /// <param name="content">The HTML content or text to analyze.</param>
    /// <param name="url">The URL that produced this content.</param>
    /// <returns>Detection result indicating if CAPTCHA was found and its type.</returns>
    CaptchaDetectionResult DetectCaptcha(string content, string url);

    /// <summary>
    /// Attempts to resolve the CAPTCHA and retrieve the actual search results.
    /// </summary>
    /// <param name="originalUrl">The original search URL.</param>
    /// <param name="captchaContent">The content containing the CAPTCHA.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resolution result with the actual content if successful.</returns>
    Task<CaptchaResolutionResult> ResolveAsync(
        string originalUrl,
        string captchaContent,
        CancellationToken ct = default);
}
