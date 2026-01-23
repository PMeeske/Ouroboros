// <copyright file="ConsolidatedMindArrows.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Steps;

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Provides Kleisli arrows and pipeline steps for integrating ConsolidatedMind
/// with the Ouroboros functional pipeline architecture.
/// </summary>
public static class ConsolidatedMindArrows
{
    /// <summary>
    /// Creates a reasoning arrow that uses the ConsolidatedMind for intelligent model selection.
    /// Automatically routes to the best specialist based on task analysis.
    /// </summary>
    /// <param name="mind">The consolidated mind instance.</param>
    /// <param name="embed">Embedding model for context retrieval.</param>
    /// <param name="topic">The topic for reasoning.</param>
    /// <param name="query">The query for context retrieval.</param>
    /// <param name="k">Number of similar documents to retrieve.</param>
    /// <returns>A pipeline step for intelligent reasoning.</returns>
    public static Step<PipelineBranch, PipelineBranch> IntelligentReasoningArrow(
        ConsolidatedMind mind,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return async branch =>
        {
            // Retrieve context
            var docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            // Build prompt with context
            string prompt = $@"Context:
{context}

Topic: {topic}

Please provide a comprehensive response addressing the topic based on the context provided.";

            // Process through the consolidated mind
            var response = await mind.ProcessAsync(prompt);

            // Create appropriate reasoning state based on the response
            ReasoningState state = response.ThinkingContent != null
                ? new Thinking(response.ThinkingContent)
                : new Draft(response.Response);

            return branch.WithReasoning(state, prompt, null);
        };
    }

    /// <summary>
    /// Creates a Result-safe reasoning arrow with error handling.
    /// </summary>
    /// <param name="mind">The consolidated mind instance.</param>
    /// <param name="embed">Embedding model for context retrieval.</param>
    /// <param name="topic">The topic for reasoning.</param>
    /// <param name="query">The query for context retrieval.</param>
    /// <param name="k">Number of similar documents to retrieve.</param>
    /// <returns>A Result-based pipeline step.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeIntelligentReasoningArrow(
        ConsolidatedMind mind,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return async branch =>
        {
            try
            {
                var result = await IntelligentReasoningArrow(mind, embed, topic, query, k)(branch);
                return Result<PipelineBranch, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Intelligent reasoning failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Creates a draft arrow that uses the ConsolidatedMind with automatic specialist selection.
    /// </summary>
    /// <param name="mind">The consolidated mind instance.</param>
    /// <param name="embed">Embedding model.</param>
    /// <param name="topic">Draft topic.</param>
    /// <param name="query">Context query.</param>
    /// <param name="k">Number of documents.</param>
    /// <returns>A draft step.</returns>
    public static Step<PipelineBranch, PipelineBranch> SmartDraftArrow(
        ConsolidatedMind mind,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return async branch =>
        {
            var docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = $@"Based on the following context, draft a comprehensive response about: {topic}

Context:
{context}

Draft:";

            var response = await mind.ProcessAsync(prompt);
            return branch.WithReasoning(new Draft(response.Response), prompt, null);
        };
    }

    /// <summary>
    /// Creates a critique arrow that uses the ConsolidatedMind's analyst capability.
    /// </summary>
    /// <param name="mind">The consolidated mind instance.</param>
    /// <param name="embed">Embedding model.</param>
    /// <param name="topic">Topic being critiqued.</param>
    /// <param name="query">Context query.</param>
    /// <param name="k">Number of documents.</param>
    /// <returns>A critique step.</returns>
    public static Step<PipelineBranch, PipelineBranch> SmartCritiqueArrow(
        ConsolidatedMind mind,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return async branch =>
        {
            // Get the most recent draft
            var recentDraft = branch.Events
                .OfType<ReasoningStep>()
                .Select(e => e.State)
                .OfType<Draft>()
                .LastOrDefault();

            if (recentDraft == null)
            {
                return branch; // No draft to critique
            }

            var docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = $@"Critically analyze the following draft about {topic}.

Context for reference:
{context}

Draft to critique:
{recentDraft.Text}

Provide a thorough critique covering:
1. Accuracy and factual correctness
2. Completeness
3. Clarity and organization
4. Specific suggestions for improvement

Critique:";

            var response = await mind.ProcessAsync(prompt);
            return branch.WithReasoning(new Critique(response.Response), prompt, null);
        };
    }

    /// <summary>
    /// Creates a complex task arrow that decomposes and processes multi-step tasks.
    /// </summary>
    /// <param name="mind">The consolidated mind instance.</param>
    /// <param name="embed">Embedding model.</param>
    /// <param name="task">The complex task description.</param>
    /// <param name="k">Number of documents.</param>
    /// <returns>A step for complex task processing.</returns>
    public static Step<PipelineBranch, PipelineBranch> ComplexTaskArrow(
        ConsolidatedMind mind,
        IEmbeddingModel embed,
        string task,
        int k = 8)
    {
        return async branch =>
        {
            var docs = await branch.Store.GetSimilarDocuments(embed, task, amount: k);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = $@"Context:
{context}

Complex Task: {task}";

            var response = await mind.ProcessComplexAsync(prompt);

            // Record thinking if available
            if (response.ThinkingContent != null)
            {
                branch = branch.WithReasoning(new Thinking(response.ThinkingContent), "thinking", null);
            }

            return branch.WithReasoning(new FinalSpec(response.Response), prompt, null);
        };
    }

    /// <summary>
    /// Creates a verification arrow using the ConsolidatedMind's verifier.
    /// </summary>
    /// <param name="mind">The consolidated mind instance.</param>
    /// <returns>A verification step.</returns>
    public static Step<PipelineBranch, PipelineBranch> VerificationArrow(ConsolidatedMind mind)
    {
        return async branch =>
        {
            // Get the most recent reasoning output
            var recentState = branch.Events
                .OfType<ReasoningStep>()
                .LastOrDefault()?.State;

            if (recentState == null)
            {
                return branch;
            }

            string verifyPrompt = $@"Verify the following content for accuracy, completeness, and consistency:

Content to verify:
{recentState.Text}

Verification result (state if VALID or INVALID with reasoning):";

            var response = await mind.ProcessAsync(verifyPrompt);

            // Add verification as a critique-type event
            return branch.WithReasoning(new Critique($"[VERIFICATION]\n{response.Response}"), verifyPrompt, null);
        };
    }

    /// <summary>
    /// Creates a complete reasoning pipeline using ConsolidatedMind.
    /// Combines: Context Retrieval -> Thinking -> Draft -> Critique -> Final
    /// </summary>
    /// <param name="mind">The consolidated mind.</param>
    /// <param name="embed">Embedding model.</param>
    /// <param name="topic">Topic to reason about.</param>
    /// <param name="query">Context query.</param>
    /// <param name="k">Document count.</param>
    /// <returns>A composed pipeline step.</returns>
    public static Step<PipelineBranch, PipelineBranch> FullReasoningPipeline(
        ConsolidatedMind mind,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        // Compose the full pipeline using functional composition
        return async branch =>
        {
            // Step 1: Initial reasoning with thinking
            branch = await IntelligentReasoningArrow(mind, embed, topic, query, k)(branch);

            // Step 2: Generate a draft
            branch = await SmartDraftArrow(mind, embed, topic, query, k)(branch);

            // Step 3: Critique the draft
            branch = await SmartCritiqueArrow(mind, embed, topic, query, k)(branch);

            // Step 4: Generate final refined response
            var critique = branch.Events
                .OfType<ReasoningStep>()
                .Select(e => e.State)
                .OfType<Critique>()
                .LastOrDefault();

            var draft = branch.Events
                .OfType<ReasoningStep>()
                .Select(e => e.State)
                .OfType<Draft>()
                .LastOrDefault();

            if (critique != null && draft != null)
            {
                string finalPrompt = $@"Based on the draft and critique, produce a final, polished response.

Original Draft:
{draft.Text}

Critique:
{critique.Text}

Final Response:";

                var finalResponse = await mind.ProcessAsync(finalPrompt);
                branch = branch.WithReasoning(new FinalSpec(finalResponse.Response), finalPrompt, null);
            }

            // Step 5: Optional verification
            branch = await VerificationArrow(mind)(branch);

            return branch;
        };
    }

    /// <summary>
    /// Creates a pipeline builder for fluent ConsolidatedMind pipeline construction.
    /// </summary>
    /// <param name="mind">The consolidated mind.</param>
    /// <returns>A pipeline builder.</returns>
    public static MindPipelineBuilder CreatePipeline(ConsolidatedMind mind)
    {
        return new MindPipelineBuilder(mind);
    }
}

/// <summary>
/// Fluent builder for constructing ConsolidatedMind pipelines.
/// </summary>
public sealed class MindPipelineBuilder
{
    private readonly ConsolidatedMind _mind;
    private readonly List<Step<PipelineBranch, PipelineBranch>> _steps = new();

    internal MindPipelineBuilder(ConsolidatedMind mind)
    {
        _mind = mind;
    }

    /// <summary>
    /// Adds intelligent reasoning step.
    /// </summary>
    public MindPipelineBuilder WithReasoning(IEmbeddingModel embed, string topic, string query, int k = 8)
    {
        _steps.Add(ConsolidatedMindArrows.IntelligentReasoningArrow(_mind, embed, topic, query, k));
        return this;
    }

    /// <summary>
    /// Adds draft step.
    /// </summary>
    public MindPipelineBuilder WithDraft(IEmbeddingModel embed, string topic, string query, int k = 8)
    {
        _steps.Add(ConsolidatedMindArrows.SmartDraftArrow(_mind, embed, topic, query, k));
        return this;
    }

    /// <summary>
    /// Adds critique step.
    /// </summary>
    public MindPipelineBuilder WithCritique(IEmbeddingModel embed, string topic, string query, int k = 8)
    {
        _steps.Add(ConsolidatedMindArrows.SmartCritiqueArrow(_mind, embed, topic, query, k));
        return this;
    }

    /// <summary>
    /// Adds verification step.
    /// </summary>
    public MindPipelineBuilder WithVerification()
    {
        _steps.Add(ConsolidatedMindArrows.VerificationArrow(_mind));
        return this;
    }

    /// <summary>
    /// Adds a custom step.
    /// </summary>
    public MindPipelineBuilder WithStep(Step<PipelineBranch, PipelineBranch> step)
    {
        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Builds the composed pipeline.
    /// </summary>
    /// <returns>A single composed step.</returns>
    public Step<PipelineBranch, PipelineBranch> Build()
    {
        if (_steps.Count == 0)
        {
            return branch => Task.FromResult(branch);
        }

        return async branch =>
        {
            var current = branch;
            foreach (var step in _steps)
            {
                current = await step(current);
            }
            return current;
        };
    }
}
