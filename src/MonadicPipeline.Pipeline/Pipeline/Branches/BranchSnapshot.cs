// <copyright file="BranchSnapshot.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Branches;

using LangChain.Databases;
using LangChain.DocumentLoaders;

public sealed class BranchSnapshot
{
    public string Name { get; set; } = string.Empty;

    public List<PipelineEvent> Events { get; set; } = [];

    public List<SerializableVector> Vectors { get; set; } = [];

    public static Task<BranchSnapshot> Capture(PipelineBranch branch)
    {
        List<SerializableVector> vectors = branch.Store.GetAll()
            .Select(v => new SerializableVector
            {
                Id = v.Id,
                Text = v.Text,
                Metadata = v.Metadata ?? new Dictionary<string, object>(),
                Embedding = v.Embedding ?? Array.Empty<float>(),
            }).ToList();

        return Task.FromResult(new BranchSnapshot
        {
            Name = branch.Name,
            Events = branch.Events.ToList(),
            Vectors = vectors,
        });
    }

    public async Task<PipelineBranch> Restore()
    {
        TrackedVectorStore store = new TrackedVectorStore();
        await store.AddAsync(this.Vectors.Select(v => new Vector
        {
            Id = v.Id,
            Text = v.Text,
            Metadata = v.Metadata ?? new Dictionary<string, object>(),
            Embedding = v.Embedding ?? Array.Empty<float>(),
        }));

        PipelineBranch branch = PipelineBranch.WithEvents(this.Name, store, DataSource.FromPath(Environment.CurrentDirectory), this.Events);
        return branch;
    }
}
