using LangChainPipeline.Core;
using Xunit;

namespace LangChainPipeline.Tests.Tests;

/// <summary>
/// Tests for EnvironmentDetector utility class.
/// </summary>
public class EnvironmentDetectorTests
{
    [Fact]
    public void IsLocalDevelopment_WithDevelopmentEnvironment_ReturnsTrue()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            // Act
            var result = EnvironmentDetector.IsLocalDevelopment();

            // Assert
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsLocalDevelopment_WithLocalEnvironment_ReturnsTrue()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Local");

            // Act
            var result = EnvironmentDetector.IsLocalDevelopment();

            // Assert
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsLocalDevelopment_WithProductionEnvironment_ReturnsFalse()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            // Act
            var result = EnvironmentDetector.IsLocalDevelopment();

            // Assert
            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsLocalDevelopment_WithStagingEnvironment_ReturnsFalse()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Staging");

            // Act
            var result = EnvironmentDetector.IsLocalDevelopment();

            // Assert
            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsLocalDevelopment_WithLocalhostOllamaEndpoint_ReturnsTrue()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var originalEndpoint = Environment.GetEnvironmentVariable("PIPELINE__LlmProvider__OllamaEndpoint");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            Environment.SetEnvironmentVariable("PIPELINE__LlmProvider__OllamaEndpoint", "http://localhost:11434");

            // Act
            var result = EnvironmentDetector.IsLocalDevelopment();

            // Assert
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
            Environment.SetEnvironmentVariable("PIPELINE__LlmProvider__OllamaEndpoint", originalEndpoint);
        }
    }

    [Fact]
    public void GetEnvironmentName_ReturnsEnvironmentVariable()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            // Act
            var result = EnvironmentDetector.GetEnvironmentName();

            // Assert
            Assert.Equal("Development", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsProduction_WithProductionEnvironment_ReturnsTrue()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            // Act
            var result = EnvironmentDetector.IsProduction();

            // Assert
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsProduction_WithDevelopmentEnvironment_ReturnsFalse()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            // Act
            var result = EnvironmentDetector.IsProduction();

            // Assert
            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsStaging_WithStagingEnvironment_ReturnsTrue()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Staging");

            // Act
            var result = EnvironmentDetector.IsStaging();

            // Assert
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsStaging_WithProductionEnvironment_ReturnsFalse()
    {
        // Arrange
        var originalEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            // Act
            var result = EnvironmentDetector.IsStaging();

            // Assert
            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnv);
        }
    }

    [Fact]
    public void IsRunningInKubernetes_WithoutKubernetesEnvironment_ReturnsFalse()
    {
        // Act
        var result = EnvironmentDetector.IsRunningInKubernetes();

        // Assert
        // In a test environment without Kubernetes, this should return false
        Assert.False(result);
    }

    [Fact]
    public void IsRunningInKubernetes_WithKubernetesServiceHost_ReturnsTrue()
    {
        // Arrange
        var originalK8sHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        try
        {
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", "10.96.0.1");

            // Act
            var result = EnvironmentDetector.IsRunningInKubernetes();

            // Assert
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", originalK8sHost);
        }
    }
}
