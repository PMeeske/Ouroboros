// <copyright file="SelfCritiqueResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent;

/// <summary>
/// Represents the result of a self-critique operation.
/// </summary>
/// <param name="Draft">The initial draft response</param>
/// <param name="Critique">The critique of the draft</param>
/// <param name="ImprovedResponse">The final improved response</param>
/// <param name="Confidence">The confidence rating of the result</param>
/// <param name="IterationsPerformed">Number of critique iterations performed</param>
/// <param name="Branch">The pipeline branch containing all events</param>
public sealed record SelfCritiqueResult(
    string Draft,
    string Critique,
    string ImprovedResponse,
    ConfidenceRating Confidence,
    int IterationsPerformed,
    PipelineBranch Branch);
