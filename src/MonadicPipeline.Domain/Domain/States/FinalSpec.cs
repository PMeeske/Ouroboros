// <copyright file="FinalSpec.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.States;

/// <summary>
/// Represents the final, refined specification after iterative improvements.
/// The culmination of the draft-critique-improve cycle.
/// </summary>
/// <param name="FinalText">The final refined text content</param>
public sealed record FinalSpec(string FinalText) : ReasoningState("Final", FinalText);
