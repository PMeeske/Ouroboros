// <copyright file="UnityMLAgentsClientTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for UnityMLAgentsClient.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UnityMLAgentsClientTests : IDisposable
{
    private readonly UnityMLAgentsClient client;
    private bool disposed;

    public UnityMLAgentsClientTests()
    {
        this.client = new UnityMLAgentsClient(NullLogger<UnityMLAgentsClient>.Instance);
    }

    [Fact]
    public async Task ConnectAsync_FirstTime_ShouldSucceed()
    {
        // Act
        var result = await this.client.ConnectAsync("localhost", 5005);

        // Assert
        result.IsSuccess.Should().BeTrue();
        this.client.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ShouldSucceed()
    {
        // Arrange
        await this.client.ConnectAsync("localhost", 5005);

        // Act
        var result = await this.client.ConnectAsync("localhost", 5005);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidHost_ShouldReturnFailure()
    {
        // Act
        var result = await this.client.ConnectAsync(string.Empty, 5005);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Host");
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidPort_ShouldReturnFailure()
    {
        // Act
        var result = await this.client.ConnectAsync("localhost", 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Port");
    }

    [Fact]
    public async Task ResetEnvironmentAsync_WhenNotConnected_ShouldReturnFailure()
    {
        // Act
        var result = await this.client.ResetEnvironmentAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task ResetEnvironmentAsync_WhenConnected_ShouldReturnState()
    {
        // Arrange
        await this.client.ConnectAsync("localhost", 5005);

        // Act
        var result = await this.client.ResetEnvironmentAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Observations.Should().NotBeNull();
        result.Value.Done.Should().BeFalse();
    }

    [Fact]
    public async Task StepAsync_WhenNotConnected_ShouldReturnFailure()
    {
        // Arrange
        var actions = new float[] { 0.5f, 0.5f };

        // Act
        var result = await this.client.StepAsync(actions);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task StepAsync_WhenConnected_ShouldReturnStepResult()
    {
        // Arrange
        await this.client.ConnectAsync("localhost", 5005);
        var actions = new float[] { 0.5f, 0.5f };

        // Act
        var result = await this.client.StepAsync(actions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.State.Should().NotBeNull();
        result.Value.State.Observations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StepAsync_WithNullActions_ShouldReturnFailure()
    {
        // Arrange
        await this.client.ConnectAsync("localhost", 5005);

        // Act
        var result = await this.client.StepAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Actions");
    }

    [Fact]
    public async Task GetEnvironmentInfoAsync_WhenNotConnected_ShouldReturnFailure()
    {
        // Act
        var result = await this.client.GetEnvironmentInfoAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task GetEnvironmentInfoAsync_WhenConnected_ShouldReturnInfo()
    {
        // Arrange
        await this.client.ConnectAsync("localhost", 5005);

        // Act
        var result = await this.client.GetEnvironmentInfoAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EnvironmentName.Should().NotBeNullOrEmpty();
        result.Value.ObservationSpaceSize.Should().BeGreaterThan(0);
        result.Value.ActionSpaceSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_ShouldSucceed()
    {
        // Arrange
        await this.client.ConnectAsync("localhost", 5005);

        // Act
        await this.client.DisconnectAsync();

        // Assert
        this.client.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAsync_WhenNotConnected_ShouldNotThrow()
    {
        // Act
        var act = async () => await this.client.DisconnectAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Dispose_ShouldDisconnect()
    {
        // Arrange
        await this.client.ConnectAsync("localhost", 5005);

        // Act
        this.client.Dispose();

        // Assert
        this.client.IsConnected.Should().BeFalse();
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.client.Dispose();
        this.disposed = true;
    }
}
