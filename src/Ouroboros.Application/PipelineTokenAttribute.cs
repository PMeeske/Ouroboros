#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Ouroboros.Application;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PipelineTokenAttribute : Attribute
{
    public PipelineTokenAttribute(params string[] names)
    {
        Names = names is { Length: > 0 } ? names : Array.Empty<string>();
    }

    public IReadOnlyList<string> Names { get; }
}

