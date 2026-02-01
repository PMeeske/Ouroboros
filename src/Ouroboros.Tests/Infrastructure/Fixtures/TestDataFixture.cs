// <copyright file="TestDataFixture.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Infrastructure.Fixtures;

/// <summary>
/// Shared test data fixture for common test data and setup.
/// </summary>
public class TestDataFixture : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataFixture"/> class.
    /// </summary>
    public TestDataFixture()
    {
        // Initialize shared test data
    }

    /// <summary>
    /// Disposes resources used by the fixture.
    /// </summary>
    public void Dispose()
    {
        // Clean up resources
        GC.SuppressFinalize(this);
    }
}
