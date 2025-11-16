// <copyright file="ConversationalKleisliExamples.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

/// <summary>
/// Examples demonstrating the integration of LangChain memory concepts with Kleisli pipes.
/// This shows how to create conversational AI systems using our monadic pipeline architecture.
/// </summary>
public static class ConversationalKleisliExamples
{
    /// <summary>
    /// Demonstrates the LangChain-style conversational chain using Kleisli pipes
    /// This mirrors the example from the problem statement but uses our monadic system.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunConversationalChainDemo()
    {
        Console.WriteLine("=== LangChain-Style Conversational Chain with Kleisli Pipes ===\n");

        // Create a prompt template similar to the LangChain example
        const string template = @"
The following is a friendly conversation between a human and an AI.

{history}
Human: {input}
AI: ";

        // Create a memory instance (similar to LangChain's memory strategies)
        var memory = new ConversationMemory(maxTurns: 5);

        // Build the conversational chain using our Kleisli pipeline system
        // This mirrors the LangChain pattern: LoadMemory | Template | LLM | UpdateMemory
        var conversationBuilder = string.Empty
            .StartConversation(memory)
            .LoadMemory(outputKey: "history")
            .Template(template)
            .Llm("AI Response:")
            .UpdateMemory(inputKey: "input", responseKey: "text");

        // Simulate a conversation
        var conversationInputs = new[]
        {
            "Hello! What's your name?",
            "What did I just ask you about?",
            "Can you remember our conversation so far?",
            "What was my first question?",
        };

        foreach (var input in conversationInputs)
        {
            Console.WriteLine($"Human: {input}");

            // Create a new context with the user input
            var inputContext = input
                .WithMemory(memory)
                .SetProperty("input", input);

            // Execute the conversational pipeline
            var result = await conversationBuilder.RunAsync();
            var aiResponse = result.GetProperty<string>("text") ?? "No response generated";

            Console.WriteLine($"AI: {aiResponse}\n");

            // Brief delay to simulate processing time
            await Task.Delay(500);
        }

        // Show the complete conversation history
        Console.WriteLine("=== Complete Conversation History ===");
        var history = memory.GetFormattedHistory();
        Console.WriteLine(history.Length > 0 ? history : "No history available");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates different memory strategies integrated with Kleisli pipes.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunMemoryStrategyDemo()
    {
        Console.WriteLine("=== Memory Strategy Demonstration ===\n");

        await DemonstrateBufferMemory();
        await DemonstrateWindowMemory();
    }

    private static async Task DemonstrateBufferMemory()
    {
        Console.WriteLine("--- Buffer Memory Strategy ---");

        var bufferMemory = new ConversationMemory(maxTurns: 10); // Keep all turns

        var pipeline = "Initial input"
            .StartConversation(bufferMemory)
            .Set("Hello, I'm learning about AI", "input")
            .LoadMemory()
            .Template("Context: {history}\nHuman: {input}\nAI: ")
            .Llm("Buffer AI:")
            .UpdateMemory();

        var result = await pipeline.RunAsync();
        var response = result.GetProperty<string>("text");
        Console.WriteLine($"Buffer Memory Response: {response}");
        Console.WriteLine($"Memory turns count: {bufferMemory.GetTurns().Count}\n");
    }

    private static async Task DemonstrateWindowMemory()
    {
        Console.WriteLine("--- Window Memory Strategy ---");

        var windowMemory = new ConversationMemory(maxTurns: 2); // Only keep last 2 turns

        // Add several turns to test windowing
        windowMemory.AddTurn("First question", "First response");
        windowMemory.AddTurn("Second question", "Second response");
        windowMemory.AddTurn("Third question", "Third response");

        var pipeline = "Latest input"
            .StartConversation(windowMemory)
            .Set("What do you remember?", "input")
            .LoadMemory()
            .Template("Recent context: {history}\nHuman: {input}\nAI: ")
            .Llm("Window AI:")
            .UpdateMemory();

        var result = await pipeline.RunAsync();
        var response = result.GetProperty<string>("text");
        Console.WriteLine($"Window Memory Response: {response}");
        Console.WriteLine($"Memory turns count: {windowMemory.GetTurns().Count}");
        Console.WriteLine($"History: {windowMemory.GetFormattedHistory()}\n");
    }

    /// <summary>
    /// Demonstrates integration with the existing Kleisli composition patterns.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunKleisliCompositionDemo()
    {
        Console.WriteLine("=== Kleisli Composition with Memory Integration ===\n");

        var memory = new ConversationMemory();

        // Create individual memory-aware Kleisli arrows
        var memoryLoader = MemoryArrows.LoadMemory<string>("history");
        var templateProcessor = MemoryArrows.Template("Context: {history}\nQuery: {input}\nResponse: ");
        var mockLlm = MemoryArrows.MockLlm("Composed AI:");
        var memoryUpdater = MemoryArrows.UpdateMemory<string>("input", "text");

        // Compose them using traditional Kleisli composition
        var composedPipeline = memoryLoader
            .Then(templateProcessor)
            .Then(mockLlm)
            .Then(memoryUpdater);

        // Test the composed pipeline
        var testInputs = new[] { "First query", "Second query", "Third query" };

        foreach (var input in testInputs)
        {
            var context = input
                .WithMemory(memory)
                .SetProperty("input", input);

            var result = await composedPipeline(context);
            var response = result.GetProperty<string>("text");

            Console.WriteLine($"Input: {input}");
            Console.WriteLine($"Response: {response}");
            Console.WriteLine($"Memory turns: {memory.GetTurns().Count}\n");
        }
    }

    /// <summary>
    /// Demonstrates error handling with memory-aware pipelines.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunErrorHandlingDemo()
    {
        Console.WriteLine("=== Error Handling with Memory Pipelines ===\n");

        var memory = new ConversationMemory();

        // Create a pipeline that might fail
        var errorProneStep = new Step<MemoryContext<string>, MemoryContext<string>>(context =>
        {
            if (context.Data.Contains("error"))
            {
                throw new InvalidOperationException("Simulated error in pipeline");
            }

            return Task.FromResult(context.SetProperty("text", $"Successfully processed: {context.Data}"));
        });

        var safeComposition = MemoryArrows.LoadMemory<string>()
            .Then(errorProneStep)
            .Then(MemoryArrows.UpdateMemory<string>());

        var testInputs = new[] { "normal input", "error input", "another normal input" };

        foreach (var input in testInputs)
        {
            try
            {
                var context = input.WithMemory(memory).SetProperty("input", input);
                var result = await safeComposition(context);
                var response = result.GetProperty<string>("text");
                Console.WriteLine($"Input: '{input}' -> Response: '{response}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error for '{input}': {ex.Message}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all conversational Kleisli demonstrations.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllDemonstrations()
    {
        Console.WriteLine("=== CONVERSATIONAL KLEISLI PIPELINE DEMONSTRATIONS ===\n");

        await RunConversationalChainDemo();
        await RunMemoryStrategyDemo();
        await RunKleisliCompositionDemo();
        await RunErrorHandlingDemo();

        Console.WriteLine("=== All Conversational Demonstrations Complete ===\n");
    }
}
