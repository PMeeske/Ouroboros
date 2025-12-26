// <copyright file="ConversationalPipelineExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

/// <summary>
/// Example demonstrating conversational pipeline usage as shown in the PR review.
/// </summary>
public static class ConversationalPipelineExample
{
    /// <summary>
    /// Demonstrates the conversational pipeline pattern from the code review.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunConversationalExample()
    {
        Console.WriteLine("=== Conversational Pipeline Example ===");

        // Create a memory context (simulating a conversation memory)
        ConversationMemory memory = new ConversationMemory(maxTurns: 5);

        // Add some conversation history
        memory.AddTurn("Hello, how are you?", "I'm doing well, thank you! How can I help you today?");
        memory.AddTurn("Can you help me with a technical question?", "Of course! I'd be happy to help with any technical questions you have.");

        // Example conversation loop similar to the code in the PR review
        await RunConversationLoop(memory);
    }

    /// <summary>
    /// Simulates the conversation loop pattern from the PR review code.
    /// </summary>
    private static async Task RunConversationLoop(ConversationMemory memory)
    {
        // Simulate user inputs
        string[] userInputs =
        [
            "What is the best way to handle async operations in C#?",
            "Can you give me an example of using Task.Run?",
            "What about cancellation tokens?"
        ];

        foreach (string input in userInputs)
        {
            Console.WriteLine($"User: {input}");

            // Create a new context with the user input using the existing memory system
            MemoryContext<string> inputContext = new MemoryContext<string>(input, memory)
                .SetProperty("input", input);

            // Simulate the conversational pipeline builder
            ConversationBuilder<string, string> conversationBuilder = new ConversationBuilder<string, string>("conversation-context")
                .AddTransformation(
                    context =>
                {
                    // Add conversation history to context
                    string history = context.Memory.GetFormattedHistory();
                    context.SetProperty("conversation_history", history);
                    return context;
                }, "Added conversation history")
                .AddProcessor(
                    async (context, _) =>
                {
                    // Simulate AI processing (replace with actual LLM call)
                    string userInput = context.GetProperty<string>("input") ?? "No input";
                    string aiResponse = await SimulateAiResponse(userInput, context);

                    context.SetProperty("text", aiResponse);
                    return context;
                }, "Generated AI response");

            // Execute the conversational pipeline
            MemoryContext<string> result = await conversationBuilder.RunAsync(inputContext);
            string aiResponse = result.GetProperty<string>("text") ?? "No response generated";

            Console.WriteLine($"AI: {aiResponse}\n");

            // Add the turn to memory
            memory.AddTurn(input, aiResponse);

            // Brief delay to simulate processing time
            await Task.Delay(500);
        }

        // Display final conversation history
        Console.WriteLine("=== Conversation History ===");
        foreach (ConversationTurn turn in memory.GetTurns())
        {
            Console.WriteLine($"[{turn.Timestamp:HH:mm:ss}] Human: {turn.HumanInput}");
            Console.WriteLine($"[{turn.Timestamp:HH:mm:ss}] AI: {turn.AiResponse}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Simulates an AI response generation (placeholder for actual LLM integration).
    /// </summary>
    private static async Task<string> SimulateAiResponse(string userInput, MemoryContext<string> context)
    {
        // Simulate async processing
        await Task.Delay(100);

        // Simple response generation based on input keywords
        return userInput.ToLower() switch
        {
            var input when input.Contains("async") =>
                "For async operations in C#, I recommend using async/await patterns with Task-based APIs. " +
                "This provides better scalability and responsiveness in your applications.",

            var input when input.Contains("task.run") =>
                "Task.Run is useful for offloading CPU-bound work to a background thread. " +
                "Example: var result = await Task.Run(() => ComputeIntensiveOperation());",

            var input when input.Contains("cancellation") =>
                "CancellationTokens are essential for cooperative cancellation in async operations. " +
                "Pass them to async methods to allow graceful cancellation when needed.",

            _ => "I understand your question. Could you provide more specific details so I can give you a more targeted response?",
        };
    }
}
