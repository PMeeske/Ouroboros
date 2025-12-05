using Ouroboros.Application.Utilities;

namespace Ouroboros.Application.Configuration;

/// <summary>
/// Configuration for directory-based document ingestion.
/// </summary>
public record DirectoryIngestionConfig
{
    /// <summary>Root directory path to ingest.</summary>
    public required string Root { get; init; }
    
    /// <summary>Whether to recursively scan subdirectories.</summary>
    public bool Recursive { get; init; } = true;
    
    /// <summary>File extensions to include (e.g., [".cs", ".md"]).</summary>
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();
    
    /// <summary>Directory names to exclude from scanning.</summary>
    public IReadOnlyList<string> ExcludeDirectories { get; init; } = Array.Empty<string>();
    
    /// <summary>File patterns to match (e.g., ["*.cs", "*.md"]).</summary>
    public IReadOnlyList<string> Patterns { get; init; } = new[] { "*" };
    
    /// <summary>Maximum file size in bytes (0 = unlimited).</summary>
    public long MaxFileBytes { get; init; }
    
    /// <summary>Chunk size for text splitting.</summary>
    public int ChunkSize { get; init; } = DefaultIngestionSettings.ChunkSize;
    
    /// <summary>Chunk overlap for text splitting.</summary>
    public int ChunkOverlap { get; init; } = DefaultIngestionSettings.ChunkOverlap;
    
    /// <summary>Batch size for vector addition (0 = add all at once).</summary>
    public int BatchSize { get; init; } = 0;
}

/// <summary>
/// Builder for DirectoryIngestionConfig.
/// </summary>
public static class DirectoryIngestionConfigBuilder
{
    /// <summary>
    /// Parses configuration from arguments string.
    /// </summary>
    /// <param name="args">The arguments string.</param>
    /// <param name="defaultRoot">The default root directory.</param>
    /// <returns>A Result containing the configuration.</returns>
    public static Result<DirectoryIngestionConfig> Parse(
        string? args,
        string defaultRoot)
    {
        var defaults = new DirectoryIngestionConfig { Root = defaultRoot };
        
        return ConfigParser.Parse(args, defaults, (dict, config) =>
        {
            string root = dict.GetValueOrDefault("root", config.Root);
            bool recursive = !dict.ContainsKey("norec");
            
            List<string> extensions = dict.TryGetValue("ext", out string? extStr)
                ? extStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.Extensions.ToList();
                
            List<string> excludeDirs = dict.TryGetValue("exclude", out string? excStr)
                ? excStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.ExcludeDirectories.ToList();
                
            List<string> patterns = dict.TryGetValue("pattern", out string? patStr)
                ? patStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.Patterns.ToList();
                
            long maxBytes = dict.TryGetValue("max", out string? maxStr) && long.TryParse(maxStr, out long mb)
                ? mb
                : config.MaxFileBytes;
                
            int batchSize = dict.TryGetValue("batch", out string? batchStr) && int.TryParse(batchStr, out int bs)
                ? bs
                : config.BatchSize;

            if (!Directory.Exists(root))
            {
                return Result<DirectoryIngestionConfig>.Failure($"Directory not found: {root}");
            }

            return Result<DirectoryIngestionConfig>.Success(new DirectoryIngestionConfig
            {
                Root = Path.GetFullPath(root),
                Recursive = recursive,
                Extensions = extensions,
                ExcludeDirectories = excludeDirs,
                Patterns = patterns.Count == 0 ? new[] { "*" } : patterns,
                MaxFileBytes = maxBytes,
                BatchSize = batchSize,
                ChunkSize = config.ChunkSize,
                ChunkOverlap = config.ChunkOverlap
            });
        });
    }

    /// <summary>
    /// Parses configuration for batched ingestion.
    /// </summary>
    /// <param name="args">The arguments string.</param>
    /// <param name="defaultRoot">The default root directory.</param>
    /// <returns>A Result containing the configuration.</returns>
    public static Result<DirectoryIngestionConfig> ParseBatched(
        string? args,
        string defaultRoot)
    {
        // Reuse the main parser but ensure batch size is set if not provided
        return Parse(args, defaultRoot).Map(config => 
        {
            if (config.BatchSize == 0)
            {
                return config with { BatchSize = DefaultIngestionSettings.DefaultBatchSize };
            }
            return config;
        });
    }
}

