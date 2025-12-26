using System.Text;
using LangChain.Databases;
using LangChain.DocumentLoaders;
using Ouroboros.Application.Configuration;
using Ouroboros.Application.Services;
using Ouroboros.Application.Utilities;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Application;

public static class IngestionCliSteps
{
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
            var defaultRoot = s.Branch.Source.Value as string ?? Environment.CurrentDirectory;
            var configResult = DirectoryIngestionConfigBuilder.Parse(args, defaultRoot);

            return await configResult.MatchAsync(
                success: async config =>
                {
                    var ingestionResult = await DirectoryIngestionService.IngestAsync(
                        config,
                        s.Branch.Store,
                        s.Embed);

                    return ingestionResult.Match(
                        onSuccess: result =>
                        {
                            if (s.Trace)
                                Console.WriteLine($"[dir] {result.Stats}");
                            
                            return s.WithBranch(
                                s.Branch.WithIngestEvent(
                                    $"dir:ingest:{Path.GetFileName(config.Root)}",
                                    result.VectorIds));
                        },
                        onFailure: error => s.WithBranch(
                            s.Branch.WithIngestEvent(
                                $"dir:error:{error.Replace('|', ':')}",
                                Array.Empty<string>())));
                },
                failure: error => Task.FromResult(
                    s.WithBranch(
                        s.Branch.WithIngestEvent(
                            $"dir:config-error:{error.Replace('|', ':')}",
                            Array.Empty<string>()))));
        };

    [PipelineToken("UseDirBatched", "DirIngestBatched")] // Usage: UseDirBatched('root=src|ext=.cs,.md|exclude=bin,obj|max=500000|pattern=*.cs;*.md|norec|addEvery=256')
    public static Step<CliPipelineState, CliPipelineState> UseDirBatched(string? args = null)
        => async s =>
        {
            var defaultRoot = s.Branch.Source.Value as string ?? Environment.CurrentDirectory;
            
            // Parse with addEvery parameter
            var configResult = DirectoryIngestionConfigBuilder.ParseBatched(args, defaultRoot);

            return await configResult.MatchAsync(
                success: async config =>
                {
                    var ingestionResult = await DirectoryIngestionService.IngestAsync(
                        config with { BatchSize = config.BatchSize > 0 ? config.BatchSize : 256 },
                        s.Branch.Store,
                        s.Embed);

                    return ingestionResult.Match(
                        onSuccess: result =>
                        {
                            if (s.Trace)
                                Console.WriteLine($"[dir-batched] {result.Stats}");
                            
                            return s.WithBranch(
                                s.Branch.WithIngestEvent(
                                    $"dir:ingest-batched:{Path.GetFileName(config.Root)}",
                                    Array.Empty<string>())); // Batched doesn't track individual IDs
                        },
                        onFailure: error => s.WithBranch(
                            s.Branch.WithIngestEvent(
                                $"dirbatched:error:{error.Replace('|', ':')}",
                                Array.Empty<string>())));
                },
                failure: error => Task.FromResult(
                    s.WithBranch(
                        s.Branch.WithIngestEvent(
                            $"dirbatched:config-error:{error.Replace('|', ':')}",
                            Array.Empty<string>()))));
        };

    [PipelineToken("UseSolution", "Solution", "UseSolutionIngest")] // Usage: Solution('maxFiles=400|maxFileBytes=600000|ext=.cs,.razor')
    public static Step<CliPipelineState, CliPipelineState> UseSolution(string? args = null)
        => async s =>
        {
            try
            {
                Ouroboros.Pipeline.Ingestion.SolutionIngestion.SolutionIngestionOptions opts = Ouroboros.Pipeline.Ingestion.SolutionIngestion.ParseOptions(CliSteps.ParseString(args));
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
                List<Vector> vectors = await Ouroboros.Pipeline.Ingestion.SolutionIngestion.IngestAsync(
                    s.Branch.Store as Ouroboros.Domain.Vectors.TrackedVectorStore ?? new Ouroboros.Domain.Vectors.TrackedVectorStore(),
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

    [PipelineToken("Zip", "UseZip")] // Usage: Zip('archive.zip|maxLines=100|binPreview=65536|noText|maxRatio=300|skip=binary|noEmbed|batch=16')
    public static Step<CliPipelineState, CliPipelineState> ZipIngest(string? args = null)
        => async s =>
        {
            string raw = CliSteps.ParseString(args);
            string? path = raw;
            bool includeXmlText = true;
            int csvMaxLines = DefaultIngestionSettings.CsvMaxLines;
            int binaryMaxBytes = DefaultIngestionSettings.BinaryMaxBytes;
            long sizeBudget = DefaultIngestionSettings.MaxArchiveSizeBytes;
            double maxRatio = DefaultIngestionSettings.MaxCompressionRatio;
            HashSet<string>? skipKinds = null;
            HashSet<string>? onlyKinds = null;
            bool noEmbed = false;
            int batchSize = DefaultIngestionSettings.DefaultBatchSize;
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
            string raw = CliSteps.ParseString(args);
            if (string.IsNullOrWhiteSpace(raw)) return s;
            string path = raw.Split('|', 2)[0];
            int batchSize = DefaultIngestionSettings.StreamingBatchSize;
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
                        IReadOnlyList<ZipFileRecord> parsedList = await ZipIngestion.ParseAsync(new[] { rec }, csvMaxLines: DefaultIngestionSettings.StreamingCsvMaxLines, binaryMaxBytes: DefaultIngestionSettings.StreamingBinaryMaxBytes, includeXmlText: includeXmlText);
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
            foreach (var item in batch)
                s.Branch = s.Branch.WithIngestEvent($"zipstream:batch-error:{item.id}:{ex.GetType().Name}", Array.Empty<string>());
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
}

