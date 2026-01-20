// <copyright file="StreamDeduplicator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Infrastructure.FeatureEngineering;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Real-time stream deduplicator that filters out redundant (nearly identical) vectors
/// based on cosine similarity. Uses an efficient LRU cache to avoid unbounded memory growth.
/// Ideal for filtering duplicate log entries, redundant code snippets, or similar data streams.
/// </summary>
public sealed class StreamDeduplicator
{
    private readonly float similarityThreshold;
    private readonly int maxCacheSize;
    private readonly LinkedList<VectorEntry> lruList;
    private readonly Dictionary<int, LinkedListNode<VectorEntry>> cache;
    private int nextId;
    private readonly object @lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamDeduplicator"/> class.
    /// </summary>
    /// <param name="similarityThreshold">
    /// Similarity threshold (0.0 to 1.0). Vectors with similarity above this threshold
    /// are considered duplicates. Default is 0.95 (95% similar).
    /// </param>
    /// <param name="maxCacheSize">
    /// Maximum number of unique vectors to keep in cache. When exceeded, least recently
    /// used vectors are evicted. Default is 1000.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when similarityThreshold is not between 0 and 1, or maxCacheSize is less than 1.
    /// </exception>
    public StreamDeduplicator(float similarityThreshold = 0.95f, int maxCacheSize = 1000)
    {
        if (similarityThreshold < 0f || similarityThreshold > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(similarityThreshold),
                "Similarity threshold must be between 0.0 and 1.0");
        }

        if (maxCacheSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCacheSize),
                "Max cache size must be at least 1");
        }

        this.similarityThreshold = similarityThreshold;
        this.maxCacheSize = maxCacheSize;
        this.lruList = new LinkedList<VectorEntry>();
        this.cache = new Dictionary<int, LinkedListNode<VectorEntry>>();
        this.nextId = 0;
    }

    /// <summary>
    /// Checks if a vector is a duplicate (similar to a cached vector).
    /// If not a duplicate, adds it to the cache.
    /// </summary>
    /// <param name="vector">The vector to check.</param>
    /// <returns>True if the vector is a duplicate (should be filtered), false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when vector is null.</exception>
    public bool IsDuplicate(float[] vector)
    {
        if (vector is null)
        {
            throw new ArgumentNullException(nameof(vector));
        }

        lock (this.@lock)
        {
            // Check against cached vectors
            foreach (VectorEntry node in this.lruList)
            {
                float similarity = CSharpHashVectorizer.CosineSimilarity(vector, node.Vector);
                if (similarity >= this.similarityThreshold)
                {
                    // Move to front (most recently used)
                    LinkedListNode<VectorEntry> cacheNode = this.cache[node.Id];
                    this.lruList.Remove(cacheNode);
                    this.lruList.AddFirst(cacheNode);
                    return true;
                }
            }

            // Not a duplicate, add to cache
            this.AddToCache(vector);
            return false;
        }
    }

    /// <summary>
    /// Filters a stream of vectors, removing duplicates in real-time.
    /// </summary>
    /// <param name="vectors">Input stream of vectors.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of unique vectors.</returns>
    public IAsyncEnumerable<float[]> FilterStreamAsync(
        IAsyncEnumerable<float[]> vectors,
        CancellationToken cancellationToken = default)
    {
        if (vectors is null)
        {
            throw new ArgumentNullException(nameof(vectors));
        }

        return this.FilterStreamAsyncCore(vectors, cancellationToken);
    }

    private async IAsyncEnumerable<float[]> FilterStreamAsyncCore(
        IAsyncEnumerable<float[]> vectors,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (float[]? vector in vectors.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!this.IsDuplicate(vector))
            {
                yield return vector;
            }
        }
    }

    /// <summary>
    /// Filters a batch of vectors, removing duplicates.
    /// </summary>
    /// <param name="vectors">Input collection of vectors.</param>
    /// <returns>A list of unique vectors.</returns>
    public List<float[]> FilterBatch(IEnumerable<float[]> vectors)
    {
        if (vectors is null)
        {
            throw new ArgumentNullException(nameof(vectors));
        }

        List<float[]> result = new List<float[]>();
        foreach (float[] vector in vectors)
        {
            if (!this.IsDuplicate(vector))
            {
                result.Add(vector);
            }
        }

        return result;
    }

    /// <summary>
    /// Clears the internal cache.
    /// </summary>
    public void ClearCache()
    {
        lock (this.@lock)
        {
            this.lruList.Clear();
            this.cache.Clear();
        }
    }

    /// <summary>
    /// Gets the current number of cached vectors.
    /// </summary>
    public int CacheSize
    {
        get
        {
            lock (this.@lock)
            {
                return this.lruList.Count;
            }
        }
    }

    /// <summary>
    /// Gets statistics about the deduplicator's performance.
    /// </summary>
    /// <returns>A tuple containing (cacheSize, maxCacheSize, similarityThreshold).</returns>
    public (int CacheSize, int MaxCacheSize, float SimilarityThreshold) GetStatistics()
    {
        lock (this.@lock)
        {
            return (this.lruList.Count, this.maxCacheSize, this.similarityThreshold);
        }
    }

    private void AddToCache(float[] vector)
    {
        VectorEntry entry = new VectorEntry(this.nextId++, vector);
        LinkedListNode<VectorEntry> node = new LinkedListNode<VectorEntry>(entry);

        this.lruList.AddFirst(node);
        this.cache[entry.Id] = node;

        // Evict least recently used if cache is full
        if (this.lruList.Count > this.maxCacheSize)
        {
            LinkedListNode<VectorEntry>? lastNode = this.lruList.Last;
            if (lastNode is not null)
            {
                this.cache.Remove(lastNode.Value.Id);
                this.lruList.RemoveLast();
            }
        }
    }

    private sealed record VectorEntry(int Id, float[] Vector);
}

/// <summary>
/// Extension methods for StreamDeduplicator to support fluent API.
/// </summary>
public static class StreamDeduplicatorExtensions
{
    /// <summary>
    /// Deduplicates a stream of vectors using the specified deduplicator.
    /// </summary>
    /// <param name="vectors">Input stream of vectors.</param>
    /// <param name="deduplicator">The deduplicator to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of unique vectors.</returns>
    public static IAsyncEnumerable<float[]> Deduplicate(
        this IAsyncEnumerable<float[]> vectors,
        StreamDeduplicator deduplicator,
        CancellationToken cancellationToken = default)
    {
        if (vectors is null)
        {
            throw new ArgumentNullException(nameof(vectors));
        }

        if (deduplicator is null)
        {
            throw new ArgumentNullException(nameof(deduplicator));
        }

        return deduplicator.FilterStreamAsync(vectors, cancellationToken);
    }

    /// <summary>
    /// Deduplicates a collection of vectors using a new deduplicator instance.
    /// </summary>
    /// <param name="vectors">Input collection of vectors.</param>
    /// <param name="similarityThreshold">Similarity threshold for duplicate detection.</param>
    /// <param name="maxCacheSize">Maximum cache size.</param>
    /// <returns>A list of unique vectors.</returns>
    public static List<float[]> Deduplicate(
        this IEnumerable<float[]> vectors,
        float similarityThreshold = 0.95f,
        int maxCacheSize = 1000)
    {
        if (vectors is null)
        {
            throw new ArgumentNullException(nameof(vectors));
        }

        StreamDeduplicator deduplicator = new StreamDeduplicator(similarityThreshold, maxCacheSize);
        return deduplicator.FilterBatch(vectors);
    }
}
