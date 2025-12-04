using System.Text;
using LangChainPipeline.CLI;
using LangChainPipeline.Tools.MeTTa;
using MonadicPipeline.CLI;

namespace LangChainPipeline.CLI;

public static class MeTTaCliSteps
{
    [PipelineToken("MottoInit", "InitMotto")]
    public static Step<CliPipelineState, CliPipelineState> MottoInit(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null)
            {
                s.MeTTaEngine = new SubprocessMeTTaEngine();
            }

            var initStep = new MottoSteps.MottoInitializeStep(s.MeTTaEngine);
            var result = await initStep.ExecuteAsync(Unit.Value);

            result.Match(
                success => 
                {
                    if (s.Trace) Console.WriteLine("[metta] Motto initialized");
                },
                failure => 
                {
                    Console.WriteLine($"[metta] Failed to initialize Motto: {failure}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:init:{failure}", Array.Empty<string>());
                }
            );

            return s;
        };

    [PipelineToken("MottoChat", "MeTTaChat")]
    public static Step<CliPipelineState, CliPipelineState> MottoChat(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null)
            {
                Console.WriteLine("[metta] Engine not initialized. Call MottoInit first.");
                return s;
            }

            // Use args as message, or fallback to s.Query/s.Prompt
            string message = ParseString(args);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Prompt;
            }

            if (string.IsNullOrWhiteSpace(message)) return s;

            var chatStep = new MottoSteps.MottoChatStep(s.MeTTaEngine);
            var result = await chatStep.ExecuteAsync(message);

            result.Match(
                success => 
                {
                    s.Output = success;
                    if (s.Trace) Console.WriteLine($"[metta] Chat response: {success}");
                    // Record as reasoning?
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(success), message, new List<ToolExecution>());
                },
                failure => 
                {
                    Console.WriteLine($"[metta] Chat failed: {failure}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:chat:{failure}", Array.Empty<string>());
                }
            );

            return s;
        };

    [PipelineToken("MottoOllama", "OllamaAgent")]
    public static Step<CliPipelineState, CliPipelineState> MottoOllama(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null)
            {
                s.MeTTaEngine = new SubprocessMeTTaEngine();
                await new MottoSteps.MottoInitializeStep(s.MeTTaEngine).ExecuteAsync(Unit.Value);
            }

            string model = "llama3";
            string message = ParseString(args);
            string? script = null;
            
            // Parse args: "model=phi3|msg=Hello|script=ollama_agent.msa"
            // Also handle single: "msg=Hello" or "Hello"
            if (message.Contains("|"))
            {
                foreach (var part in message.Split('|'))
                {
                    if (part.StartsWith("model=")) model = part.Substring(6);
                    else if (part.StartsWith("msg=")) message = part.Substring(4);
                    else if (part.StartsWith("script=")) script = part.Substring(7);
                }
            }
            else if (message.StartsWith("msg="))
            {
                message = message.Substring(4);
            }
            else if (message.StartsWith("model="))
            {
                model = message.Substring(6);
                message = string.Empty;
            }
            
            if (string.IsNullOrWhiteSpace(message)) message = s.Query;
            
            // If we have context from previous pipeline steps (like UseDir), include it
            if (!string.IsNullOrWhiteSpace(s.Context))
            {
                message = $"Context:\n{s.Context}\n\nQuestion: {message}";
            }

            // The MeTTa REPL already imports motto and ollama_agent on startup

            string query;
            if (!string.IsNullOrEmpty(script))
            {
                // Use the script runner pattern
                // !((ollama-agent "model") (Script "script.msa") (user "msg"))
                query = $"!((ollama-agent \"{model}\") (Script \"{script}\") (user \"{message.Replace("\"", "\\\"")}\"))";
                if (s.Trace) Console.WriteLine($"[metta] Calling Ollama ({model}) with script {script}: {message}");
            }
            else
            {
                // Direct call
                // !((ollama-agent "model") (user "msg"))
                query = $"!((ollama-agent \"{model}\") (user \"{message.Replace("\"", "\\\"")}\"))";
                if (s.Trace) Console.WriteLine($"[metta] Calling Ollama ({model}): {message}");
            }
            
            var result = await s.MeTTaEngine.ExecuteQueryAsync(query);
            
            result.Match(
                success => 
                {
                    s.Output = success;
                    if (s.Trace) Console.WriteLine($"[metta] Ollama response: {success}");
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(success), message, new List<ToolExecution>());
                },
                failure => 
                {
                    Console.WriteLine($"[metta] Ollama call failed: {failure}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:ollama:{failure}", Array.Empty<string>());
                }
            );

            return s;
        };

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        // Simple quote stripping if needed, similar to CliSteps.ParseString
        if (arg.StartsWith("'") && arg.EndsWith("'") && arg.Length >= 2) return arg[1..^1];
        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2) return arg[1..^1];
        return arg;
    }

    /// <summary>
    /// Read a file and set its content as the pipeline context.
    /// Usage: ReadFile('path/to/file.cs')
    /// </summary>
    [PipelineToken("ReadFile", "LoadFile")]
    public static Step<CliPipelineState, CliPipelineState> ReadFile(string? args = null)
        => s =>
        {
            string path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path))
            {
                if (s.Trace) Console.WriteLine("[file] No path provided");
                return Task.FromResult(s);
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"[file] File not found: {fullPath}");
                    return Task.FromResult(s);
                }

                string content = File.ReadAllText(fullPath);
                string fileName = Path.GetFileName(fullPath);
                
                // Set as context with file info header
                s.Context = $"=== File: {fileName} ===\n{content}";
                
                if (s.Trace) Console.WriteLine($"[file] Loaded {fileName} ({content.Length} chars)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[file] Error reading file: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Applies self-critique to MeTTa-based reasoning output.
    /// Works with MottoChat or MottoOllama outputs by wrapping them in critique cycles.
    /// Usage: MottoOllama('msg=...') | MottoSelfCritique or MottoSelfCritique('2')
    /// </summary>
    [PipelineToken("MottoSelfCritique", "MeTTaCritique")]
    public static Step<CliPipelineState, CliPipelineState> MottoSelfCritique(string? args = null)
        => async s =>
        {
            // Parse iteration count from args, default to 1
            int iterations = 1;
            if (!string.IsNullOrWhiteSpace(args))
            {
                string parsed = ParseString(args);
                if (int.TryParse(parsed, out int value) && value > 0)
                {
                    iterations = Math.Min(value, 5); // Cap at 5
                }
            }

            // Use the current output or context as the initial draft
            string initialContent = !string.IsNullOrWhiteSpace(s.Output) ? s.Output : s.Context;
            
            if (string.IsNullOrWhiteSpace(initialContent))
            {
                Console.WriteLine("[metta-critique] No content to critique. Run MottoChat or MottoOllama first.");
                return s;
            }

            // Create a draft state from the MeTTa output
            s.Branch = s.Branch.WithReasoning(new Draft(initialContent), "MeTTa initial output", new List<ToolExecution>());

            // Now apply self-critique using the standard agent
            LangChainPipeline.Agent.SelfCritiqueAgent agent = new(s.Llm, s.Tools, s.Embed);
            
            // We need to extract topic and query
            string topic = !string.IsNullOrWhiteSpace(s.Topic) ? s.Topic : "MeTTa reasoning output";
            string query = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : topic;

            Result<LangChainPipeline.Agent.SelfCritiqueResult, string> result = 
                await agent.GenerateWithCritiqueAsync(s.Branch, topic, query, iterations, s.RetrievalK);

            if (result.IsSuccess)
            {
                LangChainPipeline.Agent.SelfCritiqueResult critiqueResult = result.Value;
                s.Branch = critiqueResult.Branch;
                
                // Format output to show the critique process
                StringBuilder output = new();
                output.AppendLine("\n=== MeTTa Self-Critique Result ===");
                output.AppendLine($"Iterations: {critiqueResult.IterationsPerformed}");
                output.AppendLine($"Confidence: {critiqueResult.Confidence}");
                output.AppendLine("\n--- Original MeTTa Output ---");
                output.AppendLine(critiqueResult.Draft);
                output.AppendLine("\n--- Critique ---");
                output.AppendLine(critiqueResult.Critique);
                output.AppendLine("\n--- Improved Response ---");
                output.AppendLine(critiqueResult.ImprovedResponse);
                output.AppendLine("\n=========================");
                
                s.Output = output.ToString();
                s.Context = critiqueResult.ImprovedResponse;
                
                if (s.Trace) 
                {
                    Console.WriteLine($"[metta-critique] Self-critique completed with {critiqueResult.IterationsPerformed} iteration(s), confidence: {critiqueResult.Confidence}");
                }
            }
            else
            {
                Console.WriteLine($"[metta-critique] Failed: {result.Error}");
                s.Branch = s.Branch.WithIngestEvent($"metta-critique:error:{result.Error.Replace('|', ':')}", Array.Empty<string>());
            }

            return s;
        };
}
