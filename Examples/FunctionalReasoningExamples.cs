// ==========================================================
// Functional Reasoning Pipeline Demonstrations
// Shows the refactored monadic patterns in action
// ==========================================================

using LangChain.DocumentLoaders;
using LangChainPipeline.Core;
using LangChainPipeline.Core.Kleisli;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Pipeline.Branches;
using LangChainPipeline.Pipeline.Reasoning;
using LangChainPipeline.Tools;

namespace LangChainPipeline.Examples;

/// <summary>
/// Demonstrates the refactored functional programming patterns in the MonadicPipeline.
/// This example showcases immutability, Result-based error handling, and monadic composition.
/// </summary>
public static class FunctionalReasoningExamples
{
    /// <summary>
    /// Demonstrates the immutable PipelineBranch operations.
    /// </summary>
    public static void DemonstrateImmutableBranch()
    {
        Console.WriteLine("\n=== Immutable PipelineBranch Operations ===");
        
        // Create initial branch (immutable)
        var vectorStore = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(Environment.CurrentDirectory);
        var branch = new PipelineBranch("demo-branch", vectorStore, dataSource);
        
        Console.WriteLine($"Initial branch events: {branch.Events.Count}");
        
        // Functional updates return new instances
        var updatedBranch = branch
            .WithReasoning(new Draft("Initial draft content"), "Generate draft", null)
            .WithReasoning(new Critique("Needs improvement in clarity"), "Critique draft", null)
            .WithIngestEvent("demo-documents", new[] { "doc1", "doc2" });
        
        Console.WriteLine($"Original branch events: {branch.Events.Count}"); // Still 0
        Console.WriteLine($"Updated branch events: {updatedBranch.Events.Count}"); // Now 3
        
        // Demonstrate forking
        var forkedBranch = updatedBranch.Fork("forked-branch", new TrackedVectorStore());
        Console.WriteLine($"Forked branch events: {forkedBranch.Events.Count}"); // Still 3
        Console.WriteLine($"Forked branch name: {forkedBranch.Name}"); // "forked-branch"
        
        Console.WriteLine("✅ Immutable operations preserve original state while creating new instances");
    }

    /// <summary>
    /// Demonstrates the functional ToolRegistry operations.
    /// </summary>
    public static void DemonstrateFunctionalToolRegistry()
    {
        Console.WriteLine("\n=== Functional ToolRegistry Operations ===");
        
        // Start with empty registry
        var emptyRegistry = new ToolRegistry();
        Console.WriteLine($"Empty registry tools: {emptyRegistry.Count}");
        
        // Build registry functionally
        var registry = emptyRegistry
            .WithTool(new MathTool())
            .WithFunction("reverse", "Reverses input text", s => new string(s.Reverse().ToArray()))
            .WithFunction("length", "Gets string length", s => s.Length.ToString());
        
        Console.WriteLine($"Empty registry still has: {emptyRegistry.Count} tools"); // Still 0
        Console.WriteLine($"New registry has: {registry.Count} tools"); // Now 3
        
        // Safe tool retrieval using Option monad
        var mathTool = registry.GetTool("math");
        mathTool.Match(
            tool => Console.WriteLine($"✅ Found tool: {tool.Name} - {tool.Description}"),
            () => Console.WriteLine("❌ Tool not found")
        );
        
        var nonExistentTool = registry.GetTool("nonexistent");
        nonExistentTool.Match(
            tool => Console.WriteLine($"Found unexpected tool: {tool.Name}"),
            () => Console.WriteLine("✅ Correctly returned None for non-existent tool")
        );
        
        // Safe schema export
        var schemaResult = registry.SafeExportSchemas();
        schemaResult.Match(
            json => Console.WriteLine($"✅ Schemas exported: {json.Length} characters"),
            error => Console.WriteLine($"❌ Schema export failed: {error}")
        );
        
        Console.WriteLine("✅ Functional registry operations maintain immutability and type safety");
    }

    /// <summary>
    /// Demonstrates Result-based error handling in prompt templates.
    /// </summary>
    public static void DemonstratePromptTemplateResults()
    {
        Console.WriteLine("\n=== Result-Based PromptTemplate Operations ===");
        
        var template = new PromptTemplate("Hello {name}, you have {count} messages.");
        
        // Safe formatting with all required variables
        var goodVars = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["count"] = "3"
        };
        
        var successResult = template.SafeFormat(goodVars);
        successResult.Match(
            formatted => Console.WriteLine($"✅ Success: {formatted}"),
            error => Console.WriteLine($"❌ Error: {error}")
        );
        
        // Safe formatting with missing variables
        var badVars = new Dictionary<string, string>
        {
            ["name"] = "Bob"
            // Missing 'count'
        };
        
        var failureResult = template.SafeFormat(badVars);
        failureResult.Match(
            formatted => Console.WriteLine($"Unexpected success: {formatted}"),
            error => Console.WriteLine($"✅ Expected error: {error}")
        );
        
        // Show required variables
        Console.WriteLine($"Template requires: {string.Join(", ", template.RequiredVariables)}");
        
        Console.WriteLine("✅ Template operations provide explicit error handling");
    }

    /// <summary>
    /// Demonstrates advanced Result monad operations using the new extensions.
    /// </summary>
    public static void DemonstrateResultExtensions()
    {
        Console.WriteLine("\n=== Advanced Result Monad Operations ===");
        
        // Create some test Results
        var successResult = Result<int>.Success(42);
        var failureResult = Result<int>.Failure("Something went wrong");
        
        // Demonstrate chaining with Bind
        var chainResult = successResult
            .Bind(x => x > 0 ? Result<int>.Success(x * 2) : Result<int>.Failure("Negative number"))
            .Map(x => x + 10);
        
        Console.WriteLine($"Chained result: {chainResult}");
        
        // Demonstrate basic operations
        var result1 = Result<int>.Success(10);
        var result2 = Result<int>.Success(20);
        
        // Manual combination since we may not have all extensions working yet
        var combinedResult = result1.Bind(r1 => result2.Map(r2 => (r1, r2)));
        
        combinedResult.Match(
            combined => Console.WriteLine($"✅ Combined: ({combined.Item1}, {combined.Item2})"),
            error => Console.WriteLine($"❌ Combination failed: {error}")
        );
        
        // Demonstrate fallback values using GetValueOrDefault
        var fallbackValue = failureResult.GetValueOrDefault(100);
        Console.WriteLine($"Fallback value: {fallbackValue}");
        
        Console.WriteLine("✅ Result extensions enable powerful monadic composition");
    }

    /// <summary>
    /// Demonstrates basic monadic operations with Results.
    /// </summary>
    public static void DemonstrateBasicResultOperations()
    {
        Console.WriteLine("\n=== Basic Result Operations ===");
        
        // Create test data
        var parseNumber = (string s) => int.TryParse(s, out var n) 
            ? Result<int>.Success(n) 
            : Result<int>.Failure($"'{s}' is not a valid number");
        
        var validatePositive = (int n) => n > 0 
            ? Result<int>.Success(n) 
            : Result<int>.Failure("Number must be positive");
        
        var sqrt = (int n) => Result<double>.Success(Math.Sqrt(n));
        
        // Chain operations using Bind and Map
        var result = parseNumber("16")
            .Bind(validatePositive)
            .Bind(n => sqrt(n).Map(Math.Round));
        
        result.Match(
            value => Console.WriteLine($"✅ Result: {value}"),
            error => Console.WriteLine($"❌ {error}")
        );
        
        // Same operation with invalid input
        var failureResult = parseNumber("not-a-number")
            .Bind(validatePositive)
            .Bind(n => sqrt(n).Map(Math.Round));
        
        failureResult.Match(
            value => Console.WriteLine($"Unexpected: {value}"),
            error => Console.WriteLine($"✅ Expected error: {error}")
        );
        
        Console.WriteLine("✅ Basic monadic operations provide safe computation chains");
    }

    /// <summary>
    /// Demonstrates a complete functional pipeline with proper error handling.
    /// </summary>
    public static async Task DemonstrateFunctionalPipeline()
    {
        Console.WriteLine("\n=== Complete Functional Pipeline ===");
        
        // This would normally use real models, but we'll simulate for demonstration
        Console.WriteLine("Note: This would require actual LLM models to run completely");
        
        // Show how the safe reasoning pipeline would be constructed
        // var pipeline = ReasoningArrows.SafeReasoningPipeline(llm, tools, embed, topic, query);
        
        // Demonstrate the pattern with a mock pipeline
        var mockPipeline = CreateMockSafeReasoningPipeline();
        
        var vectorStore = new TrackedVectorStore();
        var branch = new PipelineBranch("test-branch", vectorStore, DataSource.FromPath(Environment.CurrentDirectory));
        
        var result = await mockPipeline(branch);
        result.Match(
            successBranch => Console.WriteLine($"✅ Pipeline completed with {successBranch.Events.Count} events"),
            error => Console.WriteLine($"❌ Pipeline failed: {error}")
        );
        
        Console.WriteLine("✅ Functional pipelines provide composable, error-safe operations");
    }

    /// <summary>
    /// Creates a mock safe reasoning pipeline for demonstration.
    /// </summary>
    private static KleisliResult<PipelineBranch, PipelineBranch, string> CreateMockSafeReasoningPipeline()
    {
        return async branch =>
        {
            try
            {
                await Task.Delay(10); // Simulate processing
                var updated1 = branch.WithReasoning(new Draft("Mock draft content"), "Draft prompt", null);
                
                await Task.Delay(10); // Simulate processing
                var updated2 = updated1.WithReasoning(new Critique("Mock critique content"), "Critique prompt", null);
                
                await Task.Delay(10); // Simulate processing
                var final = updated2.WithReasoning(new FinalSpec("Mock final content"), "Improve prompt", null);
                
                return Result<PipelineBranch, string>.Success(final);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Pipeline failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Runs all demonstrations.
    /// </summary>
    public static async Task RunAllDemonstrations()
    {
        Console.WriteLine("==========================================");
        Console.WriteLine("FUNCTIONAL PROGRAMMING REFACTORING DEMOS");
        Console.WriteLine("==========================================");
        
        DemonstrateImmutableBranch();
        DemonstrateFunctionalToolRegistry();
        DemonstratePromptTemplateResults();
        DemonstrateResultExtensions();
        DemonstrateBasicResultOperations();
        await DemonstrateFunctionalPipeline();
        
        Console.WriteLine("\n==========================================");
        Console.WriteLine("ALL DEMONSTRATIONS COMPLETED SUCCESSFULLY");
        Console.WriteLine("The MonadicPipeline has been successfully refactored with:");
        Console.WriteLine("✅ Immutable data structures");
        Console.WriteLine("✅ Result-based error handling");
        Console.WriteLine("✅ Functional composition patterns");
        Console.WriteLine("✅ Type-safe operations");
        Console.WriteLine("✅ Monadic pipeline construction");
        Console.WriteLine("==========================================");
    }
}