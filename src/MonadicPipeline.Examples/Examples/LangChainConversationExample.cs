// <copyright file="LangChainConversationExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

/// <summary>
/// Example demonstrating LangChain-based conversational pipeline usage.
/// This replaces the custom implementation with LangChain's built-in patterns.
/// </summary>
public static class LangChainConversationExample
{
    /// <summary>
    /// Demonstrates the LangChain-based conversational pipeline.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunLangChainConversationalExample()
    {
        Console.WriteLine("=== LangChain-Based Conversational Pipeline Example ===");

        // Create LangChain-based conversation context
        var conversationContext = new LangChainConversationContext(maxTurns: 5);

        // Add some conversation history
        conversationContext.AddTurn(
            "Hello, how are you?",
            "I'm doing well, thank you! How can I help you today?");
        conversationContext.AddTurn(
            "Can you help me with a technical question?",
            "Of course! I'd be happy to help with any technical questions you have.");

        // Example conversation loop using LangChain-based approach
        await RunLangChainConversationLoop(conversationContext);
    }

    /// <summary>
    /// Simulates the conversation loop using LangChain-based classes.
    /// </summary>
    private static async Task RunLangChainConversationLoop(LangChainConversationContext conversationContext)
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

            // Create context using LangChain-based approach
            var inputContext = input
                .WithLangChainMemory(conversationContext.GetProperties().Count + 1)
                .SetProperty("input", input);

            // Create LangChain-based conversational pipeline
            var pipeline = LangChainConversationBuilder.CreateConversationPipeline()
                .WithConversationHistory()
                .AddAiResponseGeneration(async userInput =>
                {
                    // Simulate AI processing (replace with actual LLM call)
                    await Task.Delay(100);
                    return await SimulateAiResponse(userInput, inputContext);
                });

            // Execute the LangChain-based conversational pipeline
            var result = await pipeline.RunAsync(inputContext);
            var aiResponse = result.GetProperty<string>("text") ?? "No response generated";

            Console.WriteLine($"AI: {aiResponse}\n");

            // Add the turn to conversation history
            conversationContext.AddTurn(input, aiResponse);

            // Brief delay to simulate processing time
            await Task.Delay(500);
        }

        // Display final conversation history
        Console.WriteLine("=== LangChain Conversation History ===");
        var history = conversationContext.GetConversationHistory();
        if (!string.IsNullOrEmpty(history))
        {
            Console.WriteLine(history);
        }
    }

    /// <summary>
    /// Simulates an AI response generation using LangChain context.
    /// </summary>
    private static async Task<string> SimulateAiResponse(string userInput, LangChainConversationContext context)
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
