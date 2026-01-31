#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-Knowledge Type
// Represents transferable knowledge about learning
// ==========================================================

namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Represents transferable meta-knowledge extracted from learning history.
/// </summary>
public sealed record MetaKnowledge(
    string Domain,
    string Insight,
    double Confidence,
    int SupportingExamples,
    List<string> ApplicableTaskTypes,
    DateTime DiscoveredAt);
