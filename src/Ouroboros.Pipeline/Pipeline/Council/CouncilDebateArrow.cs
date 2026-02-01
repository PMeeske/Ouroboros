// <copyright file="CouncilDebateArrow.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Provides arrow functions for council debate operations in the pipeline.
/// This class maintains backward compatibility while encouraging use of CouncilOrchestratorArrows
/// for new code following the functional arrow parameterization pattern.
/// </summary>
public static class CouncilDebateArrow
{
    /// <summary>
    /// Creates a council debate arrow that convenes a council and adds the decision to the pipeline.
    /// Note: Consider using CouncilOrchestratorArrows for new code with explicit dependency parameterization.
    /// </summary>
    /// <param name="orchestrator">The council orchestrator to use.</param>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A step that transforms a pipeline branch by adding a council decision event.</returns>
    public static Step<PipelineBranch, PipelineBranch> Create(
        ICouncilOrchestrator orchestrator,
        CouncilTopic topic,
        CouncilConfig? config = null)
        => async branch =>
        {
            config ??= CouncilConfig.Default;
            var result = await orchestrator.ConveneCouncilAsync(topic, config);

            return result.Match(
                decision => branch.WithEvent(CouncilDecisionEvent.Create(topic, decision)),
                _ => branch.WithEvent(CouncilDecisionEvent.Create(topic, CouncilDecision.Failed("Council debate failed"))));
        };

    /// <summary>
    /// Creates a council debate arrow with explicit LLM and agents parameters.
    /// This is the recommended arrow parameterization approach.
    /// </summary>
    /// <param name="llm">The language model to use for agent interactions.</param>
    /// <param name="agents">The list of agent personas to participate.</param>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A step that transforms a pipeline branch by adding a council decision event.</returns>
    public static Step<PipelineBranch, PipelineBranch> CreateWithDependencies(
        ToolAwareChatModel llm,
        IReadOnlyList<IAgentPersona> agents,
        CouncilTopic topic,
        CouncilConfig? config = null)
        => CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic, config);

    /// <summary>
    /// Creates a Result-safe council debate arrow with comprehensive error handling.
    /// </summary>
    /// <param name="orchestrator">The council orchestrator to use.</param>
    /// <param name="topic">The topic to debate.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A Kleisli arrow that returns a Result with the updated branch or error.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> CreateSafe(
        ICouncilOrchestrator orchestrator,
        CouncilTopic topic,
        CouncilConfig? config = null)
        => async branch =>
        {
            try
            {
                config ??= CouncilConfig.Default;
                var result = await orchestrator.ConveneCouncilAsync(topic, config);

                return result.Match(
                    decision => Result<PipelineBranch, string>.Success(
                        branch.WithEvent(CouncilDecisionEvent.Create(topic, decision))),
                    error => Result<PipelineBranch, string>.Failure(error));
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Council debate exception: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates a council debate arrow that builds the topic from the current pipeline state.
    /// </summary>
    /// <param name="orchestrator">The council orchestrator to use.</param>
    /// <param name="questionBuilder">Function to build the question from the branch.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A step that transforms a pipeline branch by adding a council decision event.</returns>
    public static Step<PipelineBranch, PipelineBranch> CreateDynamic(
        ICouncilOrchestrator orchestrator,
        Func<PipelineBranch, CouncilTopic> questionBuilder,
        CouncilConfig? config = null)
        => async branch =>
        {
            config ??= CouncilConfig.Default;
            var topic = questionBuilder(branch);
            var result = await orchestrator.ConveneCouncilAsync(topic, config);

            return result.Match(
                decision => branch.WithEvent(CouncilDecisionEvent.Create(topic, decision)),
                _ => branch.WithEvent(CouncilDecisionEvent.Create(topic, CouncilDecision.Failed("Council debate failed"))));
        };

    /// <summary>
    /// Creates a composed pipeline that runs a reasoning step followed by council validation.
    /// </summary>
    /// <param name="orchestrator">The council orchestrator to use.</param>
    /// <param name="reasoningStep">The reasoning step to validate.</param>
    /// <param name="topicBuilder">Function to build the council topic from the branch.</param>
    /// <param name="config">Optional configuration for the debate.</param>
    /// <returns>A composed step that runs reasoning and then council validation.</returns>
    public static Step<PipelineBranch, PipelineBranch> WithCouncilValidation(
        ICouncilOrchestrator orchestrator,
        Step<PipelineBranch, PipelineBranch> reasoningStep,
        Func<PipelineBranch, CouncilTopic> topicBuilder,
        CouncilConfig? config = null)
        => async branch =>
        {
            // First run the reasoning step
            var afterReasoning = await reasoningStep(branch);

            // Then run council validation
            config ??= CouncilConfig.Default;
            var topic = topicBuilder(afterReasoning);
            var result = await orchestrator.ConveneCouncilAsync(topic, config);

            return result.Match(
                decision => afterReasoning.WithEvent(CouncilDecisionEvent.Create(topic, decision)),
                _ => afterReasoning.WithEvent(CouncilDecisionEvent.Create(topic, CouncilDecision.Failed("Council validation failed"))));
        };

    /// <summary>
    /// Gets the most recent council decision from a pipeline branch.
    /// </summary>
    /// <param name="branch">The pipeline branch to search.</param>
    /// <returns>The most recent council decision, or null if none exists.</returns>
    public static CouncilDecision? GetMostRecentDecision(PipelineBranch branch)
    {
        return branch.Events
            .OfType<CouncilDecisionEvent>()
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault()
            ?.Decision;
    }

    /// <summary>
    /// Checks if the most recent council decision reached consensus.
    /// </summary>
    /// <param name="branch">The pipeline branch to check.</param>
    /// <returns>True if consensus was reached, false otherwise.</returns>
    public static bool HasConsensus(PipelineBranch branch)
    {
        var decision = GetMostRecentDecision(branch);
        return decision?.IsConsensus ?? false;
    }
}
