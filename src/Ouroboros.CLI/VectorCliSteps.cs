// <copyright file="VectorCliSteps.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChain.Databases;
using LangChainPipeline.Core.Configuration;

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

    /// <summary>
    /// Agentic RAG pipeline: iterative query refinement with self-critique.
    /// The agent analyzes results, refines queries, and iterates until convergence.
    /// Usage: AgentLoop('initial query;maxIter=3;minScore=0.7')
    /// Usage: AgentLoop() - uses current Query with defaults
    /// </summary>
    [PipelineToken("AgentLoop", "AgentRag", "IterativeRag")]
    public static Step<CliPipelineState, CliPipelineState> AgentLoop(string? args = null)
        => async s =>
        {
            var config = ParseAgentArgs(args);
            string currentQuery = config.Query ?? s.Query;

            if (string.IsNullOrWhiteSpace(currentQuery))
            {
                Console.WriteLine("[agent] No query provided");
                return s;
            }

            // Ensure vector store exists
            if (s.VectorStore == null)
            {
                s.VectorStore = s.Branch.Store;
                if (s.VectorStore == null)
                {
                    Console.WriteLine("[agent] No vector store available");
                    return s;
                }
            }

            Console.WriteLine($"[agent] Starting agentic RAG with query: {currentQuery}");
            Console.WriteLine($"[agent] Max iterations: {config.MaxIterations}, Min confidence: {config.MinConfidence}");

            var allContext = new List<string>();
            var queryHistory = new List<string> { currentQuery };
            string lastAnalysis = string.Empty;
            bool converged = false;

            for (int iteration = 1; iteration <= config.MaxIterations && !converged; iteration++)
            {
                Console.WriteLine($"\n[agent] === Iteration {iteration}/{config.MaxIterations} ===");
                Console.WriteLine($"[agent] Query: {currentQuery}");

                // Step 1: Perform RAG search
                var queryEmbedding = await s.Embed.CreateEmbeddingsAsync(currentQuery);
                var results = await s.VectorStore.GetSimilarDocumentsAsync(queryEmbedding, s.RetrievalK);

                if (results.Count == 0)
                {
                    Console.WriteLine("[agent] No results found, trying broader search...");

                    // Try to broaden the query
                    currentQuery = await BroadenQueryAsync(s, currentQuery);
                    queryHistory.Add(currentQuery);
                    continue;
                }

                // Collect unique context
                foreach (var doc in results)
                {
                    if (!allContext.Contains(doc.PageContent))
                    {
                        allContext.Add(doc.PageContent);
                    }
                }

                Console.WriteLine($"[agent] Retrieved {results.Count} documents ({allContext.Count} total unique)");

                // Step 2: Analyze results with LLM - determine if answer is sufficient
                var analysisResult = await AnalyzeResultsAsync(s, currentQuery, allContext, queryHistory);

                Console.WriteLine($"[agent] Analysis: confidence={analysisResult.Confidence:F2}, converged={analysisResult.IsComplete}");
                if (s.Trace && !string.IsNullOrEmpty(analysisResult.Reasoning))
                {
                    Console.WriteLine($"[agent] Reasoning: {analysisResult.Reasoning}");
                }

                lastAnalysis = analysisResult.Reasoning;

                // Step 3: Check convergence
                if (analysisResult.IsComplete || analysisResult.Confidence >= config.MinConfidence)
                {
                    Console.WriteLine($"[agent] Converged! Sufficient information gathered.");
                    converged = true;
                }
                else if (!string.IsNullOrEmpty(analysisResult.RefinedQuery))
                {
                    // Step 4: Refine query for next iteration
                    currentQuery = analysisResult.RefinedQuery;
                    queryHistory.Add(currentQuery);
                    Console.WriteLine($"[agent] Refined query: {currentQuery}");
                }
                else
                {
                    Console.WriteLine("[agent] No refinement suggested, stopping.");
                    converged = true;
                }
            }

            if (!converged)
            {
                Console.WriteLine($"\n[agent] Max iterations reached ({config.MaxIterations})");
            }

            // Build final context and prompt
            var context = string.Join("\n\n---\n\n", allContext.Take(config.MaxContextDocs));
            s.Context = context;
            s.Query = queryHistory[0]; // Original query
            s.Retrieved.Clear();
            s.Retrieved.AddRange(allContext.Take(config.MaxContextDocs));

            // Build comprehensive prompt with agent analysis
            s.Prompt = $"""
                You are an expert assistant. Use the following context gathered through iterative analysis to answer the question comprehensively.

                ## Agent Analysis Summary
                - Total iterations: {queryHistory.Count}
                - Queries explored: {string.Join(" -> ", queryHistory.Select((q, i) => $"[{i + 1}] {(q.Length > 50 ? q[..50] + "..." : q)}"))}
                - Final assessment: {lastAnalysis}

                ## Retrieved Context
                {context}

                ## Original Question
                {queryHistory[0]}

                ## Answer
                Based on the iterative analysis and gathered context, provide a comprehensive answer:
                """;

            Console.WriteLine($"\n[agent] Complete. Gathered {allContext.Count} unique context segments across {queryHistory.Count} query iterations.");

            return s;
        };

    /// <summary>
    /// Analyze search results and determine if query refinement is needed.
    /// </summary>
    private static async Task<AgentAnalysisResult> AnalyzeResultsAsync(
        CliPipelineState state,
        string currentQuery,
        List<string> context,
        List<string> queryHistory)
    {
        var contextSummary = string.Join("\n\n", context.Take(5).Select((c, i) =>
            $"[Doc {i + 1}]: {(c.Length > 500 ? c[..500] + "..." : c)}"));

        var analysisPrompt = $$"""
            You are an expert query analyst for a RAG (Retrieval Augmented Generation) system.
            
            Analyze the search results and determine if we have enough information to answer the query, 
            or if we need to refine the search.

            ## Current Query
            {{currentQuery}}

            ## Query History
            {{string.Join("\n", queryHistory.Select((q, i) => $"{i + 1}. {q}"))}}

            ## Retrieved Context (summarized)
            {{contextSummary}}

            ## Your Task
            Respond with a JSON object (and only JSON, no markdown):
            {
                "confidence": 0.0 to 1.0 (how confident are you that we can answer the query),
                "isComplete": true/false (do we have enough information?),
                "reasoning": "brief explanation of your assessment",
                "refinedQuery": "a better query if isComplete is false, or null if complete",
                "missingConcepts": ["concept1", "concept2"] (what information is missing?)
            }

            Focus on:
            - Does the context directly address the query?
            - Are there gaps in the information?
            - Would a different search angle help?
            """;

        try
        {
            var response = await state.Llm.InnerModel.GenerateTextAsync(analysisPrompt);

            // Parse JSON response
            return ParseAnalysisResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[agent] Analysis error: {ex.Message}");
            return new AgentAnalysisResult
            {
                Confidence = 0.5f,
                IsComplete = true, // Default to complete on error
                Reasoning = "Analysis failed, proceeding with current results"
            };
        }
    }

    /// <summary>
    /// Broaden a query when no results are found.
    /// </summary>
    private static async Task<string> BroadenQueryAsync(CliPipelineState state, string narrowQuery)
    {
        var broadenPrompt = $"""
            The following search query returned no results in a code/document database:
            "{narrowQuery}"

            Suggest a broader, more general query that might match existing documents.
            Consider:
            - Using more common terminology
            - Removing specific details
            - Focusing on the core concept

            Respond with ONLY the new query text, nothing else.
            """;

        try
        {
            var response = await state.Llm.InnerModel.GenerateTextAsync(broadenPrompt);
            return response.Trim().Trim('"', '\'');
        }
        catch
        {

            // Fallback: remove specific terms
            var words = narrowQuery.Split(' ').Where(w => w.Length > 3).Take(3);
            return string.Join(" ", words);
        }
    }

    /// <summary>
    /// Parse the LLM's analysis response.
    /// </summary>
    private static AgentAnalysisResult ParseAnalysisResponse(string response)
    {
        var result = new AgentAnalysisResult();

        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("confidence", out var conf))
                    result.Confidence = conf.GetSingle();

                if (root.TryGetProperty("isComplete", out var complete))
                    result.IsComplete = complete.GetBoolean();

                if (root.TryGetProperty("reasoning", out var reasoning))
                    result.Reasoning = reasoning.GetString() ?? string.Empty;

                if (root.TryGetProperty("refinedQuery", out var refined) && refined.ValueKind != System.Text.Json.JsonValueKind.Null)
                    result.RefinedQuery = refined.GetString();

                if (root.TryGetProperty("missingConcepts", out var missing) && missing.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    result.MissingConcepts = missing.EnumerateArray()
                        .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .ToList();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[agent] JSON parse warning: {ex.Message}");

            // Fall back to heuristics
            result.IsComplete = response.Contains("complete", StringComparison.OrdinalIgnoreCase)
                             || response.Contains("sufficient", StringComparison.OrdinalIgnoreCase);
            result.Confidence = result.IsComplete ? 0.8f : 0.4f;
        }

        return result;
    }

    /// <summary>
    /// Parse AgentLoop arguments.
    /// </summary>
    private static AgentLoopConfig ParseAgentArgs(string? args)
    {
        var config = new AgentLoopConfig();

        if (string.IsNullOrWhiteSpace(args)) return config;

        string parsed = ParseString(args);

        // Use semicolon as separator
        foreach (var part in parsed.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("maxIter=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(trimmed[8..], out var max))
                    config.MaxIterations = max;
            }
            else if (trimmed.StartsWith("minScore=", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("minConf=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Contains("minScore=") ? trimmed[9..] : trimmed[8..];
                if (float.TryParse(value, out var score))
                    config.MinConfidence = score;
            }
            else if (trimmed.StartsWith("maxDocs=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(trimmed[8..], out var docs))
                    config.MaxContextDocs = docs;
            }
            else if (!trimmed.Contains('='))
            {
                config.Query = trimmed;
            }
        }

        return config;
    }

    /// <summary>
    /// Configuration for AgentLoop step.
    /// </summary>
    private sealed class AgentLoopConfig
    {
        public string? Query { get; set; }
        public int MaxIterations { get; set; } = 3;
        public float MinConfidence { get; set; } = 0.75f;
        public int MaxContextDocs { get; set; } = 10;
    }

    /// <summary>
    /// Result of agent analysis step.
    /// </summary>
    private sealed class AgentAnalysisResult
    {
        public float Confidence { get; set; } = 0.5f;
        public bool IsComplete { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public string? RefinedQuery { get; set; }
        public List<string> MissingConcepts { get; set; } = new();
    }

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
