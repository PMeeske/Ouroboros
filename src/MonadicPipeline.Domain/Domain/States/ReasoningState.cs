// <copyright file="ReasoningState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.States;

using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Draft), "Draft")]
[JsonDerivedType(typeof(Critique), "Critique")]
[JsonDerivedType(typeof(FinalSpec), "Final")]
[JsonDerivedType(typeof(DocumentRevision), "DocumentRevision")]
public abstract record ReasoningState(string Kind, string Text);
