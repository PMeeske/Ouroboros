// <copyright file="DistinctionStorageConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Configuration for distinction weight storage.
/// </summary>
/// <param name="StoragePath">Path to the storage directory.</param>
public record DistinctionStorageConfig(
    string StoragePath);
