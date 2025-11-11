// <copyright file="ToolExecution.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain;

public sealed record ToolExecution(
    string ToolName,
    string Arguments,
    string Output,
    DateTime Timestamp);
