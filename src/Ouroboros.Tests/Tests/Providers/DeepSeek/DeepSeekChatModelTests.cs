// <copyright file="DeepSeekChatModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using FluentAssertions;
using LangChain.Providers.Ollama;
using Ouroboros.Providers.DeepSeek;
using Xunit;

namespace Ouroboros.Tests.Providers.DeepSeek;

/// <summary>
/// Unit tests for DeepSeekChatModel class.
/// Validates model creation and configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeepSeekChatModelTests
{
    [Fact]
    public void Constructor_WithOllamaCloud_CreatesModel()
    {
        // Arrange & Act
        var model = new DeepSeekChatModel(
            "https://api.ollama.ai",
            "test-api-key",
            DeepSeekChatModel.ModelDeepSeekR1_32B);

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidEndpoint_ThrowsException()
    {
        // Arrange & Act
        Action act = () => new DeepSeekChatModel("", "api-key", "model");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Endpoint is required*");
    }

    [Fact]
    public void Constructor_WithInvalidApiKey_ThrowsException()
    {
        // Arrange & Act
        Action act = () => new DeepSeekChatModel("https://api.ollama.ai", "", "model");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*API key is required*");
    }

    [Fact]
    public void FromEnvironment_WithoutEnvironmentVariables_ThrowsException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OLLAMA_CLOUD_ENDPOINT", null);
        Environment.SetEnvironmentVariable("CHAT_ENDPOINT", null);
        Environment.SetEnvironmentVariable("OLLAMA_CLOUD_API_KEY", null);
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", null);
        Environment.SetEnvironmentVariable("CHAT_API_KEY", null);

        // Act
        Action act = () => DeepSeekChatModel.FromEnvironment();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*environment variable is not set*");
    }

    [Fact]
    public void FromEnvironment_WithEnvironmentVariables_CreatesModel()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OLLAMA_CLOUD_ENDPOINT", "https://test.ollama.ai");
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", "test-key");

        try
        {
            // Act
            var model = DeepSeekChatModel.FromEnvironment();

            // Assert
            model.Should().NotBeNull();
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("OLLAMA_CLOUD_ENDPOINT", null);
            Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", null);
        }
    }

    [Fact]
    public void ModelConstants_HaveExpectedValues()
    {
        // Assert
        DeepSeekChatModel.ModelDeepSeekR1_7B.Should().Be("deepseek-r1:7b");
        DeepSeekChatModel.ModelDeepSeekR1_8B.Should().Be("deepseek-r1:8b");
        DeepSeekChatModel.ModelDeepSeekR1_14B.Should().Be("deepseek-r1:14b");
        DeepSeekChatModel.ModelDeepSeekR1_32B.Should().Be("deepseek-r1:32b");
        DeepSeekChatModel.ModelDeepSeekR1_70B.Should().Be("deepseek-r1:70b");
    }
}
