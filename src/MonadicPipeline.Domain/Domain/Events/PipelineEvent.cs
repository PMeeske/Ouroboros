// <copyright file="PipelineEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Events;

using System.Text.Json.Serialization;

/// <summary>
/// Base class for all pipeline events that can occur during execution.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(IngestBatch), typeDiscriminator: "Ingest")]
[JsonDerivedType(typeof(ReasoningStep), typeDiscriminator: "Reasoning")]
public abstract record PipelineEvent(Guid Id, string Kind, DateTime Timestamp);
