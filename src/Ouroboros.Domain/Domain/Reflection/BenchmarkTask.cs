// <copyright file="BenchmarkTask.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Represents a benchmark task for assessing a specific cognitive dimension.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Name">Name of the benchmark task</param>
/// <param name="Dimension">The cognitive dimension this task assesses</param>
/// <param name="Execute">Function that executes the task and returns success/failure</param>
/// <param name="Timeout">Maximum time allowed for task execution</param>
public sealed record BenchmarkTask(
    string Name,
    CognitiveDimension Dimension,
    Func<Task<bool>> Execute,
    TimeSpan Timeout)
{
    /// <summary>
    /// Executes the benchmark task with timeout handling.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if task succeeded within timeout, false otherwise</returns>
    public async Task<bool> ExecuteWithTimeoutAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(this.Timeout);

            var task = this.Execute();
            await task.WaitAsync(cts.Token).ConfigureAwait(false);
            return task.Result;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
