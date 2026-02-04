// <copyright file="LlmCostTrackerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using System;
using System.Threading;
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Comprehensive tests for the LlmCostTracker class.
/// Tests cost calculation, token tracking, usage statistics,
/// and thread-safe operations.
/// </summary>
[Trait("Category", "Unit")]
public class LlmCostTrackerTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithModelOnly_Succeeds()
    {
        // Act
        var tracker = new LlmCostTracker("gpt-4o");

        // Assert
        tracker.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithModelAndProvider_Succeeds()
    {
        // Act
        var tracker = new LlmCostTracker("custom-model", "CustomProvider");

        // Assert
        tracker.Should().NotBeNull();
    }

    #endregion

    #region GetProvider Tests

    [Theory]
    [InlineData("claude-opus-4-5", "Anthropic")]
    [InlineData("claude-sonnet-4", "Anthropic")]
    [InlineData("claude-3-5-haiku", "Anthropic")]
    [InlineData("gpt-4o", "OpenAI")]
    [InlineData("gpt-4o-mini", "OpenAI")]
    [InlineData("o1", "OpenAI")]
    [InlineData("deepseek-chat", "DeepSeek")]
    [InlineData("deepseek-reasoner", "DeepSeek")]
    [InlineData("gemini-2.0-flash", "Google")]
    [InlineData("mistral-large", "Mistral")]
    [InlineData("llama3", "Local")]
    [InlineData("unknown-model", "Unknown")]
    public void GetProvider_WithKnownModel_ReturnsCorrectProvider(string model, string expectedProvider)
    {
        // Act
        var provider = LlmCostTracker.GetProvider(model);

        // Assert
        provider.Should().Be(expectedProvider);
    }

    [Fact]
    public void GetProvider_IsCaseInsensitive()
    {
        // Act
        var provider1 = LlmCostTracker.GetProvider("GPT-4O");
        var provider2 = LlmCostTracker.GetProvider("gpt-4o");

        // Assert
        provider1.Should().Be("OpenAI");
        provider2.Should().Be("OpenAI");
        provider1.Should().Be(provider2);
    }

    #endregion

    #region GetPricing Tests

    [Fact]
    public void GetPricing_WithOpenAIModel_ReturnsCorrectPricing()
    {
        // Act
        var pricing = LlmCostTracker.GetPricing("gpt-4o");

        // Assert
        pricing.Should().NotBeNull();
        pricing.Provider.Should().Be("OpenAI");
        pricing.InputCostPer1M.Should().Be(2.50m);
        pricing.OutputCostPer1M.Should().Be(10.00m);
    }

    [Fact]
    public void GetPricing_WithAnthropicModel_ReturnsCorrectPricing()
    {
        // Act
        var pricing = LlmCostTracker.GetPricing("claude-sonnet-4");

        // Assert
        pricing.Should().NotBeNull();
        pricing.Provider.Should().Be("Anthropic");
        pricing.InputCostPer1M.Should().Be(3.00m);
        pricing.OutputCostPer1M.Should().Be(15.00m);
    }

    [Fact]
    public void GetPricing_WithDeepSeekModel_ReturnsCorrectPricing()
    {
        // Act
        var pricing = LlmCostTracker.GetPricing("deepseek-chat");

        // Assert
        pricing.Should().NotBeNull();
        pricing.Provider.Should().Be("DeepSeek");
        pricing.InputCostPer1M.Should().Be(0.14m);
        pricing.OutputCostPer1M.Should().Be(0.28m);
    }

    [Fact]
    public void GetPricing_WithLocalModel_ReturnsZeroCost()
    {
        // Act
        var pricing = LlmCostTracker.GetPricing("llama3");

        // Assert
        pricing.Should().NotBeNull();
        pricing.Provider.Should().Be("Local");
        pricing.InputCostPer1M.Should().Be(0m);
        pricing.OutputCostPer1M.Should().Be(0m);
    }

    [Fact]
    public void GetPricing_WithUnknownModel_ReturnsZeroCost()
    {
        // Act
        var pricing = LlmCostTracker.GetPricing("unknown-model-xyz");

        // Assert
        pricing.Should().NotBeNull();
        pricing.InputCostPer1M.Should().Be(0m);
        pricing.OutputCostPer1M.Should().Be(0m);
    }

    [Fact]
    public void GetPricing_IsCaseInsensitive()
    {
        // Act
        var pricing1 = LlmCostTracker.GetPricing("GPT-4O");
        var pricing2 = LlmCostTracker.GetPricing("gpt-4o");

        // Assert
        pricing1.InputCostPer1M.Should().Be(pricing2.InputCostPer1M);
        pricing1.OutputCostPer1M.Should().Be(pricing2.OutputCostPer1M);
    }

    #endregion

    #region CalculateCost Tests

    [Fact]
    public void CalculateCost_WithZeroTokens_ReturnsZero()
    {
        // Act
        var cost = LlmCostTracker.CalculateCost("gpt-4o", 0, 0);

        // Assert
        cost.Should().Be(0m);
    }

    [Fact]
    public void CalculateCost_WithInputTokensOnly_CalculatesCorrectly()
    {
        // Act
        var cost = LlmCostTracker.CalculateCost("gpt-4o", 1_000_000, 0);

        // Assert
        cost.Should().Be(2.50m); // $2.50 per 1M input tokens
    }

    [Fact]
    public void CalculateCost_WithOutputTokensOnly_CalculatesCorrectly()
    {
        // Act
        var cost = LlmCostTracker.CalculateCost("gpt-4o", 0, 1_000_000);

        // Assert
        cost.Should().Be(10.00m); // $10.00 per 1M output tokens
    }

    [Fact]
    public void CalculateCost_WithBothInputAndOutput_CalculatesCorrectly()
    {
        // Act
        var cost = LlmCostTracker.CalculateCost("gpt-4o", 500_000, 500_000);

        // Assert
        // (500k / 1M * $2.50) + (500k / 1M * $10.00) = $1.25 + $5.00 = $6.25
        cost.Should().Be(6.25m);
    }

    [Fact]
    public void CalculateCost_WithSmallTokenCounts_CalculatesCorrectly()
    {
        // Act
        var cost = LlmCostTracker.CalculateCost("gpt-4o", 1000, 2000);

        // Assert
        // (1000 / 1M * $2.50) + (2000 / 1M * $10.00) = $0.0025 + $0.020 = $0.0225
        cost.Should().BeApproximately(0.0225m, 0.00001m);
    }

    [Fact]
    public void CalculateCost_WithLocalModel_ReturnsZero()
    {
        // Act
        var cost = LlmCostTracker.CalculateCost("llama3", 1_000_000, 1_000_000);

        // Assert
        cost.Should().Be(0m);
    }

    #endregion

    #region Tracking Tests

    [Fact]
    public void StartRequest_InitializesTimer()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act
        tracker.StartRequest();
        Thread.Sleep(50); // Small delay
        var metrics = tracker.EndRequest(100, 200);

        // Assert
        metrics.Latency.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void EndRequest_ReturnsCorrectMetrics()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");
        tracker.StartRequest();

        // Act
        var metrics = tracker.EndRequest(1000, 2000);

        // Assert
        metrics.Model.Should().Be("gpt-4o");
        metrics.InputTokens.Should().Be(1000);
        metrics.OutputTokens.Should().Be(2000);
        metrics.TotalTokens.Should().Be(3000);
        metrics.Cost.Should().BeGreaterThan(0);
        metrics.Latency.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void EndRequest_WithoutStartRequest_RecordsMetrics()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act - EndRequest without StartRequest
        var metrics = tracker.EndRequest(100, 200);

        // Assert - Should still record metrics with zero latency
        metrics.InputTokens.Should().Be(100);
        metrics.OutputTokens.Should().Be(200);
    }

    [Fact]
    public void MultipleRequests_AccumulateCorrectly()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act
        tracker.StartRequest();
        tracker.EndRequest(100, 200);

        tracker.StartRequest();
        tracker.EndRequest(300, 400);

        var session = tracker.GetSessionMetrics();

        // Assert
        session.TotalRequests.Should().Be(2);
        session.TotalInputTokens.Should().Be(400);
        session.TotalOutputTokens.Should().Be(600);
        session.TotalTokens.Should().Be(1000);
    }

    #endregion

    #region SessionMetrics Tests

    [Fact]
    public void GetSessionMetrics_WithNoRequests_ReturnsZeroMetrics()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act
        var metrics = tracker.GetSessionMetrics();

        // Assert
        metrics.TotalRequests.Should().Be(0);
        metrics.TotalInputTokens.Should().Be(0);
        metrics.TotalOutputTokens.Should().Be(0);
        metrics.TotalCost.Should().Be(0);
    }

    [Fact]
    public void GetSessionMetrics_WithRequests_ReturnsAccumulatedMetrics()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act
        tracker.EndRequest(1000, 2000);
        tracker.EndRequest(500, 1000);
        var metrics = tracker.GetSessionMetrics();

        // Assert
        metrics.TotalRequests.Should().Be(2);
        metrics.TotalInputTokens.Should().Be(1500);
        metrics.TotalOutputTokens.Should().Be(3000);
        metrics.TotalTokens.Should().Be(4500);
        metrics.Model.Should().Be("gpt-4o");
        metrics.Provider.Should().Be("OpenAI");
    }

    [Fact]
    public void GetSessionMetrics_CalculatesAverageLatency()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act
        tracker.StartRequest();
        Thread.Sleep(50);
        tracker.EndRequest(100, 200);

        tracker.StartRequest();
        Thread.Sleep(50);
        tracker.EndRequest(100, 200);

        var metrics = tracker.GetSessionMetrics();

        // Assert
        metrics.AverageLatency.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.TotalRequests.Should().Be(2);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAllMetrics()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");
        tracker.EndRequest(1000, 2000);
        tracker.EndRequest(500, 1000);

        // Act
        tracker.Reset();
        var metrics = tracker.GetSessionMetrics();

        // Assert
        metrics.TotalRequests.Should().Be(0);
        metrics.TotalInputTokens.Should().Be(0);
        metrics.TotalOutputTokens.Should().Be(0);
        metrics.TotalCost.Should().Be(0);
    }

    [Fact]
    public void Reset_AllowsNewTracking()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");
        tracker.EndRequest(1000, 2000);
        tracker.Reset();

        // Act
        tracker.EndRequest(100, 200);
        var metrics = tracker.GetSessionMetrics();

        // Assert
        metrics.TotalRequests.Should().Be(1);
        metrics.TotalInputTokens.Should().Be(100);
        metrics.TotalOutputTokens.Should().Be(200);
    }

    #endregion

    #region FormatSessionSummary Tests

    [Fact]
    public void FormatSessionSummary_ReturnsFormattedString()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");
        tracker.EndRequest(1000, 2000);

        // Act
        var summary = tracker.FormatSessionSummary();

        // Assert
        summary.Should().Contain("LLM Usage Summary");
        summary.Should().Contain("Provider: OpenAI");
        summary.Should().Contain("Model: gpt-4o");
        summary.Should().Contain("Requests: 1");
        summary.Should().Contain("Tokens:");
        summary.Should().Contain("Cost:");
    }

    [Fact]
    public void FormatSessionSummary_WithLocalModel_ShowsZeroCost()
    {
        // Arrange
        var tracker = new LlmCostTracker("llama3");
        tracker.EndRequest(1000, 2000);

        // Act
        var summary = tracker.FormatSessionSummary();

        // Assert
        summary.Should().Contain("llama3");
        summary.Should().Contain("$0.0000");
    }

    #endregion

    #region GetCostAwarenessPrompt Tests

    [Fact]
    public void GetCostAwarenessPrompt_WithPaidModel_IncludesCostInfo()
    {
        // Act
        var prompt = LlmCostTracker.GetCostAwarenessPrompt("gpt-4o");

        // Assert
        prompt.Should().Contain("COST AWARENESS");
        prompt.Should().Contain("gpt-4o");
        prompt.Should().Contain("OpenAI");
        prompt.Should().Contain("Input tokens");
        prompt.Should().Contain("Output tokens");
    }

    [Fact]
    public void GetCostAwarenessPrompt_WithLocalModel_IndicatesFree()
    {
        // Act
        var prompt = LlmCostTracker.GetCostAwarenessPrompt("llama3");

        // Assert
        prompt.Should().Contain("MODEL INFO");
        prompt.Should().Contain("local/free model");
        prompt.Should().Contain("no API costs");
    }

    #endregion

    #region GetCostString Tests

    [Fact]
    public void GetCostString_WithZeroCost_ShowsTokensOnly()
    {
        // Arrange
        var tracker = new LlmCostTracker("llama3");
        tracker.EndRequest(1000, 2000);

        // Act
        var costString = tracker.GetCostString();

        // Assert
        costString.Should().Contain("3,000 tokens");
        costString.Should().NotContain("$");
    }

    [Fact]
    public void GetCostString_WithCost_ShowsTokensAndCost()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");
        tracker.EndRequest(1000, 2000);

        // Act
        var costString = tracker.GetCostString();

        // Assert
        costString.Should().Contain("3,000 tokens");
        costString.Should().Contain("$");
    }

    #endregion

    #region RequestMetrics Tests

    [Fact]
    public void RequestMetrics_CalculatesTotalTokens()
    {
        // Act
        var metrics = new RequestMetrics("gpt-4o", 100, 200, TimeSpan.FromSeconds(1), 0.01m, DateTime.UtcNow);

        // Assert
        metrics.TotalTokens.Should().Be(300);
    }

    [Fact]
    public void RequestMetrics_CalculatesTokensPerSecond()
    {
        // Act
        var metrics = new RequestMetrics("gpt-4o", 100, 200, TimeSpan.FromSeconds(2), 0.01m, DateTime.UtcNow);

        // Assert
        metrics.TokensPerSecond.Should().Be(100); // 200 output tokens / 2 seconds
    }

    [Fact]
    public void RequestMetrics_WithZeroLatency_ReturnsZeroTokensPerSecond()
    {
        // Act
        var metrics = new RequestMetrics("gpt-4o", 100, 200, TimeSpan.Zero, 0.01m, DateTime.UtcNow);

        // Assert
        metrics.TokensPerSecond.Should().Be(0);
    }

    [Fact]
    public void RequestMetrics_ToString_WithCost_IncludesCost()
    {
        // Act
        var metrics = new RequestMetrics("gpt-4o", 100, 200, TimeSpan.FromSeconds(1), 0.01m, DateTime.UtcNow);
        var str = metrics.ToString();

        // Assert
        str.Should().Contain("100→200 tokens");
        str.Should().Contain("$0.0100");
        str.Should().Contain("1.00s");
    }

    [Fact]
    public void RequestMetrics_ToString_WithZeroCost_OmitsCost()
    {
        // Act
        var metrics = new RequestMetrics("llama3", 100, 200, TimeSpan.FromSeconds(1), 0m, DateTime.UtcNow);
        var str = metrics.ToString();

        // Assert
        str.Should().Contain("100→200 tokens");
        str.Should().NotContain("$");
    }

    #endregion

    #region SessionMetrics Tests

    [Fact]
    public void SessionMetrics_CalculatesTotalTokens()
    {
        // Act
        var metrics = new SessionMetrics(
            "gpt-4o",
            "OpenAI",
            2,
            1000,
            2000,
            TimeSpan.FromSeconds(5),
            0.05m,
            TimeSpan.FromSeconds(2.5));

        // Assert
        metrics.TotalTokens.Should().Be(3000);
    }

    [Fact]
    public void SessionMetrics_ToCostString_WithCost_IncludesCost()
    {
        // Act
        var metrics = new SessionMetrics(
            "gpt-4o",
            "OpenAI",
            2,
            1000,
            2000,
            TimeSpan.FromSeconds(5),
            0.05m,
            TimeSpan.FromSeconds(2.5));

        var costString = metrics.ToCostString();

        // Assert
        costString.Should().Contain("3,000 tokens");
        costString.Should().Contain("$0.0500");
    }

    [Fact]
    public void SessionMetrics_ToCostString_WithZeroCost_ShowsTokensOnly()
    {
        // Act
        var metrics = new SessionMetrics(
            "llama3",
            "Local",
            2,
            1000,
            2000,
            TimeSpan.FromSeconds(5),
            0m,
            TimeSpan.FromSeconds(2.5));

        var costString = metrics.ToCostString();

        // Assert
        costString.Should().Contain("3,000 tokens");
        costString.Should().NotContain("$");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void ConcurrentEndRequest_TracksCorrectly()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");
        var threads = new Thread[10];
        var requestsPerThread = 10;

        // Act - Run concurrent requests
        for (int i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(() =>
            {
                for (int j = 0; j < requestsPerThread; j++)
                {
                    tracker.EndRequest(100, 200);
                }
            });
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        var metrics = tracker.GetSessionMetrics();

        // Assert
        metrics.TotalRequests.Should().Be(100); // 10 threads * 10 requests
        metrics.TotalInputTokens.Should().Be(10000); // 100 * 100
        metrics.TotalOutputTokens.Should().Be(20000); // 100 * 200
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Constructor_WithEmptyModel_DoesNotThrow()
    {
        // Act & Assert
        var act = () => new LlmCostTracker(string.Empty);
        act.Should().NotThrow();
    }

    [Fact]
    public void EndRequest_WithNegativeTokens_TracksAsProvided()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act - Negative tokens shouldn't happen in practice but test behavior
        var metrics = tracker.EndRequest(-100, -200);

        // Assert - Should record as provided
        metrics.InputTokens.Should().Be(-100);
        metrics.OutputTokens.Should().Be(-200);
    }

    [Fact]
    public void EndRequest_WithLargeTokenCounts_HandlesCorrectly()
    {
        // Arrange
        var tracker = new LlmCostTracker("gpt-4o");

        // Act
        var metrics = tracker.EndRequest(10_000_000, 20_000_000);

        // Assert
        metrics.InputTokens.Should().Be(10_000_000);
        metrics.OutputTokens.Should().Be(20_000_000);
        metrics.Cost.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetGlobalMetrics_ReturnsMetrics()
    {
        // Act
        var metrics = LlmCostTracker.GetGlobalMetrics();

        // Assert
        metrics.Should().NotBeNull();
    }

    #endregion
}
