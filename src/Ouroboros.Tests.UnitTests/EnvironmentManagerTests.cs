// <copyright file="EnvironmentManagerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Tests.UnitTests;

/// <summary>
/// Unit tests for EnvironmentManager.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EnvironmentManagerTests
{
    private readonly EnvironmentManager manager;

    public EnvironmentManagerTests()
    {
        this.manager = new EnvironmentManager(NullLogger<EnvironmentManager>.Instance);
    }

    [Fact]
    public async Task CreateEnvironmentAsync_WithValidConfig_ShouldReturnHandle()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>
            {
                ["difficulty"] = 1,
                ["seed"] = 42,
            },
            AvailableActions: new List<string> { "Move", "Jump" },
            Type: EnvironmentType.Unity);

        // Act
        var result = await this.manager.CreateEnvironmentAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SceneName.Should().Be("TestScene");
        result.Value.Type.Should().Be(EnvironmentType.Unity);
        result.Value.IsRunning.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateEnvironmentAsync_WithEmptySceneName_ShouldReturnFailure()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: string.Empty,
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        // Act
        var result = await this.manager.CreateEnvironmentAsync(config);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Scene name cannot be empty");
    }

    [Theory]
    [InlineData(EnvironmentType.Unity)]
    [InlineData(EnvironmentType.Gym)]
    [InlineData(EnvironmentType.Custom)]
    [InlineData(EnvironmentType.Simulation)]
    public async Task CreateEnvironmentAsync_WithDifferentTypes_ShouldSucceed(EnvironmentType type)
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: type);

        // Act
        var result = await this.manager.CreateEnvironmentAsync(config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(type);
    }

    [Fact]
    public async Task ResetEnvironmentAsync_WithValidHandle_ShouldSucceed()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        var createResult = await this.manager.CreateEnvironmentAsync(config);
        var handle = createResult.Value;

        // Act
        var result = await this.manager.ResetEnvironmentAsync(handle);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetEnvironmentAsync_WithInvalidHandle_ShouldReturnFailure()
    {
        // Arrange
        var invalidHandle = new EnvironmentHandle(
            Id: Guid.NewGuid(),
            SceneName: "NonExistent",
            Type: EnvironmentType.Unity,
            IsRunning: false);

        // Act
        var result = await this.manager.ResetEnvironmentAsync(invalidHandle);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task DestroyEnvironmentAsync_WithValidHandle_ShouldSucceed()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        var createResult = await this.manager.CreateEnvironmentAsync(config);
        var handle = createResult.Value;

        // Act
        var result = await this.manager.DestroyEnvironmentAsync(handle);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DestroyEnvironmentAsync_AfterDestroy_ResetShouldFail()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        var createResult = await this.manager.CreateEnvironmentAsync(config);
        var handle = createResult.Value;
        await this.manager.DestroyEnvironmentAsync(handle);

        // Act
        var resetResult = await this.manager.ResetEnvironmentAsync(handle);

        // Assert
        resetResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListAvailableEnvironmentsAsync_ShouldReturnEnvironments()
    {
        // Act
        var result = await this.manager.ListAvailableEnvironmentsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListAvailableEnvironmentsAsync_ShouldIncludeDefaultEnvironments()
    {
        // Act
        var result = await this.manager.ListAvailableEnvironmentsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(e => e.Name == "BasicNavigation");
        result.Value.Should().Contain(e => e.Name == "ManipulationTask");
        result.Value.Should().Contain(e => e.Type == EnvironmentType.Unity);
        result.Value.Should().Contain(e => e.Type == EnvironmentType.Custom);
    }

    [Fact]
    public async Task CreateEnvironmentAsync_MultipleEnvironments_ShouldHaveUniqueIds()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        // Act
        var result1 = await this.manager.CreateEnvironmentAsync(config);
        var result2 = await this.manager.CreateEnvironmentAsync(config);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Id.Should().NotBe(result2.Value.Id);
    }

    [Fact]
    public async Task EnvironmentLifecycle_CreateResetDestroy_ShouldSucceed()
    {
        // Arrange
        var config = new EnvironmentConfig(
            SceneName: "TestScene",
            Parameters: new Dictionary<string, object>(),
            AvailableActions: new List<string>(),
            Type: EnvironmentType.Unity);

        // Act & Assert - Create
        var createResult = await this.manager.CreateEnvironmentAsync(config);
        createResult.IsSuccess.Should().BeTrue();
        var handle = createResult.Value;

        // Act & Assert - Reset
        var resetResult = await this.manager.ResetEnvironmentAsync(handle);
        resetResult.IsSuccess.Should().BeTrue();

        // Act & Assert - Destroy
        var destroyResult = await this.manager.DestroyEnvironmentAsync(handle);
        destroyResult.IsSuccess.Should().BeTrue();
    }
}
