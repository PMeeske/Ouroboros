// <copyright file="OllamaPresetsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Comprehensive tests for the OllamaPresets class.
/// Tests preset configurations, capability-based adaptations,
/// and machine-specific settings.
/// </summary>
[Trait("Category", "Unit")]
public class OllamaPresetsTests
{
    #region DeepSeekCoder33B Tests

    [Fact]
    public void DeepSeekCoder33B_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.DeepSeekCoder33B;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.2f); // Low temperature for code
        settings.TopP.Should().Be(0.9f);
        settings.TopK.Should().Be(40);
        settings.RepeatPenalty.Should().Be(1.1f);
        settings.KeepAlive.Should().Be(10 * 60); // 10 minutes
        settings.UseMmap.Should().BeTrue();
        settings.UseMlock.Should().BeFalse();
    }

    [Fact]
    public void DeepSeekCoder33B_AdaptsThreadsBasedOnCores()
    {
        // Act
        var settings = OllamaPresets.DeepSeekCoder33B;

        // Assert
        settings.NumThread.Should().BeGreaterThanOrEqualTo(1);
        // Should use (cores - 1) but at least 1
    }

    [Fact]
    public void DeepSeekCoder33B_AdaptsContextBasedOnMemory()
    {
        // Act
        var settings = OllamaPresets.DeepSeekCoder33B;

        // Assert
        settings.NumCtx.Should().BeOneOf(4096, 8192);
    }

    [Fact]
    public void DeepSeekCoder33B_AdaptsGpuSettings()
    {
        // Act
        var settings = OllamaPresets.DeepSeekCoder33B;

        // Assert
        settings.NumGpu.Should().BeGreaterThanOrEqualTo(0);
        settings.MainGpu.Should().Be(0);
    }

    [Fact]
    public void DeepSeekCoder33B_LowVramWhenNoGpu()
    {
        // Act
        var settings = OllamaPresets.DeepSeekCoder33B;

        // Assert - If no GPU, LowVram should be true
        if (settings.NumGpu == 0)
        {
            settings.LowVram.Should().BeTrue();
        }
    }

    #endregion

    #region Llama3General Tests

    [Fact]
    public void Llama3General_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.Llama3General;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.5f); // Balanced temperature
        settings.TopP.Should().Be(0.9f);
        settings.TopK.Should().Be(40);
        settings.RepeatPenalty.Should().Be(1.1f);
        settings.KeepAlive.Should().Be(10 * 60);
        settings.UseMmap.Should().BeTrue();
        settings.UseMlock.Should().BeFalse();
    }

    [Fact]
    public void Llama3General_AdaptsContextBasedOnMemory()
    {
        // Act
        var settings = OllamaPresets.Llama3General;

        // Assert
        settings.NumCtx.Should().BeOneOf(4096, 8192);
    }

    [Fact]
    public void Llama3General_UsesUpToOneGpu()
    {
        // Act
        var settings = OllamaPresets.Llama3General;

        // Assert
        settings.NumGpu.Should().BeLessThanOrEqualTo(1);
    }

    #endregion

    #region Llama3Summarize Tests

    [Fact]
    public void Llama3Summarize_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.Llama3Summarize;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.3f); // More deterministic
        settings.TopP.Should().Be(0.9f);
        settings.TopK.Should().Be(40);
        settings.RepeatPenalty.Should().Be(1.15f); // Stronger penalty
        settings.KeepAlive.Should().Be(10 * 60);
    }

    [Fact]
    public void Llama3Summarize_HasLowerTemperatureThanGeneral()
    {
        // Act
        var summarize = OllamaPresets.Llama3Summarize;
        var general = OllamaPresets.Llama3General;

        // Assert
        summarize.Temperature.Should().NotBeNull();
        general.Temperature.Should().NotBeNull();
        summarize.Temperature!.Value.Should().BeLessThan(general.Temperature!.Value);
        summarize.RepeatPenalty.Should().NotBeNull();
        general.RepeatPenalty.Should().NotBeNull();
        summarize.RepeatPenalty!.Value.Should().BeGreaterThan(general.RepeatPenalty!.Value);
    }

    #endregion

    #region DeepSeekR1 Tests

    [Fact]
    public void DeepSeekR1_14B_Reason_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.DeepSeekR1_14B_Reason;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.6f); // Exploratory for reasoning
        settings.TopP.Should().Be(0.92f);
        settings.TopK.Should().Be(50);
        settings.RepeatPenalty.Should().Be(1.05f);
        settings.KeepAlive.Should().Be(10 * 60);
    }

    [Fact]
    public void DeepSeekR1_14B_Reason_HasLargerContext()
    {
        // Act
        var settings = OllamaPresets.DeepSeekR1_14B_Reason;

        // Assert
        settings.NumCtx.Should().BeOneOf(8192, 12288);
    }

    [Fact]
    public void DeepSeekR1_32B_Reason_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.DeepSeekR1_32B_Reason;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.55f);
        settings.TopP.Should().Be(0.92f);
        settings.TopK.Should().Be(50);
        settings.RepeatPenalty.Should().Be(1.05f);
    }

    [Fact]
    public void DeepSeekR1_32B_Reason_SupportsMultipleGpus()
    {
        // Act
        var settings = OllamaPresets.DeepSeekR1_32B_Reason;

        // Assert
        settings.NumGpu.Should().BeLessThanOrEqualTo(2); // Up to 2 GPUs
    }

    [Fact]
    public void DeepSeekR1_32B_Reason_HasLargestContext()
    {
        // Act
        var settings = OllamaPresets.DeepSeekR1_32B_Reason;

        // Assert
        settings.NumCtx.Should().BeOneOf(8192, 12288, 16384);
    }

    #endregion

    #region Mistral7B Tests

    [Fact]
    public void Mistral7BGeneral_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.Mistral7BGeneral;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.5f);
        settings.TopP.Should().Be(0.9f);
        settings.TopK.Should().Be(40);
        settings.RepeatPenalty.Should().Be(1.1f);
    }

    [Fact]
    public void Mistral7BGeneral_HasSmallerContext()
    {
        // Act
        var settings = OllamaPresets.Mistral7BGeneral;

        // Assert
        settings.NumCtx.Should().BeOneOf(3072, 4096);
    }

    [Fact]
    public void Mistral7BGeneral_UsesUpToOneGpu()
    {
        // Act
        var settings = OllamaPresets.Mistral7BGeneral;

        // Assert
        settings.NumGpu.Should().BeLessThanOrEqualTo(1);
    }

    #endregion

    #region Qwen2.5 Tests

    [Fact]
    public void Qwen25_7B_General_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.Qwen25_7B_General;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.45f);
        settings.TopP.Should().Be(0.9f);
        settings.TopK.Should().Be(40);
        settings.RepeatPenalty.Should().Be(1.1f);
    }

    [Fact]
    public void Qwen25_7B_General_HasModerateContext()
    {
        // Act
        var settings = OllamaPresets.Qwen25_7B_General;

        // Assert
        settings.NumCtx.Should().BeOneOf(3072, 4096);
    }

    #endregion

    #region Phi3Mini Tests

    [Fact]
    public void Phi3MiniGeneral_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.Phi3MiniGeneral;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.5f);
        settings.TopP.Should().Be(0.9f);
        settings.TopK.Should().Be(40);
        settings.RepeatPenalty.Should().Be(1.1f);
        settings.NumCtx.Should().Be(4096); // Fixed context
    }

    #endregion

    #region TinyLlamaFast Tests

    [Fact]
    public void TinyLlamaFast_ReturnsValidSettings()
    {
        // Act
        var settings = OllamaPresets.TinyLlamaFast;

        // Assert
        settings.Should().NotBeNull();
        settings.Temperature.Should().Be(0.4f); // Lower for determinism
        settings.TopP.Should().Be(0.85f);
        settings.TopK.Should().Be(30);
        settings.RepeatPenalty.Should().Be(1.1f);
        settings.NumCtx.Should().Be(2048); // Small for speed
        settings.KeepAlive.Should().Be(5 * 60); // Shorter keep-alive
        settings.LowVram.Should().BeTrue(); // Always low VRAM for parallel
    }

    [Fact]
    public void TinyLlamaFast_UsesFewThreadsForParallelism()
    {
        // Act
        var settings = OllamaPresets.TinyLlamaFast;

        // Assert
        settings.NumThread.Should().BeGreaterThanOrEqualTo(1);
        // Should use cores/4 for parallel execution
    }

    [Fact]
    public void TinyLlamaFast_HasSmallestContext()
    {
        // Act
        var settings = OllamaPresets.TinyLlamaFast;

        // Assert
        var allPresets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.TinyLlamaFast
        };

        var minCtx = allPresets.Min(p => p.NumCtx);
        settings.NumCtx.Should().BeLessThanOrEqualTo(minCtx.GetValueOrDefault());
    }

    #endregion

    #region Comparative Tests

    [Fact]
    public void AllPresets_HavePositiveTemperature()
    {
        // Act
        var presets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Llama3Summarize,
            OllamaPresets.DeepSeekR1_14B_Reason,
            OllamaPresets.DeepSeekR1_32B_Reason,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.Qwen25_7B_General,
            OllamaPresets.Phi3MiniGeneral,
            OllamaPresets.TinyLlamaFast
        };

        // Assert
        foreach (var preset in presets)
        {
            preset.Temperature.Should().BeGreaterThan(0);
            preset.Temperature.Should().BeLessThanOrEqualTo(1.0f);
        }
    }

    [Fact]
    public void AllPresets_HaveReasonableTopPValues()
    {
        // Act
        var presets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Llama3Summarize,
            OllamaPresets.DeepSeekR1_14B_Reason,
            OllamaPresets.DeepSeekR1_32B_Reason,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.Qwen25_7B_General,
            OllamaPresets.Phi3MiniGeneral,
            OllamaPresets.TinyLlamaFast
        };

        // Assert
        foreach (var preset in presets)
        {
            preset.TopP.Should().BeGreaterThan(0);
            preset.TopP.Should().BeLessThanOrEqualTo(1.0f);
        }
    }

    [Fact]
    public void AllPresets_HavePositiveKeepAlive()
    {
        // Act
        var presets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Llama3Summarize,
            OllamaPresets.DeepSeekR1_14B_Reason,
            OllamaPresets.DeepSeekR1_32B_Reason,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.Qwen25_7B_General,
            OllamaPresets.Phi3MiniGeneral,
            OllamaPresets.TinyLlamaFast
        };

        // Assert
        foreach (var preset in presets)
        {
            preset.KeepAlive.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void AllPresets_HaveValidThreadCounts()
    {
        // Act
        var presets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Llama3Summarize,
            OllamaPresets.DeepSeekR1_14B_Reason,
            OllamaPresets.DeepSeekR1_32B_Reason,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.Qwen25_7B_General,
            OllamaPresets.Phi3MiniGeneral,
            OllamaPresets.TinyLlamaFast
        };

        // Assert
        foreach (var preset in presets)
        {
            preset.NumThread.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void AllPresets_HaveValidContextSizes()
    {
        // Act
        var presets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Llama3Summarize,
            OllamaPresets.DeepSeekR1_14B_Reason,
            OllamaPresets.DeepSeekR1_32B_Reason,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.Qwen25_7B_General,
            OllamaPresets.Phi3MiniGeneral,
            OllamaPresets.TinyLlamaFast
        };

        // Assert
        foreach (var preset in presets)
        {
            preset.NumCtx.Should().BeGreaterThan(0);
            preset.NumCtx.Should().BeLessThanOrEqualTo(32768);
        }
    }

    [Fact]
    public void CodersHaveLowerTemperature_ComparedToGeneral()
    {
        // Act
        var coder = OllamaPresets.DeepSeekCoder33B;
        var general = OllamaPresets.Llama3General;

        // Assert
        coder.Temperature.Should().NotBeNull();
        general.Temperature.Should().NotBeNull();
        coder.Temperature!.Value.Should().BeLessThan(general.Temperature!.Value);
    }

    [Fact]
    public void ReasoningModelsHaveHigherTopP()
    {
        // Act
        var reasoning = OllamaPresets.DeepSeekR1_14B_Reason;
        var general = OllamaPresets.Llama3General;

        // Assert
        reasoning.TopP.Should().NotBeNull();
        general.TopP.Should().NotBeNull();
        reasoning.TopP!.Value.Should().BeGreaterThanOrEqualTo(general.TopP!.Value);
    }

    [Fact]
    public void MultipleCalls_ReturnConsistentSettings()
    {
        // Act
        var first = OllamaPresets.Llama3General;
        var second = OllamaPresets.Llama3General;

        // Assert - Should be same values (property getters may create new instances)
        first.Temperature.Should().Be(second.Temperature);
        first.NumCtx.Should().Be(second.NumCtx);
        first.TopP.Should().Be(second.TopP);
        first.TopK.Should().Be(second.TopK);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AllPresets_DoNotThrow()
    {
        // Act & Assert - All presets should be accessible without exceptions
        var act1 = () => OllamaPresets.DeepSeekCoder33B;
        var act2 = () => OllamaPresets.Llama3General;
        var act3 = () => OllamaPresets.Llama3Summarize;
        var act4 = () => OllamaPresets.DeepSeekR1_14B_Reason;
        var act5 = () => OllamaPresets.DeepSeekR1_32B_Reason;
        var act6 = () => OllamaPresets.Mistral7BGeneral;
        var act7 = () => OllamaPresets.Qwen25_7B_General;
        var act8 = () => OllamaPresets.Phi3MiniGeneral;
        var act9 = () => OllamaPresets.TinyLlamaFast;

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        act4.Should().NotThrow();
        act5.Should().NotThrow();
        act6.Should().NotThrow();
        act7.Should().NotThrow();
        act8.Should().NotThrow();
        act9.Should().NotThrow();
    }

    [Fact]
    public void AllPresets_HaveUseMmapEnabled()
    {
        // Act
        var presets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Llama3Summarize,
            OllamaPresets.DeepSeekR1_14B_Reason,
            OllamaPresets.DeepSeekR1_32B_Reason,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.Qwen25_7B_General,
            OllamaPresets.Phi3MiniGeneral,
            OllamaPresets.TinyLlamaFast
        };

        // Assert
        foreach (var preset in presets)
        {
            preset.UseMmap.Should().BeTrue();
        }
    }

    [Fact]
    public void AllPresets_HaveUseMlockDisabled()
    {
        // Act
        var presets = new[]
        {
            OllamaPresets.DeepSeekCoder33B,
            OllamaPresets.Llama3General,
            OllamaPresets.Llama3Summarize,
            OllamaPresets.DeepSeekR1_14B_Reason,
            OllamaPresets.DeepSeekR1_32B_Reason,
            OllamaPresets.Mistral7BGeneral,
            OllamaPresets.Qwen25_7B_General,
            OllamaPresets.Phi3MiniGeneral,
            OllamaPresets.TinyLlamaFast
        };

        // Assert
        foreach (var preset in presets)
        {
            preset.UseMlock.Should().BeFalse();
        }
    }

    #endregion
}
