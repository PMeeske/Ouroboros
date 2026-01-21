namespace Ouroboros.Tests.Shared.Configuration;

using Microsoft.Extensions.Configuration;

/// <summary>
/// Provides centralized configuration management for test projects.
/// Supports loading from appsettings.json, user secrets (local), and environment variables (CI/CD).
/// </summary>
public static class TestConfiguration
{
    /// <summary>
    /// Builds a configuration instance with support for multiple sources in priority order:
    /// 1. Environment variables (highest priority - for CI/CD)
    /// 2. User secrets (for local development)
    /// 3. appsettings.Test.json (optional)
    /// 4. appsettings.json (optional, lowest priority)
    /// </summary>
    /// <returns>A configured IConfiguration instance</returns>
    public static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddUserSecrets<TestConfigurationMarker>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}

/// <summary>
/// Marker class used for user secrets assembly scanning.
/// </summary>
public class TestConfigurationMarker
{
}
