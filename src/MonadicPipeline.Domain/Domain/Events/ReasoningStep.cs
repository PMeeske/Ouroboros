// <copyright file="ReasoningStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Events;

public sealed record ReasoningStep(
    Guid Id,
    string StepKind,
    ReasoningState State,
    DateTime Timestamp,
    string Prompt,
    List<ToolExecution>? ToolCalls = null) : PipelineEvent(Id, "Reasoning", Timestamp);
