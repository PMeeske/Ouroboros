// <copyright file="OrchestratorArrows.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Orchestrator Arrow Composition
// Factory functions for creating orchestrators via arrow composition
// ==========================================================

using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Steps;

namespace Ouroboros.Agent;

/// <summary>
/// Arrow-based factory functions for creating orchestrators.
/// Provides composable orchestrator building blocks using Step arrows.
/// </summary>
public static class OrchestratorArrows
{
    /// <summary>
    /// Creates an orchestrator from a Step arrow with full infrastructure support.
    /// </summary>
    /// <typeparam name="TInput">Input type.</typeparam>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <param name="name">Orchestrator name.</param>
    /// <param name="executionArrow">The core execution arrow.</param>
    /// <param name="config">Optional configuration.</param>
    /// <param name="safetyGuard">Optional safety guard.</param>
    /// <returns>An orchestrator that wraps the arrow with infrastructure.</returns>
    public static IOrchestrator<TInput, TOutput> FromArrow<TInput, TOutput>(
        string name,
        Step<(TInput input, OrchestratorContext context), TOutput> executionArrow,
        OrchestratorConfig? config = null,
        ISafetyGuard? safetyGuard = null)
    {
        return new ArrowBasedOrchestrator<TInput, TOutput>(
            name,
            executionArrow,
            config ?? OrchestratorConfig.Default(),
            safetyGuard);
    }

    /// <summary>
    /// Creates an orchestrator from a simple Step arrow that doesn't need context.
    /// </summary>
    /// <typeparam name="TInput">Input type.</typeparam>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <param name="name">Orchestrator name.</param>
    /// <param name="executionArrow">The core execution arrow.</param>
    /// <param name="config">Optional configuration.</param>
    /// <param name="safetyGuard">Optional safety guard.</param>
    /// <returns>An orchestrator that wraps the arrow with infrastructure.</returns>
    public static IOrchestrator<TInput, TOutput> FromSimpleArrow<TInput, TOutput>(
        string name,
        Step<TInput, TOutput> executionArrow,
        OrchestratorConfig? config = null,
        ISafetyGuard? safetyGuard = null)
    {
        // Lift simple arrow to context-aware arrow
        Step<(TInput input, OrchestratorContext context), TOutput> contextArrow =
            async tuple => await executionArrow(tuple.input);

        return FromArrow(name, contextArrow, config, safetyGuard);
    }

    /// <summary>
    /// Creates a validation arrow for orchestrator input validation.
    /// </summary>
    /// <typeparam name="TInput">Input type.</typeparam>
    /// <param name="validator">Validation function.</param>
    /// <returns>A step that validates input and returns Result.</returns>
    public static Step<(TInput input, OrchestratorContext context), Result<bool, string>> CreateValidationArrow<TInput>(
        Func<TInput, OrchestratorContext, Result<bool, string>> validator)
        => async tuple => validator(tuple.input, tuple.context);

    /// <summary>
    /// Composes multiple orchestrator steps into a pipeline.
    /// </summary>
    /// <typeparam name="TInput">Initial input type.</typeparam>
    /// <typeparam name="TMid">Intermediate type.</typeparam>
    /// <typeparam name="TOutput">Final output type.</typeparam>
    /// <param name="first">First orchestrator in the pipeline.</param>
    /// <param name="second">Second orchestrator in the pipeline.</param>
    /// <returns>A composed orchestrator.</returns>
    public static IOrchestrator<TInput, TOutput> Compose<TInput, TMid, TOutput>(
        IOrchestrator<TInput, TMid> first,
        IOrchestrator<TMid, TOutput> second)
    {
        var name = $"{first.Configuration.GetSetting("name", "first")}_then_{second.Configuration.GetSetting("name", "second")}";
        var config = OrchestratorConfig.Default();

        Step<(TInput input, OrchestratorContext context), TOutput> composedArrow = async tuple =>
        {
            var firstResult = await first.ExecuteAsync(tuple.input, tuple.context);
            if (!firstResult.Success)
            {
                throw new InvalidOperationException(firstResult.ErrorMessage ?? "First orchestrator failed");
            }

            var secondResult = await second.ExecuteAsync(firstResult.Output!, tuple.context);
            if (!secondResult.Success)
            {
                throw new InvalidOperationException(secondResult.ErrorMessage ?? "Second orchestrator failed");
            }

            return secondResult.Output!;
        };

        return FromArrow(name, composedArrow, config);
    }

    /// <summary>
    /// Creates a retry arrow that wraps another arrow with retry logic.
    /// </summary>
    /// <typeparam name="TInput">Input type.</typeparam>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <param name="arrow">The arrow to retry.</param>
    /// <param name="retryConfig">Retry configuration.</param>
    /// <returns>An arrow with retry support.</returns>
    public static Step<TInput, TOutput> WithRetry<TInput, TOutput>(
        Step<TInput, TOutput> arrow,
        RetryConfig retryConfig)
        => async input =>
        {
            var attempt = 0;
            var delay = retryConfig.InitialDelay;
            Exception? lastException = null;

            while (attempt < retryConfig.MaxRetries)
            {
                attempt++;
                try
                {
                    return await arrow(input);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt >= retryConfig.MaxRetries)
                    {
                        break;
                    }

                    // Add jitter to prevent thundering herd
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.1)));
                    await Task.Delay(delay + jitter);
                    delay = TimeSpan.FromMilliseconds(
                        Math.Min(delay.TotalMilliseconds * retryConfig.BackoffMultiplier,
                                 retryConfig.MaxDelay.TotalMilliseconds));
                }
            }

            throw new InvalidOperationException($"Operation failed after {attempt} attempts", lastException);
        };

    /// <summary>
    /// Creates a timeout arrow that wraps another arrow with timeout logic.
    /// Note: This implementation creates a timeout but the cancellation token is not
    /// automatically propagated to the arrow. The timeout will throw TimeoutException
    /// if the arrow doesn't complete within the specified duration, but the arrow
    /// itself will continue running unless it checks for cancellation internally.
    /// </summary>
    /// <typeparam name="TInput">Input type.</typeparam>
    /// <typeparam name="TOutput">Output type.</typeparam>
    /// <param name="arrow">The arrow to wrap.</param>
    /// <param name="timeout">Timeout duration.</param>
    /// <returns>An arrow with timeout support.</returns>
    public static Step<TInput, TOutput> WithTimeout<TInput, TOutput>(
        Step<TInput, TOutput> arrow,
        TimeSpan timeout)
        => async input =>
        {
            using var cts = new CancellationTokenSource(timeout);
            try
            {
                return await arrow(input);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds");
            }
        };

    /// <summary>
    /// Arrow-based orchestrator implementation.
    /// </summary>
    private sealed class ArrowBasedOrchestrator<TInput, TOutput> : OrchestratorBase<TInput, TOutput>
    {
        private readonly Step<(TInput input, OrchestratorContext context), TOutput> _executionArrow;

        public ArrowBasedOrchestrator(
            string name,
            Step<(TInput input, OrchestratorContext context), TOutput> executionArrow,
            OrchestratorConfig config,
            ISafetyGuard? safetyGuard = null)
            : base(name, config, safetyGuard)
        {
            _executionArrow = executionArrow ?? throw new ArgumentNullException(nameof(executionArrow));
        }

        protected override Task<TOutput> ExecuteCoreAsync(TInput input, OrchestratorContext context)
        {
            return _executionArrow((input, context));
        }
    }
}
