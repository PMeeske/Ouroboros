using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for configuration management system.
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void TestDefaultConfiguration()
    {
        var config = new PipelineConfiguration();
        
        Assert.Equal("http://localhost:11434", config.OllamaEndpoint);
        Assert.Equal("llama3.2", config.ChatModel);
        Assert.Equal("nomic-embed-text", config.EmbeddingModel);
        Assert.Equal(10, config.MaxTurns);
        Assert.Equal(8, config.RetrievalAmount);
        Assert.False(config.EnableDetailedLogging);
        Assert.NotNull(config.Tools);
    }

    [Fact]
    public void TestConfigurationHelper()
    {
        var configuration = ConfigurationHelper.CreateConfiguration();
        Assert.NotNull(configuration);
        
        var pipelineConfig = ConfigurationHelper.GetPipelineConfiguration(configuration);
        Assert.NotNull(pipelineConfig);
        
        var options = ConfigurationHelper.GetPipelineOptions(configuration);
        Assert.NotNull(options.Value);
    }

    [Fact]
    public void TestToolConfiguration()
    {
        var toolConfig = new ToolConfiguration();
        
        Assert.Equal(30, toolConfig.TimeoutSeconds);
        Assert.True(toolConfig.EnableSecurityValidation);
        Assert.Equal(10, toolConfig.MaxToolCallsPerExecution);
    }
}