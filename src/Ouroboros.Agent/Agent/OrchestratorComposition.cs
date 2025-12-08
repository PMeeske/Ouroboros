// <copyright file="OrchestratorComposition.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Orchestrator Composition Helpers
// Fluent API for composing and chaining orchestrators
// ==========================================================

namespace LangChainPipeline.Agent;

/// <summary>
/// Represents a composable orchestrator that can be chained with other orchestrators.
/// </summary>
/// <typeparam name="TInput">Input type.</typeparam>
/// <typeparam name="TOutput">Output type.</typeparam>
public interface IComposableOrchestrator<TInput, TOutput> : IOrchestrator<TInput, TOutput>
{
    /// <summary>
    /// Chains this orchestrator with another orchestrator.
    /// </summary>
    /// <typeparam name="TNext">Output type of the next orchestrator.</typeparam>
    /// <param name="next">The next orchestrator in the chain.</param>
    /// <returns>A composed orchestrator.</returns>
    IComposableOrchestrator<TInput, TNext> Then<TNext>(
        IComposableOrchestrator<TOutput, TNext> next);

    /// <summary>
    /// Maps the output of this orchestrator to a different type.
    /// </summary>
    /// <typeparam name="TNext">The mapped output type.</typeparam>
    /// <param name="mapper">Function to transform the output.</param>
    /// <returns>A composed orchestrator with mapped output.</returns>
    IComposableOrchestrator<TInput, TNext> Map<TNext>(
        Func<TOutput, TNext> mapper);

    /// <summary>
    /// Adds a side effect to execute after successful orchestration.
    /// </summary>
    /// <param name="sideEffect">Action to execute with the output.</param>
    /// <returns>The same orchestrator for chaining.</returns>
    IComposableOrchestrator<TInput, TOutput> Tap(
        Action<TOutput> sideEffect);
}

/// <summary>
/// Composite orchestrator that chains multiple orchestrators together.
/// Implements the Chain of Responsibility pattern for orchestrators.
/// </summary>
/// <typeparam name="TInput">Input type.</typeparam>
/// <typeparam name="TOutput">Output type.</typeparam>
public sealed class CompositeOrchestrator<TInput, TOutput> : OrchestratorBase<TInput, TOutput>, IComposableOrchestrator<TInput, TOutput>
{
    private readonly Func<TInput, OrchestratorContext, Task<TOutput>> _executeFunc;

    private CompositeOrchestrator(
        string name,
        OrchestratorConfig config,
        Func<TInput, OrchestratorContext, Task<TOutput>> executeFunc)
        : base(name, config)
    {
        _executeFunc = executeFunc ?? throw new ArgumentNullException(nameof(executeFunc));
    }

    /// <summary>
    /// Creates a composite orchestrator from a single base orchestrator.
    /// </summary>
    public static CompositeOrchestrator<TInput, TOutput> From(
        IOrchestrator<TInput, TOutput> orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);

        return new CompositeOrchestrator<TInput, TOutput>(
            orchestrator.Configuration.GetSetting("name", "composite_orchestrator") ?? "composite",
            orchestrator.Configuration,
            async (input, context) =>
            {
                var result = await orchestrator.ExecuteAsync(input, context);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Orchestration failed");
                }
                return result.Output!;
            });
    }

    /// <summary>
    /// Creates a composite orchestrator from a function.
    /// </summary>
    public static CompositeOrchestrator<TInput, TOutput> FromFunc(
        string name,
        Func<TInput, OrchestratorContext, Task<TOutput>> func,
        OrchestratorConfig? config = null)
    {
        return new CompositeOrchestrator<TInput, TOutput>(
            name,
            config ?? OrchestratorConfig.Default(),
            func);
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<TInput, TNext> Then<TNext>(
        IComposableOrchestrator<TOutput, TNext> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return new CompositeOrchestrator<TInput, TNext>(
            $"{OrchestratorName}_then_{next.Configuration.GetSetting<string>("name", "next")}",
            Configuration,
            async (input, context) =>
            {
                var intermediateResult = await _executeFunc(input, context);
                var finalResult = await next.ExecuteAsync(intermediateResult, context);
                if (!finalResult.Success)
                {
                    throw new InvalidOperationException(finalResult.ErrorMessage ?? "Chained orchestration failed");
                }
                return finalResult.Output!;
            });
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<TInput, TNext> Map<TNext>(
        Func<TOutput, TNext> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return new CompositeOrchestrator<TInput, TNext>(
            $"{OrchestratorName}_mapped",
            Configuration,
            async (input, context) =>
            {
                var intermediateResult = await _executeFunc(input, context);
                return mapper(intermediateResult);
            });
    }

    /// <inheritdoc/>
    public IComposableOrchestrator<TInput, TOutput> Tap(
        Action<TOutput> sideEffect)
    {
        ArgumentNullException.ThrowIfNull(sideEffect);

        return new CompositeOrchestrator<TInput, TOutput>(
            $"{OrchestratorName}_tapped",
            Configuration,
            async (input, context) =>
            {
                var result = await _executeFunc(input, context);
                sideEffect(result);
                return result;
            });
    }

    /// <inheritdoc/>
    protected override async Task<TOutput> ExecuteCoreAsync(TInput input, OrchestratorContext context)
    {
        return await _executeFunc(input, context);
    }
}

/// <summary>
/// Fluent builder for creating composite orchestrators.
/// Provides a chainable API for building complex orchestration pipelines.
/// </summary>
public sealed class OrchestratorComposer
{
    /// <summary>
    /// Starts a composition chain with the given orchestrator.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> Start<TInput, TOutput>(
        IOrchestrator<TInput, TOutput> orchestrator)
    {
        return CompositeOrchestrator<TInput, TOutput>.From(orchestrator);
    }

    /// <summary>
    /// Starts a composition chain with a function.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> StartWith<TInput, TOutput>(
        string name,
        Func<TInput, Task<TOutput>> func,
        OrchestratorConfig? config = null)
    {
        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            name,
            (input, _) => func(input),
            config);
    }

    /// <summary>
    /// Creates a parallel orchestrator that executes multiple orchestrators concurrently.
    /// </summary>
    /// <param name="maxConcurrency">Maximum number of orchestrators to execute concurrently (default: unlimited).</param>
    /// <param name="orchestrators">The orchestrators to execute in parallel.</param>
    public static IComposableOrchestrator<TInput, TOutput[]> Parallel<TInput, TOutput>(
        int maxConcurrency,
        params IOrchestrator<TInput, TOutput>[] orchestrators)
    {
        ArgumentNullException.ThrowIfNull(orchestrators);
        if (orchestrators.Length == 0)
        {
            throw new ArgumentException("At least one orchestrator is required", nameof(orchestrators));
        }

        return CompositeOrchestrator<TInput, TOutput[]>.FromFunc(
            "parallel_orchestrator",
            async (input, context) =>
            {
                var semaphore = maxConcurrency > 0 
                    ? new SemaphoreSlim(maxConcurrency, maxConcurrency) 
                    : null;

                try
                {
                    var tasks = orchestrators.Select(async o =>
                    {
                        if (semaphore != null)
                        {
                            await semaphore.WaitAsync(context.CancellationToken).ConfigureAwait(false);
                        }
                        try
                        {
                            return await o.ExecuteAsync(input, context).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore?.Release();
                        }
                    }).ToArray();

                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);

                    // Check for failures
                    var failures = results.Where(r => !r.Success).ToList();
                    if (failures.Any())
                    {
                        var errors = string.Join("; ", failures.Select(f => f.ErrorMessage));
                        throw new InvalidOperationException($"Parallel orchestration had failures: {errors}");
                    }

                    return results.Select(r => r.Output!).ToArray();
                }
                finally
                {
                    semaphore?.Dispose();
                }
            });
    }

    /// <summary>
    /// Creates a parallel orchestrator that executes multiple orchestrators concurrently without limits.
    /// </summary>
    /// <param name="orchestrators">The orchestrators to execute in parallel.</param>
    public static IComposableOrchestrator<TInput, TOutput[]> Parallel<TInput, TOutput>(
        params IOrchestrator<TInput, TOutput>[] orchestrators)
    {
        return Parallel(-1, orchestrators);
    }

    /// <summary>
    /// Creates a fallback orchestrator that tries the primary orchestrator first,
    /// then falls back to the secondary if the primary fails.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> WithFallback<TInput, TOutput>(
        IOrchestrator<TInput, TOutput> primary,
        IOrchestrator<TInput, TOutput> fallback)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(fallback);

        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            "fallback_orchestrator",
            async (input, context) =>
            {
                var primaryResult = await primary.ExecuteAsync(input, context);
                if (primaryResult.Success)
                {
                    return primaryResult.Output!;
                }

                // Try fallback
                var fallbackResult = await fallback.ExecuteAsync(input, context);
                if (!fallbackResult.Success)
                {
                    throw new InvalidOperationException(
                        $"Both primary and fallback failed. Primary: {primaryResult.ErrorMessage}, Fallback: {fallbackResult.ErrorMessage}");
                }

                return fallbackResult.Output!;
            });
    }

    /// <summary>
    /// Creates a conditional orchestrator that routes based on a predicate.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> Conditional<TInput, TOutput>(
        Func<TInput, bool> condition,
        IOrchestrator<TInput, TOutput> whenTrue,
        IOrchestrator<TInput, TOutput> whenFalse)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(whenTrue);
        ArgumentNullException.ThrowIfNull(whenFalse);

        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            "conditional_orchestrator",
            async (input, context) =>
            {
                var selectedOrchestrator = condition(input) ? whenTrue : whenFalse;
                var result = await selectedOrchestrator.ExecuteAsync(input, context);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.ErrorMessage ?? "Conditional orchestration failed");
                }
                return result.Output!;
            });
    }

    /// <summary>
    /// Creates a retry orchestrator that retries on failure.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> WithRetry<TInput, TOutput>(
        IOrchestrator<TInput, TOutput> orchestrator,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);

        var retryDelay = delay ?? TimeSpan.FromMilliseconds(100);

        return CompositeOrchestrator<TInput, TOutput>.FromFunc(
            "retry_orchestrator",
            async (input, context) =>
            {
                var attempt = 0;
                while (true)
                {
                    attempt++;
                    var result = await orchestrator.ExecuteAsync(input, context);
                    
                    if (result.Success)
                    {
                        return result.Output!;
                    }

                    if (attempt >= maxRetries)
                    {
                        throw new InvalidOperationException(
                            $"Operation failed after {attempt} attempts: {result.ErrorMessage}");
                    }

                    await Task.Delay(retryDelay, context.CancellationToken);
                }
            });
    }
}

/// <summary>
/// Extension methods for orchestrator composition.
/// </summary>
public static class OrchestratorCompositionExtensions
{
    /// <summary>
    /// Converts an orchestrator to a composable orchestrator.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput> AsComposable<TInput, TOutput>(
        this IOrchestrator<TInput, TOutput> orchestrator)
    {
        return CompositeOrchestrator<TInput, TOutput>.From(orchestrator);
    }

    /// <summary>
    /// Chains this orchestrator with another using Kleisli composition.
    /// </summary>
    public static IComposableOrchestrator<TInput, TNext> Bind<TInput, TOutput, TNext>(
        this IComposableOrchestrator<TInput, TOutput> orchestrator,
        Func<TOutput, Task<TNext>> binder)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(binder);

        return orchestrator.Then(
            CompositeOrchestrator<TOutput, TNext>.FromFunc(
                "bound_orchestrator",
                (output, _) => binder(output)));
    }

    /// <summary>
    /// Filters orchestrator execution based on a predicate.
    /// </summary>
    public static IComposableOrchestrator<TInput, TOutput?> Where<TInput, TOutput>(
        this IComposableOrchestrator<TInput, TOutput> orchestrator,
        Func<TOutput, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(predicate);

        return orchestrator.Then(
            CompositeOrchestrator<TOutput, TOutput?>.FromFunc(
                "filtered_orchestrator",
                (output, _) => Task.FromResult(predicate(output) ? output : default(TOutput?))));
    }
}
