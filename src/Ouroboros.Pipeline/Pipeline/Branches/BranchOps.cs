#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace Ouroboros.Pipeline.Branches;

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

            IEnumerable<PipelineEvent> combinedEvents = a.Events.Concat(b.Events);
            PipelineBranch merged = PipelineBranch.WithEvents($"{a.Name}+{b.Name}", mergedStore, DataSource.FromPath(Environment.CurrentDirectory), combinedEvents);

            List<Vector> vectorsA = a.Store.GetAll().ToList();
            List<Vector> vectorsB = b.Store.GetAll().ToList();
            IEnumerable<IGrouping<string, Vector>> conflicts = vectorsA.Concat(vectorsB).GroupBy(v => v.Id);

            List<Vector> resolved = [];
            foreach (IGrouping<string, Vector> group in conflicts)
            {
                if (group.Count() == 1)
                {
                    resolved.Add(group.First());
                    continue;
                }

                // Build temporary store for tie-breaking
                TrackedVectorStore temp = new TrackedVectorStore();
                await temp.AddAsync(group.Select(v => new Vector
                {
                    Id = v.Id,
                    Text = v.Text,
                    Metadata = v.Metadata,
                    Embedding = v.Embedding
                }));

                IReadOnlyCollection<Document> top = await temp.GetSimilarDocuments(embed, query, amount: topK);
                Document? best = top.FirstOrDefault();
                if (best is not null && best.Metadata.TryGetValue("id", out object? idObj) && idObj is string idStr)
                {
                    resolved.Add(group.First(g => g.Id == idStr));
                }
                else
                {
                    resolved.Add(group.First());
                }
            }

            await mergedStore.AddAsync(resolved);
            return merged;
        };
}
