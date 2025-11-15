#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace LangChainPipeline.Domain.Events;

public sealed record ReasoningStep(
    Guid Id,
    string StepKind,
    ReasoningState State,
    DateTime Timestamp,
    string Prompt,
    List<ToolExecution>? ToolCalls = null
) : PipelineEvent(Id, "Reasoning", Timestamp);
