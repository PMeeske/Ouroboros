// <copyright file="Pipeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Easy;

/// <summary>
/// Static factory class for creating pipelines using the Easy API.
/// Provides convenient entry points for common pipeline patterns.
/// </summary>
public static class Pipeline
{
    /// <summary>
    /// Creates a new pipeline about the specified topic.
    /// </summary>
    /// <param name="topic">The topic or subject for the pipeline.</param>
    /// <returns>A new EasyPipeline instance.</returns>
    public static EasyPipeline Create(string topic) => EasyPipeline.Create(topic);

    /// <summary>
    /// Creates a basic reasoning pipeline: Draft -> Critique -> Improve.
    /// This is the most common pipeline pattern for generating improved content.
    /// </summary>
    /// <param name="topic">The topic for reasoning.</param>
    /// <returns>A pre-configured EasyPipeline with Draft, Critique, and Improve operations.</returns>
    public static EasyPipeline BasicReasoning(string topic) =>
        EasyPipeline.Create(topic)
            .Draft()
            .Critique()
            .Improve();

    /// <summary>
    /// Creates a full reasoning pipeline: Think -> Draft -> Critique -> Improve.
    /// Includes an initial thinking step for more comprehensive reasoning.
    /// </summary>
    /// <param name="topic">The topic for reasoning.</param>
    /// <returns>A pre-configured EasyPipeline with Think, Draft, Critique, and Improve operations.</returns>
    public static EasyPipeline FullReasoning(string topic) =>
        EasyPipeline.Create(topic)
            .Think()
            .Draft()
            .Critique()
            .Improve();

    /// <summary>
    /// Creates a summarization pipeline: Draft -> Summarize.
    /// Generates a draft and then creates a concise summary.
    /// </summary>
    /// <param name="topic">The topic to summarize.</param>
    /// <returns>A pre-configured EasyPipeline with Draft and Summarize operations.</returns>
    public static EasyPipeline Summarize(string topic) =>
        EasyPipeline.Create(topic)
            .Draft()
            .Summarize();

    /// <summary>
    /// Creates an iterative improvement pipeline with multiple critique-improve cycles.
    /// Each cycle refines the output further based on critiques.
    /// </summary>
    /// <param name="topic">The topic for reasoning.</param>
    /// <param name="iterations">The number of critique-improve cycles to perform.</param>
    /// <returns>A pre-configured EasyPipeline with Draft followed by multiple Critique-Improve cycles.</returns>
    public static EasyPipeline IterativeReasoning(string topic, int iterations = 2)
    {
        if (iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Must have at least 1 iteration");

        EasyPipeline pipeline = EasyPipeline.Create(topic).Draft();
        
        for (int i = 0; i < iterations; i++)
        {
            pipeline = pipeline.Critique().Improve();
        }
        
        return pipeline;
    }
}
