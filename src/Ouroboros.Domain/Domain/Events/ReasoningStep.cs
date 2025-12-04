// <copyright file="ReasoningStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Events;

/// <summary>
/// Event representing a reasoning step in the pipeline execution.
/// Captures the state, prompt, and any tool calls made during reasoning.
/// </summary>
/// <param name="Id">Unique identifier for this event</param>
/// <param name="StepKind">The kind of reasoning step (Draft, Critique, etc.)</param>
/// <param name="State">The reasoning state produced by this step</param>
/// <param name="Timestamp">When this reasoning step occurred</param>
/// <param name="Prompt">The prompt used to generate this reasoning</param>
/// <param name="ToolCalls">Optional list of tool executions during this step</param>
public sealed record ReasoningStep(
    Guid Id,
    string StepKind,
    ReasoningState State,
    DateTime Timestamp,
    string Prompt,
    List<ToolExecution>? ToolCalls = null) : PipelineEvent(Id, "Reasoning", Timestamp);
