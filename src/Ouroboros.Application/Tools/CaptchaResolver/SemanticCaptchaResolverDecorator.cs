// <copyright file="SemanticCaptchaResolverDecorator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.CaptchaResolver;

using LangChainPipeline.Providers;

/// <summary>
/// Semantic analysis result for CAPTCHA content.
/// </summary>
public record SemanticCaptchaAnalysis(
    bool IsCaptcha,
    string CaptchaType,
    string? ChallengeDescription,
    string? SuggestedApproach,
    double Confidence);

/// <summary>
/// Decorator that adds semantic analysis capabilities to any CAPTCHA resolver strategy.
/// Uses an LLM to understand CAPTCHA challenges and provide intelligent resolution guidance.
/// </summary>
public class SemanticCaptchaResolverDecorator : ICaptchaResolverStrategy
{
    private readonly ICaptchaResolverStrategy _innerStrategy;
    private readonly ToolAwareChatModel _llm;
    private readonly bool _useSemanticDetection;
    private readonly bool _useSemanticGuidance;

    /// <summary>
    /// Initializes a new instance of the <see cref="SemanticCaptchaResolverDecorator"/> class.
    /// </summary>
    /// <param name="innerStrategy">The strategy to decorate.</param>
    /// <param name="llm">The LLM for semantic analysis.</param>
    /// <param name="useSemanticDetection">Whether to use LLM for CAPTCHA detection.</param>
    /// <param name="useSemanticGuidance">Whether to use LLM to guide resolution.</param>
    public SemanticCaptchaResolverDecorator(
        ICaptchaResolverStrategy innerStrategy,
        ToolAwareChatModel llm,
        bool useSemanticDetection = true,
        bool useSemanticGuidance = true)
    {
        _innerStrategy = innerStrategy ?? throw new ArgumentNullException(nameof(innerStrategy));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _useSemanticDetection = useSemanticDetection;
        _useSemanticGuidance = useSemanticGuidance;
    }

    /// <inheritdoc/>
    public string Name => $"Semantic({_innerStrategy.Name})";

    /// <inheritdoc/>
    public int Priority => _innerStrategy.Priority + 10; // Slightly higher priority than wrapped strategy

    /// <inheritdoc/>
    public CaptchaDetectionResult DetectCaptcha(string content, string url)
    {
        // First, use the inner strategy's detection
        var innerResult = _innerStrategy.DetectCaptcha(content, url);

        // If inner strategy detected CAPTCHA or semantic detection is disabled, return inner result
        if (innerResult.IsCaptcha || !_useSemanticDetection)
        {
            return innerResult;
        }

        // Use semantic analysis for edge cases the inner strategy might miss
        try
        {
            var analysis = AnalyzeCaptchaSemanticallySync(content, url);
            if (analysis.IsCaptcha && analysis.Confidence >= 0.7)
            {
                return new CaptchaDetectionResult(
                    true,
                    analysis.CaptchaType,
                    analysis.ChallengeDescription);
            }
        }
        catch
        {
            // Fall back to inner result if semantic analysis fails
        }

        return innerResult;
    }

    /// <inheritdoc/>
    public async Task<CaptchaResolutionResult> ResolveAsync(
        string originalUrl,
        string captchaContent,
        CancellationToken ct = default)
    {
        // Get semantic guidance if enabled
        SemanticCaptchaAnalysis? guidance = null;
        if (_useSemanticGuidance)
        {
            try
            {
                guidance = await AnalyzeCaptchaSemanticallyAsync(captchaContent, originalUrl, ct);
            }
            catch
            {
                // Continue without guidance if analysis fails
            }
        }

        // Attempt resolution with the inner strategy
        var result = await _innerStrategy.ResolveAsync(originalUrl, captchaContent, ct);

        // If inner strategy succeeded, enhance the result with semantic context
        if (result.Success && guidance != null)
        {
            return result with
            {
                ResolvedContent = $"{result.ResolvedContent}\n\n[Semantic Context: {guidance.CaptchaType} - {guidance.SuggestedApproach}]"
            };
        }

        // If inner strategy failed and we have guidance, provide detailed error
        if (!result.Success && guidance != null)
        {
            return result with
            {
                ErrorMessage = $"{result.ErrorMessage} | Semantic analysis suggests: {guidance.SuggestedApproach}"
            };
        }

        return result;
    }

    /// <summary>
    /// Performs semantic analysis of CAPTCHA content using LLM (async version).
    /// </summary>
    private async Task<SemanticCaptchaAnalysis> AnalyzeCaptchaSemanticallyAsync(
        string content,
        string url,
        CancellationToken ct)
    {
        var truncatedContent = content.Length > 2000 ? content[..2000] + "..." : content;

        var prompt = $@"Analyze this web page content for CAPTCHA or bot detection challenges.

URL: {url}
Content (truncated):
{truncatedContent}

Respond in this exact format:
IS_CAPTCHA: true/false
CAPTCHA_TYPE: (type name or 'none')
CHALLENGE_DESCRIPTION: (brief description or 'none')
SUGGESTED_APPROACH: (how to bypass or 'none')
CONFIDENCE: (0.0-1.0)";

        var (response, _) = await _llm.GenerateWithToolsAsync(prompt, ct);
        return ParseSemanticAnalysis(response);
    }

    /// <summary>
    /// Performs semantic analysis synchronously (for DetectCaptcha which must be sync).
    /// </summary>
    private SemanticCaptchaAnalysis AnalyzeCaptchaSemanticallySync(string content, string url)
    {
        // Use a simpler, faster check for sync context
        var truncatedContent = content.Length > 1000 ? content[..1000] : content;

        var prompt = $@"Is this a CAPTCHA page? Reply with: YES [type] or NO
Content: {truncatedContent}";

        try
        {
            var task = _llm.GenerateWithToolsAsync(prompt);
            if (task.Wait(TimeSpan.FromSeconds(5)))
            {
                var (response, _) = task.Result;
                var responseLower = response.ToLowerInvariant().Trim();

                if (responseLower.StartsWith("yes"))
                {
                    var type = response.Length > 4 ? response[4..].Trim() : "Unknown";
                    return new SemanticCaptchaAnalysis(true, type, null, null, 0.8);
                }

                return new SemanticCaptchaAnalysis(false, "none", null, null, 0.8);
            }
        }
        catch
        {
            // Timeout or error - return non-CAPTCHA result
        }

        return new SemanticCaptchaAnalysis(false, "none", null, null, 0.0);
    }

    /// <summary>
    /// Parses the LLM response into a structured analysis result.
    /// </summary>
    private static SemanticCaptchaAnalysis ParseSemanticAnalysis(string response)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        bool isCaptcha = false;
        string captchaType = "Unknown";
        string? challengeDescription = null;
        string? suggestedApproach = null;
        double confidence = 0.5;

        foreach (var line in lines)
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].ToUpperInvariant().Replace(" ", "_");
            var value = parts[1].Trim();

            switch (key)
            {
                case "IS_CAPTCHA":
                    isCaptcha = value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    break;
                case "CAPTCHA_TYPE":
                    captchaType = value != "none" ? value : "Unknown";
                    break;
                case "CHALLENGE_DESCRIPTION":
                    challengeDescription = value != "none" ? value : null;
                    break;
                case "SUGGESTED_APPROACH":
                    suggestedApproach = value != "none" ? value : null;
                    break;
                case "CONFIDENCE":
                    _ = double.TryParse(value, out confidence);
                    break;
            }
        }

        return new SemanticCaptchaAnalysis(
            isCaptcha,
            captchaType,
            challengeDescription,
            suggestedApproach,
            confidence);
    }
}
