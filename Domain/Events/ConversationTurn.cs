namespace LangChainPipeline.Domain.Events;

/// <summary>
/// Represents a single turn in a conversation with both human input and AI response.
/// </summary>
public sealed record ConversationTurn(
    string HumanInput,
    string AiResponse,
    DateTime Timestamp
);