using System;
using System.Collections.Generic;

namespace LangChainPipeline.CLI;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PipelineTokenAttribute : Attribute
{
    public PipelineTokenAttribute(params string[] names)
    {
        Names = names is { Length: > 0 } ? names : Array.Empty<string>();
    }

    public IReadOnlyList<string> Names { get; }
}
