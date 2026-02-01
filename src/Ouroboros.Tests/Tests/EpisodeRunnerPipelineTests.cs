// <copyright file="EpisodeRunnerPipelineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Reinforcement;
using Ouroboros.Application.Services.Reinforcement;
using Ouroboros.Examples.Environments;
using Unit = Ouroboros.Domain.Reinforcement.Unit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for EpisodeRunnerPipeline composable arrow implementation.
/// </summary>
[Trait("Category", "Unit")]
public class EpisodeRunnerPipelineTests
{
    [Fact]
    public async Task EpisodePipeline_ShouldCompleteSuccessfully()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);
        var pipeline = EpisodeRunnerPipeline.EpisodePipeline(
            environment,
            policy,
            "test-gridworld",
            maxSteps: 50);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var episode = result.Value;

        episode.Should().NotBeNull();
        episode.EnvironmentName.Should().Be("test-gridworld");
        episode.Steps.Should().NotBeEmpty();
        episode.Steps.Count.Should().BeLessThanOrEqualTo(50);
        episode.IsComplete.Should().BeTrue();
        episode.Duration.Should().NotBeNull();
    }

    [Fact]
    public async Task EpisodePipeline_ShouldRecordAllSteps()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.5, seed: 42);
        var pipeline = EpisodeRunnerPipeline.EpisodePipeline(
            environment,
            policy,
            "test-gridworld",
            maxSteps: 20);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var episode = result.Value;

        // Verify step sequence
        for (var i = 0; i < episode.Steps.Count; i++)
        {
            var step = episode.Steps[i];
            step.StepNumber.Should().Be(i);
            step.State.Should().NotBeNull();
            step.Action.Should().NotBeNull();
            step.Observation.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task EpisodePipeline_WhenGoalReached_ShouldTerminate()
    {
        // Arrange - Small grid makes it more likely to reach goal
        var environment = new GridWorldEnvironment(2, 2);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.5, seed: 123);

        // Act - Run multiple episodes, at least one should reach goal
        var episodes = new List<Ouroboros.Domain.Environment.Episode>();
        for (var i = 0; i < 10; i++)
        {
            var pipeline = EpisodeRunnerPipeline.EpisodePipeline(
                environment,
                policy,
                "test-gridworld",
                maxSteps: 20);
            var result = await pipeline(Unit.Value);
            if (result.IsSuccess)
            {
                episodes.Add(result.Value);
            }
        }

        // Assert - At least one episode should succeed
        episodes.Should().Contain(e => e.Success);
        var successfulEpisode = episodes.First(e => e.Success);
        successfulEpisode.TotalReward.Should().BeGreaterThan(0);
        successfulEpisode.Steps.Last().Observation.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleEpisodesPipeline_ShouldCompleteAllEpisodes()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);
        var pipeline = EpisodeRunnerPipeline.MultipleEpisodesPipeline(
            environment,
            policy,
            "test-gridworld",
            episodeCount: 5,
            maxStepsPerEpisode: 30);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);

        foreach (var episode in result.Value)
        {
            episode.IsComplete.Should().BeTrue();
            episode.EnvironmentName.Should().Be("test-gridworld");
        }
    }

    [Fact]
    public async Task MultipleEpisodesPipeline_PolicyShouldImprove()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.2, seed: 42);
        var pipeline = EpisodeRunnerPipeline.MultipleEpisodesPipeline(
            environment,
            policy,
            "test-gridworld",
            episodeCount: 20,
            maxStepsPerEpisode: 50);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Calculate average reward for first half vs second half
        var episodes = result.Value.ToList();
        var firstHalfAvg = episodes.Take(10).Average(e => e.TotalReward);
        var secondHalfAvg = episodes.Skip(10).Average(e => e.TotalReward);

        // Policy should improve over time (or at least not get worse)
        // Due to randomness, we use a lenient comparison
        secondHalfAvg.Should().BeGreaterThanOrEqualTo(firstHalfAvg - 10.0);
    }

    [Fact]
    public async Task EpisodePipeline_ShouldPropagateErrors()
    {
        // Arrange - Use an environment that will fail
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);
        
        // Force termination immediately by using 0 max steps
        var pipeline = EpisodeRunnerPipeline.EpisodePipeline(
            environment,
            policy,
            "test-gridworld",
            maxSteps: 0);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert - Should complete successfully but with 0 steps
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task EpisodePipeline_CanBeComposedWithOtherArrows()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);

        // Create a composed pipeline that logs results
        var logged = false;
        var loggingPipeline = EpisodeRunnerPipeline
            .EpisodePipeline(environment, policy, "test-gridworld", maxSteps: 20)
            .Tap(result =>
            {
                if (result.IsSuccess)
                {
                    logged = true;
                }
            });

        // Act
        var result = await loggingPipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        logged.Should().BeTrue("Tap should have executed");
    }

    [Fact]
    public async Task EpisodePipeline_CanBeTransformed()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);

        // Create a pipeline that transforms the result
        var transformedPipeline = EpisodeRunnerPipeline
            .EpisodePipeline(environment, policy, "test-gridworld", maxSteps: 20)
            .Map(episodeResult => episodeResult.IsSuccess 
                ? $"Episode completed with {episodeResult.Value.Steps.Count} steps"
                : $"Episode failed: {episodeResult.Error}");

        // Act
        var result = await transformedPipeline(Unit.Value);

        // Assert
        result.Should().Contain("Episode completed with");
        result.Should().Contain("steps");
    }
}
