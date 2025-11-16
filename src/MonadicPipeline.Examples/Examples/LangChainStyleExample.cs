// <copyright file="LangChainStyleExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

/// <summary>
/// This example directly mirrors the LangChain conversation example provided in the problem statement,
/// but implemented using our Kleisli pipeline system with memory integration.
/// It demonstrates how LangChain's Chain syntax can be replaced with our monadic pipeline approach.
/// </summary>
public static class LangChainStyleExample
{
    /// <summary>
    /// Runs a conversation loop that mirrors the LangChain example but uses Kleisli pipes
    /// This is the main demonstration requested in the problem statement.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunLangChainStyleConversation()
    {
        Console.WriteLine("=== LANGCHAIN STYLE CONVERSATION WITH KLEISLI PIPES ===");
        Console.WriteLine("This mirrors the exact example from the problem statement!\n");

        // Create a simple prompt template for the conversation to help the AI
        // This is exactly the same template from the LangChain example
        string template = @"
The following is a friendly conversation between a human and an AI.

{history}
Human: {input}
AI: ";

        // To have a conversation that remembers previous messages we need to use memory.
        // Here we pick one of a number of different strategies for implementing memory.
        ConversationMemory memory = PickMemoryStrategy();

        // Build the chain that will be used for each turn in our conversation.
        // This is the Kleisli pipeline equivalent of:
        // LoadMemory(memory, outputKey: "history")
        // | Template(template)
        // | LLM(model)
        // | UpdateMemory(memory, requestKey: "input", responseKey: "text");
        ConversationChainBuilder<string> conversationalChain = string.Empty
            .StartConversation(memory)
            .LoadMemory(outputKey: "history")
            .Template(template)
            .Llm("Friendly AI:")
            .UpdateMemory(inputKey: "input", responseKey: "text");

        Console.WriteLine();
        Console.WriteLine("Start a conversation with the friendly AI!");
        Console.WriteLine("(Enter 'exit' or hit Ctrl-C to end the conversation)");

        // Run an endless loop of conversation (limited for demo purposes)
        string[] conversationInputs = new[]
        {
            "Hello! My name is Alice. What's your name?",
            "What did I tell you my name was?",
            "Can you tell me a joke?",
            "Do you remember my name from earlier?",
            "exit",
        };

        foreach (string? input in conversationInputs)
        {
            Console.WriteLine();
            Console.Write("Human: ");

            if (input == "exit")
            {
                Console.WriteLine(input);
                break;
            }

            Console.WriteLine(input);

            // Build a new chain by prepending the user's input to the original chain
            // This is the Kleisli equivalent of: Set(input, "input") | chain
            MemoryContext<string> inputContext = input
                .WithMemory(memory)
                .SetProperty("input", input);

            // Get a response from the AI by running the conversational chain
            string? response = await conversationalChain.RunAsync<string>("text");

            Console.Write("AI: ");
            Console.WriteLine(response ?? "I couldn't generate a response.");

            // Brief delay to simulate processing time
            await Task.Delay(1000);
        }

        Console.WriteLine("\nConversation ended!");
        Console.WriteLine("\n=== Final Memory State ===");
        Console.WriteLine($"Total conversation turns: {memory.GetTurns().Count}");
        Console.WriteLine("\nComplete conversation history:");
        string fullHistory = memory.GetFormattedHistory();
        Console.WriteLine(fullHistory.Length > 0 ? fullHistory : "No history available");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates different memory strategies similar to the LangChain example
    /// This mirrors the PickMemoryStrategy method from the problem statement.
    /// </summary>
    private static ConversationMemory PickMemoryStrategy()
    {
        // For demo purposes, we'll show different strategies
        // In the real LangChain example, this would prompt the user for choice
        string[] strategies = new[]
        {
            "ConversationBufferMemory",
            "ConversationWindowBufferMemory",
            "ConversationSummaryMemory",
            "ConversationSummaryBufferMemory",
        };

        Console.WriteLine("Memory strategies available (auto-selecting for demo):");
        for (int i = 0; i < strategies.Length; i++)
        {
            Console.WriteLine($"    {i + 1}: {strategies[i]}");
        }

        // For this demo, we'll simulate selecting ConversationWindowBufferMemory
        string selectedStrategy = "ConversationWindowBufferMemory";
        Console.WriteLine($"\nAuto-selected: '{selectedStrategy}' (keeps last 3 turns)\n");

        return selectedStrategy switch
        {
            "ConversationBufferMemory" => GetConversationBufferMemory(),
            "ConversationWindowBufferMemory" => GetConversationWindowBufferMemory(),
            "ConversationSummaryMemory" => GetConversationSummaryMemory(),
            "ConversationSummaryBufferMemory" => GetConversationSummaryBufferMemory(),
            _ => throw new InvalidOperationException($"Unexpected memory class name: '{selectedStrategy}'"),
        };
    }

    /// <summary>
    /// Creates buffer memory that keeps all conversation history
    /// Mirrors GetConversationBufferMemory from the LangChain example.
    /// </summary>
    private static ConversationMemory GetConversationBufferMemory()
    {
        // Keep unlimited conversation history
        return new ConversationMemory(maxTurns: int.MaxValue);
    }

    /// <summary>
    /// Creates window buffer memory that keeps only recent conversation history
    /// Mirrors GetConversationWindowBufferMemory from the LangChain example.
    /// </summary>
    private static ConversationMemory GetConversationWindowBufferMemory()
    {
        // Keep only the last 3 conversation turns (similar to WindowSize = 3 in LangChain)
        return new ConversationMemory(maxTurns: 3);
    }

    /// <summary>
    /// Creates summary memory (simplified version for demo)
    /// In real LangChain, this would use an LLM to summarize old conversations.
    /// </summary>
    private static ConversationMemory GetConversationSummaryMemory()
    {
        // For demo purposes, this is just a regular buffer
        // In a real implementation, you would periodically summarize old conversations
        return new ConversationMemory(maxTurns: 10);
    }

    /// <summary>
    /// Creates summary buffer memory (simplified version for demo)
    /// In real LangChain, this combines token counting with summarization.
    /// </summary>
    private static ConversationMemory GetConversationSummaryBufferMemory()
    {
        // For demo purposes, this is just a buffer with moderate history
        // In a real implementation, you would count tokens and summarize when needed
        return new ConversationMemory(maxTurns: 5);
    }

    /// <summary>
    /// Demonstrates the exact LangChain chain composition pattern using Kleisli pipes.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateLangChainEquivalence()
    {
        Console.WriteLine("=== LANGCHAIN EQUIVALENCE DEMONSTRATION ===");
        Console.WriteLine("Showing how LangChain syntax maps to Kleisli pipes:\n");

        ConversationMemory memory = new ConversationMemory(maxTurns: 5);
        string template = "Context: {history}\nHuman: {input}\nAI: ";

        Console.WriteLine("LangChain syntax:");
        Console.WriteLine("var chain =");
        Console.WriteLine("    LoadMemory(memory, outputKey: \"history\")");
        Console.WriteLine("    | Template(template)");
        Console.WriteLine("    | LLM(model)");
        Console.WriteLine("    | UpdateMemory(memory, requestKey: \"input\", responseKey: \"text\");");
        Console.WriteLine();

        Console.WriteLine("Kleisli Pipeline equivalent:");
        Console.WriteLine("var chain = input");
        Console.WriteLine("    .StartConversation(memory)");
        Console.WriteLine("    .LoadMemory(outputKey: \"history\")");
        Console.WriteLine("    .Template(template)");
        Console.WriteLine("    .LLM(\"AI:\")");
        Console.WriteLine("    .UpdateMemory(inputKey: \"input\", responseKey: \"text\");");
        Console.WriteLine();

        // Demonstrate the actual execution
        string testInput = "How does memory work in this system?";

        Console.WriteLine($"Test input: \"{testInput}\"");
        Console.WriteLine("Executing Kleisli pipeline...\n");

        ConversationChainBuilder<string> chain = testInput
            .StartConversation(memory)
            .Set(testInput, "input")
            .LoadMemory(outputKey: "history")
            .Template(template)
            .Llm("Kleisli AI:")
            .UpdateMemory(inputKey: "input", responseKey: "text");

        string? result = await chain.RunAsync<string>("text");

        Console.WriteLine($"AI Response: {result}");
        Console.WriteLine($"Memory now contains: {memory.GetTurns().Count} turns");
        Console.WriteLine($"History: {memory.GetFormattedHistory()}");
        Console.WriteLine();
    }
}
