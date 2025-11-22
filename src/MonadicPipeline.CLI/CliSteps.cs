#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reflection; // for BindingFlags
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LangChain.Chains.StackableChains.Context; // for StackableChainValues
using LangChain.Databases; // for Vector, IVectorCollection
using LangChain.DocumentLoaders;
using LangChain.Providers; // for IChatModel
using LangChain.Providers.Ollama;
// for TrackedVectorStore
using LangChain.Splitters.Text;
using LangChainPipeline.CLI.Interop; // for ChainAdapters
using LangChainPipeline.Interop.LangChain; // for ExternalChainRegistry (reflection-based chain integration)
using LangChainPipeline.Pipeline.Ingestion.Zip;

namespace LangChainPipeline.CLI;

/// <summary>
/// Discoverable CLI pipeline steps. Each method is annotated with PipelineToken and returns a Step over CliPipelineState.
/// Parsing of simple args is supported via optional string? args parameter.
/// </summary>
public static class CliSteps
{
    private static (string topic, string query) Normalize(CliPipelineState s)
    {
        string topic = string.IsNullOrWhiteSpace(s.Topic) ? (string.IsNullOrWhiteSpace(s.Prompt) ? "topic" : s.Prompt) : s.Topic;
        string query = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? topic : s.Prompt) : s.Query;
        return (topic, query);
    }

    [PipelineToken("UseIngest")]
    public static Step<CliPipelineState, CliPipelineState> UseIngest(string? args = null)
        => async s =>
        {
            try
            {
                Step<PipelineBranch, PipelineBranch> ingest = IngestionArrows.IngestArrow<FileLoader>(s.Embed, tag: "cli");
                s.Branch = await ingest(s.Branch);
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"ingest:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("UseDir", "DirIngest")] // Usage: UseDir('root=src|ext=.cs,.md|exclude=bin,obj|max=500000|pattern=*.cs;*.md|norec')
    public static Step<CliPipelineState, CliPipelineState> UseDir(string? args = null)
        => async s =>
        {
            string root = s.Branch.Source.Value as string ?? Environment.CurrentDirectory;
            bool recursive = true;
            List<string> exts = new List<string>();
            List<string> excludeDirs = new List<string>();
            List<string> patterns = new List<string>();
            long maxBytes = 0;
            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("root=", StringComparison.OrdinalIgnoreCase)) root = Path.GetFullPath(part.Substring(5));
                    else if (part.StartsWith("ext=", StringComparison.OrdinalIgnoreCase)) exts.AddRange(part.Substring(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("exclude=", StringComparison.OrdinalIgnoreCase)) excludeDirs.AddRange(part.Substring(8).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("pattern=", StringComparison.OrdinalIgnoreCase)) patterns.AddRange(part.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("max=", StringComparison.OrdinalIgnoreCase) && long.TryParse(part.AsSpan(4), out long m)) maxBytes = m;
                    else if (part.Equals("norec", StringComparison.OrdinalIgnoreCase)) recursive = false;
                }
            }
            if (!Directory.Exists(root))
            {
                s.Branch = s.Branch.WithIngestEvent($"dir:missing:{root}", Array.Empty<string>());
                return s;
            }
            try
            {
                DirectoryIngestionOptions options = new DirectoryIngestionOptions
                {
                    Recursive = recursive,
                    Extensions = exts.Count == 0 ? null : exts.ToArray(),
                    ExcludeDirectories = excludeDirs.Count == 0 ? null : excludeDirs.ToArray(),
                    Patterns = patterns.Count == 0 ? ["*"] : patterns.ToArray(),
                    MaxFileBytes = maxBytes,
                    ChunkSize = 1800,
                    ChunkOverlap = 180
                };
                DirectoryDocumentLoader<FileLoader> loader = new DirectoryDocumentLoader<FileLoader>(options);
                DirectoryIngestionStats stats = new DirectoryIngestionStats();
                loader.AttachStats(stats);
                TrackedVectorStore store = s.Branch.Store as TrackedVectorStore ?? new TrackedVectorStore();
                RecursiveCharacterTextSplitter splitter = new RecursiveCharacterTextSplitter(chunkSize: options.ChunkSize, chunkOverlap: options.ChunkOverlap);
                IReadOnlyCollection<Document> docs = await loader.LoadAsync(DataSource.FromPath(root));
                List<Vector> vectors = new List<Vector>();
                int fileIndex = 0;
                foreach (Document doc in docs)
                {
                    if (string.IsNullOrWhiteSpace(doc.PageContent))
                    {
                        fileIndex++;
                        continue;
                    }

                    IReadOnlyList<string> chunks = splitter.SplitText(doc.PageContent);
                    int chunkCount = chunks.Count;
                    Dictionary<string, object> baseMetadata = BuildDocumentMetadata(doc, root, fileIndex);

                    int chunkIdx = 0;
                    foreach (string chunk in chunks)
                    {
                        string vectorId = $"dir:{fileIndex}:{chunkIdx}";
                        Dictionary<string, object> chunkMetadata = BuildChunkMetadata(baseMetadata, chunkIdx, chunkCount, vectorId);

                        try
                        {
                            float[] emb = await s.Embed.CreateEmbeddingsAsync(chunk);
                            vectors.Add(new Vector
                            {
                                Id = vectorId,
                                Text = chunk,
                                Embedding = emb,
                                Metadata = chunkMetadata
                            });
                        }
                        catch
                        {
                            chunkMetadata["embedding"] = "fallback";
                            vectors.Add(new Vector
                            {
                                Id = $"{vectorId}:fallback",
                                Text = chunk,
                                Embedding = new float[8],
                                Metadata = chunkMetadata
                            });
                        }
                        chunkIdx++;
                    }
                    fileIndex++;
                }
                if (vectors.Count > 0) await store.AddAsync(vectors);
                stats.VectorsProduced += vectors.Count;
                s.Branch = s.Branch.WithIngestEvent($"dir:ingest:{Path.GetFileName(root)}", vectors.Select(v => v.Id));
                Console.WriteLine($"[dir] {stats}");
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"dir:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("UseDirBatched", "DirIngestBatched")] // Usage: UseDirBatched('root=src|ext=.cs,.md|exclude=bin,obj|max=500000|pattern=*.cs;*.md|norec|addEvery=256')
    public static Step<CliPipelineState, CliPipelineState> UseDirBatched(string? args = null)
        => async s =>
        {
            string root = s.Branch.Source.Value as string ?? Environment.CurrentDirectory;
            bool recursive = true;
            List<string> exts = new List<string>();
            List<string> excludeDirs = new List<string>();
            List<string> patterns = new List<string>();
            long maxBytes = 0;
            int addEvery = 256; // number of vectors per AddAsync batch
            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("root=", StringComparison.OrdinalIgnoreCase)) root = Path.GetFullPath(part.Substring(5));
                    else if (part.StartsWith("ext=", StringComparison.OrdinalIgnoreCase)) exts.AddRange(part.Substring(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("exclude=", StringComparison.OrdinalIgnoreCase)) excludeDirs.AddRange(part.Substring(8).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("pattern=", StringComparison.OrdinalIgnoreCase)) patterns.AddRange(part.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("max=", StringComparison.OrdinalIgnoreCase) && long.TryParse(part.AsSpan(4), out long m)) maxBytes = m;
                    else if (part.Equals("norec", StringComparison.OrdinalIgnoreCase)) recursive = false;
                    else if (part.StartsWith("addEvery=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(9), out int ae) && ae > 0) addEvery = ae;
                }
            }
            if (!Directory.Exists(root))
            {
                s.Branch = s.Branch.WithIngestEvent($"dir:missing:{root}", Array.Empty<string>());
                return s;
            }
            try
            {
                DirectoryIngestionOptions options = new DirectoryIngestionOptions
                {
                    Recursive = recursive,
                    Extensions = exts.Count == 0 ? null : exts.ToArray(),
                    ExcludeDirectories = excludeDirs.Count == 0 ? null : excludeDirs.ToArray(),
                    Patterns = patterns.Count == 0 ? new[] { "*" } : patterns.ToArray(),
                    MaxFileBytes = maxBytes,
                    ChunkSize = 1800,
                    ChunkOverlap = 180
                };
                DirectoryDocumentLoader<FileLoader> loader = new DirectoryDocumentLoader<FileLoader>(options);
                DirectoryIngestionStats stats = new DirectoryIngestionStats();
                loader.AttachStats(stats);
                TrackedVectorStore store = s.Branch.Store as TrackedVectorStore ?? new TrackedVectorStore();
                RecursiveCharacterTextSplitter splitter = new RecursiveCharacterTextSplitter(chunkSize: options.ChunkSize, chunkOverlap: options.ChunkOverlap);
                IReadOnlyCollection<Document> docs = await loader.LoadAsync(DataSource.FromPath(root));
                List<Vector> buffer = new List<Vector>(capacity: addEvery);
                int fileIndex = 0;
                foreach (Document doc in docs)
                {
                    if (string.IsNullOrWhiteSpace(doc.PageContent)) { fileIndex++; continue; }
                    IReadOnlyList<string> chunks = splitter.SplitText(doc.PageContent);
                    int chunkCount = chunks.Count;
                    Dictionary<string, object> baseMetadata = BuildDocumentMetadata(doc, root, fileIndex);
                    int chunkIdx = 0;
                    foreach (string chunk in chunks)
                    {
                        string vectorId = $"dir:{fileIndex}:{chunkIdx}";
                        Dictionary<string, object> chunkMetadata = BuildChunkMetadata(baseMetadata, chunkIdx, chunkCount, vectorId);
                        try
                        {
                            float[] emb = await s.Embed.CreateEmbeddingsAsync(chunk);
                            buffer.Add(new Vector { Id = vectorId, Text = chunk, Embedding = emb, Metadata = chunkMetadata });
                        }
                        catch
                        {
                            chunkMetadata["embedding"] = "fallback";
                            buffer.Add(new Vector { Id = vectorId + ":fallback", Text = chunk, Embedding = new float[8], Metadata = chunkMetadata });
                        }
                        if (buffer.Count >= addEvery)
                        {
                            await store.AddAsync(buffer);
                            buffer.Clear();
                        }
                        chunkIdx++;
                    }
                    fileIndex++;
                }
                if (buffer.Count > 0)
                {
                    await store.AddAsync(buffer);
                    buffer.Clear();
                }
                s.Branch = s.Branch.WithIngestEvent($"dir:ingest-batched:{Path.GetFileName(root)}", Array.Empty<string>());
                Console.WriteLine($"[dir-batched] {stats}");
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"dirbatched:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("UseSolution", "Solution", "UseSolutionIngest")] // Usage: Solution('maxFiles=400|maxFileBytes=600000|ext=.cs,.razor')
    public static Step<CliPipelineState, CliPipelineState> UseSolution(string? args = null)
        => async s =>
        {
            try
            {
                SolutionIngestion.SolutionIngestionOptions opts = Pipeline.Ingestion.SolutionIngestion.ParseOptions(ParseString(args));
                // Recover root path: prefer last source:set event; fallback to current directory.
                string root = Environment.CurrentDirectory;
                string? sourceEvent = s.Branch.Events
                    .OfType<IngestBatch>()
                    .Select(e => e.Source)
                    .Reverse()
                    .FirstOrDefault(src => src.StartsWith("source:set:"));
                if (sourceEvent is not null)
                {
                    string[] parts = sourceEvent.Split(':', 3);
                    if (parts.Length == 3 && Directory.Exists(parts[2])) root = parts[2];
                }
                List<Vector> vectors = await Pipeline.Ingestion.SolutionIngestion.IngestAsync(
                    s.Branch.Store as LangChainPipeline.Domain.Vectors.TrackedVectorStore ?? new LangChainPipeline.Domain.Vectors.TrackedVectorStore(),
                    root,
                    s.Embed,
                    opts);
                s.Branch = s.Branch.WithIngestEvent($"solution:ingest:{Path.GetFileName(root)}", vectors.Select(v => v.Id));
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"solution:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("UseDraft")]
    public static Step<CliPipelineState, CliPipelineState> UseDraft(string? args = null)
        => async s =>
        {
            (string topic, string query) = Normalize(s);
            Step<PipelineBranch, PipelineBranch> step = ReasoningArrows.DraftArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Draft produced");
            return s;
        };

    [PipelineToken("UseCritique")]
    public static Step<CliPipelineState, CliPipelineState> UseCritique(string? args = null)
        => async s =>
        {
            (string topic, string query) = Normalize(s);
            Step<PipelineBranch, PipelineBranch> step = ReasoningArrows.CritiqueArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Critique produced");
            return s;
        };

    [PipelineToken("UseImprove", "UseFinal")]
    public static Step<CliPipelineState, CliPipelineState> UseImprove(string? args = null)
        => async s =>
        {
            (string topic, string query) = Normalize(s);
            Step<PipelineBranch, PipelineBranch> step = ReasoningArrows.ImproveArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Improvement produced");
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
            // Check if using LiteLLM endpoint via environment variable or create streaming model
            string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
            string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
            string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) ||
                (!endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) && !endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("[streaming] Warning: Streaming requires LiteLLM endpoint (CHAT_ENDPOINT), falling back to non-streaming");
                return await UseDraft(args)(s);
            }

            // Create streaming model
            LangChainPipeline.Providers.LiteLLMChatModel streamingModel = new(endpoint, apiKey, modelName);

            (string topic, string query) = Normalize(s);
            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await ReasoningArrows.StreamingDraftArrow(streamingModel, s.Tools, s.Embed, topic, query, s.RetrievalK)
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
            string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
            string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
            string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) ||
                (!endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) && !endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("[streaming] Warning: Streaming requires LiteLLM endpoint, falling back to non-streaming");
                return await UseCritique(args)(s);
            }

            LangChainPipeline.Providers.LiteLLMChatModel streamingModel = new(endpoint, apiKey, modelName);

            (string topic, string query) = Normalize(s);
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
            string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
            string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
            string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) ||
                (!endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) && !endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("[streaming] Warning: Streaming requires LiteLLM endpoint, falling back to non-streaming");
                return await UseImprove(args)(s);
            }

            LangChainPipeline.Providers.LiteLLMChatModel streamingModel = new(endpoint, apiKey, modelName);

            (string topic, string query) = Normalize(s);
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
            string? endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
            string? apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
            string? modelName = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "gpt-oss-120b-sovereign";

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) ||
                (!endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) && !endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("[streaming] Warning: Streaming requires LiteLLM endpoint, falling back to non-streaming");
                return await UseRefinementLoop("1")(s);
            }

            LangChainPipeline.Providers.LiteLLMChatModel streamingModel = new(endpoint, apiKey, modelName);

            (string topic, string query) = Normalize(s);
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

    [PipelineToken("UseAsp", "UseControllers")]
    public static Step<CliPipelineState, CliPipelineState> UseNoopAsp(string? args = null)
        => s =>
        {
            s.Branch = s.Branch.WithIngestEvent("asp:no-op", Array.Empty<string>());
            return Task.FromResult(s);
        };

    [PipelineToken("Set", "SetPrompt", "Step<string,string>")]
    public static Step<CliPipelineState, CliPipelineState> SetPrompt(string? args = null)
        => s =>
        {
            s.Prompt = ParseString(args);
            return Task.FromResult(s);
        };

    [PipelineToken("SetTopic")]
    public static Step<CliPipelineState, CliPipelineState> SetTopic(string? args = null)
        => s =>
        {
            s.Topic = ParseString(args);
            return Task.FromResult(s);
        };

    [PipelineToken("SetQuery")]
    public static Step<CliPipelineState, CliPipelineState> SetQuery(string? args = null)
        => s =>
        {
            s.Query = ParseString(args);
            return Task.FromResult(s);
        };

    [PipelineToken("SetSource", "UseSource", "Source")]
    public static Step<CliPipelineState, CliPipelineState> SetSource(string? args = null)
        => s =>
        {
            string path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(s);
            // Expand ~ and relative paths
            string expanded = path.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~', '/', '\\'))
                : path;
            string full = Path.GetFullPath(expanded);
            string finalPath = full;
            bool accessible = false;
            try
            {
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
                string testFile = Path.Combine(full, ".__pipeline_access_test");
                using (File.Create(testFile)) { }
                File.Delete(testFile);
                accessible = true;
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"source:error:{ex.GetType().Name}:{full}", Array.Empty<string>());
            }
            if (!accessible)
            {
                string fallback = Path.Combine(Environment.CurrentDirectory, "pipeline_source_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                try
                {
                    Directory.CreateDirectory(fallback);
                    finalPath = fallback;
                }
                catch (Exception ex2)
                {
                    s.Branch = s.Branch.WithIngestEvent($"source:fallback-error:{ex2.GetType().Name}:{fallback}", Array.Empty<string>());
                }
            }
            s.Branch = s.Branch.WithSource(DataSource.FromPath(finalPath));
            s.Branch = s.Branch.WithIngestEvent($"source:set:{finalPath}", Array.Empty<string>());
            return Task.FromResult(s);
        };

    [PipelineToken("SetK", "UseK", "K")]
    public static Step<CliPipelineState, CliPipelineState> SetK(string? args = null)
        => s =>
        {
            if (!string.IsNullOrWhiteSpace(args))
            {
                Match m = Regex.Match(args, @"\s*(\d+)\s*");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int k))
                {
                    s.RetrievalK = k;
                    if (s.Trace) Console.WriteLine($"[trace] RetrievalK set to {k}");
                }
            }
            return Task.FromResult(s);
        };

    [PipelineToken("TraceOn")]
    public static Step<CliPipelineState, CliPipelineState> TraceOn(string? args = null)
        => s =>
        {
            s.Trace = true;
            Console.WriteLine("[trace] tracing enabled");
            return Task.FromResult(s);
        };

    [PipelineToken("TraceOff")]
    public static Step<CliPipelineState, CliPipelineState> TraceOff(string? args = null)
        => s =>
        {
            s.Trace = false;
            Console.WriteLine("[trace] tracing disabled");
            return Task.FromResult(s);
        };

    [PipelineToken("Zip", "UseZip")] // Usage: Zip('archive.zip|maxLines=100|binPreview=65536|noText|maxRatio=300|skip=binary|noEmbed|batch=16')
    public static Step<CliPipelineState, CliPipelineState> ZipIngest(string? args = null)
        => async s =>
        {
            string raw = ParseString(args);
            string? path = raw;
            bool includeXmlText = true;
            int csvMaxLines = 50;
            int binaryMaxBytes = 128 * 1024;
            long sizeBudget = 500 * 1024 * 1024; // 500MB default
            double maxRatio = 200d;
            HashSet<string>? skipKinds = null;
            HashSet<string>? onlyKinds = null;
            bool noEmbed = false;
            int batchSize = 16;
            // Allow modifiers separated by |, e.g. 'archive.zip|noText'
            if (!string.IsNullOrWhiteSpace(raw) && raw.Contains('|'))
            {
                string[] parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0) path = parts[0];
                foreach (string? mod in parts.Skip(1))
                {
                    if (mod.Equals("noText", StringComparison.OrdinalIgnoreCase))
                        includeXmlText = false;
                    else if (mod.Equals("noEmbed", StringComparison.OrdinalIgnoreCase))
                        noEmbed = true;
                    else if (mod.StartsWith("maxLines=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(9), out int ml))
                        csvMaxLines = ml;
                    else if (mod.StartsWith("binPreview=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(11), out int bp))
                        binaryMaxBytes = bp;
                    else if (mod.StartsWith("maxBytes=", StringComparison.OrdinalIgnoreCase) && long.TryParse(mod.AsSpan(9), out long mb))
                        sizeBudget = mb;
                    else if (mod.StartsWith("maxRatio=", StringComparison.OrdinalIgnoreCase) && double.TryParse(mod.AsSpan(9), out double mr))
                        maxRatio = mr;
                    else if (mod.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(6), out int bs) && bs > 0)
                        batchSize = bs;
                    else if (mod.StartsWith("skip=", StringComparison.OrdinalIgnoreCase))
                        skipKinds = [.. mod.Substring(5).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => v.ToLowerInvariant())];
                    else if (mod.StartsWith("only=", StringComparison.OrdinalIgnoreCase))
                        onlyKinds = [.. mod.Substring(5).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => v.ToLowerInvariant())];
                }
            }
            if (string.IsNullOrWhiteSpace(path)) return s;
            try
            {
                string full = Path.GetFullPath(path);
                if (!File.Exists(full))
                {
                    s.Branch = s.Branch.WithIngestEvent($"zip:missing:{full}", Array.Empty<string>());
                    return s;
                }
                IReadOnlyList<ZipFileRecord> scanned = await ZipIngestion.ScanAsync(full, maxTotalBytes: sizeBudget, maxCompressionRatio: maxRatio);
                IReadOnlyList<ZipFileRecord> parsed = await ZipIngestion.ParseAsync(scanned, csvMaxLines, binaryMaxBytes, includeXmlText: includeXmlText);
                List<(string id, string text)> docs = new List<(string id, string text)>();
                foreach (ZipFileRecord rec in parsed)
                {
                    if (rec.Parsed is not null && rec.Parsed.TryGetValue("type", out object? t) && t?.ToString() == "skipped")
                    {
                        s.Branch = s.Branch.WithIngestEvent($"zip:skipped:{rec.FullPath}", Array.Empty<string>());
                        continue;
                    }
                    string kindString = rec.Kind.ToString().ToLowerInvariant();
                    if (onlyKinds is not null && !onlyKinds.Contains(kindString))
                    {
                        s.Branch = s.Branch.WithIngestEvent($"zip:only-filtered:{rec.FullPath}", Array.Empty<string>());
                        continue;
                    }
                    if (skipKinds is not null && skipKinds.Contains(kindString))
                    {
                        s.Branch = s.Branch.WithIngestEvent($"zip:skip-filtered:{rec.FullPath}", Array.Empty<string>());
                        continue;
                    }
                    string text = rec.Kind switch
                    {
                        ZipContentKind.Csv => CsvToText((CsvTable)rec.Parsed!["table"]),
                        ZipContentKind.Xml => (string)(rec.Parsed!.TryGetValue("textPreview", out object? preview) ? preview ?? string.Empty : ((XmlDoc)rec.Parsed!["doc"]).Document.Root?.Value ?? string.Empty),
                        ZipContentKind.Text => (string)rec.Parsed!["preview"],
                        ZipContentKind.Binary => $"[BINARY {rec.FileName} size={rec.Length} sha256={rec.Parsed!["sha256"]}]",
                        _ => string.Empty
                    };
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    docs.Add((rec.FullPath, text));
                }

                if (noEmbed)
                {
                    foreach ((string id, string text) in docs)
                    {
                        DeferredZipTextCache.Store(id, text);
                    }
                    s.Branch = s.Branch.WithIngestEvent("zip:no-embed", docs.Select(d => d.id));
                }
                else if (!noEmbed && docs.Count > 0)
                {
                    for (int i = 0; i < docs.Count; i += batchSize)
                    {
                        List<(string id, string text)> batch = docs.Skip(i).Take(batchSize).ToList();
                        try
                        {
                            string[] texts = batch.Select(b => b.text).ToArray();
                            IReadOnlyList<float[]> emb = await s.Embed.CreateEmbeddingsAsync(texts);
                            List<Vector> vectors = new List<Vector>();
                            for (int idx = 0; idx < emb.Count; idx++)
                            {
                                (string id, string text) = batch[idx];
                                vectors.Add(new Vector { Id = id, Text = text, Embedding = emb[idx] });
                            }
                            await s.Branch.Store.AddAsync(vectors);
                        }
                        catch (Exception exBatch)
                        {
                            foreach ((string id, string _) in batch)
                            {
                                s.Branch = s.Branch.WithIngestEvent($"zip:doc-error:{id}:{exBatch.GetType().Name}", Array.Empty<string>());
                            }
                        }
                    }
                }
                s.Branch = s.Branch.WithIngestEvent($"zip:ingest:{Path.GetFileName(full)}", parsed.Select(p => p.FullPath));
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"zip:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("ZipStream")] // Streaming variant: ZipStream('archive.zip|batch=8|noText|noEmbed')
    public static Step<CliPipelineState, CliPipelineState> ZipStream(string? args = null)
        => async s =>
        {
            string raw = ParseString(args);
            if (string.IsNullOrWhiteSpace(raw)) return s;
            string path = raw.Split('|', 2)[0];
            int batchSize = 8;
            bool includeXmlText = true;
            bool noEmbed = false;
            if (raw.Contains('|'))
            {
                IEnumerable<string> mods = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1);
                foreach (string? mod in mods)
                {
                    if (mod.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(6), out int bs) && bs > 0) batchSize = bs;
                    else if (mod.Equals("noText", StringComparison.OrdinalIgnoreCase)) includeXmlText = false;
                    else if (mod.Equals("noEmbed", StringComparison.OrdinalIgnoreCase)) noEmbed = true;
                }
            }
            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) { s.Branch = s.Branch.WithIngestEvent($"zip:missing:{full}", Array.Empty<string>()); return s; }
            List<(string id, string text)> buffer = new List<(string id, string text)>();
            try
            {
                await foreach (ZipFileRecord rec in ZipIngestionStreaming.EnumerateAsync(full))
                {
                    string text;
                    if (rec.Kind == ZipContentKind.Csv || rec.Kind == ZipContentKind.Xml || rec.Kind == ZipContentKind.Text)
                    {
                        IReadOnlyList<ZipFileRecord> parsedList = await ZipIngestion.ParseAsync(new[] { rec }, csvMaxLines: 20, binaryMaxBytes: 32 * 1024, includeXmlText: includeXmlText);
                        ZipFileRecord parsed = parsedList[0];
                        text = parsed.Kind switch
                        {
                            ZipContentKind.Csv => CsvToText((CsvTable)parsed.Parsed!["table"]),
                            ZipContentKind.Xml => (string)(parsed.Parsed!.TryGetValue("textPreview", out object? preview) ? preview ?? string.Empty : ((XmlDoc)parsed.Parsed!["doc"]).Document.Root?.Value ?? string.Empty),
                            ZipContentKind.Text => (string)parsed.Parsed!["preview"],
                            _ => string.Empty
                        };
                    }
                    else
                    {
                        text = $"[BINARY {rec.FileName} size={rec.Length}]";
                    }
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (noEmbed)
                    {
                        DeferredZipTextCache.Store(rec.FullPath, text);
                        s.Branch = s.Branch.WithIngestEvent("zipstream:no-embed", new[] { rec.FullPath });
                        continue;
                    }
                    buffer.Add((rec.FullPath, text));
                    if (buffer.Count >= batchSize)
                    {
                        await EmbedBatchAsync(buffer, s);
                        buffer.Clear();
                    }
                }
                if (buffer.Count > 0 && !noEmbed)
                {
                    await EmbedBatchAsync(buffer, s);
                }
                s.Branch = s.Branch.WithIngestEvent($"zipstream:complete:{Path.GetFileName(full)}", Array.Empty<string>());
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"zipstream:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    private static async Task EmbedBatchAsync(List<(string id, string text)> batch, CliPipelineState s)
    {
        try
        {
            string[] texts = batch.Select(b => b.text).ToArray();
            IReadOnlyList<float[]> emb = await s.Embed.CreateEmbeddingsAsync(texts);
            List<Vector> vectors = new List<Vector>();
            for (int i = 0; i < emb.Count; i++)
            {
                (string id, string text) = batch[i];
                vectors.Add(new Vector { Id = id, Text = text, Embedding = emb[i] });
            }
            await s.Branch.Store.AddAsync(vectors);
        }
        catch (Exception ex)
        {
            foreach ((string id, string _) in batch)
                s.Branch = s.Branch.WithIngestEvent($"zipstream:batch-error:{id}:{ex.GetType().Name}", Array.Empty<string>());
        }
    }

    private static string CsvToText(CsvTable table)
    {
        StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(" | ", table.Header));
        foreach (string[] row in table.Rows)
            sb.AppendLine(string.Join(" | ", row));
        return sb.ToString();
    }

    [PipelineToken("ListVectors", "Vectors")] // Optional arg 'ids' to print IDs
    public static Step<CliPipelineState, CliPipelineState> ListVectors(string? args = null)
        => s =>
        {
            IEnumerable<Vector> all = s.Branch.Store switch
            {
                LangChainPipeline.Domain.Vectors.TrackedVectorStore tvs => tvs.GetAll(),
                _ => Enumerable.Empty<LangChain.Databases.Vector>()
            };
            int count = all.Count();
            Console.WriteLine($"[vectors] count={count}");
            if (!string.IsNullOrWhiteSpace(args) && args.Contains("ids", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Vector? v in all.Take(100)) Console.WriteLine($" - {v.Id}");
                if (count > 100) Console.WriteLine($" ... (truncated) ...");
            }
            return Task.FromResult(s);
        };

    [PipelineToken("EmbedZip", "ZipEmbed")] // Re-embed docs that were skipped with noEmbed
    public static Step<CliPipelineState, CliPipelineState> EmbedZip(string? args = null)
        => async s =>
        {
            int batchSize = 16;
            if (!string.IsNullOrWhiteSpace(args) && args.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(args.AsSpan(6), out int bs) && bs > 0)
                batchSize = bs;
            // Heuristic: any events zip:no-embed OR zipstream:no-embed; we can't recover original text fully unless stored; for now embed placeholders.
            List<string> pendingIds = s.Branch.Events
                .Where(e => e is IngestBatch ib && (ib.Source.StartsWith("zip:no-embed") || ib.Source.StartsWith("zipstream:no-embed")))
                .SelectMany(e => ((IngestBatch)e).Ids)
                .Distinct()
                .ToList();
            if (pendingIds.Count == 0)
            {
                Console.WriteLine("[embedzip] no deferred documents found");
                return s;
            }
            Console.WriteLine($"[embedzip] embedding {pendingIds.Count} placeholder docs");
            for (int i = 0; i < pendingIds.Count; i += batchSize)
            {
                List<string> batch = pendingIds.Skip(i).Take(batchSize).ToList();
                string[] texts = batch.Select(id =>
                {
                    if (DeferredZipTextCache.TryTake(id, out string? original) && !string.IsNullOrWhiteSpace(original)) return original;
                    return $"[DEFERRED ZIP DOC] {id}";
                }).ToArray();
                try
                {
                    IReadOnlyList<float[]> emb = await s.Embed.CreateEmbeddingsAsync(texts);
                    List<Vector> vectors = new List<Vector>();
                    for (int idx = 0; idx < emb.Count; idx++)
                    {
                        string id = batch[idx];
                        vectors.Add(new Vector { Id = id, Text = texts[idx], Embedding = emb[idx] });
                    }
                    await s.Branch.Store.AddAsync(vectors);
                }
                catch (Exception ex)
                {
                    foreach (string? id in batch)
                        s.Branch = s.Branch.WithIngestEvent($"zipembed:error:{id}:{ex.GetType().Name}", Array.Empty<string>());
                }
            }
            s.Branch = s.Branch.WithIngestEvent("zipembed:complete", pendingIds);
            return s;
        };

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        Match m = Regex.Match(arg, @"^'(?<s>.*)'$");
        if (m.Success) return m.Groups["s"].Value;
        m = Regex.Match(arg, @"^""(?<s>.*)""$");
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    // New chain-style tokens -------------------------------------------------

    [PipelineToken("RetrieveSimilarDocuments", "RetrieveDocs", "Retrieve")]
    public static Step<CliPipelineState, CliPipelineState> RetrieveSimilarDocuments(string? args = null)
        => async s =>
        {
            int amount = s.RetrievalK;
            string? overrideQuery = null;
            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("amount=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(7), out int a) && a > 0)
                        amount = a;
                    else if (part.StartsWith("query=", StringComparison.OrdinalIgnoreCase))
                        overrideQuery = part.Substring(6);
                }
            }
            string query = overrideQuery ?? (string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query);
            if (string.IsNullOrWhiteSpace(query)) return s;
            try
            {
                if (s.Branch.Store is TrackedVectorStore tvs)
                {
                    IReadOnlyCollection<Document> hits = await tvs.GetSimilarDocuments(s.Embed, query, amount);
                    s.Retrieved.Clear();
                    s.Retrieved.AddRange(hits.Select(h => h.PageContent));
                    s.Branch = s.Branch.WithIngestEvent($"retrieve:{amount}:{query.Replace('|', ':').Replace('\n', ' ')}", Enumerable.Range(0, s.Retrieved.Count).Select(i => $"doc:{i}"));
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"retrieve:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("CombineDocuments", "CombineDocs")]
    public static Step<CliPipelineState, CliPipelineState> CombineDocuments(string? args = null)
        => s =>
        {
            string raw = ParseString(args);
            string separator = "\n---\n";
            string prefix = string.Empty;
            string suffix = string.Empty;
            int take = s.Retrieved.Count;
            bool appendToPrompt = false;
            bool clearRetrieved = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = part.Substring(4);
                        separator = value.Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.StartsWith("take=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out int t) && t > 0)
                    {
                        take = Math.Min(t, s.Retrieved.Count);
                    }
                    else if (part.StartsWith("prefix=", StringComparison.OrdinalIgnoreCase))
                    {
                        prefix = part.Substring(7).Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.StartsWith("suffix=", StringComparison.OrdinalIgnoreCase))
                    {
                        suffix = part.Substring(7).Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.Equals("append", StringComparison.OrdinalIgnoreCase) || part.Equals("appendPrompt", StringComparison.OrdinalIgnoreCase))
                    {
                        appendToPrompt = true;
                    }
                    else if (part.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        clearRetrieved = true;
                    }
                }
            }

            if (take <= 0 || s.Retrieved.Count == 0)
                return Task.FromResult(s);

            List<string> blocks = s.Retrieved.Take(take).Where(static r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (blocks.Count == 0)
                return Task.FromResult(s);

            string combined = string.Join(separator, blocks);
            if (!string.IsNullOrEmpty(prefix))
                combined = prefix + combined;
            if (!string.IsNullOrEmpty(suffix))
                combined += suffix;

            s.Context = combined;
            if (appendToPrompt)
            {
                s.Prompt = string.IsNullOrWhiteSpace(s.Prompt)
                    ? combined
                    : combined + "\n\n" + s.Prompt;
            }

            if (clearRetrieved)
            {
                s.Retrieved.Clear();
            }

            return Task.FromResult(s);
        };

    [PipelineToken("Template", "UseTemplate")]
    public static Step<CliPipelineState, CliPipelineState> TemplateStep(string? args = null)
        => s =>
        {
            string templateRaw = ParseString(args);
            if (string.IsNullOrWhiteSpace(templateRaw)) return Task.FromResult(s);
            PromptTemplate pt = new PromptTemplate(templateRaw);
            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            string formatted = pt.Format(new() { ["context"] = s.Context, ["question"] = question, ["prompt"] = s.Prompt, ["topic"] = s.Topic });
            s.Prompt = formatted; // prepared for LLM
            return Task.FromResult(s);
        };

    [PipelineToken("LLM", "RunLLM")]
    public static Step<CliPipelineState, CliPipelineState> LlmStep(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(s.Prompt)) return s;
            try
            {
                (string text, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(s.Prompt);
                s.Output = text;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(text), s.Prompt, toolCalls);
                if (s.Trace) Console.WriteLine("[trace] LLM output length=" + text.Length);
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"llm:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    /// <summary>
    /// Divide-and-Conquer RAG: retrieve K docs, split into groups, answer per group, then synthesize final.
    /// Args: 'k=24|group=6|template=...|final=...|sep=\\n---\\n'
    /// If s.Retrieved is empty, it will retrieve using current Query/Prompt.
    /// </summary>
    [PipelineToken("DivideAndConquerRAG", "DCRAG", "RAGMapReduce")]
    public static Step<CliPipelineState, CliPipelineState> DivideAndConquerRag(string? args = null)
        => async s =>
        {
            int k = Math.Max(4, s.RetrievalK);
            int group = 6;
            string sep = "\n---\n";
            string? template = null;
            string? finalTemplate = null;
            bool streamPartials = false; // print intermediate outputs to console

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out int kv) && kv > 0) k = kv;
                    else if (part.StartsWith("group=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(6), out int gv) && gv > 0) group = gv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase)) template = part.Substring(9);
                    else if (part.StartsWith("final=", StringComparison.OrdinalIgnoreCase)) finalTemplate = part.Substring(6);
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) streamPartials = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        string v = part.Substring(7);
                        streamPartials = v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            if (string.IsNullOrWhiteSpace(question)) return s;

            // Ensure retrieved context
            if (s.Retrieved.Count == 0)
            {
                try { s = await RetrieveSimilarDocuments($"amount={k}")(s); } catch { /* ignore */ }
            }
            if (s.Retrieved.Count == 0) return s;

            // Defaults
            template ??= "Use the following context to answer the question. Be precise and concise.\n{context}\n\nQuestion: {question}\nAnswer:";
            finalTemplate ??= "You are to synthesize a final, precise answer from multiple partial answers.\nQuestion: {question}\n\nPartial Answers:\n{partials}\n\nFinal Answer:";

            // Partition into groups
            List<string> docs = s.Retrieved.Where(static r => !string.IsNullOrWhiteSpace(r)).Take(k).ToList();
            if (docs.Count == 0) return s;

            List<List<string>> groups = new List<List<string>>();
            for (int i = 0; i < docs.Count; i += group)
            {
                groups.Add(docs.Skip(i).Take(group).ToList());
            }

            List<string> partials = new List<string>(groups.Count);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                List<string> g = groups[gi];
                string ctx = string.Join(sep, g);
                string prompt = template!
                    .Replace("{context}", ctx)
                    .Replace("{question}", question)
                    .Replace("{prompt}", s.Prompt ?? string.Empty)
                    .Replace("{topic}", s.Topic ?? string.Empty);
                try
                {
                    (string answer, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(prompt);
                    partials.Add(answer ?? string.Empty);
                    // Record as reasoning step for traceability
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(answer ?? string.Empty), prompt, toolCalls);
                    if (streamPartials || s.Trace)
                    {
                        Console.WriteLine($"\n>> [dcrag] partial {gi + 1}/{groups.Count} (docs={g.Count})");
                        if (!string.IsNullOrWhiteSpace(answer))
                        {
                            Console.WriteLine(answer);
                        }
                        Console.Out.Flush();
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"dcrag:part-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }

            // Synthesize final
            string partialText = string.Join("\n\n---\n\n", partials.Where(p => !string.IsNullOrWhiteSpace(p)));
            string finalPrompt = finalTemplate!
                .Replace("{partials}", partialText)
                .Replace("{question}", question)
                .Replace("{prompt}", s.Prompt ?? string.Empty)
                .Replace("{topic}", s.Topic ?? string.Empty);

            try
            {
                (string finalAnswer, List<ToolExecution> finalToolCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
                s.Output = finalAnswer ?? string.Empty;
                s.Prompt = finalPrompt;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(s.Output), finalPrompt, finalToolCalls);
                if (s.Trace) Console.WriteLine($"[trace] DCRAG final length={s.Output.Length}");
                if (streamPartials)
                {
                    Console.WriteLine("\n=== DCRAG FINAL ===");
                    Console.WriteLine(s.Output);
                    Console.Out.Flush();
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"dcrag:final-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    /// <summary>
    /// Decompose-and-Aggregate RAG: decomposes the main question into sub-questions, answers each with retrieved context,
    /// then synthesizes a final unified answer. Supports streaming of sub-answers and final aggregation.
    /// Args: 'subs=4|per=6|k=24|sep=\n---\n|stream|decompose=...|template=...|final=...'
    /// - subs: number of subquestions to generate (default 4)
    /// - per: number of retrieved docs per subquestion (default 6)
    /// - k: optional initial retrieval to warm cache or pre-fill (ignored if not needed)
    /// - sep: separator for combining docs
    /// - stream: print each sub-answer and the final result
    /// - decompose: custom prompt template for subquestion generation; placeholders: {question}
    /// - template: custom prompt for answering subquestions; placeholders: {context}, {subquestion}, {question}, {prompt}, {topic}
    /// - final: custom prompt for the final synthesis; placeholders: {pairs}, {question}, {prompt}, {topic}
    /// </summary>
    [PipelineToken("DecomposeAndAggregateRAG", "DARAG", "SubQAggregate")]
    public static Step<CliPipelineState, CliPipelineState> DecomposeAndAggregateRag(string? args = null)
        => async s =>
        {
            // Defaults and args
            int subs = 4;
            int per = 6;
            int k = Math.Max(4, s.RetrievalK);
            string sep = "\n---\n";
            bool stream = false;
            string? decomposeTpl = null;
            string? subTpl = null;
            string? finalTpl = null;

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("subs=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out int sv) && sv > 0) subs = sv;
                    else if (part.StartsWith("per=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(4), out int pv) && pv > 0) per = pv;
                    else if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out int kv) && kv > 0) k = kv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) stream = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        string v = part.Substring(7);
                        stream = v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (part.StartsWith("decompose=", StringComparison.OrdinalIgnoreCase)) decomposeTpl = part.Substring(10);
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase)) subTpl = part.Substring(9);
                    else if (part.StartsWith("final=", StringComparison.OrdinalIgnoreCase)) finalTpl = part.Substring(6);
                }
            }

            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            if (string.IsNullOrWhiteSpace(question)) return s;

            // Optional: warm retrieval cache using the main question
            try { s = await RetrieveSimilarDocuments($"amount={k}|query={question.Replace("|", ":")}")(s); } catch { /* ignore */ }

            // Default templates
            decomposeTpl ??= "You are tasked with answering a complex question by breaking it down into distinct sub-questions that together fully address the original.\n" +
                             "Main question: {question}\n\n" +
                             "Return exactly {N} non-overlapping sub-questions as a numbered list (1., 2., ...), one per line, focused and specific.";

            subTpl ??= "You are answering a sub-question as part of a larger task.\n" +
                      "Main question: {question}\nSub-question: {subquestion}\n\n" +
                      "Use the following context snippets to produce a precise, thorough answer. Cite facts from context; avoid speculation.\n" +
                      "Context:\n{context}\n\n" +
                      "Answer:";

            finalTpl ??= "Synthesize a high-quality final answer to the main question by integrating the following detailed sub-answers.\n" +
                       "Provide:\n- Executive summary (3-6 bullets)\n- Integrated comprehensive answer tying together all parts\n- If relevant: Considerations and Next steps\n\n" +
                       "Main question: {question}\n\nSub-answers:\n{pairs}\n\nFinal Answer:";

            // 1) Generate sub-questions
            string decomposePrompt = decomposeTpl
                .Replace("{question}", question)
                .Replace("{N}", subs.ToString());

            List<string> subQuestions = new();
            try
            {
                (string subText, List<ToolExecution> subCalls) = await s.Llm.GenerateWithToolsAsync(decomposePrompt);
                s.Branch = s.Branch.WithReasoning(new FinalSpec(subText ?? string.Empty), decomposePrompt, subCalls);
                if (!string.IsNullOrWhiteSpace(subText))
                {
                    foreach (string line in subText.Split('\n'))
                    {
                        string t = line.Trim();
                        if (string.IsNullOrWhiteSpace(t)) continue;
                        // Accept formats like: "1. ...", "1) ...", "- ..." or plain line
                        t = Regex.Replace(t, @"^\s*(\d+\.|\d+\)|[-*])\s*", string.Empty);
                        if (!string.IsNullOrWhiteSpace(t)) subQuestions.Add(t);
                    }
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"darag:decompose-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }

            if (subQuestions.Count == 0)
            {
                // Fallback: use the original question as a single sub-question
                subQuestions.Add(question);
            }
            else if (subQuestions.Count > subs)
            {
                subQuestions = subQuestions.Take(subs).ToList();
            }

            // 2) Answer each sub-question with retrieval
            List<(string q, string a)> qaPairs = new List<(string q, string a)>(subQuestions.Count);
            for (int i = 0; i < subQuestions.Count; i++)
            {
                string sq = subQuestions[i];
                // Retrieve per sub-question
                List<string> blocks = new();
                try
                {
                    if (s.Branch.Store is TrackedVectorStore tvs)
                    {
                        IReadOnlyCollection<Document> hits = await tvs.GetSimilarDocuments(s.Embed, sq, per);
                        foreach (Document doc in hits)
                        {
                            if (!string.IsNullOrWhiteSpace(doc.PageContent))
                                blocks.Add(doc.PageContent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"darag:retrieve-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }

                string ctx = string.Join(sep, blocks);
                string subPrompt = subTpl
                    .Replace("{context}", ctx)
                    .Replace("{subquestion}", sq)
                    .Replace("{question}", question)
                    .Replace("{prompt}", s.Prompt ?? string.Empty)
                    .Replace("{topic}", s.Topic ?? string.Empty);
                try
                {
                    (string ans, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(subPrompt);
                    string answer = ans ?? string.Empty;
                    qaPairs.Add((sq, answer));
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(answer), subPrompt, toolCalls);
                    if (stream || s.Trace)
                    {
                        Console.WriteLine($"\n>> [darag] sub {i + 1}/{subQuestions.Count}: {sq}");
                        if (!string.IsNullOrWhiteSpace(answer)) Console.WriteLine(answer);
                        Console.Out.Flush();
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"darag:sub-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }

            // 3) Final synthesis
            StringBuilder sbPairs = new StringBuilder();
            for (int i = 0; i < qaPairs.Count; i++)
            {
                (string q, string a) = qaPairs[i];
                sbPairs.AppendLine($"Sub-question {i + 1}: {q}");
                sbPairs.AppendLine("Answer:");
                sbPairs.AppendLine(a);
                sbPairs.AppendLine();
            }

            string finalPrompt = finalTpl
                .Replace("{pairs}", sbPairs.ToString())
                .Replace("{question}", question)
                .Replace("{prompt}", s.Prompt ?? string.Empty)
                .Replace("{topic}", s.Topic ?? string.Empty);

            try
            {
                (string finalAnswer, List<ToolExecution> finalCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
                s.Output = finalAnswer ?? string.Empty;
                s.Prompt = finalPrompt;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(s.Output), finalPrompt, finalCalls);
                if (s.Trace) Console.WriteLine($"[trace] DARAG final length={s.Output.Length}");
                if (stream)
                {
                    Console.WriteLine("\n=== DARAG FINAL ===");
                    Console.WriteLine(s.Output);
                    Console.Out.Flush();
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"darag:final-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }

            return s;
        };

    [PipelineToken("EnhanceMarkdown", "ImproveMarkdown", "RewriteMarkdown")]
    public static Step<CliPipelineState, CliPipelineState> EnhanceMarkdown(string? args = null)
        => async s =>
        {
            Dictionary<string, string> options = ParseKeyValueArgs(args);
            if (!options.TryGetValue("file", out string? fileValue) || string.IsNullOrWhiteSpace(fileValue))
            {
                s.Branch = s.Branch.WithIngestEvent("markdown:error:no-file", Array.Empty<string>());
                return s;
            }

            int iterations = 1;
            if (options.TryGetValue("iterations", out string? iterationsRaw) && int.TryParse(iterationsRaw, out int parsedIterations) && parsedIterations > 0)
            {
                iterations = Math.Min(parsedIterations, 10);
            }

            int contextCount = s.RetrievalK;
            if (options.TryGetValue("context", out string? contextRaw) && int.TryParse(contextRaw, out int parsedContext) && parsedContext >= 0)
            {
                contextCount = parsedContext;
            }
            contextCount = Math.Clamp(contextCount, 0, 16);

            bool createBackup = true;
            if (options.TryGetValue("backup", out string? backupRaw))
            {
                createBackup = ParseBool(backupRaw, true);
            }

            string? goal = options.TryGetValue("goal", out string? goalValue) ? goalValue : null;
            goal = ChooseFirstNonEmpty(goal, s.Prompt, s.Topic, s.Query);

            string basePath = s.Branch.Source.Value as string ?? Environment.CurrentDirectory;
            string resolvedFile = Path.IsPathRooted(fileValue)
                ? Path.GetFullPath(fileValue)
                : Path.GetFullPath(Path.Combine(basePath, fileValue));

            if (!File.Exists(resolvedFile))
            {
                s.Branch = s.Branch.WithIngestEvent($"markdown:missing:{resolvedFile}", Array.Empty<string>());
                return s;
            }

            if (createBackup)
            {
                try
                {
                    string backupPath = resolvedFile + ".bak";
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(resolvedFile, backupPath, overwrite: false);
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"markdown:backup-failed:{ex.GetType().Name}:{resolvedFile}", Array.Empty<string>());
                }
            }

            for (int iteration = 1; iteration <= iterations; iteration++)
            {
                string original = await File.ReadAllTextAsync(resolvedFile);
                List<string> contextBlocks = await BuildMarkdownContextAsync(s, resolvedFile, goal, contextCount);
                string prompt = BuildMarkdownRewritePrompt(resolvedFile, original, goal, contextBlocks, iteration, iterations);

                try
                {
                    (string response, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(prompt);
                    string improved = NormalizeMarkdownOutput(response);
                    if (string.IsNullOrWhiteSpace(improved))
                    {
                        improved = original;
                    }

                    bool changed = !string.Equals(improved.Trim(), original.Trim(), StringComparison.Ordinal);
                    if (changed)
                    {
                        await File.WriteAllTextAsync(resolvedFile, improved);
                    }

                    DocumentRevision revision = new DocumentRevision(resolvedFile, improved, iteration, goal);
                    s.Branch = s.Branch.WithReasoning(revision, prompt, toolCalls);
                    s.Output = improved;
                    s.Context = string.Join("\n---\n", contextBlocks);

                    if (!changed)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"markdown:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                    break;
                }
            }

            return s;
        };

    [PipelineToken("SwitchModel", "Model")] // Usage: SwitchModel('model=gpt-4o-mini|embed=text-embedding-3-small|remote')
    public static Step<CliPipelineState, CliPipelineState> SwitchModel(string? args = null)
        => async s =>
        {
            await Task.Yield(); // ensure truly async to satisfy analyzer (treat warnings as errors)
            // Parse args
            string? newModel = null; string? newEmbed = null; bool forceRemote = false;
            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("model=", StringComparison.OrdinalIgnoreCase)) newModel = part.Substring(6);
                    else if (part.StartsWith("embed=", StringComparison.OrdinalIgnoreCase)) newEmbed = part.Substring(6);
                    else if (part.Equals("remote", StringComparison.OrdinalIgnoreCase)) forceRemote = true;
                }
            }
            if (string.IsNullOrWhiteSpace(newModel) && string.IsNullOrWhiteSpace(newEmbed)) return s; // nothing to do
            // Rebuild chat model similar to Program.cs logic but simplified, prioritizing remote if key present OR remote flag
            (string? endpoint, string? key, ChatEndpointType endpointType) = ChatConfig.Resolve();
            IChatCompletionModel? model = null;
            if (!string.IsNullOrWhiteSpace(key) && (forceRemote || !string.IsNullOrWhiteSpace(newModel)))
            {
                try
                {
                    string baseUrl = string.IsNullOrWhiteSpace(endpoint) ? "https://api.openai.com" : endpoint!;
                    // Create appropriate model based on detected endpoint type
                    model = endpointType switch
                    {
                        ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(baseUrl, key!, newModel ?? "llama3.2", new ChatRuntimeSettings()),
                        ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(baseUrl, key!, newModel ?? "gpt-4o-mini", new ChatRuntimeSettings()),
                        ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(baseUrl, key!, newModel ?? "gpt-4o-mini", new ChatRuntimeSettings()),
                        _ => new HttpOpenAiCompatibleChatModel(baseUrl, key!, newModel ?? "gpt-4o-mini", new ChatRuntimeSettings())
                    };
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"switchmodel:remote-fail:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }
            if (model == null && !string.IsNullOrWhiteSpace(newModel))
            {
                OllamaProvider provider = new OllamaProvider();
                OllamaChatModel oc = new OllamaChatModel(provider, newModel);
                if (newModel == "deepseek-coder:33b") oc.Settings = OllamaPresets.DeepSeekCoder33B;
                model = new OllamaChatAdapter(oc);
            }
            if (model != null)
            {
                s.Llm = new ToolAwareChatModel(model, s.Tools); // preserve existing tool registry
                s.Branch = s.Branch.WithIngestEvent($"switchmodel:chat:{newModel}", Array.Empty<string>());
            }

            if (!string.IsNullOrWhiteSpace(newEmbed))
            {
                try
                {
                    OllamaProvider provider = new OllamaProvider();
                    OllamaEmbeddingModel oe = new OllamaEmbeddingModel(provider, newEmbed);
                    s.Embed = new LangChainPipeline.Providers.OllamaEmbeddingAdapter(oe);
                    s.Branch = s.Branch.WithIngestEvent($"switchmodel:embed:{newEmbed}", Array.Empty<string>());
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"switchmodel:embed-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }
            return s;
        };

    [PipelineToken("UseChain", "Chain")] // Usage: UseChain('name=myChain|in=Prompt,Query|out=Output|trace')
    public static Step<CliPipelineState, CliPipelineState> UseExternalChain(string? args = null)
        => async s =>
        {
            string raw = ParseString(args);
            if (string.IsNullOrWhiteSpace(raw)) return s;
            string? name = null; string[] inKeys = Array.Empty<string>(); string[] outKeys = ["Output"]; bool trace = false;
            foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith("name=", StringComparison.OrdinalIgnoreCase)) name = part.Substring(5);
                else if (part.StartsWith("in=", StringComparison.OrdinalIgnoreCase)) inKeys = part.Substring(3).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                else if (part.StartsWith("out=", StringComparison.OrdinalIgnoreCase)) outKeys = part.Substring(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                else if (part.Equals("trace", StringComparison.OrdinalIgnoreCase)) trace = true;
            }
            if (string.IsNullOrWhiteSpace(name)) { s.Branch = s.Branch.WithIngestEvent("chain:error:no-name", Array.Empty<string>()); return s; }
            if (!ExternalChainRegistry.TryGet(name, out object? chain) || chain is null)
            {
                s.Branch = s.Branch.WithIngestEvent($"chain:error:not-found:{name}", Array.Empty<string>());
                return s;
            }
            try
            {
                // Locate CallAsync via reflection
                Type type = chain.GetType();
                MethodInfo? call = type.GetMethod("CallAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (call is null)
                {
                    s.Branch = s.Branch.WithIngestEvent($"chain:error:no-call:{name}", Array.Empty<string>()); return s;
                }
                // Try to create StackableChainValues if present
                object valuesObj;
                Type? valuesType = Type.GetType("LangChain.Chains.StackableChains.Context.StackableChainValues, LangChain");
                if (valuesType is not null)
                {
                    valuesObj = Activator.CreateInstance(valuesType)!;
                }
                else
                {
                    // fallback: simple dictionary holder
                    valuesObj = new Dictionary<string, object?>();
                }
                // Attempt to set Hook=null and populate Value dict
                IDictionary<string, object?>? dict = null;
                PropertyInfo? valueProp = valuesObj.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (valueProp?.GetValue(valuesObj) is IDictionary<string, object?> existing)
                {
                    dict = existing;
                }
                else if (valuesObj is IDictionary<string, object?> fallbackDict)
                {
                    dict = fallbackDict;
                }
                // export selected keys
                if (dict is not null)
                {
                    foreach (string key in inKeys)
                    {
                        string? val = key switch
                        {
                            "Prompt" => s.Prompt,
                            "Query" => s.Query,
                            "Topic" => s.Topic,
                            "Context" => s.Context,
                            "Output" => s.Output,
                            _ => null
                        };
                        if (val is not null) dict[key] = val;
                    }
                }
                if (trace) Console.WriteLine($"[chain] {name} export={dict?.Count ?? 0}");
                object? taskObj = call.Invoke(chain, [valuesObj]);
                if (taskObj is Task t) await t.ConfigureAwait(false);
                // if Task<T> try to get Result object as updated values
                if (taskObj?.GetType().IsGenericType == true && taskObj.GetType().GetProperty("Result") is { } rp)
                {
                    valuesObj = rp.GetValue(taskObj) ?? valuesObj;
                }
                // Re-read dictionary (some implementations mutate Value in place)
                if (valueProp?.GetValue(valuesObj) is IDictionary<string, object?> dict2) dict = dict2;
                // import
                if (dict is not null)
                {
                    foreach (string key in outKeys)
                    {
                        if (dict.TryGetValue(key, out object? v) && v is not null)
                        {
                            switch (key)
                            {
                                case "Prompt": s.Prompt = v.ToString()!; break;
                                case "Query": s.Query = v.ToString()!; break;
                                case "Topic": s.Topic = v.ToString()!; break;
                                case "Context": s.Context = v.ToString()!; break;
                                case "Output": s.Output = v.ToString()!; break;
                            }
                        }
                    }
                }
                if (trace) Console.WriteLine($"[chain] {name} import keys={string.Join(',', outKeys)}");
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"chain:error:{name}:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("VectorStats")] // Quick stats about vectors
    public static Step<CliPipelineState, CliPipelineState> VectorStats(string? args = null)
        => s =>
        {
            IEnumerable<Vector> all = s.Branch.Store is TrackedVectorStore tvs ? tvs.GetAll() : Enumerable.Empty<Vector>();
            int count = 0;
            double sumNorm = 0;
            foreach (Vector v in all)
            {
                count++;
                if (v.Embedding is { Length: > 0 })
                {
                    double norm = 0;
                    foreach (float f in v.Embedding) norm += f * f;
                    sumNorm += Math.Sqrt(norm);
                }
            }
            double avgNorm = count == 0 ? 0 : sumNorm / count;
            Console.WriteLine($"[vectorstats] count={count} avgNorm={avgNorm:F3}");
            return Task.FromResult(s);
        };

    [PipelineToken("GenerateTokenDocs")] // regenerate docs/TOKENS.md
    public static Step<CliPipelineState, CliPipelineState> GenerateTokenDocs(string? args = null)
        => s =>
        {
            var groups = StepRegistry.GetTokenGroups()
                .Select(g => new { g.Method, g.Names })
                .OrderBy(g => g.Names.First(), StringComparer.OrdinalIgnoreCase);
            List<string> lines = new List<string>
            {
                "# Pipeline Tokens Index",
                "",
                "| Token(s) | Declaring Method |",
                "|----------|------------------|"
            };
            foreach (var g in groups)
            {
                lines.Add($"| {string.Join(", ", g.Names)} | {g.Method.DeclaringType?.Name}.{g.Method.Name}() |");
            }
            string docPath = Path.Combine(Environment.CurrentDirectory, "docs", "TOKENS.md");
            Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
            File.WriteAllText(docPath, string.Join(Environment.NewLine, lines));
            Console.WriteLine($"[tokendocs] updated {docPath}");
            return Task.FromResult(s);
        };

    /// <summary>
    /// Guided installation step for missing dependencies.
    /// When executed, this step can validate or install missing dependencies interactively.
    /// </summary>
    /// <param name="args">Optional parameters for dependency validation</param>
    /// <returns>A step that processes dependency installation</returns>
    [PipelineToken("InstallDependenciesGuided", "MissingDependencies", "ValidateMissingDependencies")]
    public static Step<CliPipelineState, CliPipelineState> InstallDependenciesGuided(string? args = null)
        => async s =>
        {
            await Task.Yield(); // Ensure truly async

            string raw = ParseString(args);
            string? dependencyName = null;
            string? errorMessage = null;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("dep=", StringComparison.OrdinalIgnoreCase))
                        dependencyName = part.Substring(4);
                    else if (part.StartsWith("error=", StringComparison.OrdinalIgnoreCase))
                        errorMessage = part.Substring(6);
                }
            }

            Console.WriteLine("[guided-install] Dependency installation step triggered");

            if (!string.IsNullOrWhiteSpace(dependencyName))
            {
                Console.WriteLine($"[guided-install] Missing dependency: {dependencyName}");
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                Console.WriteLine($"[guided-install] Error context: {errorMessage}");
            }

            // Record the guided installation event
            string eventSource = string.IsNullOrWhiteSpace(dependencyName)
                ? "guided-install:triggered:generic"
                : $"guided-install:triggered:{dependencyName}";

            s.Branch = s.Branch.WithIngestEvent(eventSource, Array.Empty<string>());

            Console.WriteLine("[guided-install] To install dependencies, please run the appropriate package manager:");
            Console.WriteLine("  - For .NET: dotnet restore");
            Console.WriteLine("  - For npm: npm install");
            Console.WriteLine("  - For Python: pip install -r requirements.txt");

            return s;
        };

    private static Dictionary<string, string> ParseKeyValueArgs(string? args)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string raw = ParseString(args);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return map;
        }

        foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int idx = part.IndexOf('=');
            if (idx > 0)
            {
                string key = part.Substring(0, idx).Trim();
                string value = part.Substring(idx + 1).Trim();
                map[key] = value;
            }
            else
            {
                map[part.Trim()] = "true";
            }
        }

        return map;
    }

    private static bool ParseBool(string raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (bool.TryParse(raw, out bool parsed)) return parsed;
        if (int.TryParse(raw, out int numeric)) return numeric != 0;

        return raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("y", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("on", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("enable", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("enabled", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ChooseFirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));

    private static async Task<List<string>> BuildMarkdownContextAsync(
        CliPipelineState state,
        string filePath,
        string? goal,
        int count)
    {
        List<string> blocks = new List<string>();
        if (count <= 0) return blocks;
        if (state.Branch.Store is not TrackedVectorStore store) return blocks;

        string? query = ChooseFirstNonEmpty(goal, Path.GetFileNameWithoutExtension(filePath), state.Topic, state.Query, state.Prompt);
        query ??= Path.GetFileName(filePath);

        try
        {
            IReadOnlyCollection<Document> docs = await store.GetSimilarDocuments(state.Embed, query, count);
            foreach (Document? doc in docs)
            {
                if (doc is null || string.IsNullOrWhiteSpace(doc.PageContent))
                {
                    continue;
                }

                IDictionary<string, object> metadata = doc.Metadata ?? new Dictionary<string, object>();
                if (metadata.TryGetValue("source", out object? sourceObj) && sourceObj is string src && PathsEqual(src, filePath))
                {
                    continue;
                }

                string sourceLabel = metadata.TryGetValue("relative", out object? relObj) && relObj is string rel && !string.IsNullOrWhiteSpace(rel)
                    ? rel
                    : metadata.TryGetValue("source", out object? srcObj2) && srcObj2 is string src2
                        ? src2
                        : "unknown source";

                blocks.Add($"Source: {sourceLabel}\n{Truncate(doc.PageContent, 1200)}");

                if (blocks.Count >= count)
                {
                    break;
                }
            }
        }
        catch
        {
            // Retrieval context is optional; ignore failures.
        }

        return blocks;
    }

    private static string BuildMarkdownRewritePrompt(
        string filePath,
        string original,
        string? goal,
        List<string> contextBlocks,
        int iteration,
        int totalIterations)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("You are an expert technical writer and software engineer.");
        sb.AppendLine($"File path: {filePath}");
        sb.AppendLine($"Iteration: {iteration} of {Math.Max(totalIterations, 1)}.");
        if (!string.IsNullOrWhiteSpace(goal))
        {
            sb.AppendLine($"Primary objective: {goal}");
        }

        sb.AppendLine("Revise the markdown to improve clarity, accuracy, and usefulness while preserving correct facts.");
        sb.AppendLine("Apply these rules:");
        sb.AppendLine("- Keep markdown syntax valid and consistent.");
        sb.AppendLine("- Promote clear headings, ordered steps, and actionable guidance.");
        sb.AppendLine("- Integrate important insights from the provided context when relevant.");
        sb.AppendLine("- Remove redundancies, fix typography, and ensure concise language.");
        sb.AppendLine("- Respond with the complete rewritten markdown only; don't wrap it in fences or add narration.");

        if (contextBlocks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Supporting context:");
            foreach (string block in contextBlocks)
            {
                sb.AppendLine("---");
                sb.AppendLine(block);
            }
        }

        sb.AppendLine();
        sb.AppendLine("Current markdown:");
        sb.AppendLine("```markdown");
        sb.AppendLine(original);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Return the rewritten markdown now (no backticks, no extra commentary).");
        return sb.ToString();
    }

    private static string NormalizeMarkdownOutput(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        string text = raw.Trim();

        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = text.IndexOf('\n');
            if (firstLineBreak >= 0)
            {
                text = text[(firstLineBreak + 1)..];
            }
            int closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                text = text[..closingFence];
            }
        }

        if (text.StartsWith("Updated Markdown:", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring("Updated Markdown:".Length).TrimStart();
        }

        return text.Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "…";
    }

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string TryGetRelativePath(string root, string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return Path.GetRelativePath(root, path);
        }
        catch
        {
            return path;
        }
    }

    private static Dictionary<string, object> BuildDocumentMetadata(Document doc, string root, int fileIndex)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (doc.Metadata is not null)
        {
            foreach (KeyValuePair<string, object> kvp in doc.Metadata)
            {
                metadata[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }

        string? sourcePath = null;
        if (metadata.TryGetValue("source", out object? sourceObj) && sourceObj is string sourceStr)
        {
            sourcePath = sourceStr;
        }
        else if (metadata.TryGetValue("path", out object? pathObj) && pathObj is string pathStr)
        {
            sourcePath = pathStr;
        }

        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            try
            {
                sourcePath = Path.GetFullPath(sourcePath);
            }
            catch
            {
                // ignore invalid paths
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            sourcePath = Path.Combine(root, $"document_{fileIndex}.md");
        }

        metadata["source"] = sourcePath;
        metadata["relative"] = TryGetRelativePath(root, sourcePath);
        return metadata;
    }

    private static Dictionary<string, object> BuildChunkMetadata(
        Dictionary<string, object> baseMetadata,
        int chunkIndex,
        int chunkCount,
        string vectorId)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>(baseMetadata, StringComparer.OrdinalIgnoreCase)
        {
            ["chunkIndex"] = chunkIndex,
            ["chunkCount"] = chunkCount,
            ["vectorId"] = vectorId
        };
        return metadata;
    }

    /// <summary>
    /// Helper method to handle dependency exceptions by scheduling guided-install step via ingest-event.
    /// Instead of calling Environment.Exit, this method emits an ingest-event to schedule the guided installation step.
    /// </summary>
    /// <param name="state">The current CLI pipeline state</param>
    /// <param name="exception">The exception that occurred</param>
    /// <returns>Updated pipeline state with appropriate ingest events</returns>
    private static async Task<CliPipelineState> HandleDependencyExceptionAsync(CliPipelineState state, Exception exception)
    {
        await Task.Yield(); // Ensure truly async

        string exceptionMessage = exception.Message;
        string exceptionType = exception.GetType().Name;

        // Known dependency-related exception patterns
        Dictionary<string, string[]> dependencyPatterns = new Dictionary<string, string[]>
        {
            ["NuGet"] = new[] { "nuget", "package", "restore", "project.assets.json" },
            ["NPM"] = new[] { "npm", "node_modules", "package.json" },
            ["Python"] = new[] { "pip", "requirements.txt", "python package" },
            ["Docker"] = new[] { "docker", "container", "image not found" },
            ["Ollama"] = new[] { "ollama", "model not found", "pull model" },
            ["LangChain"] = new[] { "langchain", "provider not found" }
        };

        // Check if exception matches known dependency patterns
        string? matchedDependency = null;
        foreach ((string depName, string[] patterns) in dependencyPatterns)
        {
            if (patterns.Any(pattern => exceptionMessage.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                matchedDependency = depName;
                break;
            }
        }

        if (matchedDependency != null)
        {
            // Schedule guided-install step via ingest-event for known dependency issues
            Console.WriteLine($"[dependency-handler] Detected {matchedDependency} dependency issue");

            string eventSource = $"dependency:missing:{matchedDependency}:{exceptionType}";
            state.Branch = state.Branch.WithIngestEvent(eventSource, Array.Empty<string>());

            // Schedule the guided installation step to run
            string scheduleEvent = $"schedule:guided-install|dep={matchedDependency}|error={exceptionMessage.Replace('|', ':')}";
            state.Branch = state.Branch.WithIngestEvent(scheduleEvent, Array.Empty<string>());

            Console.WriteLine($"[dependency-handler] Scheduled guided installation for {matchedDependency}");
            Console.WriteLine($"[dependency-handler] You can manually run: InstallDependenciesGuided('dep={matchedDependency}')");
        }
        else
        {
            // For non-dependency errors, record generic error event (preserve previous behavior)
            string errorEvent = $"error:generic:{exceptionType}:{exceptionMessage.Replace('|', ':')}";
            state.Branch = state.Branch.WithIngestEvent(errorEvent, Array.Empty<string>());

            Console.WriteLine($"[dependency-handler] Recorded generic error: {exceptionType}");
        }

        return state;
    }

    // ============================================================================
    // LangChain Native Pipe Operators
    // These steps leverage the original LangChain.Chains.Chain static helpers
    // to provide direct access to LangChain's pipe operator pattern
    // ============================================================================

    /// <summary>
    /// Uses LangChain's native Chain.Set() operator to set a value in the chain context.
    /// Wraps the LangChain operator for use in the monadic pipeline system.
    /// </summary>
    /// <param name="args">Format: 'value|key' where key defaults to 'text' if not specified</param>
    [PipelineToken("LangChainSet", "ChainSet")]
    public static Step<CliPipelineState, CliPipelineState> LangChainSetStep(string? args = null)
    {
        string raw = ParseString(args);
        string[] parts = raw?.Split('|', 2, StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        string value = parts.Length > 0 ? parts[0] : string.Empty;
        string key = parts.Length > 1 ? parts[1] : "text";

        LangChain.Chains.HelperChains.SetChain chain = LangChain.Chains.Chain.Set(value, key);
        return chain.ToStep(
            inputKeys: Array.Empty<string>(),
            outputKeys: new[] { key },
            trace: false);
    }

    /// <summary>
    /// Uses LangChain's native Chain.RetrieveSimilarDocuments() operator.
    /// Retrieves similar documents from the vector store using the query.
    /// </summary>
    /// <param name="args">Optional: 'amount=5'</param>
    [PipelineToken("LangChainRetrieve", "ChainRetrieve")]
    public static Step<CliPipelineState, CliPipelineState> LangChainRetrieveStep(string? args = null)
        => async s =>
        {
            int amount = 5;

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                Match m = Regex.Match(raw, @"amount=(\d+)");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int amt))
                    amount = amt;
            }

            // Get vector collection from the branch store
            if (s.Branch.Store is not IVectorCollection vectorCollection)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainRetrieve: store is not IVectorCollection");
                return s;
            }

            // Set the input text from Query or Prompt
            string query = string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainRetrieve: no query text");
                return s;
            }

            // Retrieve using the vector collection directly
            try
            {
                // Create embedding for the query
                float[] queryEmbedding = await s.Embed.CreateEmbeddingsAsync(query);

                // Create search request
                VectorSearchRequest request = new LangChain.Databases.VectorSearchRequest
                {
                    Embeddings = new[] { queryEmbedding }
                };

                VectorSearchSettings settings = new LangChain.Databases.VectorSearchSettings
                {
                    NumberOfResults = amount
                };

                // Search in vector store
                VectorSearchResponse results = await vectorCollection.SearchAsync(request, settings);

                s.Retrieved.Clear();
                foreach (Vector result in results.Items)
                {
                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        s.Retrieved.Add(result.Text);
                    }
                }

                if (s.Trace) Console.WriteLine($"[trace] LangChainRetrieve: retrieved {s.Retrieved.Count} documents");
            }
            catch (Exception ex)
            {
                if (s.Trace) Console.WriteLine($"[trace] LangChainRetrieve: error {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Uses LangChain's native Chain.CombineDocuments() operator.
    /// Combines documents from the retrieved list into a single context string.
    /// </summary>
    /// <param name="args">Optional: 'inputKey=documents|outputKey=context'</param>
    [PipelineToken("LangChainCombine", "ChainCombine")]
    public static Step<CliPipelineState, CliPipelineState> LangChainCombineStep(string? args = null)
        => async s =>
        {
            string inputKey = "documents";
            string outputKey = "context";

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("inputKey=", StringComparison.OrdinalIgnoreCase))
                        inputKey = part.Substring(9);
                    else if (part.StartsWith("outputKey=", StringComparison.OrdinalIgnoreCase))
                        outputKey = part.Substring(10);
                }
            }

            LangChain.Chains.HelperChains.StuffDocumentsChain chain = LangChain.Chains.Chain.CombineDocuments(inputKey, outputKey);

            // Prepare documents from Retrieved list
            List<Document> docs = s.Retrieved
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(text => new Document { PageContent = text })
                .ToList();

            if (docs.Count == 0)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainCombine: no documents to combine");
                return s;
            }

            StackableChainValues values = new StackableChainValues();
            values.Value[inputKey] = docs;

            LangChain.Abstractions.Schema.IChainValues result = await chain.CallAsync(values);

            if (result.Value.TryGetValue(outputKey, out object? context))
            {
                s.Context = context?.ToString() ?? string.Empty;
                if (s.Trace) Console.WriteLine($"[trace] LangChainCombine: combined context length={s.Context.Length}");
            }

            return s;
        };

    /// <summary>
    /// Uses LangChain's native Chain.Template() operator.
    /// Applies a prompt template with variable substitution.
    /// </summary>
    /// <param name="args">Template string with {variable} placeholders</param>
    [PipelineToken("LangChainTemplate", "ChainTemplate")]
    public static Step<CliPipelineState, CliPipelineState> LangChainTemplateStep(string? args = null)
        => async s =>
        {
            string templateText = ParseString(args);
            if (string.IsNullOrWhiteSpace(templateText))
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainTemplate: no template provided");
                return s;
            }

            LangChain.Chains.HelperChains.PromptChain chain = LangChain.Chains.Chain.Template(templateText, "text");

            StackableChainValues values = new StackableChainValues();
            values.Value["context"] = s.Context;
            values.Value["text"] = string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query;
            values.Value["question"] = values.Value["text"];
            values.Value["input"] = values.Value["text"];
            values.Value["prompt"] = s.Prompt;
            values.Value["topic"] = s.Topic;
            values.Value["query"] = s.Query;

            LangChain.Abstractions.Schema.IChainValues result = await chain.CallAsync(values);

            if (result.Value.TryGetValue("text", out object? output))
            {
                s.Prompt = output?.ToString() ?? string.Empty;
                if (s.Trace) Console.WriteLine($"[trace] LangChainTemplate: formatted prompt length={s.Prompt.Length}");
            }

            return s;
        };

    /// <summary>
    /// Uses LangChain's native Chain.LLM() operator.
    /// Sends the prompt to the language model using LangChain's chain system.
    /// </summary>
    /// <param name="args">Optional: 'inputKey=text|outputKey=text'</param>
    [PipelineToken("LangChainLLM", "ChainLLM")]
    public static Step<CliPipelineState, CliPipelineState> LangChainLlmStep(string? args = null)
        => async s =>
        {
            string inputKey = "text";
            string outputKey = "text";

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("inputKey=", StringComparison.OrdinalIgnoreCase))
                        inputKey = part.Substring(9);
                    else if (part.StartsWith("outputKey=", StringComparison.OrdinalIgnoreCase))
                        outputKey = part.Substring(10);
                }
            }

            // Extract the underlying IChatModel from ToolAwareChatModel
            FieldInfo? llmField = s.Llm.GetType().GetField("_model", BindingFlags.NonPublic | BindingFlags.Instance);
            if (llmField == null)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainLLM: cannot access underlying chat model");
                return s;
            }

            IChatModel? chatModel = llmField.GetValue(s.Llm) as IChatModel;
            if (chatModel == null)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainLLM: chat model is null");
                return s;
            }

            LangChain.Chains.HelperChains.LLMChain chain = LangChain.Chains.Chain.LLM(chatModel, inputKey, outputKey);

            StackableChainValues values = new StackableChainValues();
            values.Value[inputKey] = s.Prompt;

            LangChain.Abstractions.Schema.IChainValues result = await chain.CallAsync(values);

            if (result.Value.TryGetValue(outputKey, out object? output))
            {
                s.Output = output?.ToString() ?? string.Empty;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(s.Output), s.Prompt, null);
                if (s.Trace) Console.WriteLine($"[trace] LangChainLLM: output length={s.Output.Length}");
            }

            return s;
        };

    /// <summary>
    /// Creates a complete RAG pipeline using LangChain native operators.
    /// This demonstrates the pivot example pattern: Set | Retrieve | Combine | Template | LLM
    /// </summary>
    /// <param name="args">Optional: 'question=...|template=...|k=5'</param>
    [PipelineToken("LangChainRAG", "ChainRAG")]
    public static Step<CliPipelineState, CliPipelineState> LangChainRagPipeline(string? args = null)
        => async s =>
        {
            string? question = null;
            string? template = null;
            int k = 5;

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("question=", StringComparison.OrdinalIgnoreCase))
                        question = part.Substring(9);
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase))
                        template = part.Substring(9);
                    else if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out int kVal))
                        k = kVal;
                }
            }

            // Use default question and template if not provided
            question ??= string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query;
            template ??= @"Use the following pieces of context to answer the question at the end. If the answer is not in context then just say that you don't know, don't try to make up an answer. Keep the answer as short as possible.
{context}
Question: {text}
Helpful Answer:";

            if (string.IsNullOrWhiteSpace(question))
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainRAG: no question provided");
                return s;
            }

            // Set the question
            s.Query = question;
            s.Prompt = question;

            // Execute the pipeline: Retrieve | Combine | Template | LLM
            s = await LangChainRetrieveStep($"amount={k}")(s);
            s = await LangChainCombineStep()(s);
            s = await LangChainTemplateStep($"'{template}'")(s);
            s = await LangChainLlmStep()(s);

            return s;
        };
}
