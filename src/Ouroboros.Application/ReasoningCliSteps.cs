using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.Application;

public static class ReasoningCliSteps
{
    [PipelineToken("UseDraft")]
    public static Step<CliPipelineState, CliPipelineState> UseDraft(string? args = null)
        => async s =>
        {
            (string topic, string query) = CliSteps.Normalize(s);
            Step<PipelineBranch, PipelineBranch> step = ReasoningArrows.DraftArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Draft produced");
            return s;
        };

    [PipelineToken("UseCritique")]
    public static Step<CliPipelineState, CliPipelineState> UseCritique(string? args = null)
        => async s =>
        {
            (string topic, string query) = CliSteps.Normalize(s);
            Step<PipelineBranch, PipelineBranch> step = ReasoningArrows.CritiqueArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Critique produced");
            return s;
        };

    [PipelineToken("UseImprove", "UseFinal")]
    public static Step<CliPipelineState, CliPipelineState> UseImprove(string? args = null)
        => async s =>
        {
            (string topic, string query) = CliSteps.Normalize(s);
            Step<PipelineBranch, PipelineBranch> step = ReasoningArrows.ImproveArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Improvement produced");
            return s;
        };

    /// <summary>
    /// Performs self-critique on any prior output with Draft → Critique → Improve cycles.
    /// Supports optional iteration count parameter: UseSelfCritique or UseSelfCritique('2').
    /// </summary>
    [PipelineToken("UseSelfCritique")]
    public static Step<CliPipelineState, CliPipelineState> UseSelfCritique(string? args = null)
        => async s =>
        {
            (string topic, string query) = CliSteps.Normalize(s);
            
            // Parse iteration count from args, default to 1
            int iterations = 1;
            if (!string.IsNullOrWhiteSpace(args))
            {
                string parsed = CliSteps.ParseString(args);
                if (int.TryParse(parsed, out int value) && value > 0)
                {
                    iterations = value;
                }
            }

            // Create self-critique agent
            LangChainPipeline.Agent.SelfCritiqueAgent agent = new(s.Llm, s.Tools, s.Embed);
            
            // Generate with critique
            Result<LangChainPipeline.Agent.SelfCritiqueResult, string> result = 
                await agent.GenerateWithCritiqueAsync(s.Branch, topic, query, iterations, s.RetrievalK);

            if (result.IsSuccess)
            {
                LangChainPipeline.Agent.SelfCritiqueResult critiqueResult = result.Value;
                s.Branch = critiqueResult.Branch;
                
                // Format output to show Draft → Critique → Improved sections
                StringBuilder output = new();
                output.AppendLine("\n=== Self-Critique Result ===");
                output.AppendLine($"Iterations: {critiqueResult.IterationsPerformed}");
                output.AppendLine($"Confidence: {critiqueResult.Confidence}");
                output.AppendLine("\n--- Draft ---");
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
                    Console.WriteLine($"[trace] Self-critique completed with {critiqueResult.IterationsPerformed} iteration(s), confidence: {critiqueResult.Confidence}");
                }
            }
            else
            {
                Console.WriteLine($"[error] Self-critique failed: {result.Error}");
                s.Branch = s.Branch.WithIngestEvent($"self-critique:error:{result.Error.Replace('|', ':')}", Array.Empty<string>());
            }

            return s;
        };

    /// <summary>
    /// Streaming version of self-critique that shows incremental updates for each stage.
    /// Supports optional iteration count parameter: UseStreamingSelfCritique or UseStreamingSelfCritique('2').
    /// </summary>
    [PipelineToken("UseStreamingSelfCritique", "StreamSelfCritique")]
    public static Step<CliPipelineState, CliPipelineState> UseStreamingSelfCritique(string? args = null)
        => async s =>
        {
            LangChainPipeline.Providers.IStreamingChatModel? streamingModel = s.Llm.InnerModel as LangChainPipeline.Providers.IStreamingChatModel;

            if (streamingModel == null)
            {
                // Check if using LiteLLM endpoint
                string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
                string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
                string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

                if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey) &&
                    (endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
                {
                    streamingModel = new LangChainPipeline.Providers.LiteLLMChatModel(endpoint, apiKey, modelName);
                }
            }

            if (streamingModel == null)
            {
                Console.WriteLine("[streaming] Warning: Current model does not support streaming. Falling back to non-streaming self-critique.");
                return await UseSelfCritique(args)(s);
            }

            (string topic, string query) = CliSteps.Normalize(s);
            
            // Parse iteration count from args, default to 1
            int iterations = 1;
            if (!string.IsNullOrWhiteSpace(args))
            {
                string parsed = CliSteps.ParseString(args);
                if (int.TryParse(parsed, out int value) && value > 0)
                {
                    iterations = Math.Min(value, 5); // Cap at 5
                }
            }

            System.Text.StringBuilder draftText = new();
            System.Text.StringBuilder critiqueText = new();
            System.Text.StringBuilder improvedText = new();

            Console.WriteLine("\n=== Streaming Self-Critique ===");
            Console.WriteLine($"Iterations: {iterations}\n");

            // Perform critique-improve cycles with streaming
            for (int i = 0; i < iterations; i++)
            {
                Console.WriteLine($"--- Iteration {i + 1} ---");
                
                // Stream Draft (only on first iteration)
                if (i == 0)
                {
                    Console.WriteLine("\n[Draft]");
                    draftText.Clear();
                    await ReasoningArrows.StreamingDraftArrow(streamingModel, s.Tools, s.Embed, s.Branch, topic, query, s.RetrievalK)
                        .Do(tuple =>
                        {
                            Console.Write(tuple.chunk);
                            draftText.Append(tuple.chunk);
                        })
                        .LastAsync()
                        .ForEachAsync(tuple => s.Branch = tuple.branch);
                    Console.WriteLine();
                }

                // Stream Critique
                Console.WriteLine("\n[Critique]");
                critiqueText.Clear();
                await ReasoningArrows.StreamingCritiqueArrow(streamingModel, s.Tools, s.Embed, s.Branch, topic, query, s.RetrievalK)
                    .Do(tuple =>
                    {
                        Console.Write(tuple.chunk);
                        critiqueText.Append(tuple.chunk);
                    })
                    .LastAsync()
                    .ForEachAsync(tuple => s.Branch = tuple.branch);
                Console.WriteLine();

                // Stream Improvement
                Console.WriteLine("\n[Improvement]");
                improvedText.Clear();
                await ReasoningArrows.StreamingImproveArrow(streamingModel, s.Tools, s.Embed, s.Branch, topic, query, s.RetrievalK)
                    .Do(tuple =>
                    {
                        Console.Write(tuple.chunk);
                        improvedText.Append(tuple.chunk);
                    })
                    .LastAsync()
                    .ForEachAsync(tuple => s.Branch = tuple.branch);
                Console.WriteLine("\n");
            }

            // Compute confidence
            string lastCritique = critiqueText.ToString();
            ConfidenceRating confidence = ConfidenceRating.Medium;
            if (lastCritique.Contains("excellent", StringComparison.OrdinalIgnoreCase) || 
                lastCritique.Contains("high quality", StringComparison.OrdinalIgnoreCase))
            {
                confidence = ConfidenceRating.High;
            }
            else if (lastCritique.Contains("needs work", StringComparison.OrdinalIgnoreCase) ||
                     lastCritique.Contains("significant issues", StringComparison.OrdinalIgnoreCase))
            {
                confidence = ConfidenceRating.Low;
            }

            Console.WriteLine($"Confidence: {confidence}");
            Console.WriteLine("=========================\n");

            s.Output = improvedText.ToString();
            s.Context = improvedText.ToString();

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Streaming self-critique completed with {iterations} iteration(s), confidence: {confidence}");
            }

            return s;
        };

    /// <summary>
    /// Streams draft reasoning content in real-time using Reactive Extensions.
    /// Outputs incremental chunks as they are generated by the LLM.
    /// </summary>
    [PipelineToken("UseStreamingDraft", "StreamDraft")]
    public static Step<CliPipelineState, CliPipelineState> UseStreamingDraft(string? args = null)
        => async s =>
        {
            LangChainPipeline.Providers.IStreamingChatModel? streamingModel = s.Llm.InnerModel as LangChainPipeline.Providers.IStreamingChatModel;

            if (streamingModel == null)
            {
                // Check if using LiteLLM endpoint via environment variable or create streaming model
                string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
                string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
                string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

                if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey) &&
                    (endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
                {
                    streamingModel = new LangChainPipeline.Providers.LiteLLMChatModel(endpoint, apiKey, modelName);
                }
            }

            if (streamingModel == null)
            {
                Console.WriteLine("[streaming] Warning: Current model does not support streaming and no LiteLLM endpoint configured. Falling back to non-streaming.");
                return await UseDraft(args)(s);
            }

            (string topic, string query) = CliSteps.Normalize(s);
            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await ReasoningArrows.StreamingDraftArrow(streamingModel, s.Tools, s.Embed, s.Branch, topic, query, s.RetrievalK)
                .Do(tuple =>
                {
                    Console.Write(tuple.chunk);
                    fullText.Append(tuple.chunk);
                })
                .LastAsync()
                .ForEachAsync(tuple => s.Branch = tuple.branch);

            Console.WriteLine();
            if (s.Trace) Console.WriteLine("[trace] Streaming Draft completed");
            return s;
        };

    /// <summary>
    /// Streams critique reasoning content in real-time using Reactive Extensions.
    /// </summary>
    [PipelineToken("UseStreamingCritique", "StreamCritique")]
    public static Step<CliPipelineState, CliPipelineState> UseStreamingCritique(string? args = null)
        => async s =>
        {
            LangChainPipeline.Providers.IStreamingChatModel? streamingModel = s.Llm.InnerModel as LangChainPipeline.Providers.IStreamingChatModel;

            if (streamingModel == null)
            {
                string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
                string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
                string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

                if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey) &&
                    (endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
                {
                    streamingModel = new LangChainPipeline.Providers.LiteLLMChatModel(endpoint, apiKey, modelName);
                }
            }

            if (streamingModel == null)
            {
                Console.WriteLine("[streaming] Warning: Current model does not support streaming and no LiteLLM endpoint configured. Falling back to non-streaming.");
                return await UseCritique(args)(s);
            }

            (string topic, string query) = CliSteps.Normalize(s);
            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await ReasoningArrows.StreamingCritiqueArrow(streamingModel, s.Tools, s.Embed, s.Branch, topic, query, s.RetrievalK)
                .Do(tuple =>
                {
                    Console.Write(tuple.chunk);
                    fullText.Append(tuple.chunk);
                })
                .LastAsync()
                .ForEachAsync(tuple => s.Branch = tuple.branch);

            Console.WriteLine();
            if (s.Trace) Console.WriteLine("[trace] Streaming Critique completed");
            return s;
        };

    /// <summary>
    /// Streams improvement reasoning content in real-time using Reactive Extensions.
    /// </summary>
    [PipelineToken("UseStreamingImprove", "StreamImprove", "StreamFinal")]
    public static Step<CliPipelineState, CliPipelineState> UseStreamingImprove(string? args = null)
        => async s =>
        {
            LangChainPipeline.Providers.IStreamingChatModel? streamingModel = s.Llm.InnerModel as LangChainPipeline.Providers.IStreamingChatModel;

            if (streamingModel == null)
            {
                string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
                string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
                string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

                if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey) &&
                    (endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
                {
                    streamingModel = new LangChainPipeline.Providers.LiteLLMChatModel(endpoint, apiKey, modelName);
                }
            }

            if (streamingModel == null)
            {
                Console.WriteLine("[streaming] Warning: Current model does not support streaming and no LiteLLM endpoint configured. Falling back to non-streaming.");
                return await UseImprove(args)(s);
            }

            (string topic, string query) = CliSteps.Normalize(s);
            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await ReasoningArrows.StreamingImproveArrow(streamingModel, s.Tools, s.Embed, s.Branch, topic, query, s.RetrievalK)
                .Do(tuple =>
                {
                    Console.Write(tuple.chunk);
                    fullText.Append(tuple.chunk);
                })
                .LastAsync()
                .ForEachAsync(tuple => s.Branch = tuple.branch);

            Console.WriteLine();
            if (s.Trace) Console.WriteLine("[trace] Streaming Improvement completed");
            return s;
        };

    /// <summary>
    /// Executes a complete streaming reasoning pipeline (Draft -> Critique -> Improve) with real-time output.
    /// Uses Reactive Extensions to stream incremental updates throughout all reasoning stages.
    /// </summary>
    [PipelineToken("UseStreamingPipeline", "StreamReasoningPipeline")]
    public static Step<CliPipelineState, CliPipelineState> UseStreamingPipeline(string? args = null)
        => async s =>
        {
            LangChainPipeline.Providers.IStreamingChatModel? streamingModel = s.Llm.InnerModel as LangChainPipeline.Providers.IStreamingChatModel;

            if (streamingModel == null)
            {
                string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
                string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
                string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

                if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey) &&
                    (endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) || endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
                {
                    streamingModel = new LangChainPipeline.Providers.LiteLLMChatModel(endpoint, apiKey, modelName);
                }
            }

            if (streamingModel == null)
            {
                Console.WriteLine("[streaming] Warning: Current model does not support streaming and no LiteLLM endpoint configured. Falling back to non-streaming.");
                return await UseRefinementLoop("1")(s);
            }

            (string topic, string query) = CliSteps.Normalize(s);
            string currentStage = string.Empty;

            await ReasoningArrows.StreamingReasoningPipeline(streamingModel, s.Tools, s.Embed, topic, query, s.RetrievalK)
                .Do(tuple =>
                {
                    if (tuple.stage != currentStage)
                    {
                        if (!string.IsNullOrEmpty(currentStage)) Console.WriteLine();
                        Console.WriteLine($"\n=== {tuple.stage} Stage ===");
                        currentStage = tuple.stage;
                    }
                    Console.Write(tuple.chunk);
                })
                .LastAsync()
                .ForEachAsync(tuple => s.Branch = tuple.branch);

            Console.WriteLine();
            if (s.Trace) Console.WriteLine("[trace] Streaming Reasoning Pipeline completed");
            return s;
        };

    /// <summary>
    /// Executes a complete refinement loop: Draft -> Critique -> Improve.
    /// If no draft exists, one will be created automatically. Then the critique-improve
    /// cycle runs for the specified number of iterations (default: 1).
    /// </summary>
    /// <param name="args">Number of critique-improve iterations (default: 1)</param>
    /// <example>
    /// UseRefinementLoop('3')  -- Creates draft (if needed), then runs 3 critique-improve cycles
    /// </example>
    [PipelineToken("UseRefinementLoop")]
    public static Step<CliPipelineState, CliPipelineState> UseRefinementLoop(string? args = null)
        => async s =>
        {
            int count = 1;
            if (!string.IsNullOrWhiteSpace(args))
            {
                Match m = Regex.Match(args, @"\s*(\d+)\s*");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int n)) count = n;
            }

            // Check if a draft already exists
            bool hasDraft = s.Branch.Events.OfType<ReasoningStep>()
                .Any(e => e.State is Draft);

            // Create initial draft if none exists
            if (!hasDraft)
            {
                s = await UseDraft()(s);
            }

            // Run complete refinement cycles: Critique -> Improve
            for (int i = 0; i < count; i++)
            {
                s = await UseCritique()(s);
                s = await UseImprove()(s);
            }
            return s;
        };
}

