#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text.Json.Serialization;

namespace LangChainPipeline.Domain.States;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Draft), "Draft")]
[JsonDerivedType(typeof(Critique), "Critique")]
[JsonDerivedType(typeof(FinalSpec), "Final")]
[JsonDerivedType(typeof(DocumentRevision), "DocumentRevision")]
public abstract record ReasoningState(string Kind, string Text);

