// <copyright file="FinalSpec.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.States;

public sealed record FinalSpec(string FinalText) : ReasoningState("Final", FinalText);
