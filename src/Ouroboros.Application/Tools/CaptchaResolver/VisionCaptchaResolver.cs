// <copyright file="VisionCaptchaResolver.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.CaptchaResolver;

using System.Text.Json;
using Ouroboros.Application.Mcp;

/// <summary>
/// CAPTCHA resolver that uses vision AI to analyze the page screenshot
/// and extract search results visually when CAPTCHA is present.
/// </summary>
public class VisionCaptchaResolver : ICaptchaResolverStrategy
{
    private readonly PlaywrightMcpTool? _playwrightTool;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisionCaptchaResolver"/> class.
    /// </summary>
    /// <param name="playwrightTool">The Playwright MCP tool for browser automation.</param>
    public VisionCaptchaResolver(PlaywrightMcpTool? playwrightTool)
    {
        _playwrightTool = playwrightTool;
    }

    /// <inheritdoc/>
    public string Name => "VisionAI";

    /// <inheritdoc/>
    public int Priority => 100; // High priority - tried first

    /// <inheritdoc/>
    public CaptchaDetectionResult DetectCaptcha(string content, string url)
    {
        var contentLower = content.ToLowerInvariant();

        // DuckDuckGo CAPTCHA patterns
        if (contentLower.Contains("please complete the following challenge") ||
            contentLower.Contains("confirm this search was made by a human") ||
            contentLower.Contains("unusual traffic") ||
            contentLower.Contains("robot check") ||
            contentLower.Contains("are you a robot"))
        {
            return new CaptchaDetectionResult(true, "DuckDuckGo-Challenge", ExtractChallengeText(content));
        }

        // Google reCAPTCHA
        if (contentLower.Contains("recaptcha") ||
            contentLower.Contains("g-recaptcha") ||
            contentLower.Contains("unusual traffic from your computer"))
        {
            return new CaptchaDetectionResult(true, "Google-reCAPTCHA", null);
        }

        // Cloudflare challenge
        if (contentLower.Contains("checking your browser") ||
            contentLower.Contains("cloudflare") && contentLower.Contains("challenge"))
        {
            return new CaptchaDetectionResult(true, "Cloudflare-Challenge", null);
        }

        // hCaptcha
        if (contentLower.Contains("hcaptcha"))
        {
            return new CaptchaDetectionResult(true, "hCaptcha", null);
        }

        // Generic bot detection
        if (contentLower.Contains("verify you are human") ||
            contentLower.Contains("bot detection") ||
            contentLower.Contains("access denied") && contentLower.Contains("automated"))
        {
            return new CaptchaDetectionResult(true, "Generic-BotDetection", null);
        }

        return new CaptchaDetectionResult(false, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<CaptchaResolutionResult> ResolveAsync(
        string originalUrl,
        string captchaContent,
        CancellationToken ct = default)
    {
        if (_playwrightTool == null)
        {
            return new CaptchaResolutionResult(
                false,
                ErrorMessage: "Vision resolver requires Playwright tool but none is configured");
        }

        try
        {
            // Navigate to the URL using a real browser
            var navArgs = new Dictionary<string, object>
            {
                { "action", "navigate" },
                { "url", originalUrl }
            };
            await _playwrightTool.InvokeAsync(JsonSerializer.Serialize(navArgs), ct);

            // Wait a bit for any JS to execute
            await Task.Delay(2000, ct);

            // Take a screenshot
            var screenshotArgs = new Dictionary<string, object>
            {
                { "action", "screenshot" }
            };
            await _playwrightTool.InvokeAsync(JsonSerializer.Serialize(screenshotArgs), ct);

            // Use vision AI to analyze the screenshot
            var visionResult = await _playwrightTool.GetVisionAnalysisForLastScreenshotAsync(ct);

            return visionResult.Match(
                analysis =>
                {
                    if (string.IsNullOrWhiteSpace(analysis))
                    {
                        return new CaptchaResolutionResult(false, ErrorMessage: "Vision analysis returned empty result");
                    }

                    // Check if vision result also shows CAPTCHA (page didn't bypass)
                    var detectionCheck = DetectCaptcha(analysis, originalUrl);
                    if (detectionCheck.IsCaptcha)
                    {
                        return new CaptchaResolutionResult(
                            false,
                            ErrorMessage: $"CAPTCHA still present after browser navigation ({detectionCheck.CaptchaType})");
                    }

                    return new CaptchaResolutionResult(true, ResolvedContent: analysis);
                },
                error => new CaptchaResolutionResult(false, ErrorMessage: $"Vision analysis failed: {error}"));
        }
        catch (Exception ex)
        {
            return new CaptchaResolutionResult(false, ErrorMessage: $"Vision resolver error: {ex.Message}");
        }
    }

    private static string? ExtractChallengeText(string content)
    {
        // Try to extract the actual challenge description
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("challenge", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("verify", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 10 && trimmed.Length < 200)
                {
                    return trimmed;
                }
            }
        }

        return null;
    }
}
