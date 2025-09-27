using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace LangChainPipeline.Pipeline.Branches;

public sealed class BranchSnapshot
{
    public string Name { get; set; } = "";
    public List<PipelineEvent> Events { get; set; } = new();
    public List<SerializableVector> Vectors { get; set; } = new();

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
