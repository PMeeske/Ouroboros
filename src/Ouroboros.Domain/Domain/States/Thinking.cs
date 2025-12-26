namespace Ouroboros.Domain.States;

/// <summary>
/// Represents the thinking/reasoning phase where the model analyzes the problem before drafting.
/// </summary>
public record Thinking(string Text) : ReasoningState("Thinking", Text);
