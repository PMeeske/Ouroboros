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
        this.client = new UnityMLAgentsClient("localhost", 5005, NullLogger<UnityMLAgentsClient>.Instance);
    }

    [Fact]
    public async Task ConnectAsync_FirstTime_ShouldSucceed()
    {
        // Act
        var result = await this.client.ConnectAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_ShouldSucceed()
    {
        // Arrange
        await this.client.ConnectAsync();

        // Act
        var result = await this.client.ConnectAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendActionAsync_WhenNotConnected_ShouldReturnFailure()
    {
        // Arrange
        var action = EmbodiedAction.NoOp();

        // Act
        var result = await this.client.SendActionAsync(action);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task SendActionAsync_WhenConnected_ShouldReturnActionResult()
    {
        // Arrange
        await this.client.ConnectAsync();
        var action = EmbodiedAction.Move(new Vector3(1f, 0f, 0f), "Forward");

        // Act
        var result = await this.client.SendActionAsync(action);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Success.Should().BeTrue();
        result.Value.ResultingState.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSensorStateAsync_WhenNotConnected_ShouldReturnFailure()
    {
        // Act
        var result = await this.client.GetSensorStateAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not connected");
    }

    [Fact]
    public async Task GetSensorStateAsync_WhenConnected_ShouldReturnSensorState()
    {
        // Arrange
        await this.client.ConnectAsync();

        // Act
        var result = await this.client.GetSensorStateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Position.Should().NotBeNull();
        result.Value.Rotation.Should().NotBeNull();
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
    public async Task ResetEnvironmentAsync_WhenConnected_ShouldSucceed()
    {
        // Arrange
        await this.client.ConnectAsync();

        // Act
        var result = await this.client.ResetEnvironmentAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_ShouldDisconnect()
    {
        // Arrange
        await this.client.ConnectAsync();

        // Act
        await this.client.DisconnectAsync();

        // Assert - subsequent operations should fail
        var result = await this.client.GetSensorStateAsync();
        result.IsFailure.Should().BeTrue();
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
        await this.client.ConnectAsync();

        // Act
        this.client.Dispose();

        // Assert - subsequent operations should fail
        var result = await this.client.GetSensorStateAsync();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task FullWorkflow_ConnectSendActionDisconnect_ShouldSucceed()
    {
        // Arrange
        var action = EmbodiedAction.Move(Vector3.UnitX, "MoveRight");

        // Act & Assert - Connect
        var connectResult = await this.client.ConnectAsync();
        connectResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Send action
        var actionResult = await this.client.SendActionAsync(action);
        actionResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Get sensor state
        var stateResult = await this.client.GetSensorStateAsync();
        stateResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Reset
        var resetResult = await this.client.ResetEnvironmentAsync();
        resetResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Disconnect
        await this.client.DisconnectAsync();
        var postDisconnectResult = await this.client.SendActionAsync(action);
        postDisconnectResult.IsFailure.Should().BeTrue();
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
