namespace LangChainPipeline.Domain.States;

public sealed record Critique(string CritiqueText) : ReasoningState("Critique", CritiqueText);
