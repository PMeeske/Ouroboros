// <copyright file="TestCategories.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Infrastructure.Utilities;

/// <summary>
/// Constants for test categories used in [Trait] attributes.
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Unit test category for fast, isolated tests.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// Integration test category for tests involving external dependencies.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    /// BDD test category for behavior-driven development tests.
    /// </summary>
    public const string BDD = "BDD";

    /// <summary>
    /// WebApi test category for API endpoint tests.
    /// </summary>
    public const string WebApi = "WebApi";

    /// <summary>
    /// CLI test category for command-line interface tests.
    /// </summary>
    public const string CLI = "CLI";

    /// <summary>
    /// Database test category for data persistence tests.
    /// </summary>
    public const string Database = "Database";

    /// <summary>
    /// Performance test category for benchmark and load tests.
    /// </summary>
    public const string Performance = "Performance";

    /// <summary>
    /// Security test category for security-related tests.
    /// </summary>
    public const string Security = "Security";
}
