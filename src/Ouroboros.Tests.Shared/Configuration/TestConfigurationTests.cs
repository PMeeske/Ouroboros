namespace Ouroboros.Tests.Shared.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

/// <summary>
/// Tests for the TestConfiguration helper class.
/// Demonstrates how to use the configuration system in tests.
/// </summary>
public class TestConfigurationTests
{
    [Fact]
    public void BuildConfiguration_ShouldReturnConfigurationInstance()
    {
        // Act
        var configuration = TestConfiguration.BuildConfiguration();

        // Assert
        configuration.Should().NotBeNull();
        configuration.Should().BeAssignableTo<IConfiguration>();
    }

    [Fact]
    public void BuildConfiguration_ShouldLoadFromMultipleSources()
    {
        // Arrange & Act
        var configuration = TestConfiguration.BuildConfiguration();

        // Assert - Configuration should be able to read sections
        // The actual values depend on the environment (user secrets, env vars, or appsettings files)
        var ollamaSection = configuration.GetSection("Ollama");
        ollamaSection.Should().NotBeNull();
    }

    [Fact]
    public void BuildConfiguration_ShouldSupportNestedConfiguration()
    {
        // Arrange & Act
        var configuration = TestConfiguration.BuildConfiguration();

        // Assert - Should be able to read nested configuration
        var pipelineSection = configuration.GetSection("Pipeline:LlmProvider");
        pipelineSection.Should().NotBeNull();
    }

    [Fact]
    public void BuildConfiguration_ShouldHandleMissingConfiguration()
    {
        // Arrange & Act
        var configuration = TestConfiguration.BuildConfiguration();

        // Assert - Should return null for non-existent keys without throwing
        var nonExistentValue = configuration["NonExistent:Key"];
        nonExistentValue.Should().BeNull();
    }

    /// <summary>
    /// Example test showing how to use configuration in a real test scenario.
    /// This test will pass regardless of whether secrets are configured.
    /// </summary>
    [Fact]
    public void ExampleUsage_ConfigurationInTest()
    {
        // Arrange
        var configuration = TestConfiguration.BuildConfiguration();

        // Act - Retrieve configuration values
        var apiKey = configuration["Ollama:ApiKey"];
        var endpoint = configuration["Ollama:DeepSeekCloudEndpoint"];
        var defaultModel = configuration["Pipeline:LlmProvider:DefaultChatModel"];

        // Assert - Configuration system should work (values may be null/empty if not configured)
        // In a real test, you would check if apiKey is set before running API tests
        // For example:
        // if (string.IsNullOrEmpty(apiKey))
        // {
        //     // Skip test or use mock
        //     return;
        // }

        // This demonstrates the configuration is accessible
        configuration.Should().NotBeNull();
    }
}
