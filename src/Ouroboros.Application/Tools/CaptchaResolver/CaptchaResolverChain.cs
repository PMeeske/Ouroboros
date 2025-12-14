// <copyright file="CaptchaResolverChain.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.CaptchaResolver;

/// <summary>
/// Orchestrates multiple CAPTCHA resolver strategies in a chain of responsibility pattern.
/// Tries each strategy in priority order until one succeeds.
/// </summary>
public class CaptchaResolverChain
{
    private readonly List<ICaptchaResolverStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptchaResolverChain"/> class.
    /// </summary>
    public CaptchaResolverChain()
    {
        _strategies = new List<ICaptchaResolverStrategy>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CaptchaResolverChain"/> class with strategies.
    /// </summary>
    /// <param name="strategies">The strategies to use.</param>
    public CaptchaResolverChain(IEnumerable<ICaptchaResolverStrategy> strategies)
    {
        _strategies = strategies.OrderByDescending(s => s.Priority).ToList();
    }

    /// <summary>
    /// Adds a strategy to the chain.
    /// </summary>
    /// <param name="strategy">The strategy to add.</param>
    /// <returns>This chain for fluent configuration.</returns>
    public CaptchaResolverChain AddStrategy(ICaptchaResolverStrategy strategy)
    {
        _strategies.Add(strategy);
        _strategies.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        return this;
    }

    /// <summary>
    /// Detects if the given content contains a CAPTCHA using all available strategies.
    /// </summary>
    /// <param name="content">The content to check.</param>
    /// <param name="url">The source URL.</param>
    /// <returns>Detection result from the first strategy that detects a CAPTCHA.</returns>
    public CaptchaDetectionResult DetectCaptcha(string content, string url)
    {
        foreach (var strategy in _strategies)
        {
            var result = strategy.DetectCaptcha(content, url);
            if (result.IsCaptcha)
            {
                return result;
            }
        }

        return new CaptchaDetectionResult(false, string.Empty);
    }

    /// <summary>
    /// Attempts to resolve a CAPTCHA using the chain of strategies.
    /// </summary>
    /// <param name="url">The original URL that triggered the CAPTCHA.</param>
    /// <param name="captchaContent">The content containing the CAPTCHA.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resolution result from the first successful strategy.</returns>
    public async Task<CaptchaResolutionResult> ResolveAsync(
        string url,
        string captchaContent,
        CancellationToken ct = default)
    {
        var attemptedStrategies = new List<string>();
        var errors = new List<string>();

        foreach (var strategy in _strategies)
        {
            attemptedStrategies.Add(strategy.Name);

            try
            {
                var result = await strategy.ResolveAsync(url, captchaContent, ct);
                if (result.Success)
                {
                    return new CaptchaResolutionResult(
                        true,
                        result.ResolvedContent,
                        ErrorMessage: null);
                }

                errors.Add($"{strategy.Name}: {result.ErrorMessage ?? "Unknown error"}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{strategy.Name}: Exception - {ex.Message}");
            }
        }

        return new CaptchaResolutionResult(
            false,
            ErrorMessage: $"All {attemptedStrategies.Count} strategies failed. Attempts: {string.Join("; ", errors)}");
    }

    /// <summary>
    /// Gets the names of all registered strategies in priority order.
    /// </summary>
    public IReadOnlyList<string> StrategyNames =>
        _strategies.Select(s => $"{s.Name} (priority: {s.Priority})").ToList();
}
