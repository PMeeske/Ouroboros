// <copyright file="ThoughtComposer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Vectors;
using Ouroboros.Domain;

namespace Ouroboros.Application.Personality;

/// <summary>
/// Composes higher-dimensional thoughts through vector convolution.
/// Enables emergent thought patterns by combining multiple thought embeddings.
/// </summary>
/// <remarks>
/// <para>Key capabilities:</para>
/// <list type="bullet">
///   <item><description>Thought binding: Combine role+filler thoughts into single representation</description></item>
///   <item><description>Thought expansion: Increase dimensionality for richer representations</description></item>
///   <item><description>Meta-thoughts: Create summary thoughts from multiple related thoughts</description></item>
///   <item><description>Thought chains: Track evolution of thinking through gradients</description></item>
///   <item><description>Thought resonance: Amplify dominant patterns in reasoning</description></item>
/// </list>
/// </remarks>
public class ThoughtComposer
{
    private readonly IEmbeddingModel _embedding;
    private readonly int _baseDimension;
    private readonly Dictionary<string, float[]> _roleVectors = new();

    /// <summary>
    /// Event raised when a new composite thought is created.
    /// </summary>
    public event Action<CompositeThought>? OnCompositeCreated;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThoughtComposer"/> class.
    /// </summary>
    /// <param name="embedding">Embedding model for generating thought vectors.</param>
    /// <param name="baseDimension">Base embedding dimension (default: 768).</param>
    public ThoughtComposer(IEmbeddingModel embedding, int baseDimension = 768)
    {
        _embedding = embedding;
        _baseDimension = baseDimension;
        InitializeRoleVectors();
    }

    /// <summary>
    /// Pre-computed role vectors for common thought relationships.
    /// </summary>
    private void InitializeRoleVectors()
    {
        var random = new Random(42); // Deterministic roles

        _roleVectors["causes"] = GenerateRandomVector(random);
        _roleVectors["implies"] = GenerateRandomVector(random);
        _roleVectors["contradicts"] = GenerateRandomVector(random);
        _roleVectors["supports"] = GenerateRandomVector(random);
        _roleVectors["elaborates"] = GenerateRandomVector(random);
        _roleVectors["questions"] = GenerateRandomVector(random);
        _roleVectors["answers"] = GenerateRandomVector(random);
        _roleVectors["relates_to"] = GenerateRandomVector(random);
        _roleVectors["evolves_from"] = GenerateRandomVector(random);
        _roleVectors["synthesizes"] = GenerateRandomVector(random);
    }

    private float[] GenerateRandomVector(Random random)
    {
        var vector = new float[_baseDimension];
        for (int i = 0; i < _baseDimension; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1);
        }
        VectorConvolution.Normalize(vector);
        return vector;
    }

    /// <summary>
    /// Creates an embedding for the given thought content.
    /// </summary>
    /// <param name="content">Thought text content.</param>
    /// <returns>Embedding vector.</returns>
    public async Task<float[]> EmbedThoughtAsync(string content)
    {
        return await _embedding.CreateEmbeddingsAsync(content);
    }

    /// <summary>
    /// Binds two thoughts with a specified relationship.
    /// The result can later be queried to retrieve either thought given the other and the relationship.
    /// </summary>
    /// <param name="thought1">First thought content.</param>
    /// <param name="thought2">Second thought content.</param>
    /// <param name="relationship">Relationship type (e.g., "causes", "implies", "supports").</param>
    /// <returns>Composite thought capturing the relationship.</returns>
    public async Task<CompositeThought> BindThoughtsAsync(
        string thought1,
        string thought2,
        string relationship = "relates_to")
    {
        var embed1 = await EmbedThoughtAsync(thought1);
        var embed2 = await EmbedThoughtAsync(thought2);

        if (!_roleVectors.TryGetValue(relationship, out var roleVector))
        {
            // Create new role vector for unknown relationships
            var random = new Random(relationship.GetHashCode());
            roleVector = GenerateRandomVector(random);
            _roleVectors[relationship] = roleVector;
        }

        // Bind: role * thought1, then convolve with thought2
        var boundRole = VectorConvolution.HolographicBind(roleVector, embed1);
        var composite = VectorConvolution.CircularConvolve(boundRole, embed2);
        VectorConvolution.Normalize(composite);

        var result = new CompositeThought
        {
            Id = Guid.NewGuid(),
            SourceThoughts = new[] { thought1, thought2 },
            Relationship = relationship,
            CompositeVector = composite,
            Dimension = composite.Length,
            CreatedAt = DateTime.UtcNow
        };

        OnCompositeCreated?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Creates a meta-thought from multiple related thoughts.
    /// The meta-thought captures the combined semantic essence.
    /// </summary>
    /// <param name="thoughts">Collection of thought contents.</param>
    /// <returns>Meta-thought combining all inputs.</returns>
    public async Task<CompositeThought> CreateMetaThoughtAsync(IEnumerable<string> thoughts)
    {
        var thoughtList = thoughts.ToList();
        if (thoughtList.Count == 0)
            throw new ArgumentException("At least one thought required");

        var embeddings = new List<float[]>();
        foreach (var thought in thoughtList)
        {
            embeddings.Add(await EmbedThoughtAsync(thought));
        }

        var metaVector = VectorConvolution.CreateMetaThought(embeddings);

        var result = new CompositeThought
        {
            Id = Guid.NewGuid(),
            SourceThoughts = thoughtList.ToArray(),
            Relationship = "meta",
            CompositeVector = metaVector,
            Dimension = metaVector.Length,
            CreatedAt = DateTime.UtcNow
        };

        OnCompositeCreated?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Expands a thought to higher dimensions for richer representation.
    /// Useful for capturing more nuanced semantic features.
    /// </summary>
    /// <param name="thought">Thought content.</param>
    /// <param name="targetDimension">Target dimension (must be > base dimension).</param>
    /// <returns>Expanded composite thought.</returns>
    public async Task<CompositeThought> ExpandThoughtAsync(string thought, int targetDimension)
    {
        if (targetDimension <= _baseDimension)
            throw new ArgumentException($"Target dimension must be > {_baseDimension}");

        var embed = await EmbedThoughtAsync(thought);
        var expanded = VectorConvolution.ExpandDimension(embed, targetDimension);

        return new CompositeThought
        {
            Id = Guid.NewGuid(),
            SourceThoughts = new[] { thought },
            Relationship = "expanded",
            CompositeVector = expanded,
            Dimension = expanded.Length,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates multi-scale features from a thought.
    /// Captures patterns at different granularities.
    /// </summary>
    /// <param name="thought">Thought content.</param>
    /// <param name="scales">Convolution kernel sizes.</param>
    /// <returns>Multi-scale composite thought.</returns>
    public async Task<CompositeThought> MultiScaleAnalyzeAsync(string thought, params int[] scales)
    {
        if (scales.Length == 0)
            scales = new[] { 3, 5, 7, 11 }; // Default multi-scale

        var embed = await EmbedThoughtAsync(thought);
        var multiScale = VectorConvolution.MultiScaleConvolve(embed, scales);

        return new CompositeThought
        {
            Id = Guid.NewGuid(),
            SourceThoughts = new[] { thought },
            Relationship = "multi_scale",
            CompositeVector = multiScale,
            Dimension = multiScale.Length,
            Metadata = new Dictionary<string, object> { ["scales"] = scales },
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Computes the thought transition from one thought to another.
    /// Useful for understanding how thinking evolves.
    /// </summary>
    /// <param name="fromThought">Starting thought.</param>
    /// <param name="toThought">Ending thought.</param>
    /// <returns>Thought gradient representing the transition.</returns>
    public async Task<ThoughtGradient> ComputeTransitionAsync(string fromThought, string toThought)
    {
        var fromEmbed = await EmbedThoughtAsync(fromThought);
        var toEmbed = await EmbedThoughtAsync(toThought);

        var gradient = VectorConvolution.ThoughtGradient(fromEmbed, toEmbed);
        var similarity = VectorConvolution.CosineSimilarity(fromEmbed, toEmbed);

        return new ThoughtGradient
        {
            FromThought = fromThought,
            ToThought = toThought,
            GradientVector = gradient,
            Similarity = similarity,
            TransitionMagnitude = MathF.Sqrt(gradient.Sum(x => x * x))
        };
    }

    /// <summary>
    /// Applies resonance to amplify dominant patterns in a thought.
    /// </summary>
    /// <param name="thought">Thought content.</param>
    /// <param name="iterations">Resonance iterations (higher = more amplification).</param>
    /// <returns>Resonated composite thought.</returns>
    public async Task<CompositeThought> ResonateThoughtAsync(string thought, int iterations = 3)
    {
        var embed = await EmbedThoughtAsync(thought);
        var resonated = VectorConvolution.ThoughtResonance(embed, iterations);

        return new CompositeThought
        {
            Id = Guid.NewGuid(),
            SourceThoughts = new[] { thought },
            Relationship = "resonated",
            CompositeVector = resonated,
            Dimension = resonated.Length,
            Metadata = new Dictionary<string, object> { ["iterations"] = iterations },
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Extrapolates a new thought by applying a gradient to a base thought.
    /// </summary>
    /// <param name="baseThought">Starting thought.</param>
    /// <param name="gradient">Direction gradient.</param>
    /// <param name="alpha">Step size.</param>
    /// <returns>Extrapolated thought vector.</returns>
    public async Task<float[]> ExtrapolateAsync(string baseThought, ThoughtGradient gradient, float alpha = 1.0f)
    {
        var baseEmbed = await EmbedThoughtAsync(baseThought);
        return VectorConvolution.ApplyGradient(baseEmbed, gradient.GradientVector, alpha);
    }

    /// <summary>
    /// Finds the most similar thought from a collection.
    /// </summary>
    /// <param name="query">Query thought.</param>
    /// <param name="candidates">Candidate thoughts to compare against.</param>
    /// <returns>Most similar thought and similarity score.</returns>
    public async Task<(string Thought, float Similarity)> FindMostSimilarAsync(
        string query,
        IEnumerable<string> candidates)
    {
        var queryEmbed = await EmbedThoughtAsync(query);
        string? bestThought = null;
        float bestSimilarity = float.MinValue;

        foreach (var candidate in candidates)
        {
            var candidateEmbed = await EmbedThoughtAsync(candidate);
            var similarity = VectorConvolution.CosineSimilarity(queryEmbed, candidateEmbed);

            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestThought = candidate;
            }
        }

        return (bestThought ?? string.Empty, bestSimilarity);
    }
}

/// <summary>
/// Represents a composite thought created through convolution operations.
/// </summary>
public sealed record CompositeThought
{
    /// <summary>Unique identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Original thoughts that were combined.</summary>
    public required string[] SourceThoughts { get; init; }

    /// <summary>Relationship/operation used in composition.</summary>
    public required string Relationship { get; init; }

    /// <summary>The composite vector representation.</summary>
    public required float[] CompositeVector { get; init; }

    /// <summary>Dimension of the composite vector.</summary>
    public required int Dimension { get; init; }

    /// <summary>When this composite was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Additional metadata.</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Represents the gradient/transition between two thoughts.
/// </summary>
public sealed record ThoughtGradient
{
    /// <summary>Starting thought.</summary>
    public required string FromThought { get; init; }

    /// <summary>Ending thought.</summary>
    public required string ToThought { get; init; }

    /// <summary>Gradient vector (direction of change).</summary>
    public required float[] GradientVector { get; init; }

    /// <summary>Cosine similarity between thoughts.</summary>
    public required float Similarity { get; init; }

    /// <summary>Magnitude of the transition.</summary>
    public required float TransitionMagnitude { get; init; }
}
