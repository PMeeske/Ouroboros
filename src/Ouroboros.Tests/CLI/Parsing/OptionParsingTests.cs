// <copyright file="OptionParsingTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CommandLine;
using Ouroboros.Options;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Parsing;

/// <summary>
/// Tests for CommandLineParser option parsing.
/// Validates that CLI arguments are correctly parsed into option objects.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.CLI)]
public class OptionParsingTests
{
    [Fact]
    public void ParseAskOptions_WithBasicArgs_ParsesSuccessfully()
    {
        // Arrange
        string[] args = { "ask", "-q", "What is AI?" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.Should().NotBeNull();
        result.Tag.Should().Be(ParserResultType.Parsed);

        result.WithParsed<AskOptions>(opts =>
        {
            opts.Question.Should().Be("What is AI?");
        });
    }

    [Fact]
    public void ParseAskOptions_WithAllFlags_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "ask",
            "-q", "Test question",
            "--model", "llama3",
            "--rag",
            "--agent",
            "--debug",
            "--temperature", "0.8",
            "--max-tokens", "1024"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Question.Should().Be("Test question");
            opts.Model.Should().Be("llama3");
            opts.Rag.Should().BeTrue();
            opts.Agent.Should().BeTrue();
            opts.Debug.Should().BeTrue();
            opts.Temperature.Should().Be(0.8);
            opts.MaxTokens.Should().Be(1024);
        });
    }

    [Fact]
    public void ParseAskOptions_WithRouterOptions_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "ask",
            "-q", "Test",
            "--router", "auto",
            "--coder-model", "deepseek-coder",
            "--reason-model", "deepseek-r1"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Router.Should().Be("auto");
            opts.CoderModel.Should().Be("deepseek-coder");
            opts.ReasonModel.Should().Be("deepseek-r1");
        });
    }

    [Fact]
    public void ParsePipelineOptions_WithBasicArgs_ParsesSuccessfully()
    {
        // Arrange
        string[] args = { "pipeline", "-d", "SetPrompt 'test' | UseDraft" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.Tag.Should().Be(ParserResultType.Parsed);
        result.WithParsed<PipelineOptions>(opts =>
        {
            opts.Dsl.Should().Be("SetPrompt 'test' | UseDraft");
        });
    }

    [Fact]
    public void ParsePipelineOptions_WithAllFlags_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "pipeline",
            "-d", "SetPrompt | LLM",
            "--model", "llama3",
            "--source", "./data",
            "-k", "5",
            "--trace",
            "--debug"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<PipelineOptions>(opts =>
        {
            opts.Dsl.Should().Be("SetPrompt | LLM");
            opts.Model.Should().Be("llama3");
            opts.Source.Should().Be("./data");
            opts.K.Should().Be(5);
            opts.Trace.Should().BeTrue();
            opts.Debug.Should().BeTrue();
        });
    }

    [Fact]
    public void ParseAskOptions_WithMissingRequiredArg_ReturnsNotParsed()
    {
        // Arrange - Missing required -q/--question
        string[] args = { "ask", "--model", "llama3" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.Tag.Should().Be(ParserResultType.NotParsed);
    }

    [Fact]
    public void ParsePipelineOptions_WithMissingRequiredArg_ReturnsNotParsed()
    {
        // Arrange - Missing required -d/--dsl
        string[] args = { "pipeline", "--model", "llama3" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.Tag.Should().Be(ParserResultType.NotParsed);
    }

    [Fact]
    public void ParseAskOptions_WithShortFlags_ParsesCorrectly()
    {
        // Arrange
        string[] args = { "ask", "-q", "Test", "-r", "-k", "3", "-c", "en-US" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Question.Should().Be("Test");
            opts.Rag.Should().BeTrue();
            opts.K.Should().Be(3);
            opts.Culture.Should().Be("en-US");
        });
    }

    [Fact]
    public void ParseAskOptions_WithLongFlags_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "ask",
            "--question", "Test",
            "--rag",
            "--topk", "5",
            "--culture", "fr-FR"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Question.Should().Be("Test");
            opts.Rag.Should().BeTrue();
            opts.K.Should().Be(5);
            opts.Culture.Should().Be("fr-FR");
        });
    }

    [Fact]
    public void ParseAskOptions_WithAgentOptions_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "ask",
            "-q", "Test",
            "--agent",
            "--agent-mode", "react",
            "--agent-max-steps", "10",
            "--json-tools"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Agent.Should().BeTrue();
            opts.AgentMode.Should().Be("react");
            opts.AgentMaxSteps.Should().Be(10);
            opts.JsonTools.Should().BeTrue();
        });
    }

    [Fact]
    public void ParseAskOptions_WithRemoteEndpoint_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "ask",
            "-q", "Test",
            "--endpoint", "https://api.example.com",
            "--api-key", "sk-test123",
            "--endpoint-type", "openai"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Endpoint.Should().Be("https://api.example.com");
            opts.ApiKey.Should().Be("sk-test123");
            opts.EndpointType.Should().Be("openai");
        });
    }

    [Fact]
    public void ParseAskOptions_WithVoiceMode_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "ask",
            "-q", "Test",
            "-v",
            "--persona", "Aria",
            "--voice-only",
            "--voice-loop"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Voice.Should().BeTrue();
            opts.Persona.Should().Be("Aria");
            opts.VoiceOnly.Should().BeTrue();
            opts.VoiceLoop.Should().BeTrue();
        });
    }

    [Fact]
    public void ParsePipelineOptions_WithVoiceMode_ParsesCorrectly()
    {
        // Arrange
        string[] args =
        {
            "pipeline",
            "-d", "Test DSL",
            "-v",
            "--persona", "Sage"
        };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<PipelineOptions>(opts =>
        {
            opts.Voice.Should().BeTrue();
            opts.Persona.Should().Be("Sage");
        });
    }

    [Fact]
    public void ParseAskOptions_WithDefaultValues_UsesDefaults()
    {
        // Arrange
        string[] args = { "ask", "-q", "Test" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Model.Should().Be("ministral-3:latest");
            opts.Embed.Should().Be("nomic-embed-text");
            opts.K.Should().Be(3);
            opts.Temperature.Should().Be(0.7);
            opts.MaxTokens.Should().Be(512);
            opts.TimeoutSeconds.Should().Be(60);
            opts.Rag.Should().BeFalse();
            opts.Agent.Should().BeFalse();
            opts.Stream.Should().BeFalse();
        });
    }

    [Fact]
    public void ParsePipelineOptions_WithDefaultValues_UsesDefaults()
    {
        // Arrange
        string[] args = { "pipeline", "-d", "Test" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<PipelineOptions>(opts =>
        {
            opts.Model.Should().Be("ministral-3:latest");
            opts.Embed.Should().Be("nomic-embed-text");
            opts.Source.Should().Be(".");
            opts.K.Should().Be(8);
            opts.Trace.Should().BeFalse();
            opts.Debug.Should().BeFalse();
        });
    }

    [Fact]
    public void ParseAskOptions_WithStrictModel_ParsesCorrectly()
    {
        // Arrange
        string[] args = { "ask", "-q", "Test", "--strict-model" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.StrictModel.Should().BeTrue();
        });
    }

    [Fact]
    public void ParseAskOptions_WithStream_ParsesCorrectly()
    {
        // Arrange
        string[] args = { "ask", "-q", "Test", "--stream" };

        // Act
        var result = Parser.Default.ParseArguments<AskOptions, PipelineOptions>(args);

        // Assert
        result.WithParsed<AskOptions>(opts =>
        {
            opts.Stream.Should().BeTrue();
        });
    }
}
