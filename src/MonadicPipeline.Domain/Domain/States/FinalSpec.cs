namespace LangChainPipeline.Domain.States;

public sealed record FinalSpec(string FinalText) : ReasoningState("Final", FinalText);
