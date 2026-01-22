using Xunit;
using FluentAssertions;
using Ouroboros.Easy.Localization;

namespace Ouroboros.Tests.Easy;

[Trait("Category", "Unit")]
[Trait("Area", "EasyAPI")]
public class PipelineTests
{
    [Fact]
    public void Create_ShouldReturnNewPipelineInstance()
    {
        Ouroboros.Easy.Pipeline pipeline = Ouroboros.Easy.Pipeline.Create();
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void About_ShouldSetTopic()
    {
        Ouroboros.Easy.Pipeline pipeline = Ouroboros.Easy.Pipeline.Create();
        string topic = "test topic";
        
        pipeline.About(topic);
        string dsl = pipeline.ToDSL();
        
        dsl.Should().Contain($"Topic: {topic}");
    }

    [Fact]
    public void WithModel_ShouldSetModel()
    {
        Ouroboros.Easy.Pipeline pipeline = Ouroboros.Easy.Pipeline.Create();
        string model = "llama3";
        
        pipeline.WithModel(model);
        string dsl = pipeline.ToDSL();
        
        dsl.Should().Contain($"Model: {model}");
    }

    [Fact]
    public void WithTemperature_ShouldSetTemperature()
    {
        Ouroboros.Easy.Pipeline pipeline = Ouroboros.Easy.Pipeline.Create();
        double temperature = 0.8;
        
        pipeline.WithTemperature(temperature);
        string dsl = pipeline.ToDSL();
        
        dsl.Should().Contain($"Temperature: {temperature}");
    }

    [Fact]
    public void FluentAPI_ShouldSupportMethodChaining()
    {
        Ouroboros.Easy.Pipeline pipeline = Ouroboros.Easy.Pipeline.Create()
            .About("quantum computing")
            .Draft()
            .Critique()
            .Improve()
            .WithModel("llama3")
            .WithTemperature(0.7);
        
        string dsl = pipeline.ToDSL();
        
        dsl.Should().Contain("quantum computing");
        dsl.Should().Contain("llama3");
    }

    [Fact]
    public async Task RunAsync_WithoutTopic_ShouldReturnFailure()
    {
        Ouroboros.Easy.Pipeline pipeline = Ouroboros.Easy.Pipeline.Create()
            .Draft()
            .WithModel("llama3");
        
        Ouroboros.Easy.PipelineResult result = await pipeline.RunAsync();
        
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Topic");
    }

    [Fact]
    public async Task RunAsync_WithoutModel_ShouldReturnFailure()
    {
        Ouroboros.Easy.Pipeline pipeline = Ouroboros.Easy.Pipeline.Create()
            .About("test topic")
            .Draft();
        
        Ouroboros.Easy.PipelineResult result = await pipeline.RunAsync();
        
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Model");
    }
}

[Trait("Category", "Unit")]
[Trait("Area", "Localization")]
public class MultiLanguageSupportTests
{
    [Fact]
    public void CurrentLanguage_ShouldSupportGerman()
    {
        MultiLanguageSupport.CurrentLanguage = "de";
        MultiLanguageSupport.Get("WelcomeMessage").Should().Contain("Willkommen");
    }

    [Fact]
    public void IsLanguageSupported_ShouldReturnTrueForSupportedLanguages()
    {
        bool isSupported = MultiLanguageSupport.IsLanguageSupported("de");
        isSupported.Should().BeTrue();
    }
}
