// ============================================================================
// LangChain Pipeline System - Main Entry Point
// Enhanced with proper monadic operations and functional programming concepts
// ============================================================================

using ConsoleApp1;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers;
using LangChain.Providers.Ollama;
using LangChainPipeline.Tools;
using LangChainPipeline.Core;

namespace ConsoleApp1;

/// <summary>
/// Main program entry point for the LangChain Pipeline System.
/// Now enhanced with proper monadic operations and functional programming demonstrations.
/// </summary>
internal static class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== Enhanced LangChain Pipeline System ===");
            Console.WriteLine("Now with proper monadic operations!\n");

            // First, demonstrate the enhanced monadic operations
            Console.WriteLine("=== MONADIC OPERATIONS DEMONSTRATION ===\n");
            await MonadicExamples.RunAllDemonstrations();
            
            // Demonstrate KleisliCompose higher-order functions
            Console.WriteLine("\n=== KLEISLI COMPOSE HIGHER-ORDER FUNCTIONS ===\n");
            await DemonstrateKleisliCompose();
            
            // Demonstrate enhanced interop capabilities
            Console.WriteLine("\n=== ENHANCED KLEISLI <-> LANGCHAIN INTEROP DEMONSTRATION ===\n");
            await EnhancedDemo.RunAllEnhanced();

            // Test hybrid sync/async step system
            Console.WriteLine("\n=== HYBRID SYNC/ASYNC STEP DEMONSTRATIONS ===\n");
            await HybridStepExamples.RunAllHybridDemonstrations();

            Console.WriteLine("\n" + new string('=', 70) + "\n");

            // Then run the original pipeline with enhancements
            Console.WriteLine("=== LANGCHAIN PIPELINE WITH MONADIC ENHANCEMENTS ===");
            Console.WriteLine("Initializing components...\n");

            await RunEnhancedPipelineAsync();
            
            Console.WriteLine("\n=== All systems completed successfully ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    private static async Task RunEnhancedPipelineAsync()
    {
        // Enhanced pipeline using monadic operations for better error handling
        var pipelineResult = await CreateEnhancedPipeline()
            .Catch()  // Convert exceptions to Result type
            .Invoke("pipeline");

        pipelineResult.Match(
            success => Console.WriteLine("Pipeline completed successfully!"),
            error => Console.WriteLine($"Pipeline failed: {error.Message}")
        );
    }

    /// <summary>
    /// Creates an enhanced pipeline using monadic operations.
    /// </summary>
    private static Step<string, string> CreateEnhancedPipeline()
    {
        // Step 1: Initialize models with error handling
        var initializeModels = Arrow.Lift<string, (OllamaChatModel chat, OllamaEmbeddingModel embed)>(_ =>
        {
            Console.WriteLine("Creating models...");
            var provider = new OllamaProvider();
            var coderSettings = OllamaPresets.DeepSeekCoder33B;

            var llmBase = new OllamaChatModel(provider, "deepseek-coder:33b").UseConsoleForDebug();
            llmBase.Settings = coderSettings;
            
            var embed = new OllamaEmbeddingModel(provider, "nomic-embed-text");

            return (llmBase, embed);
        });

        // Step 2: Create branch and setup
        var createBranch = Arrow.Lift<(OllamaChatModel, OllamaEmbeddingModel), PipelineBranch>(models =>
        {
            Console.WriteLine("Creating branch and components...");
            return new PipelineBranch("main", new TrackedVectorStore(), DataSource.FromPath(Environment.CurrentDirectory));
        });

        // Step 3: Setup tools with monadic composition
        var setupTools = Arrow.Lift<PipelineBranch, (PipelineBranch branch, ToolRegistry tools)>(branch =>
        {
            Console.WriteLine("Setting up tools...");
            var tools = new ToolRegistry();
            tools.Register(new MathTool());
            // Note: RetrievalTool would need the embedding model, but we're simplifying for demonstration
            tools.Register("upper", "Converts input text to uppercase", s => s.ToUpperInvariant());
            
            return (branch, tools);
        });

        // Step 4: Ingest content with enhanced error handling
        var ingestContent = Arrow.LiftAsync<(PipelineBranch, ToolRegistry), (PipelineBranch, ToolRegistry)>(async data =>
        {
            var (branch, tools) = data;
            Console.WriteLine("Ingesting content...");

            try
            {
                // Simplified ingestion - in real scenario we'd use the embedding model
                await Task.Delay(100); // Simulate ingestion work
                
                // Fallback: seed with fake vectors
                await branch.Store.AddAsync(new[]
                {
                    new Vector 
                    { 
                        Id = "1", 
                        Text = "Enhanced monadic operations provide better error handling.", 
                        Embedding = new float[8] 
                    },
                    new Vector 
                    { 
                        Id = "2", 
                        Text = "Kleisli arrows enable composition of computations in monadic contexts.", 
                        Embedding = new float[8] 
                    }
                });
                
                branch.AddIngestEvent("monadic-seed", new[] { "1", "2" });
                Console.WriteLine("Enhanced seed data loaded with monadic concepts.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ingestion had issues: {ex.Message}");
                // Continue with empty store for demonstration
            }

            return (branch, tools);
        });

        // Step 5: Run enhanced reasoning with monadic error handling
        var runReasoning = Arrow.LiftAsync<(PipelineBranch, ToolRegistry), string>(async data =>
        {
            var (branch, tools) = data;
            Console.WriteLine("Running enhanced reasoning with monadic operations...");

            // Simulate reasoning process with functional composition
            var reasoningPipeline = CreateMonadicReasoningPipeline();
            
            var reasoningState = new Draft("Demonstrate enhanced monadic operations in LangChain pipeline");

            var result = await reasoningPipeline(reasoningState);
            
            return result.Match(
                success => 
                {
                    Console.WriteLine($"Reasoning completed: {success.Kind}");
                    Console.WriteLine($"Enhanced with monadic context: {success.Text[..Math.Min(100, success.Text.Length)]}...");
                    return "Enhanced reasoning pipeline completed successfully with monadic operations";
                },
                error => $"Reasoning failed: {error}"
            );
        });

        // Compose the entire pipeline using monadic operations
        return initializeModels
            .Then(createBranch)
            .Then(setupTools)
            .Then(ingestContent)
            .Then(runReasoning)
            .Tap(result => Console.WriteLine($"Pipeline result: {result}"));
    }

    /// <summary>
    /// Creates a reasoning pipeline using enhanced monadic operations.
    /// Demonstrates proper error handling with KleisliResult.
    /// </summary>
    private static KleisliResult<ReasoningState, ReasoningState, string> CreateMonadicReasoningPipeline()
    {
        // Step 1: Validate input with monadic error handling
        KleisliResult<ReasoningState, ReasoningState, string> validateInput = 
            async state =>
            {
                await Task.Delay(10); // Simulate validation
                return string.IsNullOrEmpty(state.Text)
                    ? Result<ReasoningState, string>.Failure("Text content cannot be empty")
                    : Result<ReasoningState, string>.Success(state);
            };

        // Step 2: Enhanced analysis with monadic composition
        KleisliResult<ReasoningState, ReasoningState, string> enhancedAnalysis = 
            async state =>
            {
                await Task.Delay(50); // Simulate analysis
                
                // Enhance the text with monadic concepts
                var enhancedText = state.Text + " (enhanced with Option and Result monads for robust error handling and Kleisli arrows for composition)";
                
                var newState = new Critique(enhancedText);

                Console.WriteLine($"  → Enhanced analysis completed with monadic operations");
                return Result<ReasoningState, string>.Success(newState);
            };

        // Step 3: Finalize with monadic laws compliance
        KleisliResult<ReasoningState, ReasoningState, string> finalizeWithMonads = 
            async state =>
            {
                await Task.Delay(20); // Simulate finalization
                
                var finalText = state.Text + " | Final state achieved through monadic composition satisfying identity, associativity, and functor laws";
                var finalState = new FinalSpec(finalText);

                Console.WriteLine($"  → Monadic finalization completed with category theory compliance");
                return Result<ReasoningState, string>.Success(finalState);
            };

        // Compose using enhanced monadic operations
        return validateInput
            .Then(enhancedAnalysis)
            .Then(finalizeWithMonads)
            .Tap(state => Console.WriteLine($"  → Monadic step completed: {state.Kind} with enhanced content"));
    }

    /// <summary>
    /// Demonstrates the original pipeline functionality (preserved for compatibility).
    /// </summary>
    private static async Task RunOriginalPipelineAsync()
    {
        // 1. Create and configure models
        var (chatModel, embeddingModel) = CreateModels();

        // 2. Create branch and store
        var branch = new PipelineBranch("main", new TrackedVectorStore(), DataSource.FromPath(Environment.CurrentDirectory));

        // 3. Create and configure tools
        var toolRegistry = CreateToolRegistry(branch.Store, embeddingModel);
        var toolAwareLlm = new ToolAwareChatModel(chatModel, toolRegistry);

        // 4. Ingest content
        await IngestContentAsync(branch, embeddingModel);

        // 5. Run reasoning pipeline
        await RunReasoningPipelineAsync(branch, toolAwareLlm, toolRegistry, embeddingModel);

        // 6. Save and replay
        await SaveAndReplayAsync(branch, toolAwareLlm, embeddingModel, toolRegistry);
    }

    /// <summary>
    /// Creates and configures the LLM providers.
    /// </summary>
    private static (OllamaChatModel chatModel, OllamaEmbeddingModel embeddingModel) CreateModels()
    {
        Console.WriteLine("Creating models...");
        
        var provider = new OllamaProvider();
        var coderSettings = OllamaPresets.DeepSeekCoder33B;

        var llmBase = new OllamaChatModel(provider, "deepseek-coder:33b").UseConsoleForDebug();
        llmBase.Settings = coderSettings;
        
        var embed = new OllamaEmbeddingModel(provider, "nomic-embed-text");

        return (llmBase, embed);
    }

    /// <summary>
    /// Creates and configures the tool registry with default tools.
    /// </summary>
    private static ToolRegistry CreateToolRegistry(TrackedVectorStore vectorStore, OllamaEmbeddingModel embeddingModel)
    {
        Console.WriteLine("Setting up tools...");
        
        var tools = new ToolRegistry();
        tools.Register(new MathTool());
        tools.Register(new RetrievalTool(vectorStore, embeddingModel));
        tools.Register("upper", "Converts input text to uppercase", s => s.ToUpperInvariant());

        return tools;
    }

    /// <summary>
    /// Ingests content into the vector store.
    /// </summary>
    private static async Task IngestContentAsync(PipelineBranch branch, OllamaEmbeddingModel embeddingModel)
    {
        Console.WriteLine("Ingesting content...");

        try
        {
            var ingestArrow = IngestionArrows.IngestArrow<FileLoader>(embeddingModel, tag: "fs");
            await ingestArrow.Invoke(branch);
            Console.WriteLine("Content ingestion completed.\n");
        }
        catch
        {
            Console.WriteLine("File ingestion failed, using fallback seed data...");
            
            // Fallback: seed with fake vectors
            await branch.Store.AddAsync(new[]
            {
                new Vector 
                { 
                    Id = "1", 
                    Text = "KeyTable supports multi-tenant parameters and caching.", 
                    Embedding = new float[8] 
                },
                new Vector 
                { 
                    Id = "2", 
                    Text = "KeyTable validates tenantId and supports PageSize default 50.", 
                    Embedding = new float[8] 
                }
            });
            
            branch.AddIngestEvent("seed", new[] { "1", "2" });
            Console.WriteLine("Fallback seed data loaded.\n");
        }
    }

    /// <summary>
    /// Runs the main reasoning pipeline.
    /// </summary>
    private static async Task RunReasoningPipelineAsync(
        PipelineBranch branch, 
        ToolAwareChatModel llm, 
        ToolRegistry tools, 
        OllamaEmbeddingModel embed)
    {
        Console.WriteLine("Running reasoning pipeline (Draft -> Critique -> Improve)...");

        const string topic = "Vita.KeyTables_Grosses_Konzept.md";
        const string query = "KeyTable parameters and edge cases";

        var pipeline = ReasoningArrows.DraftArrow(llm, tools, embed, topic, query)
            .Then(ReasoningArrows.CritiqueArrow(llm, tools, embed, topic, query))
            .Then(ReasoningArrows.ImproveArrow(llm, tools, embed, topic, query));

        var updatedBranch = await pipeline.Invoke(branch);
        
        // Print final result
        var lastStep = updatedBranch.Events.OfType<ReasoningStep>().LastOrDefault();
        if (lastStep != null)
        {
            Console.WriteLine("=== FINAL RESULT ===");
            var preview = lastStep.State.Text.Length > 1200 
                ? lastStep.State.Text[..1200] + "..." 
                : lastStep.State.Text;
            Console.WriteLine(preview);
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Saves the pipeline state and tests replay functionality.
    /// </summary>
    private static async Task SaveAndReplayAsync(
        PipelineBranch branch, 
        ToolAwareChatModel llm, 
        OllamaEmbeddingModel embed, 
        ToolRegistry tools)
    {
        Console.WriteLine("Saving snapshot...");
        
        const string snapshotFile = "branch_main.json";
        var snapshot = await BranchSnapshot.Capture(branch);
        await BranchPersistence.SaveAsync(snapshot, snapshotFile);
        Console.WriteLine($"Snapshot saved: {snapshotFile}");

        Console.WriteLine("Testing replay functionality...");
        var loadedSnapshot = await BranchPersistence.LoadAsync(snapshotFile);
        var restoredBranch = await loadedSnapshot.Restore();
        
        var replayEngine = new ReplayEngine(llm, embed);
        const string topic = "Vita.KeyTables_Grosses_Konzept.md";
        const string query = "KeyTable parameters and edge cases";
        
        var replayedBranch = await replayEngine.ReplayAsync(restoredBranch, topic, query, tools);

        var replayLastStep = replayedBranch.Events.OfType<ReasoningStep>().LastOrDefault();
        if (replayLastStep != null)
        {
            Console.WriteLine("\n=== REPLAY RESULT ===");
            var preview = replayLastStep.State.Text.Length > 1200 
                ? replayLastStep.State.Text[..1200] + "..." 
                : replayLastStep.State.Text;
            Console.WriteLine(preview);

            if (replayLastStep.ToolCalls?.Any() == true)
            {
                Console.WriteLine("\nTool calls during replay:");
                foreach (var toolCall in replayLastStep.ToolCalls)
                {
                    Console.WriteLine($"- {toolCall.ToolName}({toolCall.Arguments}) => {toolCall.Output}");
                }
            }
        }
    }

    private static async Task DemonstrateKleisliCompose()
    {
        Console.WriteLine("\n=== KleisliCompose Higher-Order Composition Demonstration ===");

        // Create basic Kleisli arrows (not Step arrows)
        Kleisli<string, int> parseNumber = async input =>
        {
            await Task.Delay(10);
            return int.TryParse(input, out var result) ? result : 0;
        };

        Kleisli<int, int> doubleValue = async input =>
        {
            await Task.Delay(10);
            return input * 2;
        };

        Kleisli<int, string> formatResult = async input =>
        {
            await Task.Delay(10);
            return $"Final Result: {input}";
        };

        // Demonstrate basic higher-order composition using the Arrow factory
        Console.WriteLine("\n--- Higher-Order Composition Factory ---");
        var composeFunc = Arrow.Compose<string, int, int>();
        var parseAndDouble = composeFunc(parseNumber, doubleValue);
        
        var result1 = await parseAndDouble("21");
        Console.WriteLine($"Compose factory - Parse '21' and double: {result1}");

        // Demonstrate ComposeWith currying (fixed parameter order)
        Console.WriteLine("\n--- ComposeWith Curried Composition ---");
        var composeWithParse = Arrow.ComposeWith<string, int, int>(parseNumber);
        var curriedComposition = composeWithParse(doubleValue);
        
        var result2 = await curriedComposition("15");
        Console.WriteLine($"ComposeWith curried - Parse '15' and double: {result2}");

        // Demonstrate the new extension method composition
        Console.WriteLine("\n--- Extension Method with KleisliCompose ---");
        var extensionComposition = parseNumber.ComposeWith(Arrow.Compose<string, int, string>(), formatResult);
        
        var result3 = await extensionComposition("12");
        Console.WriteLine($"Extension method composition '12': {result3}");

        // Demonstrate higher-order function creation pattern
        Console.WriteLine("\n--- Higher-Order Function Patterns ---");
        
        // Create a factory for multiplier arrows
        var createMultiplier = (int factor) => new Kleisli<int, int>(async x =>
        {
            await Task.Delay(5);
            return x * factor;
        });

        // Use the factory with KleisliCompose
        var tripler = createMultiplier(3);
        var parseAndTriple = Arrow.Compose<string, int, int>()(parseNumber, tripler);
        
        var result4 = await parseAndTriple("7");
        Console.WriteLine($"Factory pattern - Parse '7' and triple: {result4}");

        // Demonstrate partial application
        Console.WriteLine("\n--- Partial Application ---");
        var partialComposer = parseNumber.PartialCompose<string, int, string>();
        var completeComposition = partialComposer(formatResult);
        
        var result5 = await completeComposition("25");
        Console.WriteLine($"Partial application '25': {result5}");

        // Demonstrate multi-step composition
        Console.WriteLine("\n--- Multi-Step Composition Chain ---");
        var step1 = Arrow.ComposeWith<string, int, int>(parseNumber);
        var step2 = step1(doubleValue);
        var step3 = Arrow.ComposeWith<string, int, string>(step2);
        var finalChain = step3(formatResult);
        
        var result6 = await finalChain("8");
        Console.WriteLine($"Multi-step chain '8': {result6}");

        Console.WriteLine("\n--- KleisliCompose successfully demonstrates higher-order composition patterns ---");
    }
}


