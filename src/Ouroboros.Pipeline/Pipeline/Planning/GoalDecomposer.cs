// <copyright file="GoalDecomposer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Planning;

using System.Text.Json;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Domain.Events;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Reasoning;
using Ouroboros.Providers;

/// <summary>
/// Decomposes high-level goals into executable sub-goals using LLM reasoning.
/// Follows monadic composition patterns for error handling.
/// </summary>
public static class GoalDecomposer
{
    private const string DecompositionPrompt = """
        Analyze the following goal and decompose it into 2-4 concrete, actionable sub-goals.
        Each sub-goal should be specific, measurable, and achievable.

        Goal: {goal}

        Context from previous reasoning:
        {context}

        Respond with a JSON array of sub-goal descriptions only:
        ["sub-goal 1", "sub-goal 2", ...]
        """;

    /// <summary>
    /// Creates a step that decomposes a goal into sub-goals using LLM reasoning.
    /// </summary>
    /// <param name="llm">The tool-aware language model for decomposition.</param>
    /// <param name="parentGoal">The goal to decompose.</param>
    /// <param name="maxDepth">Maximum decomposition depth (default: 3).</param>
    /// <returns>A step that produces a decomposed goal wrapped in Result.</returns>
    public static Step<PipelineBranch, Result<Goal>> DecomposeArrow(
        ToolAwareChatModel llm,
        Goal parentGoal,
        int maxDepth = 3)
    {
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(parentGoal);

        return async branch =>
        {
            if (maxDepth <= 0)
            {
                return Result<Goal>.Success(parentGoal);
            }

            if (string.IsNullOrWhiteSpace(parentGoal.Description))
            {
                return Result<Goal>.Failure("Goal description cannot be empty");
            }

            string context = ExtractContext(branch);
            PromptTemplate template = new PromptTemplate(DecompositionPrompt);
            string prompt = template.Format(new Dictionary<string, string>
            {
                ["goal"] = parentGoal.Description,
                ["context"] = context,
            });

            try
            {
                (string response, List<ToolExecution> _) = await llm.GenerateWithToolsAsync(prompt);
                return ParseSubGoals(response)
                    .Map(descriptions => parentGoal.WithSubGoals(
                        descriptions.Select(Goal.Atomic).ToArray()));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result<Goal>.Failure($"Goal decomposition failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Creates a recursive decomposition step that decomposes sub-goals to specified depth.
    /// </summary>
    /// <param name="llm">The tool-aware language model.</param>
    /// <param name="parentGoal">Root goal to decompose.</param>
    /// <param name="maxDepth">Maximum recursion depth.</param>
    /// <returns>A step producing fully decomposed goal hierarchy.</returns>
    public static Step<PipelineBranch, Result<Goal>> DecomposeRecursiveArrow(
        ToolAwareChatModel llm,
        Goal parentGoal,
        int maxDepth = 3)
    {
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(parentGoal);

        return async branch =>
        {
            Result<Goal> initialResult = await DecomposeArrow(llm, parentGoal, maxDepth)(branch);

            if (initialResult.IsFailure)
            {
                return initialResult;
            }

            Goal goal = initialResult.Value;

            if (maxDepth <= 1 || goal.SubGoals.Count == 0)
            {
                return Result<Goal>.Success(goal);
            }

            List<Goal> decomposedSubGoals = new List<Goal>();
            foreach (Goal subGoal in goal.SubGoals)
            {
                Result<Goal> subResult = await DecomposeRecursiveArrow(llm, subGoal, maxDepth - 1)(branch);
                if (subResult.IsFailure)
                {
                    return subResult;
                }

                decomposedSubGoals.Add(subResult.Value);
            }

            return Result<Goal>.Success(goal.WithSubGoals(decomposedSubGoals.ToArray()));
        };
    }

    private static string ExtractContext(PipelineBranch branch)
    {
        IEnumerable<string> recentSteps = branch.Events
            .OfType<ReasoningStep>()
            .TakeLast(3)
            .Select(e =>
            {
                string promptPreview = e.Prompt?.Length > 100
                    ? e.Prompt[..100] + "..."
                    : e.Prompt ?? string.Empty;
                return $"- {e.State.Kind}: {promptPreview}";
            });

        return string.Join("\n", recentSteps);
    }

    private static Result<IEnumerable<string>> ParseSubGoals(string response)
    {
        try
        {
            string trimmed = response.Trim();

            // Handle markdown code blocks
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                string[] lines = trimmed.Split('\n');
                trimmed = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.StartsWith("```", StringComparison.Ordinal)));
            }

            // Find JSON array in the response
            int arrayStart = trimmed.IndexOf('[');
            int arrayEnd = trimmed.LastIndexOf(']');

            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                trimmed = trimmed[arrayStart..(arrayEnd + 1)];
            }

            string[]? descriptions = JsonSerializer.Deserialize<string[]>(trimmed);

            return descriptions is { Length: > 0 }
                ? Result<IEnumerable<string>>.Success(descriptions.Where(d => !string.IsNullOrWhiteSpace(d)))
                : Result<IEnumerable<string>>.Failure("No sub-goals parsed from response");
        }
        catch (JsonException ex)
        {
            return Result<IEnumerable<string>>.Failure($"Failed to parse sub-goals JSON: {ex.Message}");
        }
    }
}
