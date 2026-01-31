#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Grounded Concept Type Definitions
// Represents concepts grounded in both neural and symbolic representations
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Represents a concept grounded in both neural embeddings and symbolic types.
/// </summary>
public sealed record GroundedConcept(
    string Name,
    string MeTTaType,
    List<string> Properties,
    List<string> Relations,
    float[] Embedding,
    double GroundingConfidence);
