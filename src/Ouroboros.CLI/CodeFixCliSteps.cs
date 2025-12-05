using LangChainPipeline.CLI.CodeGeneration;

namespace LangChainPipeline.CLI;

public static class CodeFixCliSteps
{
    [PipelineToken("UseUniversalFix", "UniversalFix")] // Usage: UseUniversalFix('code=...|id=CS8600')
    public static Step<CliPipelineState, CliPipelineState> UseUniversalFix(string? args = null)
        => async s =>
        {
            string raw = CliSteps.ParseString(args);
            string? code = null;
            string? id = null;
            
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("code=", StringComparison.OrdinalIgnoreCase)) code = part.Substring(5);
                    else if (part.StartsWith("id=", StringComparison.OrdinalIgnoreCase)) id = part.Substring(3);
                }
            }

            // Fallback to context or prompt if code not provided
            code ??= s.Context;
            if (string.IsNullOrWhiteSpace(code)) code = s.Prompt;
            
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(id))
            {
                Console.WriteLine("[universal-fix] Missing code or diagnostic ID");
                return s;
            }

            try
            {
                var tool = new RoslynCodeTool();
                var result = await tool.ApplyUniversalFixAsync(code, id);
                
                if (result.IsSuccess)
                {
                    s.Output = result.Value;
                    s.Context = result.Value; // Update context for chaining
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(result.Value), $"Fix {id}", new List<ToolExecution>());
                    if (s.Trace) Console.WriteLine($"[universal-fix] Applied fix for {id}");
                }
                else
                {
                    Console.WriteLine($"[universal-fix] Failed: {result.Error}");
                    s.Branch = s.Branch.WithIngestEvent($"fix:error:{id}:{result.Error.Replace('|', ':')}", Array.Empty<string>());
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"fix:exception:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            
            return s;
        };
}
