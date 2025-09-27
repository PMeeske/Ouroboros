using LangChainPipeline.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the configuration system.
/// </summary>
public class ConfigurationTests
{
    /// <summary>
    /// Tests basic configuration creation with default values.
    /// </summary>
    [Fact]
    public void TestPipelineConfigurationDefaults()
    {
        var config = new PipelineConfiguration();
        
        Assert.Equal("http://localhost:11434", config.OllamaEndpoint);
        Assert.Equal(10, config.MaxTurns);
        Assert.Equal(100, config.VectorStoreBatchSize);
        Assert.Equal(8, config.DefaultSimilarDocumentCount);
        Assert.Equal("nomic-embed-text", config.DefaultEmbeddingModel);
        Assert.Equal("llama3.2", config.DefaultChatModel);
        Assert.False(config.EnableVerboseLogging);
        Assert.Equal(120, config.LlmTimeoutSeconds);
    }

    /// <summary>
    /// Tests configuration binding from JSON.
    /// </summary>
    [Fact]
    public void TestConfigurationBinding()
    {
        var configData = new Dictionary<string, string>
        {
            {"Pipeline:OllamaEndpoint", "http://custom:11434"},
            {"Pipeline:MaxTurns", "5"},
            {"Pipeline:VectorStoreBatchSize", "50"},
            {"Pipeline:EnableVerboseLogging", "true"}
        };
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();
        
        var pipelineConfig = new PipelineConfiguration();
        configuration.GetSection("Pipeline").Bind(pipelineConfig);
        
        Assert.Equal("http://custom:11434", pipelineConfig.OllamaEndpoint);
        Assert.Equal(5, pipelineConfig.MaxTurns);
        Assert.Equal(50, pipelineConfig.VectorStoreBatchSize);
        Assert.True(pipelineConfig.EnableVerboseLogging);
    }
}