// <copyright file="ResilientReasonerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Resilience;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Agent.Resilience;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Resilience;
using Ouroboros.Providers;
using Xunit;
using NsReasoningMode = Ouroboros.Agent.NeuralSymbolic.ReasoningMode;
using CoreReasoningMode = Ouroboros.Core.Resilience.ReasoningMode;

/// <summary>
/// Tests for ResilientReasoner implementation.
/// Validates automatic fallback, circuit breaker integration, and health monitoring.
/// </summary>
[Trait("Category", "Unit")]
public class ResilientReasonerTests
{
    [Fact]
    public async Task ReasonAsync_WithValidQuery_ReturnsSuccessfulResult()
    {
        // Arrange
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: true);
        var reasoner = new ResilientReasoner(bridge);

        // Act
        var result = await reasoner.ReasonAsync("What is 2+2?");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReasonAsync_WithEmptyQuery_ReturnsFailure()
    {
        // Arrange
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: true);
        var reasoner = new ResilientReasoner(bridge);

        // Act
        var result = await reasoner.ReasonAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task ReasonAsync_AfterMultipleLlmFailures_UsesSymbolicOnly()
    {
        // Arrange
        var config = new CircuitBreakerConfig { FailureThreshold = 2 };
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: false, symbolicWorks: true);
        var reasoner = new ResilientReasoner(bridge, null, config, NullLogger<ResilientReasoner>.Instance);

        // Act - First two requests should fail neural and use symbolic fallback
        var result1 = await reasoner.ReasonAsync("Query 1", CoreReasoningMode.NeuralFirst);
        var result2 = await reasoner.ReasonAsync("Query 2", CoreReasoningMode.NeuralFirst);

        // Third request should force symbolic-only due to circuit breaker
        bridge.ResetCallTracking();
        var result3 = await reasoner.ReasonAsync("Query 3", CoreReasoningMode.NeuralFirst);

        // Assert
        result1.IsSuccess.Should().BeTrue("First request should fall back to symbolic");
        result2.IsSuccess.Should().BeTrue("Second request should fall back to symbolic");
        result3.IsSuccess.Should().BeTrue("Third request should use symbolic-only");
        
        // Verify that the third request used symbolic-only mode
        bridge.LastUsedMode.Should().Be(NsReasoningMode.SymbolicOnly);
    }

    [Fact]
    public async Task ReasonAsync_CircuitOpenThenRecovered_RestoresNormalOperation()
    {
        // Arrange
        var config = new CircuitBreakerConfig 
        { 
            FailureThreshold = 2, 
            OpenDuration = TimeSpan.FromMilliseconds(100)
        };
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: false, symbolicWorks: true);
        var reasoner = new ResilientReasoner(bridge, null, config, NullLogger<ResilientReasoner>.Instance);

        // Act - Open the circuit
        await reasoner.ReasonAsync("Query 1", CoreReasoningMode.NeuralFirst);
        await reasoner.ReasonAsync("Query 2", CoreReasoningMode.NeuralFirst);
        
        var healthBeforeRecovery = reasoner.GetHealth();
        healthBeforeRecovery.CircuitState.Should().Be("Open");

        // Wait for circuit to transition to half-open
        await Task.Delay(150);

        // Make the bridge succeed now
        bridge.SetShouldSucceed(true);
        
        // Make a successful request in half-open state
        var recoveryResult = await reasoner.ReasonAsync("Recovery query", CoreReasoningMode.NeuralFirst);

        // Assert
        recoveryResult.IsSuccess.Should().BeTrue();
        var healthAfterRecovery = reasoner.GetHealth();
        healthAfterRecovery.CircuitState.Should().Be("Closed");
        healthAfterRecovery.ConsecutiveLlmFailures.Should().Be(0);
        healthAfterRecovery.LastLlmSuccess.Should().NotBeNull();
    }

    [Fact]
    public async Task ReasonAsync_HalfOpenFailure_ReopensCircuit()
    {
        // Arrange
        var config = new CircuitBreakerConfig 
        { 
            FailureThreshold = 2, 
            OpenDuration = TimeSpan.FromMilliseconds(100)
        };
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: false, symbolicWorks: true);
        var reasoner = new ResilientReasoner(bridge, null, config, NullLogger<ResilientReasoner>.Instance);

        // Open the circuit
        await reasoner.ReasonAsync("Query 1", CoreReasoningMode.NeuralFirst);
        await reasoner.ReasonAsync("Query 2", CoreReasoningMode.NeuralFirst);

        // Wait for half-open transition
        await Task.Delay(150);

        // Act - Fail in half-open state
        var result = await reasoner.ReasonAsync("Half-open query", CoreReasoningMode.NeuralFirst);

        // Assert - Should fall back to symbolic and circuit should be open again
        result.IsSuccess.Should().BeTrue("Should fall back to symbolic");
        var health = reasoner.GetHealth();
        health.CircuitState.Should().Be("Open", "Circuit should reopen after half-open failure");
    }

    [Fact]
    public void GetHealth_ReturnsAccurateCircuitState()
    {
        // Arrange
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: true);
        var reasoner = new ResilientReasoner(bridge);

        // Act
        var health = reasoner.GetHealth();

        // Assert
        health.Should().NotBeNull();
        health.CircuitState.Should().Be("Closed");
        health.SymbolicAvailable.Should().BeTrue();
        health.ConsecutiveLlmFailures.Should().Be(0);
        health.LastLlmSuccess.Should().BeNull();
    }

    [Fact]
    public async Task GetHealth_AfterFailures_ReturnsFailureCount()
    {
        // Arrange
        var config = new CircuitBreakerConfig { FailureThreshold = 5 };
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: false, symbolicWorks: true);
        var reasoner = new ResilientReasoner(bridge, null, config, NullLogger<ResilientReasoner>.Instance);

        // Act - Generate some failures
        await reasoner.ReasonAsync("Query 1", CoreReasoningMode.NeuralFirst);
        await reasoner.ReasonAsync("Query 2", CoreReasoningMode.NeuralFirst);
        await reasoner.ReasonAsync("Query 3", CoreReasoningMode.NeuralFirst);

        var health = reasoner.GetHealth();

        // Assert
        health.ConsecutiveLlmFailures.Should().Be(3);
        health.CircuitState.Should().Be("Closed", "Should still be closed with threshold of 5");
    }

    [Fact]
    public async Task ReasonAsync_SymbolicOnlyMode_BypassesCircuitBreaker()
    {
        // Arrange
        var config = new CircuitBreakerConfig { FailureThreshold = 1 };
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: false, symbolicWorks: true);
        var reasoner = new ResilientReasoner(bridge, null, config, NullLogger<ResilientReasoner>.Instance);

        // Open the circuit
        await reasoner.ReasonAsync("Query 1", CoreReasoningMode.NeuralFirst);

        // Act - Use SymbolicOnly mode
        bridge.ResetCallTracking();
        var result = await reasoner.ReasonAsync("Symbolic query", CoreReasoningMode.SymbolicOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        bridge.LastUsedMode.Should().Be(NsReasoningMode.SymbolicOnly);
    }

    [Fact]
    public async Task ReasonAsync_BothBackendsFail_ReturnsErrorMessage()
    {
        // Arrange
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: false, symbolicWorks: false);
        var reasoner = new ResilientReasoner(bridge);

        // Act
        var result = await reasoner.ReasonAsync("Query", CoreReasoningMode.NeuralFirst);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    [Fact]
    public async Task ReasonAsync_NormalOperation_UsesPreferredMode()
    {
        // Arrange
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: true);
        var reasoner = new ResilientReasoner(bridge);

        // Act
        await reasoner.ReasonAsync("Query", CoreReasoningMode.SymbolicFirst);

        // Assert
        bridge.LastUsedMode.Should().Be(NsReasoningMode.SymbolicFirst);
    }

    [Fact]
    public async Task ReasonAsync_SuccessfulOperation_UpdatesLastSuccessTime()
    {
        // Arrange
        var bridge = new MockNeuralSymbolicBridge(shouldSucceed: true);
        var reasoner = new ResilientReasoner(bridge);

        var healthBefore = reasoner.GetHealth();
        healthBefore.LastLlmSuccess.Should().BeNull();

        // Act
        await reasoner.ReasonAsync("Query", CoreReasoningMode.NeuralFirst);

        // Assert
        var healthAfter = reasoner.GetHealth();
        healthAfter.LastLlmSuccess.Should().NotBeNull();
        healthAfter.LastLlmSuccess.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReasoningModeEnums_AreAligned()
    {
        // This test verifies that Core.Resilience.ReasoningMode and NeuralSymbolic.ReasoningMode
        // have identical integer values, which is required for the enum cast in ResilientReasoner
        // to work correctly. If this test fails, the enums have diverged and must be synchronized.

        // Assert - verify each enum value matches
        ((int)CoreReasoningMode.SymbolicFirst).Should().Be((int)NsReasoningMode.SymbolicFirst);
        ((int)CoreReasoningMode.NeuralFirst).Should().Be((int)NsReasoningMode.NeuralFirst);
        ((int)CoreReasoningMode.Parallel).Should().Be((int)NsReasoningMode.Parallel);
        ((int)CoreReasoningMode.SymbolicOnly).Should().Be((int)NsReasoningMode.SymbolicOnly);
        ((int)CoreReasoningMode.NeuralOnly).Should().Be((int)NsReasoningMode.NeuralOnly);
        
        // Verify they have the same number of values
        Enum.GetValues(typeof(CoreReasoningMode)).Length
            .Should().Be(Enum.GetValues(typeof(NsReasoningMode)).Length,
                "both enums should have the same number of values");
    }

    /// <summary>
    /// Mock implementation of INeuralSymbolicBridge for testing.
    /// </summary>
    private class MockNeuralSymbolicBridge : INeuralSymbolicBridge
    {
        private bool _shouldSucceed;
        private readonly bool _symbolicWorks;
        
        public NsReasoningMode LastUsedMode { get; private set; }

        public MockNeuralSymbolicBridge(bool shouldSucceed, bool symbolicWorks = true)
        {
            _shouldSucceed = shouldSucceed;
            _symbolicWorks = symbolicWorks;
        }

        public void SetShouldSucceed(bool shouldSucceed) => _shouldSucceed = shouldSucceed;
        
        public void ResetCallTracking() => LastUsedMode = default;

        public Task<Result<List<SymbolicRule>, string>> ExtractRulesFromSkillAsync(
            Ouroboros.Agent.MetaAI.Skill skill, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<MeTTaExpression, string>> NaturalLanguageToMeTTaAsync(
            string naturalLanguage, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<string, string>> MeTTaToNaturalLanguageAsync(
            MeTTaExpression expression, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<ReasoningResult, string>> HybridReasonAsync(
            string query, NsReasoningMode mode = NsReasoningMode.SymbolicFirst, CancellationToken ct = default)
        {
            LastUsedMode = mode;

            // Simulate neural failure for neural-heavy modes
            if ((mode == NsReasoningMode.NeuralFirst || mode == NsReasoningMode.NeuralOnly) && !_shouldSucceed)
            {
                // If symbolic works and mode allows fallback, the bridge would handle it
                // But we're simulating the initial failure here
                return Task.FromResult(
                    Result<ReasoningResult, string>.Failure("Neural reasoning failed"));
            }

            // Symbolic-only mode
            if (mode == NsReasoningMode.SymbolicOnly && !_symbolicWorks)
            {
                return Task.FromResult(
                    Result<ReasoningResult, string>.Failure("Symbolic reasoning failed"));
            }

            // Success case
            var result = new ReasoningResult(
                query,
                "Mock answer",
                mode,
                new List<ReasoningStep>(),
                0.8,
                SymbolicSucceeded: _symbolicWorks,
                NeuralSucceeded: _shouldSucceed,
                TimeSpan.FromMilliseconds(100));

            return Task.FromResult(Result<ReasoningResult, string>.Success(result));
        }

        public Task<Result<ConsistencyReport, string>> CheckConsistencyAsync(
            Ouroboros.Agent.MetaAI.Hypothesis hypothesis,
            IReadOnlyList<SymbolicRule> knowledgeBase,
            CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<GroundedConcept, string>> GroundConceptAsync(
            string conceptDescription, float[] embedding, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
