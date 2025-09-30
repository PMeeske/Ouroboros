using System.Diagnostics;
using LangChainPipeline.Options;
using CommandLine;

namespace LangChainPipeline.Tests;

/// <summary>
/// Comprehensive end-to-end tests for all CLI commands and variations.
/// Tests the CLI interface without requiring actual LLM/embedding models.
/// </summary>
public static class CliEndToEndTests
{
    /// <summary>
    /// Runs all CLI end-to-end tests.
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Running CLI End-to-End Tests ===");
        
        // Command parsing tests
        TestAskCommandParsing();
        TestAskCommandWithRagParsing();
        TestAskCommandWithAgentParsing();
        TestAskCommandWithRouterParsing();
        TestAskCommandWithTemperatureAndTokens();
        TestAskCommandWithDebugAndStream();
        TestAskCommandWithStrictModel();
        TestAskCommandWithJsonTools();
        TestPipelineCommandParsing();
        TestPipelineCommandWithTraceParsing();
        TestPipelineCommandWithDebug();
        TestPipelineCommandWithCustomSource();
        TestDslVariations();
        TestListCommandParsing();
        TestExplainCommandParsing();
        TestTestCommandParsing();
        
        // Error handling tests
        TestInvalidCommandParsing();
        TestMissingRequiredParametersParsing();
        
        // CLI invocation tests (without LLM)
        await TestListCommandExecution();
        await TestExplainCommandExecution();
        await TestExplainCommandWithComplexDsl();
        
        Console.WriteLine("✓ All CLI end-to-end tests passed!\n");
    }

    /// <summary>
    /// Tests parsing of basic 'ask' command.
    /// </summary>
    private static void TestAskCommandParsing()
    {
        Console.WriteLine("Testing 'ask' command parsing...");
        
        var args = new[] { "ask", "-q", "What is functional programming?" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "What is functional programming?")
                throw new Exception("Question not parsed correctly");
            if (opts.Rag != false)
                throw new Exception("RAG should be false by default");
            if (opts.Model != "deepseek-coder:33b")
                throw new Exception("Default model not set correctly");
            if (opts.Embed != "nomic-embed-text")
                throw new Exception("Default embed model not set correctly");
            if (opts.K != 3)
                throw new Exception("Default K not set correctly");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse ask command");
        
        Console.WriteLine("✓ 'ask' command parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'ask' command with RAG enabled.
    /// </summary>
    private static void TestAskCommandWithRagParsing()
    {
        Console.WriteLine("Testing 'ask' command with RAG parsing...");
        
        var args = new[] { "ask", "-q", "Test question", "-r", "--model", "llama3", "-k", "5" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "Test question")
                throw new Exception("Question not parsed correctly");
            if (opts.Rag != true)
                throw new Exception("RAG should be true");
            if (opts.Model != "llama3")
                throw new Exception("Model override not set correctly");
            if (opts.K != 5)
                throw new Exception("K override not set correctly");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse ask command with RAG");
        
        Console.WriteLine("✓ 'ask' command with RAG parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'ask' command with agent mode.
    /// </summary>
    private static void TestAskCommandWithAgentParsing()
    {
        Console.WriteLine("Testing 'ask' command with agent mode parsing...");
        
        // Test with lc agent mode
        var args1 = new[] { "ask", "-q", "Agent test", "--agent", "--agent-mode", "lc", "--agent-max-steps", "10" };
        var result1 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args1);
        
        var parsed1 = false;
        result1.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "Agent test")
                throw new Exception("Question not parsed correctly");
            if (opts.Agent != true)
                throw new Exception("Agent should be true");
            if (opts.AgentMode != "lc")
                throw new Exception("Agent mode not set correctly");
            if (opts.AgentMaxSteps != 10)
                throw new Exception("Agent max steps not set correctly");
            parsed1 = true;
        });
        
        if (!parsed1)
            throw new Exception("Failed to parse ask command with lc agent");
        
        // Test with simple agent mode
        var args2 = new[] { "ask", "-q", "Simple agent test", "--agent", "--agent-mode", "simple" };
        var result2 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args2);
        
        var parsed2 = false;
        result2.WithParsed<AskOptions>(opts =>
        {
            if (opts.AgentMode != "simple")
                throw new Exception("Simple agent mode not set correctly");
            parsed2 = true;
        });
        
        if (!parsed2)
            throw new Exception("Failed to parse ask command with simple agent");
        
        // Test with react agent mode
        var args3 = new[] { "ask", "-q", "React agent test", "--agent", "--agent-mode", "react" };
        var result3 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args3);
        
        var parsed3 = false;
        result3.WithParsed<AskOptions>(opts =>
        {
            if (opts.AgentMode != "react")
                throw new Exception("React agent mode not set correctly");
            parsed3 = true;
        });
        
        if (!parsed3)
            throw new Exception("Failed to parse ask command with react agent");
        
        Console.WriteLine("✓ 'ask' command with agent mode parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'ask' command with temperature and max tokens.
    /// </summary>
    private static void TestAskCommandWithTemperatureAndTokens()
    {
        Console.WriteLine("Testing 'ask' command with temperature and max tokens parsing...");
        
        var args = new[] { 
            "ask", "-q", "Temperature test", 
            "--temperature", "0.9", 
            "--max-tokens", "1024",
            "--timeout-seconds", "120"
        };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "Temperature test")
                throw new Exception("Question not parsed correctly");
            if (Math.Abs(opts.Temperature - 0.9) > 0.001)
                throw new Exception("Temperature not set correctly");
            if (opts.MaxTokens != 1024)
                throw new Exception("Max tokens not set correctly");
            if (opts.TimeoutSeconds != 120)
                throw new Exception("Timeout seconds not set correctly");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse ask command with temperature and tokens");
        
        Console.WriteLine("✓ 'ask' command with temperature and max tokens parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'ask' command with debug and stream options.
    /// </summary>
    private static void TestAskCommandWithDebugAndStream()
    {
        Console.WriteLine("Testing 'ask' command with debug and stream parsing...");
        
        var args = new[] { "ask", "-q", "Debug test", "--debug", "--stream" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "Debug test")
                throw new Exception("Question not parsed correctly");
            if (opts.Debug != true)
                throw new Exception("Debug should be true");
            if (opts.Stream != true)
                throw new Exception("Stream should be true");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse ask command with debug and stream");
        
        Console.WriteLine("✓ 'ask' command with debug and stream parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'ask' command with strict model option.
    /// </summary>
    private static void TestAskCommandWithStrictModel()
    {
        Console.WriteLine("Testing 'ask' command with strict model parsing...");
        
        var args = new[] { "ask", "-q", "Strict model test", "--strict-model" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "Strict model test")
                throw new Exception("Question not parsed correctly");
            if (opts.StrictModel != true)
                throw new Exception("StrictModel should be true");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse ask command with strict model");
        
        Console.WriteLine("✓ 'ask' command with strict model parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'ask' command with JSON tools option.
    /// </summary>
    private static void TestAskCommandWithJsonTools()
    {
        Console.WriteLine("Testing 'ask' command with JSON tools parsing...");
        
        var args = new[] { "ask", "-q", "JSON tools test", "--json-tools" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "JSON tools test")
                throw new Exception("Question not parsed correctly");
            if (opts.JsonTools != true)
                throw new Exception("JsonTools should be true");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse ask command with JSON tools");
        
        Console.WriteLine("✓ 'ask' command with JSON tools parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'pipeline' command with debug option.
    /// </summary>
    private static void TestPipelineCommandWithDebug()
    {
        Console.WriteLine("Testing 'pipeline' command with debug parsing...");
        
        var args = new[] { "pipeline", "-d", "SetPrompt('test') | UseDraft", "--debug" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "SetPrompt('test') | UseDraft")
                throw new Exception("DSL not parsed correctly");
            if (opts.Debug != true)
                throw new Exception("Debug should be true");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse pipeline command with debug");
        
        Console.WriteLine("✓ 'pipeline' command with debug parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'pipeline' command with custom source path.
    /// </summary>
    private static void TestPipelineCommandWithCustomSource()
    {
        Console.WriteLine("Testing 'pipeline' command with custom source parsing...");
        
        var args = new[] { 
            "pipeline", 
            "-d", "UseDir | UseDraft", 
            "--source", "/custom/path",
            "--embed", "custom-embed-model"
        };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "UseDir | UseDraft")
                throw new Exception("DSL not parsed correctly");
            if (opts.Source != "/custom/path")
                throw new Exception("Source path not set correctly");
            if (opts.Embed != "custom-embed-model")
                throw new Exception("Embed model not set correctly");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse pipeline command with custom source");
        
        Console.WriteLine("✓ 'pipeline' command with custom source parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'test' command.
    /// </summary>
    private static void TestTestCommandParsing()
    {
        Console.WriteLine("Testing 'test' command parsing...");
        
        var args1 = new[] { "test", "-s", "cli" };
        var result1 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args1);
        
        var parsed1 = false;
        result1.WithParsed<TestOptions>(opts =>
        {
            if (opts.Suite != "cli")
                throw new Exception("Test suite not parsed correctly");
            parsed1 = true;
        });
        
        if (!parsed1)
            throw new Exception("Failed to parse test command with cli suite");
        
        // Test default suite value
        var args2 = new[] { "test" };
        var result2 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args2);
        
        var parsed2 = false;
        result2.WithParsed<TestOptions>(opts =>
        {
            if (opts.Suite != "all")
                throw new Exception("Test suite default not set correctly");
            parsed2 = true;
        });
        
        if (!parsed2)
            throw new Exception("Failed to parse test command with default suite");
        
        Console.WriteLine("✓ 'test' command parsing test passed");
    }

    /// <summary>
    /// Tests execution of 'explain' command with complex DSL.
    /// </summary>
    private static async Task TestExplainCommandWithComplexDsl()
    {
        Console.WriteLine("Testing 'explain' command with complex DSL execution...");
        
        // Capture console output
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        
        try
        {
            // Execute explain command with complex multi-step DSL
            var opts = new ExplainOptions { 
                Dsl = "SetPrompt('test') | SetTopic('topic') | UseDraft | UseCritique | UseImprove" 
            };
            await RunExplainAsync(opts);
            
            var output = writer.ToString();
            
            // Verify output is not empty
            if (string.IsNullOrWhiteSpace(output))
                throw new Exception("Explain output should not be empty for complex DSL");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        
        Console.WriteLine("✓ 'explain' command with complex DSL execution test passed");
    }

    /// <summary>
    /// Tests parsing of various DSL variations.
    /// </summary>
    private static void TestDslVariations()
    {
        Console.WriteLine("Testing various DSL variations parsing...");
        
        // Test simple single-step DSL
        var args1 = new[] { "pipeline", "-d", "UseDraft" };
        var result1 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args1);
        var parsed1 = false;
        result1.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "UseDraft")
                throw new Exception("Single-step DSL not parsed correctly");
            parsed1 = true;
        });
        if (!parsed1)
            throw new Exception("Failed to parse single-step DSL");
        
        // Test DSL with parameters
        var args2 = new[] { "pipeline", "-d", "SetPrompt('hello world') | SetTopic('AI')" };
        var result2 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args2);
        var parsed2 = false;
        result2.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "SetPrompt('hello world') | SetTopic('AI')")
                throw new Exception("Parameterized DSL not parsed correctly");
            parsed2 = true;
        });
        if (!parsed2)
            throw new Exception("Failed to parse parameterized DSL");
        
        // Test DSL with trace tokens
        var args3 = new[] { "pipeline", "-d", "TraceOn | UseDraft | TraceOff" };
        var result3 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args3);
        var parsed3 = false;
        result3.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "TraceOn | UseDraft | TraceOff")
                throw new Exception("DSL with trace tokens not parsed correctly");
            parsed3 = true;
        });
        if (!parsed3)
            throw new Exception("Failed to parse DSL with trace tokens");
        
        // Test DSL with retrieval steps
        var args4 = new[] { "pipeline", "-d", "SetK('10') | Retrieve | CombineDocs" };
        var result4 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args4);
        var parsed4 = false;
        result4.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "SetK('10') | Retrieve | CombineDocs")
                throw new Exception("DSL with retrieval steps not parsed correctly");
            parsed4 = true;
        });
        if (!parsed4)
            throw new Exception("Failed to parse DSL with retrieval steps");
        
        Console.WriteLine("✓ DSL variations parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'ask' command with router mode.
    /// </summary>
    private static void TestAskCommandWithRouterParsing()
    {
        Console.WriteLine("Testing 'ask' command with router mode parsing...");
        
        var args = new[] { 
            "ask", "-q", "Router test", 
            "--router", "auto", 
            "--coder-model", "codellama",
            "--summarize-model", "llama3",
            "--reason-model", "mistral"
        };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(opts =>
        {
            if (opts.Question != "Router test")
                throw new Exception("Question not parsed correctly");
            if (opts.Router != "auto")
                throw new Exception("Router should be auto");
            if (opts.CoderModel != "codellama")
                throw new Exception("Coder model not set correctly");
            if (opts.SummarizeModel != "llama3")
                throw new Exception("Summarize model not set correctly");
            if (opts.ReasonModel != "mistral")
                throw new Exception("Reason model not set correctly");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse ask command with router");
        
        Console.WriteLine("✓ 'ask' command with router mode parsing test passed");
    }

    /// <summary>
    /// Tests parsing of basic 'pipeline' command.
    /// </summary>
    private static void TestPipelineCommandParsing()
    {
        Console.WriteLine("Testing 'pipeline' command parsing...");
        
        var args = new[] { "pipeline", "-d", "SetPrompt('test') | UseDraft" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "SetPrompt('test') | UseDraft")
                throw new Exception("DSL not parsed correctly");
            if (opts.Model != "deepseek-coder:33b")
                throw new Exception("Default model not set correctly");
            if (opts.Embed != "nomic-embed-text")
                throw new Exception("Default embed model not set correctly");
            if (opts.K != 8)
                throw new Exception("Default K not set correctly");
            if (opts.Trace != false)
                throw new Exception("Trace should be false by default");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse pipeline command");
        
        Console.WriteLine("✓ 'pipeline' command parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'pipeline' command with trace enabled.
    /// </summary>
    private static void TestPipelineCommandWithTraceParsing()
    {
        Console.WriteLine("Testing 'pipeline' command with trace parsing...");
        
        var args = new[] { 
            "pipeline", 
            "-d", "SetPrompt('test') | UseDraft | UseCritique", 
            "-t",
            "--model", "llama3",
            "--source", "/tmp/test",
            "-k", "12"
        };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<PipelineOptions>(opts =>
        {
            if (opts.Dsl != "SetPrompt('test') | UseDraft | UseCritique")
                throw new Exception("DSL not parsed correctly");
            if (opts.Trace != true)
                throw new Exception("Trace should be true");
            if (opts.Model != "llama3")
                throw new Exception("Model override not set correctly");
            if (opts.Source != "/tmp/test")
                throw new Exception("Source path not set correctly");
            if (opts.K != 12)
                throw new Exception("K override not set correctly");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse pipeline command with trace");
        
        Console.WriteLine("✓ 'pipeline' command with trace parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'list' command.
    /// </summary>
    private static void TestListCommandParsing()
    {
        Console.WriteLine("Testing 'list' command parsing...");
        
        var args = new[] { "list" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<ListTokensOptions>(opts =>
        {
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse list command");
        
        Console.WriteLine("✓ 'list' command parsing test passed");
    }

    /// <summary>
    /// Tests parsing of 'explain' command.
    /// </summary>
    private static void TestExplainCommandParsing()
    {
        Console.WriteLine("Testing 'explain' command parsing...");
        
        var args = new[] { "explain", "-d", "SetPrompt('hello') | UseDraft" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<ExplainOptions>(opts =>
        {
            if (opts.Dsl != "SetPrompt('hello') | UseDraft")
                throw new Exception("DSL not parsed correctly");
            parsed = true;
        });
        
        if (!parsed)
            throw new Exception("Failed to parse explain command");
        
        Console.WriteLine("✓ 'explain' command parsing test passed");
    }

    /// <summary>
    /// Tests handling of invalid command.
    /// </summary>
    private static void TestInvalidCommandParsing()
    {
        Console.WriteLine("Testing invalid command parsing...");
        
        var args = new[] { "invalid", "-d", "test" };
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args);
        
        var parsed = false;
        result.WithParsed<AskOptions>(_ => parsed = true)
              .WithParsed<PipelineOptions>(_ => parsed = true)
              .WithParsed<ListTokensOptions>(_ => parsed = true)
              .WithParsed<ExplainOptions>(_ => parsed = true);
        
        if (parsed)
            throw new Exception("Invalid command should not parse successfully");
        
        Console.WriteLine("✓ Invalid command parsing test passed");
    }

    /// <summary>
    /// Tests handling of missing required parameters.
    /// </summary>
    private static void TestMissingRequiredParametersParsing()
    {
        Console.WriteLine("Testing missing required parameters parsing...");
        
        // Ask without question
        var args1 = new[] { "ask" };
        var result1 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args1);
        
        var parsed1 = false;
        result1.WithParsed<AskOptions>(_ => parsed1 = true);
        
        if (parsed1)
            throw new Exception("Ask command without question should not parse successfully");
        
        // Pipeline without DSL
        var args2 = new[] { "pipeline" };
        var result2 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args2);
        
        var parsed2 = false;
        result2.WithParsed<PipelineOptions>(_ => parsed2 = true);
        
        if (parsed2)
            throw new Exception("Pipeline command without DSL should not parse successfully");
        
        // Explain without DSL
        var args3 = new[] { "explain" };
        var result3 = Parser.Default.ParseArguments<AskOptions, PipelineOptions, ListTokensOptions, ExplainOptions, TestOptions>(args3);
        
        var parsed3 = false;
        result3.WithParsed<ExplainOptions>(_ => parsed3 = true);
        
        if (parsed3)
            throw new Exception("Explain command without DSL should not parse successfully");
        
        Console.WriteLine("✓ Missing required parameters parsing test passed");
    }

    /// <summary>
    /// Tests execution of 'list' command.
    /// </summary>
    private static async Task TestListCommandExecution()
    {
        Console.WriteLine("Testing 'list' command execution...");
        
        // Capture console output
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        
        try
        {
            // Execute list command
            await RunListTokensAsync();
            
            var output = writer.ToString();
            
            // Verify output contains expected tokens
            if (!output.Contains("Available token groups:"))
                throw new Exception("List output should contain 'Available token groups:'");
            
            if (!output.Contains("UseDraft"))
                throw new Exception("List output should contain 'UseDraft'");
            
            if (!output.Contains("UseCritique"))
                throw new Exception("List output should contain 'UseCritique'");
            
            if (!output.Contains("SetPrompt"))
                throw new Exception("List output should contain 'SetPrompt'");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        
        Console.WriteLine("✓ 'list' command execution test passed");
    }

    /// <summary>
    /// Tests execution of 'explain' command.
    /// </summary>
    private static async Task TestExplainCommandExecution()
    {
        Console.WriteLine("Testing 'explain' command execution...");
        
        // Capture console output
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        
        try
        {
            // Execute explain command with simple DSL
            var opts = new ExplainOptions { Dsl = "SetPrompt('test') | UseDraft" };
            await RunExplainAsync(opts);
            
            var output = writer.ToString();
            
            // Verify output is not empty
            if (string.IsNullOrWhiteSpace(output))
                throw new Exception("Explain output should not be empty");
            
            // Verify output contains DSL information
            if (!output.Contains("SetPrompt") && !output.Contains("UseDraft"))
                throw new Exception("Explain output should contain DSL step information");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        
        Console.WriteLine("✓ 'explain' command execution test passed");
    }

    // Helper methods copied from Program.cs for testing
    private static Task RunListTokensAsync()
    {
        Console.WriteLine("Available token groups:");
        foreach (var (method, names) in LangChainPipeline.CLI.StepRegistry.GetTokenGroups())
        {
            Console.WriteLine($"- {method.DeclaringType?.Name}.{method.Name}(): {string.Join(", ", names)}");
        }
        return Task.CompletedTask;
    }

    private static Task RunExplainAsync(ExplainOptions o)
    {
        Console.WriteLine(LangChainPipeline.CLI.PipelineDsl.Explain(o.Dsl));
        return Task.CompletedTask;
    }
}
