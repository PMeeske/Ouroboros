#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.DocumentLoaders;

namespace Ouroboros.Pipeline.Ingestion;

/// <summary>
/// Directory-aware document loader that enumerates all files under a directory (optionally recursively)
/// and delegates loading of each file to the underlying single-file loader provided via generic type parameter.
/// This works around loaders that only accept a single file DataSource.
/// </summary>
/// <typeparam name="TInner">Concrete file loader implementing IDocumentLoader for single files.</typeparam>
public sealed class DirectoryDocumentLoader<TInner> : IDocumentLoader where TInner : IDocumentLoader, new()
{
    private readonly bool _recursive;
    private readonly string[] _fileGlobs;
    private readonly HashSet<string>? _allowedExtensions;
    private readonly HashSet<string>? _excludeDirs;
    private readonly long? _maxFileBytes;
    private readonly bool _useCache;
    private readonly DirectoryIngestionCache? _cache;

    public DirectoryDocumentLoader(bool recursive = true, params string[] fileGlobs)
        : this(new DirectoryIngestionOptions { Recursive = recursive, Patterns = fileGlobs }) { }

    public DirectoryDocumentLoader(DirectoryIngestionOptions options)
    {
        _recursive = options.Recursive;
        _fileGlobs = (options.Patterns is { Length: > 0 }) ? options.Patterns : ["*"];
        _allowedExtensions = options.Extensions?.Length > 0 ? [.. options.Extensions.Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())] : null;
        _excludeDirs = options.ExcludeDirectories?.Length > 0 ? [.. options.ExcludeDirectories.Select(d => d.ToLowerInvariant())] : null;
        _maxFileBytes = options.MaxFileBytes > 0 ? options.MaxFileBytes : null;
        _useCache = !options.DisableCache;
        _cache = _useCache ? new DirectoryIngestionCache(options.CacheFilePath) : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Document>> LoadAsync(
        DataSource source,
        DocumentLoaderSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        if (source.Value is not string path)
            throw new ArgumentException("DataSource must contain a path string for directory loading");

        if (File.Exists(path))
        {
            // Single file – delegate directly
            return await new TInner().LoadAsync(source, settings, cancellationToken);
        }

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory '{path}' not found");

        List<Document> docs = new List<Document>();
        bool debug = Environment.GetEnvironmentVariable("MONADIC_DEBUG") == "1";
        DateTime start = DateTime.UtcNow;
        SearchOption dirEnumOption = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        DirectoryIngestionStats stats = _optionsStats ?? new DirectoryIngestionStats();
        foreach (string pattern in _fileGlobs)
        {
            foreach (string file in Directory.EnumerateFiles(path, pattern, dirEnumOption))
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Directory exclusion
                if (_excludeDirs is not null)
                {
                    string rel = Path.GetRelativePath(path, Path.GetDirectoryName(file)!);
                    string[] parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (parts.Any(p => _excludeDirs.Contains(p.ToLowerInvariant()))) continue;
                }
                // Size filter
                if (_maxFileBytes is not null)
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        if (info.Length > _maxFileBytes) continue;
                    }
                    catch { /* ignore */ }
                }
                // Extension filter
                if (_allowedExtensions is not null)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (!_allowedExtensions.Contains(ext)) continue;
                }
                bool skipByCache = false;
                if (_useCache && _cache is not null)
                {
                    if (_cache.IsUnchanged(file)) { stats.SkippedUnchanged++; if (debug) Console.WriteLine($"[ingest] skip unchanged {file}"); continue; }
                }
                try
                {
                    DataSource fileSource = DataSource.FromPath(file);
                    IReadOnlyCollection<Document> loaded = await new TInner().LoadAsync(fileSource, settings, cancellationToken);
                    foreach (Document d in loaded)
                    {
                        // Build a fresh document if we need to augment metadata
                        IDictionary<string, object> metaBase = d.Metadata ?? new Dictionary<string, object>();
                        Dictionary<string, object> meta = new Dictionary<string, object>(metaBase)
                        {
                            ["directoryRoot"] = path,
                            ["relativePath"] = Path.GetRelativePath(path, file)
                        };
                        docs.Add(new Document
                        {
                            PageContent = d.PageContent,
                            Metadata = meta
                        });
                        stats.FilesLoaded++;
                    }
                    if (_useCache && _cache is not null && !skipByCache) _cache.UpdateHash(file);
                    if (debug) Console.WriteLine($"[ingest] loaded {file} docs={loaded.Count}");
                }
                catch (Exception ex)
                {
                    if (debug) Console.WriteLine($"[ingest] error {file} {ex.Message}");
                    docs.Add(new Document
                    {
                        PageContent = string.Empty,
                        Metadata = new Dictionary<string, object>
                        {
                            ["error"] = ex.Message,
                            ["path"] = file
                        }
                    });
                    stats.Errors++;
                }
            }
        }
        if (_useCache) _cache?.Persist();
        if (stats != null)
        {
            stats.Elapsed = DateTime.UtcNow - start;
            if (debug) Console.WriteLine($"[ingest] summary {stats}");
        }
        return docs;
    }

    // internal hook to pass stats object without altering interface signature
    private DirectoryIngestionStats? _optionsStats;
    public void AttachStats(DirectoryIngestionStats stats) => _optionsStats = stats;
}

public sealed class DirectoryIngestionOptions
{
    public bool Recursive { get; set; } = true;
    public string[] Patterns { get; set; } = Array.Empty<string>();
    public string[]? Extensions { get; set; }
    public string[]? ExcludeDirectories { get; set; }
    public long MaxFileBytes { get; set; } = 0;
    public bool DisableCache { get; set; }
    public string CacheFilePath { get; set; } = ".monadic_ingest_cache.json";
    public int ChunkSize { get; set; } = 2000;
    public int ChunkOverlap { get; set; } = 200;
}

public sealed class DirectoryIngestionStats
{
    public int FilesLoaded { get; set; }
    public int SkippedUnchanged { get; set; }
    public int Errors { get; set; }
    public int VectorsProduced { get; set; }
    public TimeSpan Elapsed { get; set; }
    /// <inheritdoc/>
    public override string ToString() => $"files={FilesLoaded} skipped={SkippedUnchanged} errors={Errors} vectors={VectorsProduced} elapsed={Elapsed.TotalMilliseconds:F0}ms";
}

internal sealed class DirectoryIngestionCache
{
    private readonly string _path;
    private readonly Dictionary<string, string> _hashes = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;
    public DirectoryIngestionCache(string path)
    {
        _path = Path.GetFullPath(path);
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                Dictionary<string, string>? loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded is not null)
                {
                    foreach (KeyValuePair<string, string> kv in loaded) _hashes[kv.Key] = kv.Value;
                }
            }
        }
        catch { /* ignore cache load issues */ }
    }

    public bool IsUnchanged(string file)
    {
        try
        {
            string h = ComputeHash(file);
            if (_hashes.TryGetValue(file, out string? existing) && existing == h) return true;
            return false;
        }
        catch { return false; }
    }

    public void UpdateHash(string file)
    {
        try
        {
            string h = ComputeHash(file);
            _hashes[file] = h;
            _dirty = true;
        }
        catch { }
    }

    public void Persist()
    {
        if (!_dirty) return;
        try
        {
            string json = System.Text.Json.JsonSerializer.Serialize(_hashes);
            File.WriteAllText(_path, json);
            _dirty = false;
        }
        catch { }
    }

    private static string ComputeHash(string file)
    {
        using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
        using FileStream fs = File.OpenRead(file);
        byte[] hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}
