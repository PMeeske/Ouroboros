#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LangChainPipeline.Pipeline.Branches;

public static class BranchPersistence
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() } // PipelineEvent has JsonPolymorphic attrs
    };

    public static async Task SaveAsync(BranchSnapshot snapshot, string path)
    {
        string json = JsonSerializer.Serialize(snapshot, Options);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<BranchSnapshot> LoadAsync(string path)
    {
        string json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<BranchSnapshot>(json, Options)!;
    }
}
