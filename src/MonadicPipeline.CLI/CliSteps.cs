using System.Reflection; // for BindingFlags
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
using LangChainPipeline.Options;
using LangChainPipeline.Pipeline.Ingestion.Zip;
using MonadicPipeline.CLI.Setup;

namespace LangChainPipeline.CLI;

/// <summary>
/// Discoverable CLI pipeline steps. Each method is annotated with PipelineToken and returns a Step over CliPipelineState.
/// Parsing of simple args is supported via optional string? args parameter.
/// </summary>
public static class CliSteps
{
    private static (string topic, string query) Normalize(CliPipelineState s)
    {
        var topic = string.IsNullOrWhiteSpace(s.Topic) ? (string.IsNullOrWhiteSpace(s.Prompt) ? "topic" : s.Prompt) : s.Topic;
        var query = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? topic : s.Prompt) : s.Query;
        return (topic, query);
    }

    [PipelineToken("UseIngest")]
    public static Step<CliPipelineState, CliPipelineState> UseIngest(string? args = null)
        => async s =>
        {
            try
            {
                var ingest = IngestionArrows.IngestArrow<FileLoader>(s.Embed, tag: "cli");
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
            var exts = new List<string>();
            var excludeDirs = new List<string>();
            var patterns = new List<string>();
            long maxBytes = 0;
            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("root=", StringComparison.OrdinalIgnoreCase)) root = Path.GetFullPath(part.Substring(5));
                    else if (part.StartsWith("ext=", StringComparison.OrdinalIgnoreCase)) exts.AddRange(part.Substring(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("exclude=", StringComparison.OrdinalIgnoreCase)) excludeDirs.AddRange(part.Substring(8).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("pattern=", StringComparison.OrdinalIgnoreCase)) patterns.AddRange(part.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("max=", StringComparison.OrdinalIgnoreCase) && long.TryParse(part.AsSpan(4), out var m)) maxBytes = m;
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
                var options = new DirectoryIngestionOptions
                {
                    Recursive = recursive,
                    Extensions = exts.Count == 0 ? null : exts.ToArray(),
                    ExcludeDirectories = excludeDirs.Count == 0 ? null : excludeDirs.ToArray(),
                    Patterns = patterns.Count == 0 ? ["*"] : patterns.ToArray(),
                    MaxFileBytes = maxBytes,
                    ChunkSize = 1800,
                    ChunkOverlap = 180
                };
                var loader = new DirectoryDocumentLoader<FileLoader>(options);
                var stats = new DirectoryIngestionStats();
                loader.AttachStats(stats);
                var store = s.Branch.Store as TrackedVectorStore ?? new TrackedVectorStore();
                var splitter = new RecursiveCharacterTextSplitter(chunkSize: options.ChunkSize, chunkOverlap: options.ChunkOverlap);
                var docs = await loader.LoadAsync(DataSource.FromPath(root));
                var vectors = new List<Vector>();
                int fileIndex = 0;
                foreach (var doc in docs)
                {
                    if (string.IsNullOrWhiteSpace(doc.PageContent))
                    {
                        fileIndex++;
                        continue;
                    }

                    var chunks = splitter.SplitText(doc.PageContent);
                    int chunkCount = chunks.Count;
                    var baseMetadata = BuildDocumentMetadata(doc, root, fileIndex);

                    int chunkIdx = 0;
                    foreach (var chunk in chunks)
                    {
                        string vectorId = $"dir:{fileIndex}:{chunkIdx}";
                        var chunkMetadata = BuildChunkMetadata(baseMetadata, chunkIdx, chunkCount, vectorId);

                        try
                        {
                            var emb = await s.Embed.CreateEmbeddingsAsync(chunk);
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
            var exts = new List<string>();
            var excludeDirs = new List<string>();
            var patterns = new List<string>();
            long maxBytes = 0;
            int addEvery = 256; // number of vectors per AddAsync batch
            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("root=", StringComparison.OrdinalIgnoreCase)) root = Path.GetFullPath(part.Substring(5));
                    else if (part.StartsWith("ext=", StringComparison.OrdinalIgnoreCase)) exts.AddRange(part.Substring(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("exclude=", StringComparison.OrdinalIgnoreCase)) excludeDirs.AddRange(part.Substring(8).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("pattern=", StringComparison.OrdinalIgnoreCase)) patterns.AddRange(part.Substring(8).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    else if (part.StartsWith("max=", StringComparison.OrdinalIgnoreCase) && long.TryParse(part.AsSpan(4), out var m)) maxBytes = m;
                    else if (part.Equals("norec", StringComparison.OrdinalIgnoreCase)) recursive = false;
                    else if (part.StartsWith("addEvery=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(9), out var ae) && ae > 0) addEvery = ae;
                }
            }
            if (!Directory.Exists(root))
            {
                s.Branch = s.Branch.WithIngestEvent($"dir:missing:{root}", Array.Empty<string>());
                return s;
            }
            try
            {
                var options = new DirectoryIngestionOptions
                {
                    Recursive = recursive,
                    Extensions = exts.Count == 0 ? null : exts.ToArray(),
                    ExcludeDirectories = excludeDirs.Count == 0 ? null : excludeDirs.ToArray(),
                    Patterns = patterns.Count == 0 ? new[] { "*" } : patterns.ToArray(),
                    MaxFileBytes = maxBytes,
                    ChunkSize = 1800,
                    ChunkOverlap = 180
                };
                var loader = new DirectoryDocumentLoader<FileLoader>(options);
                var stats = new DirectoryIngestionStats();
                loader.AttachStats(stats);
                var store = s.Branch.Store as TrackedVectorStore ?? new TrackedVectorStore();
                var splitter = new RecursiveCharacterTextSplitter(chunkSize: options.ChunkSize, chunkOverlap: options.ChunkOverlap);
                var docs = await loader.LoadAsync(DataSource.FromPath(root));
                var buffer = new List<Vector>(capacity: addEvery);
                int fileIndex = 0;
                foreach (var doc in docs)
                {
                    if (string.IsNullOrWhiteSpace(doc.PageContent)) { fileIndex++; continue; }
                    var chunks = splitter.SplitText(doc.PageContent);
                    int chunkCount = chunks.Count;
                    var baseMetadata = BuildDocumentMetadata(doc, root, fileIndex);
                    int chunkIdx = 0;
                    foreach (var chunk in chunks)
                    {
                        string vectorId = $"dir:{fileIndex}:{chunkIdx}";
                        var chunkMetadata = BuildChunkMetadata(baseMetadata, chunkIdx, chunkCount, vectorId);
                        try
                        {
                            var emb = await s.Embed.CreateEmbeddingsAsync(chunk);
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
                var opts = Pipeline.Ingestion.SolutionIngestion.ParseOptions(ParseString(args));
                // Recover root path: prefer last source:set event; fallback to current directory.
                string root = Environment.CurrentDirectory;
                var sourceEvent = s.Branch.Events
                    .OfType<IngestBatch>()
                    .Select(e => e.Source)
                    .Reverse()
                    .FirstOrDefault(src => src.StartsWith("source:set:"));
                if (sourceEvent is not null)
                {
                    var parts = sourceEvent.Split(':', 3);
                    if (parts.Length == 3 && Directory.Exists(parts[2])) root = parts[2];
                }
                var vectors = await Pipeline.Ingestion.SolutionIngestion.IngestAsync(
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
            var (topic, query) = Normalize(s);
            var step = ReasoningArrows.DraftArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Draft produced");
            return s;
        };

    [PipelineToken("UseCritique")]
    public static Step<CliPipelineState, CliPipelineState> UseCritique(string? args = null)
        => async s =>
        {
            var (topic, query) = Normalize(s);
            var step = ReasoningArrows.CritiqueArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Critique produced");
            return s;
        };

    [PipelineToken("UseImprove", "UseFinal")]
    public static Step<CliPipelineState, CliPipelineState> UseImprove(string? args = null)
        => async s =>
        {
            var (topic, query) = Normalize(s);
            var step = ReasoningArrows.ImproveArrow(s.Llm, s.Tools, s.Embed, topic, query, s.RetrievalK);
            s.Branch = await step(s.Branch);
            if (s.Trace) Console.WriteLine("[trace] Improvement produced");
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
                var m = Regex.Match(args, @"\s*(\d+)\s*");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) count = n;
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
            var path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(s);
            // Expand ~ and relative paths
            var expanded = path.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~', '/', '\\'))
                : path;
            var full = Path.GetFullPath(expanded);
            string finalPath = full;
            bool accessible = false;
            try
            {
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
                var testFile = Path.Combine(full, ".__pipeline_access_test");
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
                var fallback = Path.Combine(Environment.CurrentDirectory, "pipeline_source_" + Guid.NewGuid().ToString("N").Substring(0, 6));
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
                var m = Regex.Match(args, @"\s*(\d+)\s*");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var k))
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
            var raw = ParseString(args);
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
                var parts = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0) path = parts[0];
                foreach (var mod in parts.Skip(1))
                {
                    if (mod.Equals("noText", StringComparison.OrdinalIgnoreCase))
                        includeXmlText = false;
                    else if (mod.Equals("noEmbed", StringComparison.OrdinalIgnoreCase))
                        noEmbed = true;
                    else if (mod.StartsWith("maxLines=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(9), out var ml))
                        csvMaxLines = ml;
                    else if (mod.StartsWith("binPreview=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(11), out var bp))
                        binaryMaxBytes = bp;
                    else if (mod.StartsWith("maxBytes=", StringComparison.OrdinalIgnoreCase) && long.TryParse(mod.AsSpan(9), out var mb))
                        sizeBudget = mb;
                    else if (mod.StartsWith("maxRatio=", StringComparison.OrdinalIgnoreCase) && double.TryParse(mod.AsSpan(9), out var mr))
                        maxRatio = mr;
                    else if (mod.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(6), out var bs) && bs > 0)
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
                var full = Path.GetFullPath(path);
                if (!File.Exists(full))
                {
                    s.Branch = s.Branch.WithIngestEvent($"zip:missing:{full}", Array.Empty<string>());
                    return s;
                }
                var scanned = await ZipIngestion.ScanAsync(full, maxTotalBytes: sizeBudget, maxCompressionRatio: maxRatio);
                var parsed = await ZipIngestion.ParseAsync(scanned, csvMaxLines, binaryMaxBytes, includeXmlText: includeXmlText);
                var docs = new List<(string id, string text)>();
                foreach (var rec in parsed)
                {
                    if (rec.Parsed is not null && rec.Parsed.TryGetValue("type", out var t) && t?.ToString() == "skipped")
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
                        ZipContentKind.Xml => (string)(rec.Parsed!.TryGetValue("textPreview", out var preview) ? preview ?? string.Empty : ((XmlDoc)rec.Parsed!["doc"]).Document.Root?.Value ?? string.Empty),
                        ZipContentKind.Text => (string)rec.Parsed!["preview"],
                        ZipContentKind.Binary => $"[BINARY {rec.FileName} size={rec.Length} sha256={rec.Parsed!["sha256"]}]",
                        _ => string.Empty
                    };
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    docs.Add((rec.FullPath, text));
                }

                if (noEmbed)
                {
                    foreach (var (id, text) in docs)
                    {
                        DeferredZipTextCache.Store(id, text);
                    }
                    s.Branch = s.Branch.WithIngestEvent("zip:no-embed", docs.Select(d => d.id));
                }
                else if (!noEmbed && docs.Count > 0)
                {
                    for (int i = 0; i < docs.Count; i += batchSize)
                    {
                        var batch = docs.Skip(i).Take(batchSize).ToList();
                        try
                        {
                            var texts = batch.Select(b => b.text).ToArray();
                            var emb = await s.Embed.CreateEmbeddingsAsync(texts);
                            var vectors = new List<Vector>();
                            for (int idx = 0; idx < emb.Count; idx++)
                            {
                                var (id, text) = batch[idx];
                                vectors.Add(new Vector { Id = id, Text = text, Embedding = emb[idx] });
                            }
                            await s.Branch.Store.AddAsync(vectors);
                        }
                        catch (Exception exBatch)
                        {
                            foreach (var (id, _) in batch)
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
            var raw = ParseString(args);
            if (string.IsNullOrWhiteSpace(raw)) return s;
            var path = raw.Split('|', 2)[0];
            int batchSize = 8;
            bool includeXmlText = true;
            bool noEmbed = false;
            if (raw.Contains('|'))
            {
                var mods = raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1);
                foreach (var mod in mods)
                {
                    if (mod.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(mod.AsSpan(6), out var bs) && bs > 0) batchSize = bs;
                    else if (mod.Equals("noText", StringComparison.OrdinalIgnoreCase)) includeXmlText = false;
                    else if (mod.Equals("noEmbed", StringComparison.OrdinalIgnoreCase)) noEmbed = true;
                }
            }
            var full = Path.GetFullPath(path);
            if (!File.Exists(full)) { s.Branch = s.Branch.WithIngestEvent($"zip:missing:{full}", Array.Empty<string>()); return s; }
            var buffer = new List<(string id, string text)>();
            try
            {
                await foreach (var rec in ZipIngestionStreaming.EnumerateAsync(full))
                {
                    string text;
                    if (rec.Kind == ZipContentKind.Csv || rec.Kind == ZipContentKind.Xml || rec.Kind == ZipContentKind.Text)
                    {
                        var parsedList = await ZipIngestion.ParseAsync(new[] { rec }, csvMaxLines: 20, binaryMaxBytes: 32 * 1024, includeXmlText: includeXmlText);
                        var parsed = parsedList[0];
                        text = parsed.Kind switch
                        {
                            ZipContentKind.Csv => CsvToText((CsvTable)parsed.Parsed!["table"]),
                            ZipContentKind.Xml => (string)(parsed.Parsed!.TryGetValue("textPreview", out var preview) ? preview ?? string.Empty : ((XmlDoc)parsed.Parsed!["doc"]).Document.Root?.Value ?? string.Empty),
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
            var texts = batch.Select(b => b.text).ToArray();
            var emb = await s.Embed.CreateEmbeddingsAsync(texts);
            var vectors = new List<Vector>();
            for (int i = 0; i < emb.Count; i++)
            {
                var (id, text) = batch[i];
                vectors.Add(new Vector { Id = id, Text = text, Embedding = emb[i] });
            }
            await s.Branch.Store.AddAsync(vectors);
        }
        catch (Exception ex)
        {
            foreach (var (id, _) in batch)
                s.Branch = s.Branch.WithIngestEvent($"zipstream:batch-error:{id}:{ex.GetType().Name}", Array.Empty<string>());
        }
    }

    private static string CsvToText(CsvTable table)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(" | ", table.Header));
        foreach (var row in table.Rows)
            sb.AppendLine(string.Join(" | ", row));
        return sb.ToString();
    }

    [PipelineToken("ListVectors", "Vectors")] // Optional arg 'ids' to print IDs
    public static Step<CliPipelineState, CliPipelineState> ListVectors(string? args = null)
        => s =>
        {
            var all = s.Branch.Store switch
            {
                LangChainPipeline.Domain.Vectors.TrackedVectorStore tvs => tvs.GetAll(),
                _ => Enumerable.Empty<LangChain.Databases.Vector>()
            };
            var count = all.Count();
            Console.WriteLine($"[vectors] count={count}");
            if (!string.IsNullOrWhiteSpace(args) && args.Contains("ids", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var v in all.Take(100)) Console.WriteLine($" - {v.Id}");
                if (count > 100) Console.WriteLine($" ... (truncated) ...");
            }
            return Task.FromResult(s);
        };

    [PipelineToken("EmbedZip", "ZipEmbed")] // Re-embed docs that were skipped with noEmbed
    public static Step<CliPipelineState, CliPipelineState> EmbedZip(string? args = null)
        => async s =>
        {
            int batchSize = 16;
            if (!string.IsNullOrWhiteSpace(args) && args.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(args.AsSpan(6), out var bs) && bs > 0)
                batchSize = bs;
            // Heuristic: any events zip:no-embed OR zipstream:no-embed; we can't recover original text fully unless stored; for now embed placeholders.
            var pendingIds = s.Branch.Events
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
                var batch = pendingIds.Skip(i).Take(batchSize).ToList();
                var texts = batch.Select(id =>
                {
                    if (DeferredZipTextCache.TryTake(id, out var original) && !string.IsNullOrWhiteSpace(original)) return original;
                    return $"[DEFERRED ZIP DOC] {id}";
                }).ToArray();
                try
                {
                    var emb = await s.Embed.CreateEmbeddingsAsync(texts);
                    var vectors = new List<Vector>();
                    for (int idx = 0; idx < emb.Count; idx++)
                    {
                        string id = batch[idx];
                        vectors.Add(new Vector { Id = id, Text = texts[idx], Embedding = emb[idx] });
                    }
                    await s.Branch.Store.AddAsync(vectors);
                }
                catch (Exception ex)
                {
                    foreach (var id in batch)
                        s.Branch = s.Branch.WithIngestEvent($"zipembed:error:{id}:{ex.GetType().Name}", Array.Empty<string>());
                }
            }
            s.Branch = s.Branch.WithIngestEvent("zipembed:complete", pendingIds);
            return s;
        };

    private static async Task HandleDependencyExceptionAsync(Exception ex, CliPipelineState state)
    {
        if (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
        {
            Console.Error.WriteLine("⚠ Error: Ollama is not running or not reachable.");
            if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for Ollama?"))
            {
                await GuidedSetup.RunAsync(new SetupOptions { InstallOllama = true });
                Console.WriteLine("Please re-run your command.");
            }
            Environment.Exit(1);
        }
        else if (ex.Message.Contains("metta") && (ex.Message.Contains("not found") || ex.Message.Contains("No such file")))
        {
            Console.Error.WriteLine("⚠ Error: MeTTa engine not found.");
            if (GuidedSetup.PromptYesNo("Would you like to run the guided setup for MeTTa?"))
            {
                await GuidedSetup.RunAsync(new SetupOptions { InstallMeTTa = true });
                Console.WriteLine("Please re-run your command.");
            }
            Environment.Exit(1);
        }
        else
        {
            state.Branch = state.Branch.WithIngestEvent($"error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
        }
    }

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        var m = Regex.Match(arg, @"^'(?<s>.*)'$");
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
            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("amount=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(7), out var a) && a > 0)
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
                    var hits = await tvs.GetSimilarDocuments(s.Embed, query, amount);
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
            var raw = ParseString(args);
            string separator = "\n---\n";
            string prefix = string.Empty;
            string suffix = string.Empty;
            int take = s.Retrieved.Count;
            bool appendToPrompt = false;
            bool clearRetrieved = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = part.Substring(4);
                        separator = value.Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.StartsWith("take=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out var t) && t > 0)
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

            var blocks = s.Retrieved.Take(take).Where(static r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (blocks.Count == 0)
                return Task.FromResult(s);

            var combined = string.Join(separator, blocks);
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
            var templateRaw = ParseString(args);
            if (string.IsNullOrWhiteSpace(templateRaw)) return Task.FromResult(s);
            var pt = new PromptTemplate(templateRaw);
            var question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            var formatted = pt.Format(new() { ["context"] = s.Context, ["question"] = question, ["prompt"] = s.Prompt, ["topic"] = s.Topic });
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
                var (text, toolCalls) = await s.Llm.GenerateWithToolsAsync(s.Prompt);
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

            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out var kv) && kv > 0) k = kv;
                    else if (part.StartsWith("group=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(6), out var gv) && gv > 0) group = gv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase)) template = part.Substring(9);
                    else if (part.StartsWith("final=", StringComparison.OrdinalIgnoreCase)) finalTemplate = part.Substring(6);
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) streamPartials = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        var v = part.Substring(7);
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
            var docs = s.Retrieved.Where(static r => !string.IsNullOrWhiteSpace(r)).Take(k).ToList();
            if (docs.Count == 0) return s;

            var groups = new List<List<string>>();
            for (int i = 0; i < docs.Count; i += group)
            {
                groups.Add(docs.Skip(i).Take(group).ToList());
            }

            var partials = new List<string>(groups.Count);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var g = groups[gi];
                var ctx = string.Join(sep, g);
                var prompt = template!
                    .Replace("{context}", ctx)
                    .Replace("{question}", question)
                    .Replace("{prompt}", s.Prompt ?? string.Empty)
                    .Replace("{topic}", s.Topic ?? string.Empty);
                try
                {
                    var (answer, toolCalls) = await s.Llm.GenerateWithToolsAsync(prompt);
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
            var partialText = string.Join("\n\n---\n\n", partials.Where(p => !string.IsNullOrWhiteSpace(p)));
            var finalPrompt = finalTemplate!
                .Replace("{partials}", partialText)
                .Replace("{question}", question)
                .Replace("{prompt}", s.Prompt ?? string.Empty)
                .Replace("{topic}", s.Topic ?? string.Empty);

            try
            {
                var (finalAnswer, finalToolCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
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

            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("subs=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out var sv) && sv > 0) subs = sv;
                    else if (part.StartsWith("per=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(4), out var pv) && pv > 0) per = pv;
                    else if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out var kv) && kv > 0) k = kv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) stream = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        var v = part.Substring(7);
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
                var (subText, subCalls) = await s.Llm.GenerateWithToolsAsync(decomposePrompt);
                s.Branch = s.Branch.WithReasoning(new FinalSpec(subText ?? string.Empty), decomposePrompt, subCalls);
                if (!string.IsNullOrWhiteSpace(subText))
                {
                    foreach (var line in subText.Split('\n'))
                    {
                        var t = line.Trim();
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
            var qaPairs = new List<(string q, string a)>(subQuestions.Count);
            for (int i = 0; i < subQuestions.Count; i++)
            {
                string sq = subQuestions[i];
                // Retrieve per sub-question
                List<string> blocks = new();
                try
                {
                    if (s.Branch.Store is TrackedVectorStore tvs)
                    {
                        var hits = await tvs.GetSimilarDocuments(s.Embed, sq, per);
                        foreach (var doc in hits)
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
                    var (ans, toolCalls) = await s.Llm.GenerateWithToolsAsync(subPrompt);
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
            var sbPairs = new StringBuilder();
            for (int i = 0; i < qaPairs.Count; i++)
            {
                var (q, a) = qaPairs[i];
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
                var (finalAnswer, finalCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
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
            var options = ParseKeyValueArgs(args);
            if (!options.TryGetValue("file", out var fileValue) || string.IsNullOrWhiteSpace(fileValue))
            {
                s.Branch = s.Branch.WithIngestEvent("markdown:error:no-file", Array.Empty<string>());
                return s;
            }

            int iterations = 1;
            if (options.TryGetValue("iterations", out var iterationsRaw) && int.TryParse(iterationsRaw, out var parsedIterations) && parsedIterations > 0)
            {
                iterations = Math.Min(parsedIterations, 10);
            }

            int contextCount = s.RetrievalK;
            if (options.TryGetValue("context", out var contextRaw) && int.TryParse(contextRaw, out var parsedContext) && parsedContext >= 0)
            {
                contextCount = parsedContext;
            }
            contextCount = Math.Clamp(contextCount, 0, 16);

            bool createBackup = true;
            if (options.TryGetValue("backup", out var backupRaw))
            {
                createBackup = ParseBool(backupRaw, true);
            }

            string? goal = options.TryGetValue("goal", out var goalValue) ? goalValue : null;
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
                var contextBlocks = await BuildMarkdownContextAsync(s, resolvedFile, goal, contextCount);
                string prompt = BuildMarkdownRewritePrompt(resolvedFile, original, goal, contextBlocks, iteration, iterations);

                try
                {
                    var (response, toolCalls) = await s.Llm.GenerateWithToolsAsync(prompt);
                    var improved = NormalizeMarkdownOutput(response);
                    if (string.IsNullOrWhiteSpace(improved))
                    {
                        improved = original;
                    }

                    bool changed = !string.Equals(improved.Trim(), original.Trim(), StringComparison.Ordinal);
                    if (changed)
                    {
                        await File.WriteAllTextAsync(resolvedFile, improved);
                    }

                    var revision = new DocumentRevision(resolvedFile, improved, iteration, goal);
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
            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("model=", StringComparison.OrdinalIgnoreCase)) newModel = part.Substring(6);
                    else if (part.StartsWith("embed=", StringComparison.OrdinalIgnoreCase)) newEmbed = part.Substring(6);
                    else if (part.Equals("remote", StringComparison.OrdinalIgnoreCase)) forceRemote = true;
                }
            }
            if (string.IsNullOrWhiteSpace(newModel) && string.IsNullOrWhiteSpace(newEmbed)) return s; // nothing to do
            // Rebuild chat model similar to Program.cs logic but simplified, prioritizing remote if key present OR remote flag
            var (endpoint, key, endpointType) = ChatConfig.Resolve();
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
                var provider = new OllamaProvider();
                var oc = new OllamaChatModel(provider, newModel);
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
                    var provider = new OllamaProvider();
                    var oe = new OllamaEmbeddingModel(provider, newEmbed);
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
            var raw = ParseString(args);
            if (string.IsNullOrWhiteSpace(raw)) return s;
            string? name = null; string[] inKeys = Array.Empty<string>(); string[] outKeys = ["Output"]; bool trace = false;
            foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith("name=", StringComparison.OrdinalIgnoreCase)) name = part.Substring(5);
                else if (part.StartsWith("in=", StringComparison.OrdinalIgnoreCase)) inKeys = part.Substring(3).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                else if (part.StartsWith("out=", StringComparison.OrdinalIgnoreCase)) outKeys = part.Substring(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                else if (part.Equals("trace", StringComparison.OrdinalIgnoreCase)) trace = true;
            }
            if (string.IsNullOrWhiteSpace(name)) { s.Branch = s.Branch.WithIngestEvent("chain:error:no-name", Array.Empty<string>()); return s; }
            if (!ExternalChainRegistry.TryGet(name, out var chain) || chain is null)
            {
                s.Branch = s.Branch.WithIngestEvent($"chain:error:not-found:{name}", Array.Empty<string>());
                return s;
            }
            try
            {
                // Locate CallAsync via reflection
                var type = chain.GetType();
                var call = type.GetMethod("CallAsync", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (call is null)
                {
                    s.Branch = s.Branch.WithIngestEvent($"chain:error:no-call:{name}", Array.Empty<string>()); return s;
                }
                // Try to create StackableChainValues if present
                object valuesObj;
                var valuesType = Type.GetType("LangChain.Chains.StackableChains.Context.StackableChainValues, LangChain");
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
                var valueProp = valuesObj.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
                    foreach (var key in inKeys)
                    {
                        var val = key switch
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
                var taskObj = call.Invoke(chain, [valuesObj]);
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
                    foreach (var key in outKeys)
                    {
                        if (dict.TryGetValue(key, out var v) && v is not null)
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
            var all = s.Branch.Store is TrackedVectorStore tvs ? tvs.GetAll() : Enumerable.Empty<Vector>();
            int count = 0;
            double sumNorm = 0;
            foreach (var v in all)
            {
                count++;
                if (v.Embedding is { Length: > 0 })
                {
                    double norm = 0;
                    foreach (var f in v.Embedding) norm += f * f;
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
            var lines = new List<string>
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
            var docPath = Path.Combine(Environment.CurrentDirectory, "docs", "TOKENS.md");
            Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
            File.WriteAllText(docPath, string.Join(Environment.NewLine, lines));
            Console.WriteLine($"[tokendocs] updated {docPath}");
            return Task.FromResult(s);
        };

    private static Dictionary<string, string> ParseKeyValueArgs(string? args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var raw = ParseString(args);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return map;
        }

        foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
        var blocks = new List<string>();
        if (count <= 0) return blocks;
        if (state.Branch.Store is not TrackedVectorStore store) return blocks;

        string? query = ChooseFirstNonEmpty(goal, Path.GetFileNameWithoutExtension(filePath), state.Topic, state.Query, state.Prompt);
        query ??= Path.GetFileName(filePath);

        try
        {
            IReadOnlyCollection<Document> docs = await store.GetSimilarDocuments(state.Embed, query, count);
            foreach (var doc in docs)
            {
                if (doc is null || string.IsNullOrWhiteSpace(doc.PageContent))
                {
                    continue;
                }

                var metadata = doc.Metadata ?? new Dictionary<string, object>();
                if (metadata.TryGetValue("source", out var sourceObj) && sourceObj is string src && PathsEqual(src, filePath))
                {
                    continue;
                }

                string sourceLabel = metadata.TryGetValue("relative", out var relObj) && relObj is string rel && !string.IsNullOrWhiteSpace(rel)
                    ? rel
                    : metadata.TryGetValue("source", out var srcObj2) && srcObj2 is string src2
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
        var sb = new StringBuilder();
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
            foreach (var block in contextBlocks)
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
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (doc.Metadata is not null)
        {
            foreach (var kvp in doc.Metadata)
            {
                metadata[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }

        string? sourcePath = null;
        if (metadata.TryGetValue("source", out var sourceObj) && sourceObj is string sourceStr)
        {
            sourcePath = sourceStr;
        }
        else if (metadata.TryGetValue("path", out var pathObj) && pathObj is string pathStr)
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
        var metadata = new Dictionary<string, object>(baseMetadata, StringComparer.OrdinalIgnoreCase)
        {
            ["chunkIndex"] = chunkIndex,
            ["chunkCount"] = chunkCount,
            ["vectorId"] = vectorId
        };
        return metadata;
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
        var raw = ParseString(args);
        var parts = raw?.Split('|', 2, StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
        var value = parts.Length > 0 ? parts[0] : string.Empty;
        var key = parts.Length > 1 ? parts[1] : "text";

        var chain = LangChain.Chains.Chain.Set(value, key);
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

            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var m = Regex.Match(raw, @"amount=(\d+)");
                if (m.Success && int.TryParse(m.Groups[1].Value, out var amt))
                    amount = amt;
            }

            // Get vector collection from the branch store
            if (s.Branch.Store is not IVectorCollection vectorCollection)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainRetrieve: store is not IVectorCollection");
                return s;
            }

            // Set the input text from Query or Prompt
            var query = string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainRetrieve: no query text");
                return s;
            }

            // Retrieve using the vector collection directly
            try
            {
                // Create embedding for the query
                var queryEmbedding = await s.Embed.CreateEmbeddingsAsync(query);

                // Create search request
                var request = new LangChain.Databases.VectorSearchRequest
                {
                    Embeddings = new[] { queryEmbedding }
                };

                var settings = new LangChain.Databases.VectorSearchSettings
                {
                    NumberOfResults = amount
                };

                // Search in vector store
                var results = await vectorCollection.SearchAsync(request, settings);

                s.Retrieved.Clear();
                foreach (var result in results.Items)
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

            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("inputKey=", StringComparison.OrdinalIgnoreCase))
                        inputKey = part.Substring(9);
                    else if (part.StartsWith("outputKey=", StringComparison.OrdinalIgnoreCase))
                        outputKey = part.Substring(10);
                }
            }

            var chain = LangChain.Chains.Chain.CombineDocuments(inputKey, outputKey);

            // Prepare documents from Retrieved list
            var docs = s.Retrieved
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(text => new Document { PageContent = text })
                .ToList();

            if (docs.Count == 0)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainCombine: no documents to combine");
                return s;
            }

            var values = new StackableChainValues();
            values.Value[inputKey] = docs;

            var result = await chain.CallAsync(values);

            if (result.Value.TryGetValue(outputKey, out var context))
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
            var templateText = ParseString(args);
            if (string.IsNullOrWhiteSpace(templateText))
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainTemplate: no template provided");
                return s;
            }

            var chain = LangChain.Chains.Chain.Template(templateText, "text");

            var values = new StackableChainValues();
            values.Value["context"] = s.Context;
            values.Value["text"] = string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query;
            values.Value["question"] = values.Value["text"];
            values.Value["input"] = values.Value["text"];
            values.Value["prompt"] = s.Prompt;
            values.Value["topic"] = s.Topic;
            values.Value["query"] = s.Query;

            var result = await chain.CallAsync(values);

            if (result.Value.TryGetValue("text", out var output))
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

            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("inputKey=", StringComparison.OrdinalIgnoreCase))
                        inputKey = part.Substring(9);
                    else if (part.StartsWith("outputKey=", StringComparison.OrdinalIgnoreCase))
                        outputKey = part.Substring(10);
                }
            }

            // Extract the underlying IChatModel from ToolAwareChatModel
            var llmField = s.Llm.GetType().GetField("_model", BindingFlags.NonPublic | BindingFlags.Instance);
            if (llmField == null)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainLLM: cannot access underlying chat model");
                return s;
            }

            var chatModel = llmField.GetValue(s.Llm) as IChatModel;
            if (chatModel == null)
            {
                if (s.Trace) Console.WriteLine("[trace] LangChainLLM: chat model is null");
                return s;
            }

            var chain = LangChain.Chains.Chain.LLM(chatModel, inputKey, outputKey);

            var values = new StackableChainValues();
            values.Value[inputKey] = s.Prompt;

            var result = await chain.CallAsync(values);

            if (result.Value.TryGetValue(outputKey, out var output))
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

            var raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("question=", StringComparison.OrdinalIgnoreCase))
                        question = part.Substring(9);
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase))
                        template = part.Substring(9);
                    else if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out var kVal))
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
