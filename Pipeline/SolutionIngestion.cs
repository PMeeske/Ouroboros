using System.Xml.Linq;
using LangChain.Databases;
using LangChainPipeline.Providers;
using LangChain.Splitters.Text;
using LangChain.DocumentLoaders;
using LangChainPipeline.Core;
using LangChainPipeline.Domain.Vectors; // TrackedVectorStore

namespace LangChainPipeline.Pipeline.Ingestion;

/// <summary>
/// Specialized ingestion for .NET solutions. Produces vector embeddings for:
///  - Solution/project structure summaries (synthetic meta documents)
///  - Source code files (.cs by default) chunked with RecursiveCharacterTextSplitter
///  - Optional additional extensions (.razor,.cshtml, etc.) if requested
/// </summary>
public static class SolutionIngestion
{
    private static readonly string[] DefaultCodeExtensions = new[] { ".cs" };

    public sealed record SolutionIngestionOptions(
        int MaxFiles,
        long MaxFileBytes,
        bool MetaOnly,
        string[] Extensions,
        bool IncludeProjectMeta,
        bool IncludeSolutionMeta);

    public static SolutionIngestionOptions ParseOptions(string? raw)
    {
        int maxFiles = 500;               // generous default
        long maxFileBytes = 512 * 1024;    // 512KB per file cap
        bool metaOnly = false;
        bool includeProjectMeta = true;
        bool includeSolutionMeta = true;
        List<string> exts = new(DefaultCodeExtensions);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.StartsWith("maxFiles=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(9), out var mf))
                    maxFiles = mf;
                else if (part.StartsWith("maxFileBytes=", StringComparison.OrdinalIgnoreCase) && long.TryParse(part.AsSpan(13), out var mfb))
                    maxFileBytes = mfb;
                else if (part.Equals("metaOnly", StringComparison.OrdinalIgnoreCase))
                    metaOnly = true;
                else if (part.StartsWith("ext=", StringComparison.OrdinalIgnoreCase))
                {
                    exts.Clear();
                    exts.AddRange(part.Substring(4).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant()));
                }
                else if (part.Equals("noProjectMeta", StringComparison.OrdinalIgnoreCase))
                    includeProjectMeta = false;
                else if (part.Equals("noSolutionMeta", StringComparison.OrdinalIgnoreCase))
                    includeSolutionMeta = false;
            }
        }
        return new SolutionIngestionOptions(maxFiles, maxFileBytes, metaOnly, exts.Distinct().ToArray(), includeProjectMeta, includeSolutionMeta);
    }

    /// <summary>
    /// Performs ingestion. Emits vectors to the provided store and returns the created vector list.
    /// </summary>
    public static async Task<List<Vector>> IngestAsync(
        TrackedVectorStore store,
        string rootPath,
        IEmbeddingModel embed,
        SolutionIngestionOptions options,
        CancellationToken ct = default)
    {
        var splitter = new RecursiveCharacterTextSplitter(chunkSize: 2000, chunkOverlap: 200);
        var vectors = new List<Vector>();
        var root = Directory.Exists(rootPath) ? rootPath : Environment.CurrentDirectory;

        string? solutionFile = Directory.GetFiles(root, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        var projectFiles = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !IsIgnoredPath(p))
            .ToList();

        if (options.IncludeSolutionMeta && solutionFile is not null)
        {
            try
            {
                var lines = File.ReadAllLines(solutionFile);
                var projectLines = lines.Where(l => l.TrimStart().StartsWith("Project(", StringComparison.OrdinalIgnoreCase)).Take(500).ToList();
                string meta = $"SOLUTION SUMMARY\nPath: {Path.GetFileName(solutionFile)}\nProject Count: {projectLines.Count}\nProjects:\n" + string.Join('\n', projectLines);
                await EmbedSyntheticAsync(embed, vectors, meta, solutionFile + "#meta:solution", ct);
            }
            catch { /* ignore meta failures */ }
        }

        if (options.IncludeProjectMeta)
        {
            foreach (var csproj in projectFiles.Take(1000))
            {
                try
                {
                    var doc = XDocument.Load(csproj);
                    var sdk = doc.Root?.Attribute("Sdk")?.Value ?? "";
                    var pkgRefs = doc.Descendants().Where(e => e.Name.LocalName == "PackageReference")
                        .Select(e => $"{e.Attribute("Include")?.Value}:{e.Attribute("Version")?.Value}")
                        .Take(100)
                        .ToList();
                    var tfms = doc.Descendants().Where(e => e.Name.LocalName == "TargetFramework" || e.Name.LocalName == "TargetFrameworks")
                        .Select(e => e.Value)
                        .ToList();
                    string meta = $"PROJECT SUMMARY\nName: {Path.GetFileName(csproj)}\nSDK: {sdk}\nTargetFramework(s): {string.Join(",", tfms)}\nPackages:\n" + string.Join('\n', pkgRefs);
                    await EmbedSyntheticAsync(embed, vectors, meta, csproj + "#meta:project", ct);
                }
                catch { }
            }
        }

        if (options.MetaOnly)
        {
            if (vectors.Count > 0)
                await store.AddAsync(vectors);
            return vectors;
        }

        // Enumerate code files
        var allowedExt = new HashSet<string>(options.Extensions, StringComparer.OrdinalIgnoreCase);
        var codeFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => allowedExt.Contains(Path.GetExtension(f)))
            .Where(f => !IsIgnoredPath(f))
            .Take(options.MaxFiles)
            .ToList();

        foreach (var file in codeFiles)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var fi = new FileInfo(file);
                if (fi.Length > options.MaxFileBytes) continue;
                string text = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(text)) continue;
                var chunks = splitter.SplitText(text);
                int ci = 0;
                foreach (var chunk in chunks)
                {
                    try
                    {
                        var embResp = await embed.CreateEmbeddingsAsync(chunk);
                        var meta = new Dictionary<string, object?>
                        {
                            ["path"] = file,
                            ["chunkIndex"] = ci,
                            ["type"] = "code"
                        };
                        vectors.Add(new Vector
                        {
                            Id = file + "#chunk" + ci,
                            Text = chunk,
                            Metadata = (IDictionary<string, object>) (object) meta,
                            Embedding = embResp
                        });
                        ci++;
                    }
                    catch
                    {
                        // skip bad chunk
                    }
                }
            }
            catch { /* skip file */ }
        }

        if (vectors.Count > 0)
            await store.AddAsync(vectors);
        return vectors;
    }

    private static async Task EmbedSyntheticAsync(IEmbeddingModel embed, List<Vector> vectors, string content, string id, CancellationToken ct)
    {
        try
        {
            var emb = await embed.CreateEmbeddingsAsync(content);
            var meta = new Dictionary<string, object?> { ["type"] = "meta" };
            vectors.Add(new Vector
            {
                Id = id,
                Text = content,
                Metadata = (IDictionary<string, object>) (object) meta,
                Embedding = emb
            });
        }
        catch
        {
            // Fallback deterministic embedding so downstream logic & tests still have a vector even without a live model.
            var meta = new Dictionary<string, object?> { ["type"] = "meta", ["fallback"] = true };
            var seed = content.Length;
            var arr = new float[16];
            for (int i = 0; i < arr.Length; i++) arr[i] = (float)((seed * (i + 31)) % 100) / 100f;
            vectors.Add(new Vector
            {
                Id = id,
                Text = content,
                Metadata = (IDictionary<string, object>) (object) meta,
                Embedding = arr
            });
        }
    }

    private static bool IsIgnoredPath(string path)
    {
        // basic ignore heuristics (bin/obj, hidden, .git, node_modules)
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var s in segments)
        {
            if (s.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                s.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                s.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
