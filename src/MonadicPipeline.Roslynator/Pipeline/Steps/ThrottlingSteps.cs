using System;
using System.Threading;
using System.Threading.Tasks;

namespace LangChainPipeline.Roslynator.Pipeline.Steps;

/// <summary>
/// Semaphore-based throttling helpers — wrap an async step to limit concurrency.
/// </summary>
public static class ThrottlingSteps
{
    // Global semaphore; adjust concurrency as needed or make configurable
    private static readonly SemaphoreSlim _aiSemaphore = new SemaphoreSlim(2, 2);

    /// <summary>
    /// Wrap an AI step to limit concurrent calls.
    /// Usage: | ThrottlingSteps.WithLock(OllamaSteps.GenerateFix)
    /// </summary>
    public static Func<FixState, Task<FixState>> WithLock(Func<FixState, Task<FixState>> innerStep)
    {
        if (innerStep is null) throw new ArgumentNullException(nameof(innerStep));

        return async state =>
        {
            // If earlier steps already modified the AST, skip heavy operations
            if (!state.Changes.IsEmpty) return state;

            await _aiSemaphore.WaitAsync(state.CancellationToken).ConfigureAwait(false);
            try
            {
                return await innerStep(state).ConfigureAwait(false);
            }
            finally
            {
                _aiSemaphore.Release();
            }
        };
    }
}