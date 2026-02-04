// <copyright file="TracingTestsCollection.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Infrastructure.Fixtures;

using Xunit;

/// <summary>
/// Collection definition for tracing tests to ensure they run sequentially.
/// Tracing tests modify global state (TracingConfiguration) and must not run in parallel.
/// </summary>
[CollectionDefinition("TracingTests", DisableParallelization = true)]
public class TracingTestsCollection
{
}
