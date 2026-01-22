// <copyright file="IPipelineContext.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Core;

/// <summary>
/// Represents a context that can flow through a pipeline with tracing capabilities.
/// </summary>
public interface IPipelineContext
{
    /// <summary>
    /// Gets or sets a value indicating whether tracing is enabled.
    /// </summary>
    bool Trace { get; set; }

    /// <summary>
    /// Gets the current output from the pipeline.
    /// </summary>
    string Output { get; }
}

/// <summary>
/// Represents a context with vector store capabilities.
/// </summary>
public interface IVectorContext
{
    /// <summary>
    /// Gets the vector store associated with this context.
    /// </summary>
    object? VectorStore { get; }
}

/// <summary>
/// Represents a context with LLM capabilities.
/// </summary>
public interface ILlmContext
{
    /// <summary>
    /// Gets the tool-aware chat model.
    /// </summary>
    object Llm { get; }

    /// <summary>
    /// Gets the tool registry.
    /// </summary>
    object Tools { get; }
}

/// <summary>
/// Marker interface for step metadata.
/// </summary>
public interface IStepMetadata
{
    /// <summary>
    /// Gets the name of the step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the step.
    /// </summary>
    string Description { get; }
}
