// <copyright file="CaptchaResolverTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests;

using FluentAssertions;
using Ouroboros.Application.Tools.CaptchaResolver;
using Xunit;

/// <summary>
/// Tests for CAPTCHA resolver strategies.
/// </summary>
public class CaptchaResolverTests
{
    [Fact]
    public void DetectCaptcha_DuckDuckGoChallenge_ShouldDetect()
    {
        // Arrange
        var resolver = new VisionCaptchaResolver(null);
        var content = "Please complete the following challenge to confirm this search was made by a human.";

        // Act
        var result = resolver.DetectCaptcha(content, "https://duckduckgo.com");

        // Assert
        result.IsCaptcha.Should().BeTrue();
        result.CaptchaType.Should().Be("DuckDuckGo-Challenge");
    }

    [Fact]
    public void DetectCaptcha_NormalContent_ShouldNotDetect()
    {
        // Arrange
        var resolver = new VisionCaptchaResolver(null);
        var content = "Web results for 'test query'. 1. Example.com - This is an example website...";

        // Act
        var result = resolver.DetectCaptcha(content, "https://duckduckgo.com");

        // Assert
        result.IsCaptcha.Should().BeFalse();
    }

    [Fact]
    public void DetectCaptcha_CloudflareChallenge_ShouldDetect()
    {
        // Arrange
        var resolver = new VisionCaptchaResolver(null);
        var content = "Checking your browser before accessing cloudflare challenge page.";

        // Act
        var result = resolver.DetectCaptcha(content, "https://example.com");

        // Assert
        result.IsCaptcha.Should().BeTrue();
        result.CaptchaType.Should().Be("Cloudflare-Challenge");
    }

    [Fact]
    public void DetectCaptcha_GoogleRecaptcha_ShouldDetect()
    {
        // Arrange
        var resolver = new VisionCaptchaResolver(null);
        var content = "This page uses recaptcha to verify you're not a robot";

        // Act
        var result = resolver.DetectCaptcha(content, "https://google.com");

        // Assert
        result.IsCaptcha.Should().BeTrue();
        result.CaptchaType.Should().Be("Google-reCAPTCHA");
    }

    [Fact]
    public void DetectCaptcha_UnusualTraffic_ShouldDetect()
    {
        // Arrange
        var resolver = new VisionCaptchaResolver(null);
        var content = "We detected unusual traffic from your computer network.";

        // Act
        var result = resolver.DetectCaptcha(content, "https://duckduckgo.com");

        // Assert
        result.IsCaptcha.Should().BeTrue();
    }

    [Fact]
    public void CaptchaResolverChain_ShouldDetectFromHighestPriorityStrategy()
    {
        // Arrange - VisionCaptchaResolver has priority 100, AlternativeSearchResolver has priority 50
        var visionResolver = new VisionCaptchaResolver(null);
        var altResolver = new AlternativeSearchResolver();

        // Act - Add in reverse priority order to verify sorting
        var chain = new CaptchaResolverChain()
            .AddStrategy(altResolver)  // Priority 50
            .AddStrategy(visionResolver);  // Priority 100

        // The chain should use VisionResolver first since it has higher priority
        var ddgCaptcha = "Please complete the following challenge to confirm this search was made by a human.";
        var result = chain.DetectCaptcha(ddgCaptcha, "https://duckduckgo.com");

        // Assert - VisionResolver should detect this with its specific type
        result.IsCaptcha.Should().BeTrue();
        result.CaptchaType.Should().Be("DuckDuckGo-Challenge");
    }

    [Fact]
    public void CaptchaResolverChain_DetectCaptcha_ShouldUseFirstMatchingStrategy()
    {
        // Arrange
        var chain = new CaptchaResolverChain()
            .AddStrategy(new VisionCaptchaResolver(null))
            .AddStrategy(new AlternativeSearchResolver());

        var ddgCaptcha = "Please complete the following challenge to confirm this search was made by a human.";

        // Act
        var result = chain.DetectCaptcha(ddgCaptcha, "https://duckduckgo.com");

        // Assert
        result.IsCaptcha.Should().BeTrue();
        result.CaptchaType.Should().Be("DuckDuckGo-Challenge");
    }

    [Fact]
    public void AlternativeSearchResolver_ShouldNotDetectCaptcha()
    {
        // AlternativeSearchResolver is a fallback, doesn't do detection
        var resolver = new AlternativeSearchResolver();

        var result = resolver.DetectCaptcha("any content", "https://any.url");

        result.IsCaptcha.Should().BeFalse();
    }

    [Fact]
    public async Task VisionCaptchaResolver_ResolveAsync_WithoutPlaywright_ShouldReturnError()
    {
        // Arrange
        var resolver = new VisionCaptchaResolver(null);

        // Act
        var result = await resolver.ResolveAsync("https://duckduckgo.com", "captcha content");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Playwright tool");
    }
}
