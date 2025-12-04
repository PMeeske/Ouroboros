# Feature Engineering Infrastructure

This document describes the C# code vectorization and streaming deduplication features added to Ouroboros.

## Overview

The Feature Engineering infrastructure provides two main components:

1. **CSharpHashVectorizer** - Fast, deterministic code vectorization using bag-of-tokens and hashing
2. **StreamDeduplicator** - Real-time deduplication of vectors using similarity-based filtering

## CSharpHashVectorizer

### Purpose

Transforms C# source code into fixed-dimension numerical vectors suitable for:
- Code similarity search
- Duplicate detection
- Clustering and classification
- Refactoring hints
- Code recommendation systems

### Features

- **Deterministic**: Same code always produces the same vector
- **Fast**: Uses efficient hashing (XxHash32) instead of ML models
- **Configurable**: Adjustable vector dimension (power of 2: 256 to 262144)
- **C#-aware**: Handles C# keywords and identifiers intelligently
- **Normalized**: All vectors are L2-normalized for cosine similarity

### Basic Usage

```csharp
using LangChainPipeline.Infrastructure.FeatureEngineering;

// Create a vectorizer with 4096-dimensional vectors
var vectorizer = new CSharpHashVectorizer(dimension: 4096, lowercase: true);

// Vectorize code
var code = "public class Calculator { public int Add(int a, int b) => a + b; }";
var vector = vectorizer.TransformCode(code);

// Vectorize a file
var fileVector = vectorizer.TransformFile("MyClass.cs");

// Batch vectorization
var files = new[] { "File1.cs", "File2.cs", "File3.cs" };
var vectors = vectorizer.TransformFiles(files);

// Async operations
var asyncVector = await vectorizer.TransformCodeAsync(code);
var asyncVectors = await vectorizer.TransformFilesAsync(files);
```

### Similarity Comparison

```csharp
var code1 = "public int Add(int a, int b) => a + b;";
var code2 = "public int Add(int x, int y) => x + y;";

var vector1 = vectorizer.TransformCode(code1);
var vector2 = vectorizer.TransformCode(code2);

var similarity = CSharpHashVectorizer.CosineSimilarity(vector1, vector2);
// similarity ≈ 0.67 (similar structure, different variable names)
```

### Configuration

```csharp
// Small, fast vectors for quick comparisons
var small = new CSharpHashVectorizer(dimension: 256);

// Default balanced configuration
var balanced = new CSharpHashVectorizer(dimension: 4096);

// Large, high-precision vectors
var large = new CSharpHashVectorizer(dimension: 65536);

// Case-sensitive identifiers
var caseSensitive = new CSharpHashVectorizer(dimension: 4096, lowercase: false);
```

### Use Cases

#### 1. Duplicate Detection

```csharp
var vectorizer = new CSharpHashVectorizer(4096);
var codeFiles = Directory.GetFiles("src", "*.cs", SearchOption.AllDirectories);

var vectors = codeFiles.ToDictionary(
    file => file,
    file => vectorizer.TransformFile(file)
);

// Find duplicates
foreach (var file1 in vectors.Keys)
{
    foreach (var file2 in vectors.Keys.Where(f => string.CompareOrdinal(f, file1) > 0))
    {
        var similarity = CSharpHashVectorizer.CosineSimilarity(
            vectors[file1], 
            vectors[file2]
        );
        
        if (similarity > 0.95f)
        {
            Console.WriteLine($"Potential duplicate: {file1} <-> {file2} ({similarity:F4})");
        }
    }
}
```

#### 2. Code Similarity Search

```csharp
var vectorizer = new CSharpHashVectorizer(4096);

// Index a codebase
var codebase = new Dictionary<string, float[]>();
foreach (var file in Directory.GetFiles("src", "*.cs"))
{
    codebase[file] = vectorizer.TransformFile(file);
}

// Find similar code
var query = "public int Sum(int a, int b) => a + b;";
var queryVector = vectorizer.TransformCode(query);

var topMatches = codebase
    .Select(kvp => new { 
        File = kvp.Key, 
        Similarity = CSharpHashVectorizer.CosineSimilarity(queryVector, kvp.Value) 
    })
    .OrderByDescending(x => x.Similarity)
    .Take(5);

foreach (var match in topMatches)
{
    Console.WriteLine($"{match.File}: {match.Similarity:F4}");
}
```

## StreamDeduplicator

### Purpose

Filters redundant (nearly identical) vectors from data streams in real-time:
- Log deduplication
- Real-time code change filtering
- Streaming data preprocessing
- Memory-efficient duplicate detection

### Features

- **Real-time**: Processes streams as they arrive
- **Configurable threshold**: Adjustable similarity threshold
- **LRU cache**: Efficient memory management with automatic eviction
- **Thread-safe**: Safe for concurrent access
- **Async support**: Works with `IAsyncEnumerable<T>`

### Basic Usage

```csharp
using LangChainPipeline.Infrastructure.FeatureEngineering;

// Create a deduplicator
var deduplicator = new StreamDeduplicator(
    similarityThreshold: 0.95f,  // 95% similarity threshold
    maxCacheSize: 1000            // Keep last 1000 unique items
);

// Check individual vectors
var vector = new float[] { 0.1f, 0.2f, 0.3f };
if (!deduplicator.IsDuplicate(vector))
{
    // Process unique vector
    ProcessVector(vector);
}

// Filter a batch
var vectors = GetVectors();
var uniqueVectors = deduplicator.FilterBatch(vectors);

// Filter an async stream
await foreach (var uniqueVector in deduplicator.FilterStreamAsync(GetVectorsAsync()))
{
    await ProcessVectorAsync(uniqueVector);
}
```

### Extension Methods

```csharp
// Fluent API for batch deduplication
var uniqueVectors = vectors.Deduplicate(
    similarityThreshold: 0.95f, 
    maxCacheSize: 1000
);

// Fluent API for async streams
await foreach (var vector in GetVectorsAsync().Deduplicate(deduplicator))
{
    await ProcessAsync(vector);
}
```

### Cache Management

```csharp
// Get statistics
var (cacheSize, maxSize, threshold) = deduplicator.GetStatistics();
Console.WriteLine($"Cache: {cacheSize}/{maxSize}, Threshold: {threshold}");

// Clear cache
deduplicator.ClearCache();

// Check current cache size
Console.WriteLine($"Current cache size: {deduplicator.CacheSize}");
```

### Use Cases

#### 1. Log Deduplication

```csharp
var vectorizer = new CSharpHashVectorizer(4096);
var deduplicator = new StreamDeduplicator(0.95f, 1000);

async IAsyncEnumerable<string> ProcessLogsAsync(IAsyncEnumerable<string> logStream)
{
    await foreach (var log in logStream)
    {
        var vector = vectorizer.TransformCode(log);
        
        if (!deduplicator.IsDuplicate(vector))
        {
            yield return log; // Only unique logs
        }
    }
}
```

#### 2. Real-time Code Change Filtering

```csharp
var vectorizer = new CSharpHashVectorizer(4096);
var deduplicator = new StreamDeduplicator(0.98f, 500);

// Watch for file changes
var watcher = new FileSystemWatcher("src", "*.cs");
watcher.Changed += (sender, e) =>
{
    var code = File.ReadAllText(e.FullPath);
    var vector = vectorizer.TransformCode(code);
    
    if (!deduplicator.IsDuplicate(vector))
    {
        Console.WriteLine($"Significant change detected in {e.Name}");
        // Trigger rebuild, analysis, etc.
    }
};
```

#### 3. Combined Vectorization and Deduplication

```csharp
var vectorizer = new CSharpHashVectorizer(4096);
var deduplicator = new StreamDeduplicator(0.95f, 1000);

var codeFiles = Directory.GetFiles("src", "*.cs");

// Find unique code patterns
var uniqueVectors = codeFiles
    .Select(file => vectorizer.TransformFile(file))
    .Deduplicate(0.95f, 1000);

Console.WriteLine($"Found {uniqueVectors.Count} unique code patterns out of {codeFiles.Length} files");
```

## Performance Considerations

### Vector Dimension

- **256-1024**: Fast, low memory, suitable for rough similarity
- **4096-16384**: Balanced performance and accuracy (recommended)
- **65536-262144**: High accuracy, more memory and CPU

### Similarity Threshold

- **0.90-0.95**: Strict duplicate detection
- **0.80-0.90**: Similar code detection
- **0.70-0.80**: Related code detection
- **< 0.70**: Broad similarity

### Cache Size

- Choose based on expected unique items in your stream
- Larger cache = better duplicate detection, more memory
- Smaller cache = less memory, may miss older duplicates

## Integration with Ouroboros

These components follow Ouroboros's functional programming principles:

```csharp
using LangChainPipeline.Core.Kleisli;
using LangChainPipeline.Infrastructure.FeatureEngineering;

// Create a pipeline step for code vectorization
Step<string, float[]> VectorizeStep(CSharpHashVectorizer vectorizer) =>
    async code =>
    {
        var vector = vectorizer.TransformCode(code);
        return vector;
    };

// Compose with deduplication
var vectorizer = new CSharpHashVectorizer(4096);
var deduplicator = new StreamDeduplicator(0.95f, 1000);

var pipeline = VectorizeStep(vectorizer)
    .Map(vector => deduplicator.IsDuplicate(vector) ? null : vector)
    .Map(vector => vector != null ? ProcessUnique(vector) : null);
```

## Examples

See `src/Ouroboros.Examples/Examples/FeatureEngineeringExamples.cs` for comprehensive examples including:

1. Basic code vectorization
2. Batch vectorization
3. Stream deduplication
4. Async stream processing
5. Fluent API usage
6. Code similarity search
7. Duplicate detection

Run the examples:

```bash
dotnet run --project src/Ouroboros.Examples
```

## Architecture

### CSharpHashVectorizer

- **Tokenization**: Regex-based token extraction
- **Keyword Normalization**: C# keywords always lowercase
- **Hashing**: XxHash32 for fast, high-quality hashing
- **Feature Extraction**: Signed hashing with term frequency weighting
- **Normalization**: L2 normalization for cosine similarity

### StreamDeduplicator

- **Similarity Check**: Cosine similarity between vectors
- **Caching**: LRU (Least Recently Used) eviction policy
- **Thread Safety**: Lock-based synchronization
- **Memory Management**: Bounded cache with configurable size

## Testing

Both components have comprehensive test coverage:

- **CSharpHashVectorizer**: 28 unit tests
- **StreamDeduplicator**: 18 unit tests

Run tests:

```bash
dotnet test --filter "FullyQualifiedName~CSharpHashVectorizer|FullyQualifiedName~StreamDeduplicator"
```

## Future Enhancements

Potential improvements:

1. **Parallel Processing**: Multi-threaded vectorization for large batches
2. **Persistent Cache**: Disk-backed deduplication cache
3. **Semantic Parsing**: AST-based code analysis (more accurate but slower)
4. **Language Extensions**: Support for other languages (TypeScript, Python)
5. **Approximate Nearest Neighbors**: HNSW or LSH for faster similarity search

## References

- XxHash: Fast hashing algorithm by Yann Collet
- Cosine Similarity: Standard metric for vector similarity
- Feature Hashing: Weinberger et al., "Feature Hashing for Large Scale Multitask Learning"
