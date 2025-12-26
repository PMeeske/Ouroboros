using System.Text.Json.Serialization;

namespace Ouroboros.Domain.States;

/// <summary>
/// Base class for reasoning states in the pipeline.
/// Supports polymorphic JSON serialization for different reasoning phases.
/// </summary>
/// <param name="Kind">The type discriminator for the reasoning state</param>
/// <param name="Text">The text content of this reasoning state</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(Draft), "Draft")]
[JsonDerivedType(typeof(Critique), "Critique")]
[JsonDerivedType(typeof(FinalSpec), "Final")]
[JsonDerivedType(typeof(DocumentRevision), "DocumentRevision")]
[JsonDerivedType(typeof(Thinking), "Thinking")]
public abstract record ReasoningState(string Kind, string Text);
