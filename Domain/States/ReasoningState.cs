using System.Text.Json.Serialization;

namespace LangChainPipeline.Domain.States;


[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Draft), "Draft")]
[JsonDerivedType(typeof(Critique), "Critique")]
[JsonDerivedType(typeof(FinalSpec), "Final")]
public abstract record ReasoningState(string Kind, string Text);

