#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChainPipeline.Diagnostics;

namespace LangChainPipeline.Providers;

public static class EmbeddingExtensions
{
    public static async Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        this IEmbeddingModel model,
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(inputs);

        IList<string> list = inputs as IList<string> ?? inputs.Where(s => s is not null).Select(static s => s ?? string.Empty).ToList();
        if (list.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        Telemetry.RecordEmbeddingInput(list);

        List<float[]> results = new List<float[]>(list.Count);
        foreach (string item in list)
        {
            try
            {
                float[] embedding = await model.CreateEmbeddingsAsync(item, cancellationToken).ConfigureAwait(false);
                results.Add(embedding);
                Telemetry.RecordEmbeddingSuccess(embedding.Length);
                Telemetry.RecordVectors(1);
            }
            catch
            {
                Telemetry.RecordEmbeddingFailure();
                results.Add(Array.Empty<float>());
            }
        }

        return results;
    }
}
