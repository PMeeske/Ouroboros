// <copyright file="PipelineTokenAttribute.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.CLI;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PipelineTokenAttribute : Attribute
{
    public PipelineTokenAttribute(params string[] names)
    {
        this.Names = names is { Length: > 0 } ? names : Array.Empty<string>();
    }

    public IReadOnlyList<string> Names { get; }
}
