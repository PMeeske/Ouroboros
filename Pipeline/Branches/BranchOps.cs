using LangChain.Databases;
using LangChain.Databases.InMemory;
using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers;
using LangChain.Providers.Ollama;
using LangChainPipeline.Core;
using LangChainPipeline.Domain.Vectors;

namespace LangChainPipeline.Pipeline.Branches;

/// <summary>
/// Provides operations for working with pipeline branches.
/// </summary>
public static class BranchOps
{
    /// <summary>
    /// Merges two pipeline branches by resolving conflicts based on relevance to a query.
    /// </summary>
    /// <param name="embed">The embedding model for similarity calculations.</param>
    /// <param name="topK">Number of top results to consider for tie-breaking.</param>
    /// <returns>A step that merges two branches.</returns>
    public static Step<(PipelineBranch A, PipelineBranch B, string Query), PipelineBranch> MergeByRelevance(
        IEmbeddingModel embed, int topK = 1)
        => async input =>
        {
            (PipelineBranch a, PipelineBranch b, string query) = input;
            TrackedVectorStore mergedStore = new TrackedVectorStore();
            PipelineBranch merged = new PipelineBranch($"{a.Name}+{b.Name}", mergedStore, DataSource.FromPath(Environment.CurrentDirectory));

            merged.EventsInternal.AddRange(a.Events);
            merged.EventsInternal.AddRange(b.Events);

            List<Vector> vectorsA = a.Store.GetAll().ToList();
            List<Vector> vectorsB = b.Store.GetAll().ToList();
            IEnumerable<IGrouping<string, Vector>> conflicts = vectorsA.Concat(vectorsB).GroupBy(v => v.Id);

            List<Vector> resolved = new List<Vector>();
            foreach (IGrouping<string, Vector> group in conflicts)
            {
                if (group.Count() == 1)
                {
                    resolved.Add(group.First());
                    continue;
                }

                // Build temporary store for tie-breaking
                InMemoryVectorCollection temp = new InMemoryVectorCollection();
                await temp.AddAsync(group.ToArray());

                IReadOnlyCollection<Document> top = await temp.GetSimilarDocuments(embed, query, amount: topK);
                object bestId = top.Count > 0 ? top.First().Metadata["id"] : group.First().Id;
                resolved.Add(group.First(g => g.Id == (string)bestId));
            }

            await mergedStore.AddAsync(resolved);
            return merged;
        };
}
