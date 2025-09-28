using LangChainPipeline.Tools;

namespace LangChainPipeline.Examples;

/// <summary>
/// Demonstrates the DSL parser functionality for tool calls.
/// Shows the improvement from basic string splitting to sophisticated parsing.
/// </summary>
public static class DslParserExamples
{
    public static async Task RunDslParserExamples()
    {
        Console.WriteLine("=== DSL PARSER FUNCTIONALITY DEMONSTRATION ===");
        
        await DemonstrateBasicToolCalls();
        await DemonstrateJsonToolCalls();
        await DemonstrateMathToolCalls();
        await DemonstrateComplexScenarios();
        
        Console.WriteLine("=== All DSL Parser Examples Complete ===");
    }

    private static async Task DemonstrateBasicToolCalls()
    {
        Console.WriteLine("\n--- Basic Tool Call Parsing ---");

        // Create a basic registry with our tools
        var toolRegistry = new ToolRegistry()
            .WithTool(new MathTool());

        // Simulate text that contains tool calls
        string responseText = @"Let me calculate that for you.
[TOOL:math 2+2*5]
The result should be 12.";

        Console.WriteLine($"Input text: {responseText.Replace("\n", "\\n")}");

        var toolCalls = ToolCallParser.ParseToolCalls(responseText);
        Console.WriteLine($"Parsed {toolCalls.Count} tool call(s):");
        
        foreach (var call in toolCalls)
        {
            Console.WriteLine($"  Tool: {call.Name}");
            Console.WriteLine($"  Args: '{call.Arguments}'");
            
            // Execute the tool call
            var tool = toolRegistry.Get(call.Name);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(call.Arguments);
                result.Match(
                    success => Console.WriteLine($"  Result: {success}"),
                    error => Console.WriteLine($"  Error: {error}")
                );
            }
        }
    }

    private static async Task DemonstrateJsonToolCalls()
    {
        Console.WriteLine("\n--- JSON Tool Call Parsing ---");

        // Create mock vector store and embedding model for the search tool
        var mockStore = new TrackedVectorStore();
        var embeddingModel = CreateMockEmbeddingModel();
        
        // Add some test data
        await mockStore.AddAsync(new[]
        {
            new LangChain.Databases.Vector
            {
                Id = "1",
                Text = "Information about monadic pipelines and functional programming",
                Embedding = new float[8] // Mock embedding
            },
            new LangChain.Databases.Vector
            {
                Id = "2", 
                Text = "Details about DSL parsing and tool invocation",
                Embedding = new float[8]
            }
        });

        var toolRegistry = new ToolRegistry()
            .WithTool(new RetrievalTool(mockStore, embeddingModel));

        // Simulate complex JSON tool calls that would fail with simple parsing
        string responseText = @"I'll search for that information.
[TOOL:search {""q"":""monadic pipelines"", ""k"":2}]
Let me also search for something more specific.
[TOOL:search {""q"":""DSL parsing techniques"", ""k"":1}]";

        Console.WriteLine($"Input text with JSON: {responseText.Replace("\n", "\\n")}");

        var toolCalls = ToolCallParser.ParseToolCalls(responseText);
        Console.WriteLine($"Parsed {toolCalls.Count} tool call(s):");
        
        foreach (var call in toolCalls)
        {
            Console.WriteLine($"  Tool: {call.Name}");
            Console.WriteLine($"  Args: {call.Arguments}");
            
            // Validate JSON format
            var jsonValidation = ToolCallParser.ValidateJsonArguments(call.Arguments);
            jsonValidation.Match(
                success => Console.WriteLine($"  JSON validation: ✓ Valid"),
                error => Console.WriteLine($"  JSON validation: ✗ {error}")
            );
            
            // Execute the tool call
            var tool = toolRegistry.Get(call.Name);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(call.Arguments);
                result.Match(
                    success => Console.WriteLine($"  Result: {success[..Math.Min(60, success.Length)]}..."),
                    error => Console.WriteLine($"  Error: {error}")
                );
            }
        }
    }

    private static async Task DemonstrateMathToolCalls()
    {
        Console.WriteLine("\n--- Math Expression Tool Calls ---");

        var toolRegistry = new ToolRegistry()
            .WithTool(new MathTool());

        // Complex math expressions that would be broken by simple space splitting
        string responseText = @"Let me calculate these complex expressions:
[TOOL:math (10 - 5) * 2 + 3]
[TOOL:math ((25 / 5) + 3) * 2]
[TOOL:math 2 * (3 + 4 * (5 - 2))]";

        Console.WriteLine($"Input text with complex math: {responseText.Replace("\n", "\\n")}");

        var toolCalls = ToolCallParser.ParseToolCalls(responseText);
        Console.WriteLine($"Parsed {toolCalls.Count} tool call(s):");
        
        foreach (var call in toolCalls)
        {
            Console.WriteLine($"  Tool: {call.Name}");
            Console.WriteLine($"  Expression: '{call.Arguments}'");
            
            // Check if it's detected as math
            bool isMath = ToolCallParser.IsMathExpression(call.Arguments);
            Console.WriteLine($"  Detected as math: {(isMath ? "✓" : "✗")}");
            
            // Execute the calculation
            var tool = toolRegistry.Get(call.Name);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(call.Arguments);
                result.Match(
                    success => Console.WriteLine($"  Result: {success}"),
                    error => Console.WriteLine($"  Error: {error}")
                );
            }
        }
    }

    private static async Task DemonstrateComplexScenarios()
    {
        Console.WriteLine("\n--- Complex Mixed Scenarios ---");

        var mockStore = new TrackedVectorStore();
        var embeddingModel = CreateMockEmbeddingModel();
        
        var toolRegistry = new ToolRegistry()
            .WithTool(new MathTool())
            .WithTool(new RetrievalTool(mockStore, embeddingModel));

        // Mix of different tool call types in one response
        string responseText = @"I'll help you with multiple calculations and searches.
First, let me calculate: [TOOL:math (15 + 25) / 2]
Now I'll search for information: [TOOL:search {""q"":""functional programming benefits"", ""k"":3}]
Let me also compute this: [TOOL:math 2 * 3.14 * 5]
And search for more: [TOOL:search {""q"":""monadic error handling"", ""k"":2}]";

        Console.WriteLine($"Mixed tool calls: {responseText.Replace("\n", "\\n")}");

        var toolCalls = ToolCallParser.ParseToolCalls(responseText);
        Console.WriteLine($"Parsed {toolCalls.Count} tool call(s):");
        
        foreach (var call in toolCalls)
        {
            Console.WriteLine($"\n  Tool: {call.Name}");
            Console.WriteLine($"  Args: {call.Arguments}");
            
            // Analyze argument type
            if (ToolCallParser.IsJsonArguments(call.Arguments))
            {
                Console.WriteLine($"  Type: JSON arguments");
                var validation = ToolCallParser.ValidateJsonArguments(call.Arguments);
                Console.WriteLine($"  Valid: {(validation.IsSuccess ? "✓" : "✗")}");
            }
            else if (ToolCallParser.IsMathExpression(call.Arguments))
            {
                Console.WriteLine($"  Type: Math expression");
            }
            else
            {
                Console.WriteLine($"  Type: Plain text");
            }
            
            // Execute the tool
            var tool = toolRegistry.Get(call.Name);
            if (tool != null)
            {
                var result = await tool.InvokeAsync(call.Arguments);
                result.Match(
                    success => Console.WriteLine($"  Result: {success}"),
                    error => Console.WriteLine($"  Error: {error}")
                );
            }
            else
            {
                Console.WriteLine($"  Error: Tool '{call.Name}' not found");
            }
        }
    }

    /// <summary>
    /// Creates a mock embedding model for testing purposes.
    /// </summary>
    private static LangChain.Providers.Ollama.OllamaEmbeddingModel CreateMockEmbeddingModel()
    {
        // Create a mock embedding model - in real scenarios this would be properly configured
        try
        {
            var provider = new LangChain.Providers.Ollama.OllamaProvider();
            return new LangChain.Providers.Ollama.OllamaEmbeddingModel(provider, "mock-model");
        }
        catch
        {
            // If Ollama model creation fails, return null and handle gracefully
            return null!;
        }
    }
}