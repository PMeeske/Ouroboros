// <copyright file="IPipelineMiddleware.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Middleware;

/// <summary>
/// Interface for pipeline middleware components.
/// </summary>
public interface IPipelineMiddleware
{
    /// <summary>
    /// Processes a pipeline request.
    /// </summary>
    /// <param name="context">The pipeline context.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pipeline result.</returns>
    Task<PipelineResult> ProcessAsync(
        PipelineContext context,
        Func<PipelineContext, CancellationToken, Task<PipelineResult>> next,
        CancellationToken ct = default);
}
