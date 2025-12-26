// <copyright file="Critique.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.States;

/// <summary>
/// Represents critical analysis and feedback on a draft or previous state.
/// Used in the refinement loop to identify areas for improvement.
/// </summary>
/// <param name="CritiqueText">The critique text with feedback and suggestions</param>
public sealed record Critique(string CritiqueText) : ReasoningState("Critique", CritiqueText);
