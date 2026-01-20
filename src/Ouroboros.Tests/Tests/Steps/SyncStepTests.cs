// <copyright file="SyncStepTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Steps;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Xunit;

/// <summary>
/// Unit tests for SyncStep and Step delegates.
/// </summary>
[Trait("Category", "Unit")]
public class SyncStepTests
{
    [Fact]
    public void Constructor_WithValidFunction_CreatesInstance()
    {
        // Arrange & Act
        var step = new SyncStep<int, string>(x => x.ToString());

        // Assert
        step.Invoke(42).Should().Be("42");
    }

    [Fact]
    public void Constructor_WithNullFunction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SyncStep<int, string>(null!));
    }

    [Fact]
    public void Invoke_ExecutesFunction()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => x * 2);

        // Act
        var result = step.Invoke(5);

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public async Task ToAsync_ConvertsToAsyncStep()
    {
        // Arrange
        var syncStep = new SyncStep<int, string>(x => x.ToString());

        // Act
        Step<int, string> asyncStep = syncStep.ToAsync();
        var result = await asyncStep(42);

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public void Pipe_ComposesWithAnotherSyncStep()
    {
        // Arrange
        var step1 = new SyncStep<int, int>(x => x * 2);
        var step2 = new SyncStep<int, string>(x => x.ToString());

        // Act
        var composed = step1.Pipe(step2);
        var result = composed.Invoke(5);

        // Assert
        result.Should().Be("10");
    }

    [Fact]
    public async Task Pipe_ComposesWithAsyncStep()
    {
        // Arrange
        var syncStep = new SyncStep<int, int>(x => x * 2);
        Step<int, string> asyncStep = x => Task.FromResult(x.ToString());

        // Act
        var composed = syncStep.Pipe(asyncStep);
        var result = await composed(5);

        // Assert
        result.Should().Be("10");
    }

    [Fact]
    public void Map_TransformsOutput()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => x * 2);

        // Act
        var mapped = step.Map(x => x.ToString());
        var result = mapped.Invoke(5);

        // Assert
        result.Should().Be("10");
    }

    [Fact]
    public void Bind_ComposesMonadically()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => x * 2);

        // Act
        var bound = step.Bind(x => new SyncStep<int, string>(input => $"{input}:{x}"));
        var result = bound.Invoke(5);

        // Assert
        result.Should().Be("5:10"); // input:intermediate
    }

    [Fact]
    public void Identity_ReturnsInput()
    {
        // Arrange
        var identity = SyncStep<int, int>.Identity;

        // Act
        var result = identity.Invoke(42);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromFunction_CreatesSyncStep()
    {
        // Arrange
        Func<int, string> func = x => x.ToString();

        // Act
        SyncStep<int, string> step = func;

        // Assert
        step.Invoke(42).Should().Be("42");
    }

    [Fact]
    public async Task ImplicitConversion_ToAsyncStep_Works()
    {
        // Arrange
        var syncStep = new SyncStep<int, string>(x => x.ToString());

        // Act
        Step<int, string> asyncStep = syncStep;
        var result = await asyncStep(42);

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public void Equals_SameDelegate_ReturnsTrue()
    {
        // Arrange
        Func<int, int> func = x => x * 2;
        var step1 = new SyncStep<int, int>(func);
        var step2 = new SyncStep<int, int>(func);

        // Act & Assert
        step1.Equals(step2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentDelegates_ReturnsFalse()
    {
        // Arrange
        var step1 = new SyncStep<int, int>(x => x * 2);
        var step2 = new SyncStep<int, int>(x => x * 2);

        // Act & Assert (different lambda instances)
        step1.Equals(step2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithObject_ReturnsCorrectResult()
    {
        // Arrange
        Func<int, int> func = x => x * 2;
        var step1 = new SyncStep<int, int>(func);
        object boxed = new SyncStep<int, int>(func);

        // Act & Assert
        step1.Equals(boxed).Should().BeTrue();
    }

    [Fact]
    public void Equals_WithInvalidObject_ReturnsFalse()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => x * 2);
        object invalid = "not a step";

        // Act & Assert
        step.Equals(invalid).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_ReturnsConsistentValue()
    {
        // Arrange
        Func<int, int> func = x => x * 2;
        var step1 = new SyncStep<int, int>(func);
        var step2 = new SyncStep<int, int>(func);

        // Act & Assert
        step1.GetHashCode().Should().Be(step2.GetHashCode());
    }
}

/// <summary>
/// Unit tests for SyncStepExtensions.
/// </summary>
[Trait("Category", "Unit")]
public class SyncStepExtensionsTests
{
    [Fact]
    public void ToSyncStep_ConvertsFunction()
    {
        // Arrange
        Func<int, string> func = x => x.ToString();

        // Act
        var step = func.ToSyncStep();

        // Assert
        step.Invoke(42).Should().Be("42");
    }

    [Fact]
    public async Task Then_SyncToAsync_ComposesCorrectly()
    {
        // Arrange
        var syncStep = new SyncStep<int, int>(x => x * 2);
        Step<int, string> asyncStep = x => Task.FromResult(x.ToString());

        // Act
        var composed = syncStep.Then(asyncStep);
        var result = await composed(5);

        // Assert
        result.Should().Be("10");
    }

    [Fact]
    public async Task Then_AsyncToSync_ComposesCorrectly()
    {
        // Arrange
        Step<int, int> asyncStep = x => Task.FromResult(x * 2);
        var syncStep = new SyncStep<int, string>(x => x.ToString());

        // Act
        var composed = asyncStep.Then(syncStep);
        var result = await composed(5);

        // Assert
        result.Should().Be("10");
    }

    [Fact]
    public void TrySync_OnSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => x * 2);

        // Act
        var tryStep = step.TrySync();
        var result = tryStep.Invoke(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public void TrySync_OnException_ReturnsFailureResult()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => throw new InvalidOperationException("Test error"));

        // Act
        var tryStep = step.TrySync();
        var result = tryStep.Invoke(5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TryOption_WhenPredicateTrue_ReturnsSome()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => x * 2);

        // Act
        var optionStep = step.TryOption(x => x > 0);
        var result = optionStep.Invoke(5);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public void TryOption_WhenPredicateFalse_ReturnsNone()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => x * 2);

        // Act
        var optionStep = step.TryOption(x => x < 0);
        var result = optionStep.Invoke(5);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void TryOption_OnException_ReturnsNone()
    {
        // Arrange
        var step = new SyncStep<int, int>(x => throw new InvalidOperationException());

        // Act
        var optionStep = step.TryOption(x => true);
        var result = optionStep.Invoke(5);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToSync_ConvertsAsyncStep()
    {
        // Arrange
        Step<int, string> asyncStep = x => Task.FromResult(x.ToString());

        // Act
        var syncStep = asyncStep.ToSync();
        var result = syncStep.Invoke(42);

        // Assert
        result.Should().Be("42");
    }
}
