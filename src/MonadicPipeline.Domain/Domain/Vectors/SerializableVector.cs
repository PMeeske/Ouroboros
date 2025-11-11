// <copyright file="SerializableVector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Vectors;

public sealed class SerializableVector
{
    public string Id { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();

    public float[] Embedding { get; set; } = Array.Empty<float>();
}
