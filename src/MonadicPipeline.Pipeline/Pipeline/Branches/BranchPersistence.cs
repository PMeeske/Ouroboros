using System.Text.Json;
using System.Text.Json.Serialization;

namespace LangChainPipeline.Pipeline.Branches;

/// <summary>
/// Handles serialization and persistence of pipeline branch snapshots.
/// Supports saving/loading branch state to/from JSON files.
/// </summary>
public static class BranchPersistence
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() } // PipelineEvent has JsonPolymorphic attrs
    };

    /// <summary>
    /// Saves a branch snapshot to a JSON file.
    /// </summary>
    /// <param name="snapshot">The snapshot to save</param>
    /// <param name="path">File path where snapshot will be saved</param>
    public static async Task SaveAsync(BranchSnapshot snapshot, string path)
    {
        string json = JsonSerializer.Serialize(snapshot, Options);
        await File.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Loads a branch snapshot from a JSON file.
    /// </summary>
    /// <param name="path">File path to load snapshot from</param>
    /// <returns>The deserialized branch snapshot</returns>
    public static async Task<BranchSnapshot> LoadAsync(string path)
    {
        string json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<BranchSnapshot>(json, Options)!;
    }
}
