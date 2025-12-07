#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.DocumentLoaders;
using System.Reactive.Linq;

namespace LangChainPipeline.Pipeline.Reasoning;

/// <summary>
/// Provides arrow functions for reasoning operations in the pipeline.
/// Enhanced with Result monad for better error handling.
/// </summary>
public static class ReasoningArrows
{
    private static string ToolSchemasOrEmpty(ToolRegistry registry)
        => registry.ExportSchemas();

    /// <summary>
    /// Gets the most recent reasoning state (Draft or FinalSpec) for iterative refinement.
    /// This enables proper chaining in multi-iteration refinement loops where subsequent
    /// iterations build upon the previous improvement rather than the original draft.
    /// </summary>
    private static ReasoningState? GetMostRecentReasoningState(PipelineBranch branch)
    {
        List<ReasoningState> reasoningStates = branch.Events
            .OfType<ReasoningStep>()
            .Select(e => e.State)
            .Where(s => s is Draft or FinalSpec)
            .ToList();

        // Return the most recent state (last in the list)
        return reasoningStates.LastOrDefault();
    }

    /// <summary>
    /// Creates a thinking arrow that generates a reasoning process before drafting.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> ThinkingArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Thinking.Format(new()
            {
                ["context"] = context,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Thinking(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe thinking arrow.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeThinkingArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            try
            {
                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = Prompts.Thinking.Format(new()
                {
                    ["context"] = context,
                    ["topic"] = topic,
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools)
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                PipelineBranch result = branch.WithReasoning(new Thinking(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Thinking generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a draft arrow that generates an initial response.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> DraftArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Draft.Format(new()
            {
                ["context"] = context,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Draft(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe draft arrow that generates an initial response with error handling.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeDraftArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            try
            {
                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = Prompts.Draft.Format(new()
                {
                    ["context"] = context,
                    ["topic"] = topic,
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools)
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                PipelineBranch result = branch.WithReasoning(new Draft(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Draft generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a critique arrow that analyzes and critiques the most recent reasoning state.
    /// Supports iterative refinement by critiquing either the initial Draft or a previous FinalSpec.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> CritiqueArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            ReasoningState? currentState = GetMostRecentReasoningState(branch);
            if (currentState is null) return branch;

            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Critique.Format(new()
            {
                ["context"] = context,
                ["draft"] = currentState.Text,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new Critique(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe critique arrow with proper error handling and validation.
    /// Supports iterative refinement by critiquing the most recent reasoning state.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeCritiqueArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            try
            {
                ReasoningState? currentState = GetMostRecentReasoningState(branch);
                if (currentState is null)
                    return Result<PipelineBranch, string>.Failure("No draft or previous improvement found to critique");

                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = Prompts.Critique.Format(new()
                {
                    ["context"] = context,
                    ["draft"] = currentState.Text,
                    ["topic"] = topic,
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools)
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                PipelineBranch result = branch.WithReasoning(new Critique(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Critique generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates an improvement arrow that generates a final improved version.
    /// Supports iterative refinement by improving upon the most recent reasoning state.
    /// </summary>
    public static Step<PipelineBranch, PipelineBranch> ImproveArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            ReasoningState? currentState = GetMostRecentReasoningState(branch);
            Critique? critique = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Critique>().LastOrDefault();
            if (currentState is null || critique is null) return branch;

            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Improve.Format(new()
            {
                ["context"] = context,
                ["draft"] = currentState.Text,
                ["critique"] = critique.CritiqueText,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
            return branch.WithReasoning(new FinalSpec(text), prompt, toolCalls);
        };

    /// <summary>
    /// Creates a Result-safe improvement arrow with comprehensive error handling.
    /// Supports iterative refinement by improving upon the most recent reasoning state.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeImproveArrow(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => async branch =>
        {
            try
            {
                ReasoningState? currentState = GetMostRecentReasoningState(branch);
                Critique? critique = branch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Critique>().LastOrDefault();

                if (currentState is null)
                    return Result<PipelineBranch, string>.Failure("No draft or previous improvement found for improvement");
                if (critique is null)
                    return Result<PipelineBranch, string>.Failure("No critique found for improvement");

                IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
                string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

                string prompt = Prompts.Improve.Format(new()
                {
                    ["context"] = context,
                    ["draft"] = currentState.Text,
                    ["critique"] = critique.CritiqueText,
                    ["topic"] = topic,
                    ["tools_schemas"] = ToolSchemasOrEmpty(tools)
                });

                (string text, List<ToolExecution> toolCalls) = await llm.GenerateWithToolsAsync(prompt);
                PipelineBranch result = branch.WithReasoning(new FinalSpec(text), prompt, toolCalls);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Improvement generation failed: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a complete safe reasoning pipeline that chains thinking -> draft -> critique -> improve with error handling.
    /// Demonstrates monadic composition for robust pipeline execution.
    /// </summary>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeReasoningPipeline(
        ToolAwareChatModel llm, ToolRegistry tools, IEmbeddingModel embed, string topic, string query, int k = 8)
        => SafeThinkingArrow(llm, tools, embed, topic, query, k)
            .Then(SafeDraftArrow(llm, tools, embed, topic, query, k))
            .Then(SafeCritiqueArrow(llm, tools, embed, topic, query, k))
            .Then(SafeImproveArrow(llm, tools, embed, topic, query, k));

    /// <summary>
    /// Creates a streaming thinking arrow that emits reasoning chunks in real-time.
    /// Supports agentic tool calls within the thinking process.
    /// </summary>
    public static IObservable<(string chunk, PipelineBranch branch)> StreamingThinkingArrow(
        LangChainPipeline.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            PipelineBranch branch = new PipelineBranch("streaming", new TrackedVectorStore(), DataSource.FromPath("."));
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Thinking.Format(new()
            {
                ["context"] = context,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();
            System.Text.StringBuilder currentTurnText = new System.Text.StringBuilder();
            List<ToolExecution> allToolCalls = new List<ToolExecution>();

            // Agent loop: continue as long as tools are called
            while (!ct.IsCancellationRequested)
            {
                bool toolCalled = false;
                currentTurnText.Clear();

                await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable())
                {
                    fullText.Append(chunk);
                    currentTurnText.Append(chunk);

                    PipelineBranch updatedBranch = branch.WithReasoning(new Thinking(fullText.ToString()), prompt, allToolCalls);
                    yield return (chunk, updatedBranch);
                }

                // Check for tool calls in the current turn's output
                var toolPattern = new System.Text.RegularExpressions.Regex(@"\[TOOL:([^\s]+)\s*([^\]]*)\]");
                var matches = toolPattern.Matches(currentTurnText.ToString());

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    toolCalled = true;
                    string name = match.Groups[1].Value;
                    string args = match.Groups[2].Value.Trim();

                    ITool? tool = tools.Get(name);
                    string output;
                    if (tool is null)
                    {
                        output = $"error: tool '{name}' not found";
                    }
                    else
                    {
                        try
                        {
                            Result<string, string> toolResult = await tool.InvokeAsync(args, ct);
                            output = toolResult.Match(success => success, error => $"error: {error}");
                        }
                        catch (Exception ex)
                        {
                            output = $"error: {ex.Message}";
                        }
                    }

                    ToolExecution execution = new ToolExecution(name, args, output, DateTime.UtcNow);
                    allToolCalls.Add(execution);

                    string resultTag = $"[TOOL-RESULT:{name}] {output}";

                    // Emit the tool result as a chunk so the user sees it
                    fullText.Append(resultTag);
                    PipelineBranch withTool = branch.WithReasoning(new Thinking(fullText.ToString()), prompt, allToolCalls);
                    yield return (resultTag, withTool);

                    // Append to prompt for the next turn
                    prompt += $"\n{match.Value} {resultTag}\nContinue thinking.";
                }

                if (!toolCalled)
                {
                    break; // No tools called, thinking is done
                }
            }
        }

        return StreamAsync().ToObservable();
    }

    /// <summary>
    /// Creates a streaming draft arrow using Reactive Extensions that emits reasoning content chunks in real-time.
    /// </summary>
    /// <param name="streamingModel">IStreamingChatModel with streaming support.</param>
    /// <param name="tools">Tool registry.</param>
    /// <param name="embed">Embedding model.</param>
    /// <param name="inputBranch">Input branch from previous step.</param>
    /// <param name="topic">Topic for reasoning.</param>
    /// <param name="query">Query for RAG retrieval.</param>
    /// <param name="k">Number of documents to retrieve.</param>
    /// <returns>Observable sequence of (chunk, branch) tuples.</returns>
    public static IObservable<(string chunk, PipelineBranch branch)> StreamingDraftArrow(
        LangChainPipeline.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        PipelineBranch inputBranch,
        string topic,
        string query,
        int k = 8)
    {
        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            PipelineBranch branch = inputBranch;
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Draft.Format(new()
            {
                ["context"] = context,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable())
            {
                fullText.Append(chunk);
                PipelineBranch updatedBranch = branch.WithReasoning(new Draft(fullText.ToString()), prompt, null);
                yield return (chunk, updatedBranch);
            }
        }

        return StreamAsync().ToObservable();
    }    /// <summary>
         /// Creates a streaming critique arrow that emits critique chunks in real-time.
         /// </summary>
    public static IObservable<(string chunk, PipelineBranch branch)> StreamingCritiqueArrow(
        LangChainPipeline.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        PipelineBranch inputBranch,
        string topic,
        string query,
        int k = 8)
    {
        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            ReasoningState? currentState = GetMostRecentReasoningState(inputBranch);
            if (currentState is null)
                throw new InvalidOperationException("No draft or previous improvement found to critique");

            IReadOnlyCollection<Document> docs = await inputBranch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Critique.Format(new()
            {
                ["context"] = context,
                ["draft"] = currentState.Text,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable())
            {
                fullText.Append(chunk);
                PipelineBranch updatedBranch = inputBranch.WithReasoning(new Critique(fullText.ToString()), prompt, null);
                yield return (chunk, updatedBranch);
            }
        }

        return StreamAsync().ToObservable();
    }

    /// <summary>
    /// Creates a streaming improvement arrow that emits improvement chunks in real-time.
    /// </summary>
    public static IObservable<(string chunk, PipelineBranch branch)> StreamingImproveArrow(
        LangChainPipeline.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        PipelineBranch inputBranch,
        string topic,
        string query,
        int k = 8)
    {
        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            ReasoningState? currentState = GetMostRecentReasoningState(inputBranch);
            Critique? critique = inputBranch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Critique>().LastOrDefault();

            if (currentState is null)
                throw new InvalidOperationException("No draft or previous improvement found");
            if (critique is null)
                throw new InvalidOperationException("No critique found for improvement");

            IReadOnlyCollection<Document> docs = await inputBranch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Improve.Format(new()
            {
                ["context"] = context,
                ["draft"] = currentState.Text,
                ["critique"] = critique.CritiqueText,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable())
            {
                fullText.Append(chunk);
                PipelineBranch updatedBranch = inputBranch.WithReasoning(new FinalSpec(fullText.ToString()), prompt, null);
                yield return (chunk, updatedBranch);
            }
        }

        return StreamAsync().ToObservable();
    }

    /// <summary>
    /// Creates a complete streaming reasoning pipeline (Thinking -> Draft -> Critique -> Improve) using Reactive Extensions.
    /// Emits incremental updates as reasoning progresses through each stage.
    /// </summary>
    public static IObservable<(string stage, string chunk, PipelineBranch branch)> StreamingReasoningPipeline(
        LangChainPipeline.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return Observable.Create<(string stage, string chunk, PipelineBranch branch)>(async (observer, ct) =>
        {
            try
            {
                PipelineBranch branch = new PipelineBranch("streaming-pipeline", new TrackedVectorStore(), DataSource.FromPath("."));

                // Stage 1: Thinking
                await StreamingThinkingArrow(streamingModel, tools, embed, topic, query, k)
                    .Do(tuple => observer.OnNext(("Thinking", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct);

                // Stage 2: Draft
                await StreamingDraftArrow(streamingModel, tools, embed, branch, topic, query, k)
                    .Do(tuple => observer.OnNext(("Draft", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct);

                // Stage 3: Critique
                await StreamingCritiqueArrow(streamingModel, tools, embed, branch, topic, query, k)
                    .Do(tuple => observer.OnNext(("Critique", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct);

                // Stage 4: Improve
                await StreamingImproveArrow(streamingModel, tools, embed, branch, topic, query, k)
                    .Do(tuple => observer.OnNext(("Improve", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct);

                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }
}
