namespace LangChainPipeline.Domain.States;

public sealed record Draft(string DraftText) : ReasoningState("Draft", DraftText);
