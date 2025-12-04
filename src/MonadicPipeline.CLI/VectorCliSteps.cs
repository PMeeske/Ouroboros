// <copyright file="VectorCliSteps.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChainPipeline.CLI;
using LangChainPipeline.Core.Configuration;
using LangChainPipeline.Domain.Vectors;
using MonadicPipeline.CLI;

namespace LangChainPipeline.CLI;

/// <summary>
/// CLI Pipeline steps for vector store operations.
/// Supports in-memory, Qdrant, and other IVectorStore implementations.
/// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
/// </summary>
public static class VectorCliSteps
{
    /// <summary>
    /// Initialize vector store from configuration or explicit type.
    /// Usage: VectorInit('Qdrant;connection=http://localhost:6334;collection=my_vectors')
    /// Usage: VectorInit('InMemory')
    /// </summary>
    [PipelineToken("VectorInit", "InitVector")]
    public static Step<CliPipelineState, CliPipelineState> VectorInit(string? args = null)
        => s =>
        {
            var parsed = ParseVectorArgs(args);
            
            var config = new VectorStoreConfiguration
            {
                Type = parsed.Type,
                ConnectionString = parsed.ConnectionString,
                DefaultCollection = parsed.CollectionName
            };

            try
            {
                var factory = new VectorStoreFactory(config);
                s.VectorStore = factory.Create();
                
                if (s.Trace) Console.WriteLine($"[vector] Initialized {config.Type} store (collection: {config.DefaultCollection})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to initialize store: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Initialize Qdrant vector store specifically.
    /// Usage: UseQdrant('http://localhost:6334;collection=my_vectors')
    /// Usage: UseQdrant() - uses default localhost:6334 with pipeline_vectors collection
    /// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
    /// </summary>
    [PipelineToken("UseQdrant", "QdrantInit")]
    public static Step<CliPipelineState, CliPipelineState> UseQdrant(string? args = null)
        => s =>
        {
            var parsed = ParseVectorArgs(args);
            
            // Default to localhost gRPC port for Qdrant if no connection string
            string connectionString = string.IsNullOrEmpty(parsed.ConnectionString) 
                ? "http://localhost:6334" 
                : parsed.ConnectionString;

            try
            {
                s.VectorStore = new QdrantVectorStore(connectionString, parsed.CollectionName);
                
                if (s.Trace) Console.WriteLine($"[vector] Connected to Qdrant at {connectionString} (collection: {parsed.CollectionName})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to connect to Qdrant: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Use in-memory vector store (TrackedVectorStore).
    /// Usage: UseInMemory()
    /// </summary>
    [PipelineToken("UseInMemory", "MemoryVector")]
    public static Step<CliPipelineState, CliPipelineState> UseInMemory(string? args = null)
        => s =>
        {
            s.VectorStore = new TrackedVectorStore();
            
            if (s.Trace) Console.WriteLine("[vector] Using in-memory vector store");

            return Task.FromResult(s);
        };

    /// <summary>
    /// Embed and store text in the vector store.
    /// Usage: VectorAdd('text to embed and store')
    /// Usage: VectorAdd() - uses current Context
    /// </summary>
    [PipelineToken("VectorAdd", "AddVector", "Vectorize")]
    public static Step<CliPipelineState, CliPipelineState> VectorAdd(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                // Default to in-memory if not initialized
                s.VectorStore = new TrackedVectorStore();
                if (s.Trace) Console.WriteLine("[vector] Auto-initialized in-memory store");
            }

            string text = ParseString(args);
            if (string.IsNullOrWhiteSpace(text))
            {
                text = s.Context;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[vector] No text to embed");
                return s;
            }

            try
            {
                // Split text into chunks if it's long (simple chunking)
                var chunks = ChunkText(text, 500);
                
                foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                {
                    var embedding = await s.Embed.CreateEmbeddingsAsync(chunk);
                    
                    var vector = new Vector
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = chunk,
                        Embedding = embedding
                    };

                    await s.VectorStore.AddAsync(new[] { vector });
                    
                    if (s.Trace) Console.WriteLine($"[vector] Added chunk {index + 1}/{chunks.Count} ({embedding.Length} dims)");
                }
                
                if (s.Trace) Console.WriteLine($"[vector] Stored {chunks.Count} chunks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to add vector: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Search the vector store for similar documents.
    /// Usage: VectorSearch('query text')
    /// Usage: VectorSearch() - uses current Query
    /// </summary>
    [PipelineToken("VectorSearch", "SearchVector", "Retrieve")]
    public static Step<CliPipelineState, CliPipelineState> VectorSearch(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                Console.WriteLine("[vector] No vector store initialized");
                return s;
            }

            string query = ParseString(args);
            if (string.IsNullOrWhiteSpace(query))
            {
                query = s.Query;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[vector] No query provided");
                return s;
            }

            try
            {
                var queryEmbedding = await s.Embed.CreateEmbeddingsAsync(query);
                var results = await s.VectorStore.GetSimilarDocumentsAsync(queryEmbedding, s.RetrievalK);

                s.Retrieved.Clear();
                foreach (var doc in results)
                {
                    s.Retrieved.Add(doc.PageContent);
                }

                // Build context from retrieved documents
                s.Context = string.Join("\n\n---\n\n", s.Retrieved);
                
                if (s.Trace)
                {
                    Console.WriteLine($"[vector] Found {results.Count} similar documents");
                    foreach (var (doc, i) in results.Select((d, i) => (d, i)))
                    {
                        var preview = doc.PageContent.Length > 100 
                            ? doc.PageContent[..100] + "..." 
                            : doc.PageContent;
                        Console.WriteLine($"  {i + 1}. {preview}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Search failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Clear all vectors from the store.
    /// Usage: VectorClear()
    /// </summary>
    [PipelineToken("VectorClear", "ClearVector")]
    public static Step<CliPipelineState, CliPipelineState> VectorClear(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                Console.WriteLine("[vector] No vector store to clear");
                return s;
            }

            try
            {
                await s.VectorStore.ClearAsync();
                
                if (s.Trace) Console.WriteLine("[vector] Store cleared");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to clear store: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Ingest a file into the vector store.
    /// Usage: VectorIngestFile('path/to/file.txt')
    /// </summary>
    [PipelineToken("VectorIngestFile", "IngestFile")]
    public static Step<CliPipelineState, CliPipelineState> VectorIngestFile(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                s.VectorStore = new TrackedVectorStore();
                if (s.Trace) Console.WriteLine("[vector] Auto-initialized in-memory store");
            }

            string path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("[vector] No file path provided");
                return s;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"[vector] File not found: {fullPath}");
                    return s;
                }

                // Read with fallback encoding to handle special characters
                string content;
                try
                {
                    content = await File.ReadAllTextAsync(fullPath, System.Text.Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Fallback: read as bytes and decode with replacement for invalid chars
                    var bytes = await File.ReadAllBytesAsync(fullPath);
                    content = System.Text.Encoding.UTF8.GetString(bytes).Replace("\uFFFD", "?");
                }
                
                string fileName = Path.GetFileName(fullPath);

                // Chunk the content
                var chunks = ChunkText(content, 500);

                foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                {
                    try
                    {
                        // Sanitize chunk for embedding (remove problematic chars)
                        var sanitizedChunk = SanitizeForEmbedding(chunk);
                        if (string.IsNullOrWhiteSpace(sanitizedChunk)) continue;
                        
                        var embedding = await s.Embed.CreateEmbeddingsAsync(sanitizedChunk);
                        
                        var vector = new Vector
                        {
                            Id = Guid.NewGuid().ToString(),
                            Text = sanitizedChunk,
                            Embedding = embedding,
                            Metadata = new Dictionary<string, object>
                            {
                                ["source"] = fileName,
                                ["chunk_index"] = index
                            }
                        };

                        await s.VectorStore.AddAsync(new[] { vector });
                    }
                    catch (Exception chunkEx)
                    {
                        if (s.Trace) Console.WriteLine($"[vector] Skipped chunk {index}: {chunkEx.Message}");
                    }
                }

                if (s.Trace) Console.WriteLine($"[vector] Ingested {fileName}: {chunks.Count} chunks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to ingest file: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Ingest all files from a directory into the vector store.
    /// Usage: VectorIngestDir('path/to/dir;pattern=*.cs')
    /// </summary>
    [PipelineToken("VectorIngestDir", "IngestDir")]
    public static Step<CliPipelineState, CliPipelineState> VectorIngestDir(string? args = null)
        => async s =>
        {
            if (s.VectorStore == null)
            {
                s.VectorStore = new TrackedVectorStore();
                if (s.Trace) Console.WriteLine("[vector] Auto-initialized in-memory store");
            }

            var parsed = ParseDirArgs(args);

            try
            {
                string fullPath = Path.GetFullPath(parsed.Path);
                if (!Directory.Exists(fullPath))
                {
                    Console.WriteLine($"[vector] Directory not found: {fullPath}");
                    return s;
                }

                var files = Directory.GetFiles(fullPath, parsed.Pattern, SearchOption.AllDirectories);
                int totalChunks = 0;

                foreach (var file in files)
                {
                    try
                    {
                        // Read with fallback encoding
                        string content;
                        try
                        {
                            content = await File.ReadAllTextAsync(file, System.Text.Encoding.UTF8);
                        }
                        catch (Exception)
                        {
                            var bytes = await File.ReadAllBytesAsync(file);
                            content = System.Text.Encoding.UTF8.GetString(bytes).Replace("\uFFFD", "?");
                        }
                        
                        string relativePath = Path.GetRelativePath(fullPath, file);

                        var chunks = ChunkText(content, 500);
                        int successChunks = 0;

                        foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
                        {
                            try
                            {
                                var sanitizedChunk = SanitizeForEmbedding(chunk);
                                if (string.IsNullOrWhiteSpace(sanitizedChunk)) continue;
                                
                                var embedding = await s.Embed.CreateEmbeddingsAsync(sanitizedChunk);
                                
                                var vector = new Vector
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Text = sanitizedChunk,
                                    Embedding = embedding,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["source"] = relativePath,
                                        ["chunk_index"] = index
                                    }
                                };

                                await s.VectorStore.AddAsync(new[] { vector });
                                successChunks++;
                            }
                            catch (Exception)
                            {
                                // Skip problematic chunks silently
                            }
                        }

                        totalChunks += successChunks;
                        if (s.Trace) Console.WriteLine($"[vector] Ingested {relativePath}: {successChunks} chunks");
                    }
                    catch (Exception ex)
                    {
                        if (s.Trace) Console.WriteLine($"[vector] Skipped {file}: {ex.Message}");
                    }
                }

                if (s.Trace) Console.WriteLine($"[vector] Total: {files.Length} files, {totalChunks} chunks");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[vector] Failed to ingest directory: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// RAG pipeline step: search vectors and augment the query.
    /// Usage: Rag('query')
    /// Usage: Rag() - uses current Query, augments Context for LLM
    /// </summary>
    [PipelineToken("Rag", "RAG")]
    public static Step<CliPipelineState, CliPipelineState> Rag(string? args = null)
        => async s =>
        {
            string query = ParseString(args);
            if (string.IsNullOrWhiteSpace(query))
            {
                query = s.Query;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[rag] No query provided");
                return s;
            }

            // Ensure vector store exists
            if (s.VectorStore == null)
            {
                // Try using Branch's vector store if available
                s.VectorStore = s.Branch.Store;
                if (s.VectorStore == null)
                {
                    Console.WriteLine("[rag] No vector store available");
                    return s;
                }
            }

            try
            {
                var queryEmbedding = await s.Embed.CreateEmbeddingsAsync(query);
                var results = await s.VectorStore.GetSimilarDocumentsAsync(queryEmbedding, s.RetrievalK);

                if (results.Count == 0)
                {
                    if (s.Trace) Console.WriteLine("[rag] No relevant context found");
                    s.Query = query;
                    return s;
                }

                // Build augmented context
                var contextParts = results.Select(d => d.PageContent);
                var context = string.Join("\n\n---\n\n", contextParts);

                s.Context = context;
                s.Query = query;
                s.Retrieved.Clear();
                s.Retrieved.AddRange(contextParts);

                // Build a prompt for LLM with context
                s.Prompt = $"""
                Use the following context to answer the question.

                Context:
                {context}

                Question: {query}

                Answer:
                """;

                if (s.Trace)
                {
                    Console.WriteLine($"[rag] Retrieved {results.Count} documents for context");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[rag] Search failed: {ex.Message}");
            }

            return s;
        };

    #region Helper Methods

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        if (arg.StartsWith("'") && arg.EndsWith("'") && arg.Length >= 2) return arg[1..^1];
        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2) return arg[1..^1];
        return arg;
    }

    private static (string Type, string? ConnectionString, string CollectionName) ParseVectorArgs(string? args)
    {
        string type = "InMemory";
        string? connectionString = null;
        string collectionName = "pipeline_vectors";

        if (string.IsNullOrWhiteSpace(args))
        {
            return (type, connectionString, collectionName);
        }

        string parsed = ParseString(args);
        
        // Use semicolon as separator since pipe (|) is the DSL step separator
        if (parsed.Contains(';'))
        {
            foreach (var part in parsed.Split(';'))
            {
                if (part.StartsWith("connection=")) connectionString = part[11..];
                else if (part.StartsWith("collection=")) collectionName = part[11..];
                else if (!part.Contains('=')) type = part;
            }
        }
        else
        {
            // Single value - treat as type or connection string
            if (parsed.StartsWith("http://") || parsed.StartsWith("https://"))
            {
                connectionString = parsed;
                type = "Qdrant"; // Assume Qdrant if URL provided
            }
            else
            {
                type = parsed;
            }
        }

        return (type, connectionString, collectionName);
    }

    private static (string Path, string Pattern) ParseDirArgs(string? args)
    {
        string path = ".";
        string pattern = "*.*";

        if (string.IsNullOrWhiteSpace(args))
        {
            return (path, pattern);
        }

        string parsed = ParseString(args);

        // Use semicolon as separator since pipe (|) is the DSL step separator
        if (parsed.Contains(';'))
        {
            foreach (var part in parsed.Split(';'))
            {
                if (part.StartsWith("pattern=")) pattern = part[8..];
                else if (!part.Contains('=')) path = part;
            }
        }
        else
        {
            path = parsed;
        }

        return (path, pattern);
    }

    private static List<string> ChunkText(string text, int chunkSize)
    {
        var chunks = new List<string>();
        
        // Split by paragraphs first
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        var currentChunk = new System.Text.StringBuilder();
        
        foreach (var para in paragraphs)
        {
            if (currentChunk.Length + para.Length > chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }
            
            if (para.Length > chunkSize)
            {
                // Split long paragraph by sentences or lines
                var lines = para.Split(new[] { ". ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (currentChunk.Length + line.Length > chunkSize && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                    currentChunk.Append(line);
                    currentChunk.Append(' ');
                }
            }
            else
            {
                currentChunk.Append(para);
                currentChunk.Append("\n\n");
            }
        }
        
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks.Count > 0 ? chunks : new List<string> { text };
    }

    /// <summary>
    /// Sanitize text for embedding by removing problematic characters.
    /// </summary>
    private static string SanitizeForEmbedding(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            // Keep printable ASCII and common Unicode
            if (c >= 32 && c < 127) // Printable ASCII
            {
                sb.Append(c);
            }
            else if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c))
            {
                sb.Append(c);
            }
            else if (c == '\n' || c == '\r' || c == '\t')
            {
                sb.Append(c);
            }
            // Skip other control characters and problematic Unicode
        }
        
        return sb.ToString();
    }

    #endregion
}
