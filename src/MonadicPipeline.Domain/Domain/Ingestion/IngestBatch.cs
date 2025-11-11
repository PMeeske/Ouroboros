// <copyright file="IngestBatch.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Events;

public sealed record IngestBatch(
    Guid Id,
    string Source,
    IReadOnlyList<string> Ids,
    DateTime Timestamp) : PipelineEvent(Id, "Ingest", Timestamp);
