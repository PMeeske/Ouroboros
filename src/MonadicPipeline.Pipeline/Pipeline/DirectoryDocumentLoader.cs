// <copyright file="DirectoryDocumentLoader.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Ingestion;

using LangChain.DocumentLoaders;

/// <summary>
/// Directory-aware document loader that enumerates all files under a directory (optionally recursively)
/// and delegates loading of each file to the underlying single-file loader provided via generic type parameter.
/// This works around loaders that only accept a single file DataSource.
/// </summary>
/// <typeparam name="TInner">Concrete file loader implementing IDocumentLoader for single files.</typeparam>
public sealed class DirectoryDocumentLoader<TInner> : IDocumentLoader
    where TInner : IDocumentLoader, new()
{
    private readonly bool recursive;
    private readonly string[] fileGlobs;
    private readonly HashSet<string>? allowedExtensions;
    private readonly HashSet<string>? excludeDirs;
    private readonly long? maxFileBytes;
    private readonly bool useCache;
    private readonly DirectoryIngestionCache? cache;

    public DirectoryDocumentLoader(bool recursive = true, params string[] fileGlobs)
        : this(new DirectoryIngestionOptions { Recursive = recursive, Patterns = fileGlobs })
    {
    }

    public DirectoryDocumentLoader(DirectoryIngestionOptions options)
    {
        this.recursive = options.Recursive;
        this.fileGlobs = (options.Patterns is { Length: > 0 }) ? options.Patterns : ["*"];
        this.allowedExtensions = options.Extensions?.Length > 0 ? [.. options.Extensions.Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())] : null;
        this.excludeDirs = options.ExcludeDirectories?.Length > 0 ? [.. options.ExcludeDirectories.Select(d => d.ToLowerInvariant())] : null;
        this.maxFileBytes = options.MaxFileBytes > 0 ? options.MaxFileBytes : null;
        this.useCache = !options.DisableCache;
        this.cache = this.useCache ? new DirectoryIngestionCache(options.CacheFilePath) : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Document>> LoadAsync(
        DataSource source,
        DocumentLoaderSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        if (source.Value is not string path)
        {
            throw new ArgumentException("DataSource must contain a path string for directory loading");
        }

        if (File.Exists(path))
        {
            // Single file – delegate directly
            return await new TInner().LoadAsync(source, settings, cancellationToken);
        }

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory '{path}' not found");
        }

        var docs = new List<Document>();
        var debug = Environment.GetEnvironmentVariable("MONADIC_DEBUG") == "1";
        var start = DateTime.UtcNow;
        var dirEnumOption = this.recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var stats = this.optionsStats ?? new DirectoryIngestionStats();
        foreach (var pattern in this.fileGlobs)
        {
            foreach (var file in Directory.EnumerateFiles(path, pattern, dirEnumOption))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Directory exclusion
                if (this.excludeDirs is not null)
                {
                    var rel = Path.GetRelativePath(path, Path.GetDirectoryName(file)!);
                    var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (parts.Any(p => this.excludeDirs.Contains(p.ToLowerInvariant())))
                    {
                        continue;
                    }
                }

                // Size filter
                if (this.maxFileBytes is not null)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length > this.maxFileBytes)
                        {
                            continue;
                        }
                    }
                    catch
                    { /* ignore */
                    }
                }

                // Extension filter
                if (this.allowedExtensions is not null)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (!this.allowedExtensions.Contains(ext))
                    {
                        continue;
                    }
                }

                bool skipByCache = false;
                if (this.useCache && this.cache is not null)
                {
                    if (this.cache.IsUnchanged(file))
                    {
                        stats.SkippedUnchanged++;
                        if (debug)
                        {
                            Console.WriteLine($"[ingest] skip unchanged {file}");
                        }

                        continue;
                    }
                }

                try
                {
                    var fileSource = DataSource.FromPath(file);
                    var loaded = await new TInner().LoadAsync(fileSource, settings, cancellationToken);
                    foreach (var d in loaded)
                    {
                        // Build a fresh document if we need to augment metadata
                        var metaBase = d.Metadata ?? new Dictionary<string, object>();
                        var meta = new Dictionary<string, object>(metaBase)
                        {
                            ["directoryRoot"] = path,
                            ["relativePath"] = Path.GetRelativePath(path, file),
                        };
                        docs.Add(new Document
                        {
                            PageContent = d.PageContent,
                            Metadata = meta,
                        });
                        stats.FilesLoaded++;
                    }

                    if (this.useCache && this.cache is not null && !skipByCache)
                    {
                        this.cache.UpdateHash(file);
                    }

                    if (debug)
                    {
                        Console.WriteLine($"[ingest] loaded {file} docs={loaded.Count}");
                    }
                }
                catch (Exception ex)
                {
                    if (debug)
                    {
                        Console.WriteLine($"[ingest] error {file} {ex.Message}");
                    }

                    docs.Add(new Document
                    {
                        PageContent = string.Empty,
                        Metadata = new Dictionary<string, object>
                        {
                            ["error"] = ex.Message,
                            ["path"] = file
                        },
                    });
                    stats.Errors++;
                }
            }
        }

        if (this.useCache)
        {
            this.cache?.Persist();
        }

        if (stats != null)
        {
            stats.Elapsed = DateTime.UtcNow - start;
            if (debug)
            {
                Console.WriteLine($"[ingest] summary {stats}");
            }
        }

        return docs;
    }

    // internal hook to pass stats object without altering interface signature
    private DirectoryIngestionStats? optionsStats;

    public void AttachStats(DirectoryIngestionStats stats) => this.optionsStats = stats;
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
    public override string ToString() => $"files={this.FilesLoaded} skipped={this.SkippedUnchanged} errors={this.Errors} vectors={this.VectorsProduced} elapsed={this.Elapsed.TotalMilliseconds:F0}ms";
}

internal sealed class DirectoryIngestionCache
{
    private readonly string path;
    private readonly Dictionary<string, string> hashes = new(StringComparer.OrdinalIgnoreCase);
    private bool dirty;

    public DirectoryIngestionCache(string path)
    {
        this.path = Path.GetFullPath(path);
        try
        {
            if (File.Exists(this.path))
            {
                var json = File.ReadAllText(this.path);
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded is not null)
                {
                    foreach (var kv in loaded)
                    {
                        this.hashes[kv.Key] = kv.Value;
                    }
                }
            }
        }
        catch
        { /* ignore cache load issues */
        }
    }

    public bool IsUnchanged(string file)
    {
        try
        {
            var h = ComputeHash(file);
            if (this.hashes.TryGetValue(file, out var existing) && existing == h)
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public void UpdateHash(string file)
    {
        try
        {
            var h = ComputeHash(file);
            this.hashes[file] = h;
            this.dirty = true;
        }
        catch
        {
        }
    }

    public void Persist()
    {
        if (!this.dirty)
        {
            return;
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this.hashes);
            File.WriteAllText(this.path, json);
            this.dirty = false;
        }
        catch
        {
        }
    }

    private static string ComputeHash(string file)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(file);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}
