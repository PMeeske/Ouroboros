// <copyright file="Critique.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.States;

public sealed record Critique(string CritiqueText) : ReasoningState("Critique", CritiqueText);
