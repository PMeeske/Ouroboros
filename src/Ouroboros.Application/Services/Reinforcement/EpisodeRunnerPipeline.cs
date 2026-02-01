// <copyright file="EpisodeRunnerPipeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Application.Services.Reinforcement;

/// <summary>
/// Pipeline-based episode runner using composable arrows.
/// Demonstrates transformation from imperative to functional pipeline architecture.
/// </summary>
public static class EpisodeRunnerPipeline
{
    /// <summary>
    /// Represents the intermediate state during episode execution.
    /// </summary>
    public sealed record EpisodeContext(
        Guid EpisodeId,
        string EnvironmentName,
        IEnvironmentActor Environment,
        IPolicy Policy,
        IReadOnlyList<EnvironmentStep> Steps,
        EnvironmentState CurrentState,
        double TotalReward,
        DateTime StartTime,
        int StepNumber,
        int MaxSteps,
        bool IsTerminal,
        bool Success,
        CancellationToken CancellationToken);

    /// <summary>
    /// Represents a single step execution result.
    /// </summary>
    private sealed record StepResult(
        EnvironmentStep Step,
        EnvironmentState NextState,
        bool IsTerminal,
        double Reward);

    /// <summary>
    /// Creates a complete episode execution pipeline.
    /// Composes all episode steps into a single arrow.
    /// </summary>
    /// <param name="environment">The environment actor</param>
    /// <param name="policy">The policy to use</param>
    /// <param name="environmentName">Name of the environment</param>
    /// <param name="maxSteps">Maximum number of steps</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A step that executes the complete episode</returns>
    public static Step<Unit, Result<Episode>> EpisodePipeline(
        IEnvironmentActor environment,
        IPolicy policy,
        string environmentName,
        int maxSteps,
        CancellationToken cancellationToken = default) =>
        InitializeEpisodeArrow(environment, policy, environmentName, maxSteps, cancellationToken)
            .Then(ExecuteEpisodeLoopArrow())
            .Map(FinalizeEpisode);

    /// <summary>
    /// Arrow that initializes a new episode context by resetting the environment.
    /// </summary>
    private static Step<Unit, Result<EpisodeContext>> InitializeEpisodeArrow(
        IEnvironmentActor environment,
        IPolicy policy,
        string environmentName,
        int maxSteps,
        CancellationToken cancellationToken) =>
        async _ =>
        {
            var episodeId = Guid.NewGuid();
            var startTime = DateTime.UtcNow;

            var resetResult = await environment.ResetAsync(cancellationToken);
            if (resetResult.IsFailure)
            {
                return Result<EpisodeContext>.Failure($"Failed to reset environment: {resetResult.Error}");
            }

            var context = new EpisodeContext(
                EpisodeId: episodeId,
                EnvironmentName: environmentName,
                Environment: environment,
                Policy: policy,
                Steps: new List<EnvironmentStep>(),
                CurrentState: resetResult.Value,
                TotalReward: 0.0,
                StartTime: startTime,
                StepNumber: 0,
                MaxSteps: maxSteps,
                IsTerminal: false,
                Success: false,
                CancellationToken: cancellationToken);

            return Result<EpisodeContext>.Success(context);
        };

    /// <summary>
    /// Arrow that executes the episode loop until termination or max steps.
    /// </summary>
    private static Step<Result<EpisodeContext>, Result<EpisodeContext>> ExecuteEpisodeLoopArrow() =>
        async contextResult =>
        {
            if (contextResult.IsFailure)
            {
                return contextResult;
            }

            var context = contextResult.Value;

            while (context.StepNumber < context.MaxSteps && !context.IsTerminal)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var stepResult = await ExecuteSingleStepArrow()(context);
                if (stepResult.IsFailure)
                {
                    return Result<EpisodeContext>.Failure(stepResult.Error);
                }

                context = stepResult.Value;
            }

            return Result<EpisodeContext>.Success(context);
        };

    /// <summary>
    /// Arrow that executes a single step in the episode.
    /// Composes: GetActions -> SelectAction -> ExecuteAction -> UpdatePolicy.
    /// </summary>
    private static Step<EpisodeContext, Result<EpisodeContext>> ExecuteSingleStepArrow() =>
        GetActionsArrow()
            .Then(SelectActionArrow())
            .Then(ExecuteActionArrow())
            .Then(UpdatePolicyArrow())
            .Then(RecordStepArrow());

    /// <summary>
    /// Arrow that retrieves available actions from the environment.
    /// </summary>
    private static Step<EpisodeContext, Result<(EpisodeContext Context, IReadOnlyList<EnvironmentAction> Actions)>> GetActionsArrow() =>
        async context =>
        {
            var actionsResult = await context.Environment.GetAvailableActionsAsync(context.CancellationToken);
            if (actionsResult.IsFailure)
            {
                return Result<(EpisodeContext, IReadOnlyList<EnvironmentAction>)>.Failure(
                    $"Failed to get available actions: {actionsResult.Error}");
            }

            return Result<(EpisodeContext, IReadOnlyList<EnvironmentAction>)>.Success((context, actionsResult.Value));
        };

    /// <summary>
    /// Arrow that selects an action using the policy.
    /// </summary>
    private static Step<Result<(EpisodeContext Context, IReadOnlyList<EnvironmentAction> Actions)>, Result<(EpisodeContext Context, EnvironmentAction Action)>> SelectActionArrow() =>
        async tupleResult =>
        {
            if (tupleResult.IsFailure)
            {
                return Result<(EpisodeContext, EnvironmentAction)>.Failure(tupleResult.Error);
            }

            var (context, actions) = tupleResult.Value;
            var actionResult = await context.Policy.SelectActionAsync(
                context.CurrentState,
                actions,
                context.CancellationToken);

            if (actionResult.IsFailure)
            {
                return Result<(EpisodeContext, EnvironmentAction)>.Failure(
                    $"Failed to select action: {actionResult.Error}");
            }

            return Result<(EpisodeContext, EnvironmentAction)>.Success((context, actionResult.Value));
        };

    /// <summary>
    /// Arrow that executes the selected action in the environment.
    /// </summary>
    private static Step<Result<(EpisodeContext Context, EnvironmentAction Action)>, Result<(EpisodeContext Context, EnvironmentAction Action, Observation Observation)>> ExecuteActionArrow() =>
        async tupleResult =>
        {
            if (tupleResult.IsFailure)
            {
                return Result<(EpisodeContext, EnvironmentAction, Observation)>.Failure(tupleResult.Error);
            }

            var (context, action) = tupleResult.Value;
            var observationResult = await context.Environment.ExecuteActionAsync(action, context.CancellationToken);

            if (observationResult.IsFailure)
            {
                return Result<(EpisodeContext, EnvironmentAction, Observation)>.Failure(
                    $"Failed to execute action: {observationResult.Error}");
            }

            return Result<(EpisodeContext, EnvironmentAction, Observation)>.Success(
                (context, action, observationResult.Value));
        };

    /// <summary>
    /// Arrow that updates the policy with the step results.
    /// </summary>
    private static Step<Result<(EpisodeContext Context, EnvironmentAction Action, Observation Observation)>, Result<(EpisodeContext Context, EnvironmentAction Action, Observation Observation)>> UpdatePolicyArrow() =>
        async tupleResult =>
        {
            if (tupleResult.IsFailure)
            {
                return tupleResult;
            }

            var (context, action, observation) = tupleResult.Value;
            var updateResult = await context.Policy.UpdateAsync(
                context.CurrentState,
                action,
                observation,
                context.CancellationToken);

            if (updateResult.IsFailure)
            {
                // Policy update is optional - continue execution even if it fails
                // In production, this should use proper logging (ILogger)
            }

            return tupleResult;
        };

    /// <summary>
    /// Arrow that records the step and updates the context.
    /// </summary>
    private static Step<Result<(EpisodeContext Context, EnvironmentAction Action, Observation Observation)>, Result<EpisodeContext>> RecordStepArrow() =>
        async tupleResult =>
        {
            if (tupleResult.IsFailure)
            {
                return Result<EpisodeContext>.Failure(tupleResult.Error);
            }

            var (context, action, observation) = tupleResult.Value;

            var envStep = new EnvironmentStep(
                context.StepNumber,
                context.CurrentState,
                action,
                observation,
                DateTime.UtcNow);

            var updatedSteps = new List<EnvironmentStep>(context.Steps) { envStep };

            var updatedContext = context with
            {
                Steps = updatedSteps,
                CurrentState = observation.State,
                TotalReward = context.TotalReward + observation.Reward,
                StepNumber = context.StepNumber + 1,
                IsTerminal = observation.IsTerminal,
                Success = observation.IsTerminal && observation.Reward > 0
            };

            return Result<EpisodeContext>.Success(updatedContext);
        };

    /// <summary>
    /// Pure function that finalizes the episode context into an Episode record.
    /// </summary>
    private static Result<Episode> FinalizeEpisode(Result<EpisodeContext> contextResult)
    {
        if (contextResult.IsFailure)
        {
            return Result<Episode>.Failure(contextResult.Error);
        }

        var context = contextResult.Value;
        var endTime = DateTime.UtcNow;

        var episode = new Episode(
            context.EpisodeId,
            context.EnvironmentName,
            context.Steps,
            context.TotalReward,
            context.StartTime,
            endTime,
            context.Success);

        return Result<Episode>.Success(episode);
    }

    /// <summary>
    /// Creates a pipeline for running multiple episodes.
    /// </summary>
    /// <param name="environment">The environment actor</param>
    /// <param name="policy">The policy to use</param>
    /// <param name="environmentName">Name of the environment</param>
    /// <param name="episodeCount">Number of episodes to run</param>
    /// <param name="maxStepsPerEpisode">Maximum steps per episode</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A step that executes multiple episodes</returns>
    public static Step<Unit, Result<IReadOnlyList<Episode>>> MultipleEpisodesPipeline(
        IEnvironmentActor environment,
        IPolicy policy,
        string environmentName,
        int episodeCount,
        int maxStepsPerEpisode,
        CancellationToken cancellationToken = default) =>
        async _ =>
        {
            var episodes = new List<Episode>();

            for (var i = 0; i < episodeCount; i++)
            {
                var pipeline = EpisodePipeline(environment, policy, environmentName, maxStepsPerEpisode, cancellationToken);
                var result = await pipeline(Unit.Value);

                if (result.IsFailure)
                {
                    return Result<IReadOnlyList<Episode>>.Failure($"Episode {i} failed: {result.Error}");
                }

                episodes.Add(result.Value);
            }

            return Result<IReadOnlyList<Episode>>.Success(episodes);
        };
}
