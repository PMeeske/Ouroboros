// <copyright file="EnvironmentVariableTestsCollection.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Infrastructure.Fixtures;

using Xunit;

/// <summary>
/// Collection definition for tests that modify environment variables.
/// These tests must run sequentially to avoid interference between tests.
/// </summary>
[CollectionDefinition("EnvironmentVariableTests", DisableParallelization = true)]
public class EnvironmentVariableTestsCollection
{
}
