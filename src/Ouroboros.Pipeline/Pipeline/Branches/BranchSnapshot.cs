using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Serializable snapshot of a pipeline branch for persistence and replay.
/// Captures events and vector store state at a point in time.
/// </summary>
public sealed class BranchSnapshot
{
    /// <summary>
    /// Name of the branch
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// List of pipeline events in this branch
    /// </summary>
    public List<PipelineEvent> Events { get; set; } = [];

    /// <summary>
    /// Serialized vector store contents
    /// </summary>
    public List<SerializableVector> Vectors { get; set; } = [];

    /// <summary>
    /// Captures the current state of a pipeline branch as a snapshot.
    /// </summary>
    /// <param name="branch">The branch to capture</param>
    /// <returns>A snapshot containing the branch state</returns>
    public static Task<BranchSnapshot> Capture(PipelineBranch branch)
    {
        List<SerializableVector> vectors = branch.Store.GetAll()
            .Select(v => new SerializableVector
            {
                Id = v.Id,
                Text = v.Text,
                Metadata = v.Metadata ?? new Dictionary<string, object>(),
                Embedding = v.Embedding ?? Array.Empty<float>()
            }).ToList();

        return Task.FromResult(new BranchSnapshot
        {
            Name = branch.Name,
            Events = branch.Events.ToList(),
            Vectors = vectors
        });
    }

    /// <summary>
    /// Restores a pipeline branch from this snapshot.
    /// </summary>
    /// <returns>A reconstructed pipeline branch with the saved state</returns>
    public async Task<PipelineBranch> Restore()
    {
        TrackedVectorStore store = new TrackedVectorStore();
        await store.AddAsync(Vectors.Select(v => new Vector
        {
            Id = v.Id,
            Text = v.Text,
            Metadata = v.Metadata ?? new Dictionary<string, object>(),
            Embedding = v.Embedding ?? Array.Empty<float>()
        }));

        PipelineBranch branch = PipelineBranch.WithEvents(Name, store, DataSource.FromPath(Environment.CurrentDirectory), Events);
        return branch;
    }
}
