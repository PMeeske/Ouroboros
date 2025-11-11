// <copyright file="Draft.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.States;

public sealed record Draft(string DraftText) : ReasoningState("Draft", DraftText);
