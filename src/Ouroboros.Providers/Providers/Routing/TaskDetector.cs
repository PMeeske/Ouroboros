// <copyright file="TaskDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Routing;

/// <summary>
/// Detects task types from prompts using heuristic analysis.
/// Used by HybridModelRouter to select appropriate models.
/// </summary>
public static class TaskDetector
{
    // Reasoning keywords
    private static readonly HashSet<string> ReasoningKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "reason", "why", "because", "therefore", "analyze", "analyse", "evaluate",
        "explain", "justify", "infer", "deduce", "logic", "think", "consider",
        "understand", "rationalize", "conclude", "inference", "consequence",
    };

    // Planning keywords
    private static readonly HashSet<string> PlanningKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "plan", "steps", "how to", "strategy", "approach", "decompose",
        "break down", "organize", "structure", "outline", "roadmap",
        "schedule", "procedure", "method", "process", "workflow",
        "framework", "design", "architect", "coordinate",
    };

    // Coding keywords
    private static readonly HashSet<string> CodingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "implement", "function", "class", "method", "variable",
        "program", "script", "algorithm", "syntax", "compile", "debug",
        "refactor", "api", "library", "framework", "module", "package",
        "import", "export", "return", "loop", "condition", "interface",
    };

    /// <summary>
    /// Detects the task type from a prompt using heuristic analysis.
    /// </summary>
    /// <param name="prompt">The prompt to analyze.</param>
    /// <param name="strategy">Detection strategy to use (default: Heuristic).</param>
    /// <returns>The detected task type.</returns>
    public static TaskType DetectTaskType(string prompt, TaskDetectionStrategy strategy = TaskDetectionStrategy.Heuristic)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return TaskType.Simple;
        }

        return strategy switch
        {
            TaskDetectionStrategy.Heuristic => DetectUsingHeuristics(prompt),
            TaskDetectionStrategy.RuleBased => DetectUsingRules(prompt),
            TaskDetectionStrategy.Hybrid => DetectUsingHybrid(prompt),
            _ => TaskType.Unknown,
        };
    }

    /// <summary>
    /// Detects task type using keyword-based heuristics.
    /// </summary>
    private static TaskType DetectUsingHeuristics(string prompt)
    {
        // Normalize prompt for analysis
        string normalized = prompt.ToLowerInvariant();

        // Check for code blocks or code-specific patterns
        if (normalized.Contains("```") ||
            normalized.Contains("def ") ||
            normalized.Contains("class ") ||
            normalized.Contains("function ") ||
            normalized.Contains("const ") ||
            normalized.Contains("var ") ||
            normalized.Contains("let ") ||
            normalized.Contains("import ") ||
            normalized.Contains("public ") ||
            normalized.Contains("private "))
        {
            return TaskType.Coding;
        }

        // Count keyword matches for each category
        int reasoningScore = CountKeywordMatches(normalized, ReasoningKeywords);
        int planningScore = CountKeywordMatches(normalized, PlanningKeywords);
        int codingScore = CountKeywordMatches(normalized, CodingKeywords);

        // Determine task type based on highest score
        int maxScore = Math.Max(reasoningScore, Math.Max(planningScore, codingScore));

        if (maxScore == 0)
        {
            // No keywords found, check length
            return prompt.Length < 500 ? TaskType.Simple : TaskType.Unknown;
        }

        if (reasoningScore == maxScore)
        {
            return TaskType.Reasoning;
        }

        if (planningScore == maxScore)
        {
            return TaskType.Planning;
        }

        if (codingScore == maxScore)
        {
            return TaskType.Coding;
        }

        return TaskType.Unknown;
    }

    /// <summary>
    /// Detects task type using rule-based analysis.
    /// </summary>
    private static TaskType DetectUsingRules(string prompt)
    {
        // Code detection (presence of code blocks or programming syntax)
        if (prompt.Contains("```") || HasProgrammingSyntax(prompt))
        {
            return TaskType.Coding;
        }

        // Planning detection (multi-step or list-like structure)
        if (HasListStructure(prompt) || ContainsPhrase(prompt, "step", "steps"))
        {
            return TaskType.Planning;
        }

        // Reasoning detection (questions, analysis requests)
        if (prompt.TrimStart().StartsWith("why", StringComparison.OrdinalIgnoreCase) ||
            ContainsPhrase(prompt, "explain", "analyze", "reason"))
        {
            return TaskType.Reasoning;
        }

        // Simple queries (short and straightforward)
        if (prompt.Length < 100 && !prompt.Contains('\n'))
        {
            return TaskType.Simple;
        }

        // Default to unknown for complex prompts
        return TaskType.Unknown;
    }

    /// <summary>
    /// Detects task type using hybrid approach (combines heuristics and rules).
    /// </summary>
    private static TaskType DetectUsingHybrid(string prompt)
    {
        TaskType heuristicResult = DetectUsingHeuristics(prompt);
        TaskType ruleResult = DetectUsingRules(prompt);

        // If both agree, return the result
        if (heuristicResult == ruleResult)
        {
            return heuristicResult;
        }

        // If one is Unknown, prefer the other
        if (heuristicResult == TaskType.Unknown)
        {
            return ruleResult;
        }

        if (ruleResult == TaskType.Unknown)
        {
            return heuristicResult;
        }

        // If both differ and neither is Unknown, prefer heuristic result
        return heuristicResult;
    }

    /// <summary>
    /// Counts how many keywords from a set appear in the prompt.
    /// </summary>
    private static int CountKeywordMatches(string normalizedPrompt, HashSet<string> keywords)
    {
        int count = 0;
        foreach (string keyword in keywords)
        {
            if (normalizedPrompt.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Checks if the prompt contains programming syntax patterns.
    /// </summary>
    private static bool HasProgrammingSyntax(string prompt)
    {
        string[] syntaxPatterns = new[]
        {
            "def ", "class ", "function ", "const ", "var ", "let ",
            "import ", "export ", "return ", "public ", "private ",
            "void ", "int ", "string ", "bool ", "float ", "double ",
            "=>", "->", "=>", "func ", "proc ", "sub ",
        };

        foreach (string pattern in syntaxPatterns)
        {
            if (prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the prompt has a list-like structure (numbered or bulleted).
    /// </summary>
    private static bool HasListStructure(string prompt)
    {
        string[] lines = prompt.Split('\n');
        int listItems = 0;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("1.") ||
                trimmed.StartsWith("2.") ||
                trimmed.StartsWith("3.") ||
                trimmed.StartsWith("-") ||
                trimmed.StartsWith("*") ||
                trimmed.StartsWith("•"))
            {
                listItems++;
            }
        }

        return listItems >= 2;
    }

    /// <summary>
    /// Checks if the prompt contains any of the specified phrases.
    /// </summary>
    private static bool ContainsPhrase(string prompt, params string[] phrases)
    {
        foreach (string phrase in phrases)
        {
            if (prompt.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
