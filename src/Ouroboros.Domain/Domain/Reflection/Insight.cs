// <copyright file="Insight.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

using Ouroboros.Domain.Environment;

/// <summary>
/// Represents an insight derived from performance analysis.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Type">The type of insight (Strength, Weakness, etc.)</param>
/// <param name="Description">Human-readable description of the insight</param>
/// <param name="Confidence">Confidence level in this insight (0.0 to 1.0)</param>
/// <param name="SupportingEvidence">Episodes that support this insight</param>
public sealed record Insight(
    InsightType Type,
    string Description,
    double Confidence,
    IReadOnlyList<Episode> SupportingEvidence);
