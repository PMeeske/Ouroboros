#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace LangChainPipeline.Domain.States;

public sealed record FinalSpec(string FinalText) : ReasoningState("Final", FinalText);
