// <copyright file="EpisodeRunnerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using LangChainPipeline.Domain.Reinforcement;
using Ouroboros.Application.Services.Reinforcement;
using Ouroboros.Examples.Environments;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for EpisodeRunner.
/// </summary>
public class EpisodeRunnerTests
{
    [Fact]
    public async Task RunEpisodeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);
        var runner = new EpisodeRunner(environment, policy, "test-gridworld");

        // Act
        var result = await runner.RunEpisodeAsync(maxSteps: 50);

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
    public async Task RunEpisodeAsync_ShouldRecordAllSteps()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.5, seed: 42);
        var runner = new EpisodeRunner(environment, policy, "test-gridworld");

        // Act
        var result = await runner.RunEpisodeAsync(maxSteps: 20);

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
    public async Task RunEpisodeAsync_WhenGoalReached_ShouldTerminate()
    {
        // Arrange - Small grid makes it more likely to reach goal
        var environment = new GridWorldEnvironment(2, 2);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.5, seed: 123);
        var runner = new EpisodeRunner(environment, policy, "test-gridworld");

        // Act - Run multiple episodes, at least one should reach goal
        var episodes = new List<LangChainPipeline.Domain.Environment.Episode>();
        for (var i = 0; i < 10; i++)
        {
            var result = await runner.RunEpisodeAsync(maxSteps: 20);
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
    public async Task RunMultipleEpisodesAsync_ShouldCompleteAllEpisodes()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);
        var runner = new EpisodeRunner(environment, policy, "test-gridworld");

        // Act
        var result = await runner.RunMultipleEpisodesAsync(episodeCount: 5, maxStepsPerEpisode: 30);

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
    public async Task RunMultipleEpisodesAsync_PolicyShouldImprove()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.2, seed: 42);
        var runner = new EpisodeRunner(environment, policy, "test-gridworld");

        // Act - Run many episodes
        var result = await runner.RunMultipleEpisodesAsync(episodeCount: 20, maxStepsPerEpisode: 50);

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
    public async Task EpisodeMetrics_FromEpisode_ShouldCalculateCorrectly()
    {
        // Arrange
        var environment = new GridWorldEnvironment(3, 3);
        var policy = new EpsilonGreedyPolicy(epsilon: 0.3, seed: 42);
        var runner = new EpisodeRunner(environment, policy, "test-gridworld");

        // Act
        var episodeResult = await runner.RunEpisodeAsync(maxSteps: 20);
        var episode = episodeResult.Value;
        var metrics = EpisodeMetrics.FromEpisode(episode);

        // Assert
        metrics.EpisodeId.Should().Be(episode.Id);
        metrics.StepCount.Should().Be(episode.StepCount);
        metrics.TotalReward.Should().Be(episode.TotalReward);
        metrics.AverageReward.Should().Be(episode.TotalReward / episode.StepCount);
        metrics.Duration.Should().Be(episode.Duration!.Value);
    }

    [Fact]
    public void EpisodeMetrics_FromIncompleteEpisode_ShouldThrow()
    {
        // Arrange
        var incompleteEpisode = new LangChainPipeline.Domain.Environment.Episode(
            Guid.NewGuid(),
            "test",
            new List<LangChainPipeline.Domain.Environment.EnvironmentStep>(),
            0.0,
            DateTime.UtcNow,
            null); // No end time

        // Act & Assert
        Action act = () => EpisodeMetrics.FromEpisode(incompleteEpisode);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*incomplete*");
    }
}
