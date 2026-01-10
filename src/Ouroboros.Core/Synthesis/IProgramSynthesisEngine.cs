// <copyright file="IProgramSynthesisEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Core.Synthesis;

/// <summary>
/// Interface for a neural-guided program synthesis engine with library learning.
/// Implements DreamCoder-style wake-sleep algorithm for program synthesis and learning.
/// </summary>
public interface IProgramSynthesisEngine
{
    /// <summary>
    /// Synthesizes a program that satisfies the given input-output examples.
    /// Uses enumerative search guided by a neural recognition model.
    /// </summary>
    /// <param name="examples">The input-output examples that the program must satisfy.</param>
    /// <param name="dsl">The domain-specific language to use for synthesis.</param>
    /// <param name="timeout">Maximum time allowed for synthesis.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the synthesized program or an error message.</returns>
    Task<Result<Program, string>> SynthesizeProgramAsync(
        List<InputOutputExample> examples,
        DomainSpecificLanguage dsl,
        TimeSpan timeout,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts reusable primitives from a set of successful programs.
    /// Uses the specified compression strategy to identify common patterns.
    /// </summary>
    /// <param name="successfulPrograms">Programs to analyze for common patterns.</param>
    /// <param name="strategy">The compression strategy to use for extraction.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the list of extracted primitives or an error message.</returns>
    Task<Result<List<Primitive>, string>> ExtractReusablePrimitivesAsync(
        List<Program> successfulPrograms,
        CompressionStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Trains the neural recognition model on task-solution pairs.
    /// This implements the "sleep" phase of the wake-sleep algorithm.
    /// </summary>
    /// <param name="pairs">Training pairs of synthesis tasks and their solutions.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result indicating success or containing an error message.</returns>
    Task<Result<Unit, string>> TrainRecognitionModelAsync(
        List<(SynthesisTask Task, Program Solution)> pairs,
        CancellationToken ct = default);

    /// <summary>
    /// Evolves the DSL by incorporating new primitives and updating statistics.
    /// Creates a new DSL with updated primitives and adjusted priors.
    /// </summary>
    /// <param name="currentDSL">The current domain-specific language.</param>
    /// <param name="newPrimitives">New primitives to add to the DSL.</param>
    /// <param name="stats">Usage statistics for updating primitive priors.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>A Result containing the evolved DSL or an error message.</returns>
    Task<Result<DomainSpecificLanguage, string>> EvolveDSLAsync(
        DomainSpecificLanguage currentDSL,
        List<Primitive> newPrimitives,
        UsageStatistics stats,
        CancellationToken ct = default);
}
