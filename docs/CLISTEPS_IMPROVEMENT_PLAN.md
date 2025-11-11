# CliSteps.cs Improvement Plan

**Status:** 📋 Planning Phase  
**Target Completion:** 18-22 business days  
**Last Updated:** 2025-11-10

---

## Overview

This document outlines the specific, actionable steps to improve the code quality of `src/MonadicPipeline.CLI/CliSteps.cs` based on the findings in `CLISTEPS_QUALITY_REPORT.md`.

The improvements are organized into three phases with clear deliverables and success criteria.

---

## Phase 1: Critical Fixes (6-8 days)

### Step 1.1: Introduce Result<T> Monad Infrastructure

**Objective:** Create infrastructure for monadic error handling across all CLI steps.

**Tasks:**
1. ✅ Review existing `Result<T>` implementation in the codebase
2. ⬜ Create `CliStepResult<T>` type alias if needed
3. ⬜ Create extension methods for Result<T> in CLI context:
   ```csharp
   public static class CliResultExtensions
   {
       public static CliPipelineState WithResult<T>(
           this CliPipelineState state,
           Result<T> result,
           Func<CliPipelineState, T, CliPipelineState> onSuccess,
           Func<CliPipelineState, string, CliPipelineState> onFailure)
       {
           return result.Match(
               success: value => onSuccess(state, value),
               failure: error => onFailure(state, error));
       }
   }
   ```

**Files to Create:**
- `src/MonadicPipeline.CLI/Utilities/CliResultExtensions.cs`

**Acceptance Criteria:**
- Extension methods compile without errors
- Basic unit tests pass for Result handling

---

### Step 1.2: Extract Configuration Types

**Objective:** Create typed configuration records for parameter parsing.

**Tasks:**
1. ⬜ Create `src/MonadicPipeline.CLI/Configuration/` directory
2. ⬜ Define configuration records:

```csharp
// Configuration/DirectoryIngestionConfig.cs
namespace LangChainPipeline.CLI.Configuration;

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

// Configuration/ZipIngestionConfig.cs
public record ZipIngestionConfig
{
    public required string ArchivePath { get; init; }
    public bool IncludeXmlText { get; init; } = true;
    public int CsvMaxLines { get; init; } = 50;
    public int BinaryMaxBytes { get; init; } = 128 * 1024;
    public long MaxTotalBytes { get; init; } = 500 * 1024 * 1024;
    public double MaxCompressionRatio { get; init; } = 200.0;
    public HashSet<string>? SkipKinds { get; init; }
    public HashSet<string>? OnlyKinds { get; init; }
    public bool NoEmbed { get; init; } = false;
    public int BatchSize { get; init; } = 16;
}

// Configuration/RagConfig.cs
public record DivideAndConquerRagConfig
{
    public int RetrievalCount { get; init; } = 24;
    public int GroupSize { get; init; } = 6;
    public string Separator { get; init; } = "\n---\n";
    public string? CustomTemplate { get; init; }
    public string? FinalTemplate { get; init; }
    public bool StreamPartials { get; init; } = false;
}

public record DecomposeAndAggregateRagConfig
{
    public int SubQuestions { get; init; } = 4;
    public int DocsPerSubQuestion { get; init; } = 6;
    public int InitialRetrievalCount { get; init; } = 24;
    public string Separator { get; init; } = "\n---\n";
    public bool StreamOutputs { get; init; } = false;
    public string? DecomposeTemplate { get; init; }
    public string? SubQuestionTemplate { get; init; }
    public string? FinalTemplate { get; init; }
}

// Configuration/MarkdownEnhancementConfig.cs
public record MarkdownEnhancementConfig
{
    public required string FilePath { get; init; }
    public int Iterations { get; init; } = 1;
    public int ContextCount { get; init; } = 8;
    public bool CreateBackup { get; init; } = true;
    public string? Goal { get; init; }
}

// Configuration/ModelSwitchConfig.cs
public record ModelSwitchConfig
{
    public string? ChatModel { get; init; }
    public string? EmbeddingModel { get; init; }
    public bool ForceRemote { get; init; } = false;
}
```

3. ⬜ Create constants file:

```csharp
// Configuration/DefaultIngestionSettings.cs
namespace LangChainPipeline.CLI.Configuration;

/// <summary>
/// Default settings for document ingestion operations.
/// </summary>
public static class DefaultIngestionSettings
{
    /// <summary>Default chunk size for text splitting (characters).</summary>
    public const int ChunkSize = 1800;
    
    /// <summary>Default chunk overlap for text splitting (characters).</summary>
    public const int ChunkOverlap = 180;
    
    /// <summary>Default maximum archive size (500 MB).</summary>
    public const long MaxArchiveSizeBytes = 500 * 1024 * 1024;
    
    /// <summary>Default maximum compression ratio for zip files.</summary>
    public const double MaxCompressionRatio = 200.0;
    
    /// <summary>Default batch size for vector additions.</summary>
    public const int DefaultBatchSize = 16;
    
    /// <summary>Default document separator for combining contexts.</summary>
    public const string DocumentSeparator = "\n---\n";
}

/// <summary>
/// Standard keys used in pipeline state and chain values.
/// </summary>
public static class StateKeys
{
    public const string Text = "text";
    public const string Context = "context";
    public const string Question = "question";
    public const string Prompt = "prompt";
    public const string Topic = "topic";
    public const string Query = "query";
    public const string Input = "input";
    public const string Output = "output";
    public const string Documents = "documents";
}
```

**Files to Create:**
- `src/MonadicPipeline.CLI/Configuration/DirectoryIngestionConfig.cs`
- `src/MonadicPipeline.CLI/Configuration/ZipIngestionConfig.cs`
- `src/MonadicPipeline.CLI/Configuration/RagConfig.cs`
- `src/MonadicPipeline.CLI/Configuration/MarkdownEnhancementConfig.cs`
- `src/MonadicPipeline.CLI/Configuration/ModelSwitchConfig.cs`
- `src/MonadicPipeline.CLI/Configuration/DefaultIngestionSettings.cs`

**Acceptance Criteria:**
- All configuration types compile
- Records have XML documentation
- Default values match current hardcoded values

---

### Step 1.3: Create Configuration Parser

**Objective:** Extract argument parsing logic into reusable, testable functions.

**Tasks:**
1. ⬜ Create parser infrastructure:

```csharp
// Utilities/ConfigParser.cs
namespace LangChainPipeline.CLI.Utilities;

/// <summary>
/// Provides parsing utilities for CLI argument strings into typed configurations.
/// </summary>
public static class ConfigParser
{
    /// <summary>
    /// Parses a pipe-delimited argument string into a configuration object.
    /// </summary>
    public static Result<TConfig> Parse<TConfig>(
        string? args,
        TConfig defaults,
        Func<Dictionary<string, string>, TConfig, Result<TConfig>> builder)
    {
        try
        {
            var raw = ParseString(args);
            var dict = ParseKeyValueArgs(raw);
            return builder(dict, defaults);
        }
        catch (Exception ex)
        {
            return Result<TConfig>.Failure($"Configuration parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses pipe-delimited key-value pairs into a dictionary.
    /// </summary>
    public static Dictionary<string, string> ParseKeyValueArgs(string? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return map;

        foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int idx = part.IndexOf('=');
            if (idx > 0)
            {
                string key = part[..idx].Trim();
                string value = part[(idx + 1)..].Trim();
                map[key] = value;
            }
            else
            {
                map[part.Trim()] = "true";
            }
        }

        return map;
    }

    /// <summary>
    /// Removes surrounding quotes from a string argument.
    /// </summary>
    public static string ParseString(string? arg)
    {
        if (string.IsNullOrEmpty(arg)) return string.Empty;

        var trimmed = arg.Trim();
        
        if ((trimmed.StartsWith('\'') && trimmed.EndsWith('\'')) ||
            (trimmed.StartsWith('"') && trimmed.EndsWith('"')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    /// <summary>
    /// Parses a boolean value from various string representations.
    /// </summary>
    public static bool ParseBool(string? raw, bool defaultValue = false)
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

    /// <summary>
    /// Returns the first non-empty string from the provided values.
    /// </summary>
    public static string? ChooseFirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));
}
```

2. ⬜ Create specific config builders:

```csharp
// Configuration/DirectoryIngestionConfig.cs (continued)
public static class DirectoryIngestionConfigBuilder
{
    public static Result<DirectoryIngestionConfig> Parse(
        string? args,
        string defaultRoot)
    {
        var defaults = new DirectoryIngestionConfig { Root = defaultRoot };
        
        return ConfigParser.Parse(args, defaults, (dict, config) =>
        {
            var root = dict.GetValueOrDefault("root", config.Root);
            var recursive = !dict.ContainsKey("norec");
            
            var extensions = dict.TryGetValue("ext", out var extStr)
                ? extStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.Extensions.ToList();
                
            var excludeDirs = dict.TryGetValue("exclude", out var excStr)
                ? excStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.ExcludeDirectories.ToList();
                
            var patterns = dict.TryGetValue("pattern", out var patStr)
                ? patStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.Patterns.ToList();
                
            var maxBytes = dict.TryGetValue("max", out var maxStr) && long.TryParse(maxStr, out var mb)
                ? mb
                : config.MaxFileBytes;
                
            var batchSize = dict.TryGetValue("batch", out var batchStr) && int.TryParse(batchStr, out var bs)
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
                BatchSize = batchSize
            });
        });
    }
}
```

**Files to Create:**
- `src/MonadicPipeline.CLI/Utilities/ConfigParser.cs`
- Configuration builders in respective config files

**Acceptance Criteria:**
- All parsing functions are pure (no side effects)
- Unit tests pass for all parsing scenarios
- Error cases return proper Result.Failure

---

### Step 1.4: Refactor UseDir with New Infrastructure

**Objective:** Refactor `UseDir` as a proof-of-concept using new patterns.

**Tasks:**
1. ⬜ Create directory ingestion service:

```csharp
// Services/DirectoryIngestionService.cs
namespace LangChainPipeline.CLI.Services;

/// <summary>
/// Service for ingesting documents from a directory into the vector store.
/// </summary>
public static class DirectoryIngestionService
{
    /// <summary>
    /// Ingests documents from a directory according to the provided configuration.
    /// </summary>
    public static async Task<Result<DirectoryIngestionResult>> IngestAsync(
        DirectoryIngestionConfig config,
        IVectorStore store,
        IEmbeddingModel embedModel)
    {
        try
        {
            var options = CreateIngestionOptions(config);
            var loader = new DirectoryDocumentLoader<FileLoader>(options);
            var stats = new DirectoryIngestionStats();
            loader.AttachStats(stats);

            var splitter = new RecursiveCharacterTextSplitter(
                chunkSize: config.ChunkSize,
                chunkOverlap: config.ChunkOverlap);

            var docs = await loader.LoadAsync(DataSource.FromPath(config.Root));
            var vectors = await CreateVectorsAsync(docs, splitter, embedModel, config.Root);

            if (config.BatchSize > 0)
            {
                await AddVectorsBatchedAsync(store, vectors, config.BatchSize);
            }
            else
            {
                await store.AddAsync(vectors);
            }

            stats.VectorsProduced += vectors.Count;

            return Result<DirectoryIngestionResult>.Success(new DirectoryIngestionResult
            {
                VectorIds = vectors.Select(v => v.Id).ToList(),
                Stats = stats
            });
        }
        catch (Exception ex)
        {
            return Result<DirectoryIngestionResult>.Failure(
                $"Directory ingestion failed: {ex.Message}");
        }
    }

    private static DirectoryIngestionOptions CreateIngestionOptions(DirectoryIngestionConfig config)
        => new()
        {
            Recursive = config.Recursive,
            Extensions = config.Extensions.Count == 0 ? null : config.Extensions.ToArray(),
            ExcludeDirectories = config.ExcludeDirectories.Count == 0 ? null : config.ExcludeDirectories.ToArray(),
            Patterns = config.Patterns.ToArray(),
            MaxFileBytes = config.MaxFileBytes,
            ChunkSize = config.ChunkSize,
            ChunkOverlap = config.ChunkOverlap
        };

    private static async Task<List<Vector>> CreateVectorsAsync(
        IEnumerable<Document> docs,
        RecursiveCharacterTextSplitter splitter,
        IEmbeddingModel embedModel,
        string root)
    {
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
            var baseMetadata = BuildDocumentMetadata(doc, root, fileIndex);

            for (int chunkIdx = 0; chunkIdx < chunks.Count; chunkIdx++)
            {
                string chunk = chunks[chunkIdx];
                string vectorId = $"dir:{fileIndex}:{chunkIdx}";
                var chunkMetadata = BuildChunkMetadata(baseMetadata, chunkIdx, chunks.Count, vectorId);

                try
                {
                    var embedding = await embedModel.CreateEmbeddingsAsync(chunk);
                    vectors.Add(new Vector
                    {
                        Id = vectorId,
                        Text = chunk,
                        Embedding = embedding,
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
            }

            fileIndex++;
        }

        return vectors;
    }

    private static async Task AddVectorsBatchedAsync(
        IVectorStore store,
        List<Vector> vectors,
        int batchSize)
    {
        for (int i = 0; i < vectors.Count; i += batchSize)
        {
            var batch = vectors.Skip(i).Take(batchSize).ToList();
            await store.AddAsync(batch);
        }
    }

    // Helper methods moved from CliSteps
    private static Dictionary<string, object> BuildDocumentMetadata(Document doc, string root, int fileIndex)
    {
        // ... implementation from CliSteps (lines 1753-1793)
    }

    private static Dictionary<string, object> BuildChunkMetadata(
        Dictionary<string, object> baseMetadata,
        int chunkIndex,
        int chunkCount,
        string vectorId)
    {
        // ... implementation from CliSteps (lines 1796-1809)
    }
}

public record DirectoryIngestionResult
{
    public required IReadOnlyList<string> VectorIds { get; init; }
    public required DirectoryIngestionStats Stats { get; init; }
}
```

2. ⬜ Refactor UseDir:

```csharp
// CliSteps.cs (refactored UseDir)
[PipelineToken("UseDir", "DirIngest")]
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
                    success: result =>
                    {
                        if (s.Trace)
                            Console.WriteLine($"[dir] {result.Stats}");
                        
                        return s.WithBranch(
                            s.Branch.WithIngestEvent(
                                $"dir:ingest:{Path.GetFileName(config.Root)}",
                                result.VectorIds));
                    },
                    failure: error => s.WithBranch(
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
```

**Files to Create:**
- `src/MonadicPipeline.CLI/Services/DirectoryIngestionService.cs`

**Files to Modify:**
- `src/MonadicPipeline.CLI/CliSteps.cs` (UseDir method)

**Acceptance Criteria:**
- UseDir compiles and passes existing tests
- Code is <30 lines in CliSteps.cs
- Business logic is in DirectoryIngestionService
- All error paths use Result monad
- No try-catch in UseDir itself

---

### Step 1.5: Apply Pattern to UseDirBatched

**Objective:** Eliminate duplication between UseDir and UseDirBatched.

**Tasks:**
1. ⬜ Verify DirectoryIngestionService supports batching via config
2. ⬜ Refactor UseDirBatched to use same service:

```csharp
[PipelineToken("UseDirBatched", "DirIngestBatched")]
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
                    success: result =>
                    {
                        if (s.Trace)
                            Console.WriteLine($"[dir-batched] {result.Stats}");
                        
                        return s.WithBranch(
                            s.Branch.WithIngestEvent(
                                $"dir:ingest-batched:{Path.GetFileName(config.Root)}",
                                Array.Empty<string>())); // Batched doesn't track individual IDs
                    },
                    failure: error => s.WithBranch(
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
```

**Acceptance Criteria:**
- UseDirBatched reuses DirectoryIngestionService
- ~150 lines of code eliminated
- Both methods pass tests
- Identical behavior to original implementation

---

### Step 1.6: Apply Pattern to Other Critical Methods

**Objective:** Refactor high-priority methods using established patterns.

**Methods to Refactor:**
1. ⬜ `UseIngest` - simple, good starting point
2. ⬜ `UseSolution` - moderate complexity
3. ⬜ `ZipIngest` - complex, create ZipIngestionService
4. ⬜ `ZipStream` - related to ZipIngest

**For each method:**
1. Create configuration record
2. Create configuration builder
3. Create service with business logic
4. Refactor CLI step to use service
5. Update tests
6. Verify behavior unchanged

**Acceptance Criteria:**
- All 4 methods refactored
- Each CLI step method <30 lines
- All use Result monad for error handling
- All tests pass

---

## Phase 2: Quality Improvements (6-7 days)

### Step 2.1: Decompose Large RAG Methods

**Objective:** Break down `DivideAndConquerRag` and `DecomposeAndAggregateRag` into smaller functions.

**Tasks:**
1. ⬜ Create RagService:

```csharp
// Services/RagService.cs
public static class RagService
{
    public static async Task<Result<string>> DivideAndConquerAsync(
        DivideAndConquerRagConfig config,
        CliPipelineState state)
    {
        var retrievalResult = await EnsureDocuments(state, config.RetrievalCount);
        return await retrievalResult.BindAsync(docs =>
            ProcessWithGrouping(docs, config, state));
    }

    private static async Task<Result<List<string>>> EnsureDocuments(
        CliPipelineState state,
        int count)
    {
        // Retrieval logic
    }

    private static async Task<Result<string>> ProcessWithGrouping(
        List<string> docs,
        DivideAndConquerRagConfig config,
        CliPipelineState state)
    {
        var groups = PartitionDocuments(docs, config.GroupSize);
        var partials = await GeneratePartialAnswers(groups, config, state);
        return await SynthesizeFinalAnswer(partials, config, state);
    }

    // ... more focused methods
}
```

2. ⬜ Refactor both methods to use service
3. ⬜ Extract template management to separate module

**Acceptance Criteria:**
- Each service method <50 lines
- Clear single responsibility per method
- All tests pass

---

### Step 2.2: Refactor EnhanceMarkdown

**Objective:** Simplify markdown enhancement logic.

**Tasks:**
1. ⬜ Create MarkdownService
2. ⬜ Extract context building
3. ⬜ Extract prompt generation
4. ⬜ Extract output normalization
5. ⬜ Refactor CLI step

**Acceptance Criteria:**
- EnhanceMarkdown <30 lines in CliSteps
- Helper methods in MarkdownService
- Tests pass

---

### Step 2.3: Extract Remaining Constants

**Objective:** Eliminate all magic strings and numbers.

**Tasks:**
1. ⬜ Audit codebase for hardcoded values
2. ⬜ Add to DefaultIngestionSettings
3. ⬜ Replace all occurrences
4. ⬜ Update documentation

**Acceptance Criteria:**
- Zero magic numbers in methods
- All constants in configuration classes
- Build succeeds with no warnings

---

### Step 2.4: Standardize Documentation

**Objective:** Ensure all public methods have complete XML documentation.

**Tasks:**
1. ⬜ Create documentation template
2. ⬜ Document all [PipelineToken] methods
3. ⬜ Document all public helpers
4. ⬜ Add usage examples

**Example Template:**
```csharp
/// <summary>
/// [Brief description of what the step does]
/// </summary>
/// <param name="args">
/// Configuration string format: '[key=value|key=value|...]'
/// Supported parameters:
/// - param1: description (default: value)
/// - param2: description (required)
/// </param>
/// <returns>
/// A pipeline step that transforms CliPipelineState by [specific transformation].
/// On success, adds [event type] event. On failure, adds error event.
/// </returns>
/// <example>
/// <code>
/// // Example 1: Basic usage
/// UseToken('param1=value')
/// 
/// // Example 2: Multiple parameters
/// UseToken('param1=value1|param2=value2')
/// </code>
/// </example>
/// <remarks>
/// [Any important notes, side effects, or caveats]
/// </remarks>
```

**Acceptance Criteria:**
- 100% public method documentation
- All parameters documented
- Examples for complex methods
- Build succeeds with documentation warnings enabled

---

## Phase 3: Architectural Refinement (3 days)

### Step 3.1: Improve UseExternalChain

**Objective:** Reduce reflection complexity and improve maintainability.

**Tasks:**
1. ⬜ Create IExternalChain adapter interface
2. ⬜ Create typed chain wrappers
3. ⬜ Refactor registration to use adapters
4. ⬜ Add better error messages

**Acceptance Criteria:**
- Reduced reflection calls by 80%
- Clear error messages for common failures
- Tests pass

---

### Step 3.2: Add Structured Logging (Optional)

**Objective:** Replace Console.WriteLine with ILogger.

**Tasks:**
1. ⬜ Add ILogger parameter to CliPipelineState (optional)
2. ⬜ Create logging extension methods
3. ⬜ Replace Console.WriteLine calls
4. ⬜ Add log levels

**Acceptance Criteria:**
- Configurable logging
- No Console.WriteLine in production paths
- Backward compatible (logging optional)

---

## Testing Strategy

### Unit Tests to Create

For each refactored component:

1. **Configuration Parsers**
   ```csharp
   [Fact]
   public void DirectoryConfig_Parse_ValidArgs_ReturnsSuccess()
   [Fact]
   public void DirectoryConfig_Parse_InvalidDir_ReturnsFailure()
   [Fact]
   public void DirectoryConfig_Parse_MissingOptional_UsesDefaults()
   ```

2. **Services**
   ```csharp
   [Fact]
   public async Task DirectoryIngestion_ValidConfig_ReturnsVectors()
   [Fact]
   public async Task DirectoryIngestion_EmbedFailure_ReturnsFallback()
   [Fact]
   public async Task DirectoryIngestion_Batched_AddsInChunks()
   ```

3. **CLI Steps**
   ```csharp
   [Fact]
   public async Task UseDir_ValidArgs_IngestsFiles()
   [Fact]
   public async Task UseDir_InvalidDir_ReturnsErrorEvent()
   [Fact]
   public async Task UseDir_Integration_EndToEnd()
   ```

**Target:** >80% code coverage for new code

---

## Integration Testing

1. ⬜ Run existing CliEndToEndTests
2. ⬜ Add new tests for refactored methods
3. ⬜ Test error paths explicitly
4. ⬜ Test backward compatibility

---

## Validation Checklist

Before considering each phase complete:

- [ ] All new code compiles without warnings
- [ ] All existing tests pass
- [ ] New tests added and passing (>80% coverage)
- [ ] Code review completed
- [ ] Documentation updated
- [ ] No performance regression (benchmark if needed)
- [ ] Backward compatibility verified
- [ ] StyleCop rules satisfied

---

## Success Metrics

### Quantitative

| Metric | Before | Target | Actual |
|--------|--------|--------|--------|
| Total Lines | 2,108 | <1,500 | TBD |
| Avg Method Lines | 39.8 | <25 | TBD |
| Max Method Lines | 180 | <80 | TBD |
| Cyclomatic Complexity (avg) | ~10 | <7 | TBD |
| Code Duplication | ~20% | <5% | TBD |
| XML Documentation | ~50% | >80% | TBD |
| Test Coverage | Unknown | >80% | TBD |

### Qualitative

- [ ] All error handling uses Result<T> monad
- [ ] No try-catch at method level (only in services)
- [ ] All configuration uses typed records
- [ ] All methods follow SRP
- [ ] All magic values in constants
- [ ] Consistent documentation format
- [ ] All tests pass
- [ ] Backward compatible

---

## Risk Mitigation

### Identified Risks

1. **Breaking Changes to CLI Syntax**
   - Mitigation: Maintain exact argument parsing behavior
   - Validation: Comprehensive integration tests

2. **Performance Regression**
   - Mitigation: Profile before/after refactoring
   - Validation: Benchmark critical paths

3. **Incomplete Error Handling**
   - Mitigation: Explicit Result types for all paths
   - Validation: Test error scenarios

4. **Loss of Functionality**
   - Mitigation: Maintain feature parity
   - Validation: Side-by-side comparison tests

---

## Timeline

### Week 1-2: Phase 1 (Critical Fixes)
- Days 1-2: Infrastructure (Result extensions, config types, parsers)
- Days 3-4: UseDir/UseDirBatched refactoring
- Days 5-6: UseIngest, UseSolution
- Days 7-8: ZipIngest, ZipStream

### Week 3-4: Phase 2 (Quality)
- Days 9-10: RAG method decomposition
- Days 11-12: EnhanceMarkdown, constants
- Days 13-14: Documentation
- Days 15: Buffer/testing

### Week 5: Phase 3 (Refinement)
- Days 16-17: Chain integration improvements
- Day 18: Final testing and validation
- Days 19-22: Buffer for unexpected issues

---

## Progress Tracking

| Step | Status | Assigned | Started | Completed |
|------|--------|----------|---------|-----------|
| 1.1 Result Infrastructure | ⬜ Not Started | - | - | - |
| 1.2 Configuration Types | ⬜ Not Started | - | - | - |
| 1.3 Configuration Parser | ⬜ Not Started | - | - | - |
| 1.4 Refactor UseDir | ⬜ Not Started | - | - | - |
| 1.5 Refactor UseDirBatched | ⬜ Not Started | - | - | - |
| 1.6 Other Critical Methods | ⬜ Not Started | - | - | - |
| 2.1 Decompose RAG Methods | ⬜ Not Started | - | - | - |
| 2.2 EnhanceMarkdown | ⬜ Not Started | - | - | - |
| 2.3 Extract Constants | ⬜ Not Started | - | - | - |
| 2.4 Documentation | ⬜ Not Started | - | - | - |
| 3.1 Chain Integration | ⬜ Not Started | - | - | - |
| 3.2 Structured Logging | ⬜ Not Started | - | - | - |

**Legend:**
- ✅ Complete
- 🔄 In Progress
- ⬜ Not Started
- ⏸️ Blocked
- ❌ Cancelled

---

## Notes

- This plan maintains backward compatibility with existing CLI syntax
- All refactorings preserve current functionality
- Tests should be written before refactoring (TDD approach recommended)
- Each step can be done incrementally with commits
- PR reviews after each major step

---

*Last Updated: 2025-11-10*
*Next Review: After Phase 1 completion*
