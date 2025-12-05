// <copyright file="RetrievalArgs.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

/// <summary>
/// Arguments for semantic search retrieval operations.
/// </summary>
public sealed class RetrievalArgs
{
    /// <summary>
    /// Gets or sets the query string for semantic search.
    /// </summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets number of documents to retrieve (default: 3).
    /// </summary>
    public int K { get; set; } = 3;
}
