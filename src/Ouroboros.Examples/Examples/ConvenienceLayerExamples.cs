// <copyright file="ConvenienceLayerExamples.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Practical examples demonstrating the Meta-AI Convenience Layer.
/// Shows how to use preset configurations and one-liner methods.
/// </summary>
public static class ConvenienceLayerExamples
{
    /// <summary>
    /// Example 1: Quick Question and Answer with Simple Orchestrator.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task QuickQuestionAnswering()
    {
        Console.WriteLine("=== Example 1: Quick Question Answering ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

        // Create simple orchestrator
        Result<IMetaAIPlannerOrchestrator, string> orchestratorResult = MetaAIConvenience.CreateSimple(chatModel);

        if (!orchestratorResult.IsSuccess)
        {
            Console.WriteLine($"Failed to create orchestrator: {orchestratorResult.Error}");
            return;
        }

        IMetaAIPlannerOrchestrator orchestrator = orchestratorResult.Value;

        // Ask questions
        string[] questions = new[]
        {
            "What is functional programming?",
            "Explain monads in simple terms",
            "What are the benefits of type safety?",
        };

        foreach (string? question in questions)
        {
            Console.WriteLine($"Q: {question}");

            Result<string, string> answer = await orchestrator.AskQuestion(question);

            answer.Match(
                result => Console.WriteLine($"A: {result.Substring(0, Math.Min(150, result.Length))}...\n"),
                error => Console.WriteLine($"Error: {error}\n"));
        }
    }

    /// <summary>
    /// Example 2: Code Generation Assistant.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task CodeGenerationAssistant()
    {
        Console.WriteLine("=== Example 2: Code Generation Assistant ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        // Create code assistant
        Result<IMetaAIPlannerOrchestrator, string> orchestratorResult = MetaAIConvenience.CreateCodeAssistant(chatModel, tools);

        if (!orchestratorResult.IsSuccess)
        {
            Console.WriteLine($"Failed to create orchestrator: {orchestratorResult.Error}");
            return;
        }

        IMetaAIPlannerOrchestrator orchestrator = orchestratorResult.Value;

        // Generate code
        Result<string, string> codeResult = await orchestrator.GenerateCode(
            description: "Create a generic repository pattern interface",
            language: "C#");

        codeResult.Match(
            code =>
            {
                Console.WriteLine("Generated Code:");
                Console.WriteLine(code);
            },
            error => Console.WriteLine($"Code generation failed: {error}"));
    }

    /// <summary>
    /// Example 3: Research and Analysis.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task ResearchAnalysis()
    {
        Console.WriteLine("=== Example 3: Research and Analysis ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        // Create research assistant
        Result<IMetaAIPlannerOrchestrator, string> orchestratorResult = MetaAIConvenience.CreateResearchAssistant(chatModel, tools, embedModel);

        if (!orchestratorResult.IsSuccess)
        {
            Console.WriteLine($"Failed to create orchestrator: {orchestratorResult.Error}");
            return;
        }

        IMetaAIPlannerOrchestrator orchestrator = orchestratorResult.Value;

        // Sample text to analyze
        string text = @"
            Functional programming emphasizes immutability, pure functions, and composability.
            Monads provide a way to structure programs generically. They allow you to build
            computations using sequenced steps while abstracting away the control flow.
            The Result monad, for example, encapsulates the concept of operations that can fail.
        ";

        // Analyze with quality verification
        Result<(string analysis, double quality), string> analysisResult = await orchestrator.AnalyzeText(
            text: text,
            analysisGoal: "Extract key concepts and explain their relationships");

        analysisResult.Match(
            result =>
            {
                Console.WriteLine($"Analysis (Quality: {result.quality:P0}):");
                Console.WriteLine(result.analysis);
            },
            error => Console.WriteLine($"Analysis failed: {error}"));
    }

    /// <summary>
    /// Example 4: Complete Workflow with Learning.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task CompleteWorkflowWithLearning()
    {
        Console.WriteLine("=== Example 4: Complete Workflow with Learning ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        // Create advanced orchestrator
        Result<IMetaAIPlannerOrchestrator, string> orchestratorResult = MetaAIConvenience.CreateAdvanced(chatModel, tools, embedModel);

        if (!orchestratorResult.IsSuccess)
        {
            Console.WriteLine($"Failed to create orchestrator: {orchestratorResult.Error}");
            return;
        }

        IMetaAIPlannerOrchestrator orchestrator = orchestratorResult.Value;

        // Execute complete workflow
        Result<VerificationResult, string> workflowResult = await orchestrator.CompleteWorkflow(
            goal: "Design a caching strategy for a distributed system",
            context: new Dictionary<string, object>
            {
                ["scale"] = "high",
                ["consistency"] = "eventual",
                ["availability"] = "99.9%",
            },
            autoLearn: true); // Enable automatic learning

        workflowResult.Match(
            verification =>
            {
                Console.WriteLine($"Workflow Completed!");
                Console.WriteLine($"Verified: {verification.Verified}");
                Console.WriteLine($"Quality Score: {verification.QualityScore:P0}");

                if (verification.Verified && verification.QualityScore > 0.8)
                {
                    Console.WriteLine("\n✓ High-quality solution generated and learned!");
                }
            },
            error => Console.WriteLine($"Workflow failed: {error}"));
    }

    /// <summary>
    /// Example 5: Batch Processing.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task BatchProcessing()
    {
        Console.WriteLine("=== Example 5: Batch Processing ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();

        // Create standard orchestrator
        Result<IMetaAIPlannerOrchestrator, string> orchestratorResult = MetaAIConvenience.CreateStandard(
            chatModel,
            tools,
            new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text")));

        if (!orchestratorResult.IsSuccess)
        {
            Console.WriteLine($"Failed to create orchestrator: {orchestratorResult.Error}");
            return;
        }

        IMetaAIPlannerOrchestrator orchestrator = orchestratorResult.Value;

        // Process multiple related tasks
        string[] tasks = new[]
        {
            "Explain dependency injection",
            "Explain inversion of control",
            "Compare dependency injection vs service locator pattern",
        };

        Dictionary<string, object> context = new Dictionary<string, object>
        {
            ["format"] = "concise",
            ["audience"] = "intermediate developers",
        };

        Console.WriteLine("Processing batch tasks...\n");

        List<Result<string, string>> results = await orchestrator.ProcessBatch(tasks, context);

        for (int i = 0; i < tasks.Length; i++)
        {
            Console.WriteLine($"Task {i + 1}: {tasks[i]}");

            results[i].Match(
                output => Console.WriteLine($"✓ {output.Substring(0, Math.Min(100, output.Length))}...\n"),
                error => Console.WriteLine($"✗ Error: {error}\n"));
        }
    }

    /// <summary>
    /// Example 6: Interactive Chat Session.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task InteractiveChatSession()
    {
        Console.WriteLine("=== Example 6: Interactive Chat Session ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));

        // Create chat assistant
        Result<IMetaAIPlannerOrchestrator, string> orchestratorResult = MetaAIConvenience.CreateChatAssistant(chatModel);

        if (!orchestratorResult.IsSuccess)
        {
            Console.WriteLine($"Failed to create orchestrator: {orchestratorResult.Error}");
            return;
        }

        IMetaAIPlannerOrchestrator orchestrator = orchestratorResult.Value;

        Console.WriteLine("Chat Assistant ready! (Type 'exit' to quit)\n");

        // Simulate a few interactions (in real usage, use Console.ReadLine())
        string[] interactions = new[]
        {
            "Hello, how are you?",
            "What can you help me with?",
            "Explain what a pipeline is",
        };

        foreach (string? userInput in interactions)
        {
            Console.WriteLine($"You: {userInput}");

            Result<string, string> response = await orchestrator.AskQuestion(userInput);

            response.Match(
                answer => Console.WriteLine($"Assistant: {answer.Substring(0, Math.Min(120, answer.Length))}...\n"),
                error => Console.WriteLine($"Error: {error}\n"));
        }
    }

    /// <summary>
    /// Example 7: Comparing Presets.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task ComparePresets()
    {
        Console.WriteLine("=== Example 7: Comparing Presets ===\n");

        OllamaProvider provider = new OllamaProvider();
        OllamaChatAdapter chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        ToolRegistry tools = ToolRegistry.CreateDefault();
        OllamaEmbeddingAdapter embedModel = new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, "nomic-embed-text"));

        string question = "What is event sourcing?";

        // Test Simple preset
        Result<IMetaAIPlannerOrchestrator, string> simpleResult = MetaAIConvenience.CreateSimple(chatModel);
        if (simpleResult.IsSuccess)
        {
            Result<string, string> answer = await simpleResult.Value.AskQuestion(question);
            Console.WriteLine("Simple Preset:");
            answer.Match(
                a => Console.WriteLine($"  {a.Substring(0, Math.Min(80, a.Length))}...\n"),
                e => Console.WriteLine($"  Error: {e}\n"));
        }

        // Test Standard preset
        Result<IMetaAIPlannerOrchestrator, string> standardResult = MetaAIConvenience.CreateStandard(chatModel, tools, embedModel);
        if (standardResult.IsSuccess)
        {
            Result<string, string> answer = await standardResult.Value.AskQuestion(question);
            Console.WriteLine("Standard Preset:");
            answer.Match(
                a => Console.WriteLine($"  {a.Substring(0, Math.Min(80, a.Length))}...\n"),
                e => Console.WriteLine($"  Error: {e}\n"));
        }

        // Test Advanced preset
        Result<IMetaAIPlannerOrchestrator, string> advancedResult = MetaAIConvenience.CreateAdvanced(chatModel, tools, embedModel);
        if (advancedResult.IsSuccess)
        {
            Result<string, string> answer = await advancedResult.Value.AskQuestion(question);
            Console.WriteLine("Advanced Preset:");
            answer.Match(
                a => Console.WriteLine($"  {a.Substring(0, Math.Min(80, a.Length))}...\n"),
                e => Console.WriteLine($"  Error: {e}\n"));
        }
    }

    /// <summary>
    /// Runs all convenience layer examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("     META-AI CONVENIENCE LAYER - PRACTICAL EXAMPLES");
        Console.WriteLine(new string('=', 60) + "\n");

        try
        {
            await QuickQuestionAnswering();
            Console.WriteLine();

            await CodeGenerationAssistant();
            Console.WriteLine();

            await ResearchAnalysis();
            Console.WriteLine();

            await CompleteWorkflowWithLearning();
            Console.WriteLine();

            await BatchProcessing();
            Console.WriteLine();

            await InteractiveChatSession();
            Console.WriteLine();

            await ComparePresets();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection"))
            {
                Console.WriteLine("\n⚠ Ollama not available - examples require running Ollama service\n");
            }
            else
            {
                Console.WriteLine($"\n✗ Error running examples: {ex.Message}\n");
            }
        }

        Console.WriteLine(new string('=', 60));
        Console.WriteLine("            EXAMPLES COMPLETED");
        Console.WriteLine(new string('=', 60) + "\n");
    }
}
