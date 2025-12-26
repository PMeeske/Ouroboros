#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Ouroboros.Domain;

/// <summary>
/// Simple embedding abstraction used across the pipeline.
/// Provides a wrapper around various embedding providers with a consistent interface.
/// </summary>
public interface IEmbeddingModel
{
    Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default);
}
