// <copyright file="TaskAnalyzer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.RegularExpressions;

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Result of task analysis for routing decisions.
/// </summary>
/// <param name="PrimaryRole">The primary role best suited for this task.</param>
/// <param name="SecondaryRoles">Secondary roles that might assist.</param>
/// <param name="RequiredCapabilities">Capabilities required to complete the task.</param>
/// <param name="EstimatedComplexity">Complexity score from 0 (trivial) to 1 (highly complex).</param>
/// <param name="RequiresThinking">Whether extended thinking mode is recommended.</param>
/// <param name="RequiresVerification">Whether verification step is recommended.</param>
/// <param name="Confidence">Confidence in this analysis (0 to 1).</param>
public sealed record TaskAnalysis(
    SpecializedRole PrimaryRole,
    SpecializedRole[] SecondaryRoles,
    string[] RequiredCapabilities,
    double EstimatedComplexity,
    bool RequiresThinking,
    bool RequiresVerification,
    double Confidence);

/// <summary>
/// Analyzes tasks to determine optimal routing within the ConsolidatedMind.
/// Uses pattern matching and heuristics for fast, local analysis.
/// </summary>
public static class TaskAnalyzer
{
    // Pattern definitions for task classification
    private static readonly Dictionary<SpecializedRole, (Regex[] Patterns, string[] Keywords)> RolePatterns = new()
    {
        [SpecializedRole.CodeExpert] = (
            new[]
            {
                new Regex(@"```[\w]*\s*[\s\S]*?```", RegexOptions.Compiled),
                new Regex(@"\b(def|class|function|const|let|var|import|export|public|private|async|await)\s+\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(implement|code|program|debug|fix|refactor|optimize)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(neural|embedding|vector|tensor|model)\s*(architecture|layer|network)", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "code", "implement", "function", "class", "method", "debug", "fix", "refactor", "programming", "syntax", "compile", "api", "library", "bug", "error", "exception", "neural", "embedding", "vector", "tensor", "architecture", "pipeline", "codebase" }
        ),

        [SpecializedRole.DeepReasoning] = (
            new[]
            {
                new Regex(@"\b(why|how come|explain why|reason)\b.*\?", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(analyze|evaluate|assess|consider)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(frequency|spectrum|harmonic|waveform|acoustic)\s*(analysis|pattern)", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "reason", "why", "because", "therefore", "analyze", "evaluate", "explain", "logic", "think", "understand", "deduce", "infer", "consequence", "implication", "cognitive", "consciousness", "metacognition", "self-aware", "introspect" }
        ),

        [SpecializedRole.Mathematical] = (
            new[]
            {
                new Regex(@"[\d]+\s*[\+\-\*\/\^]\s*[\d]+", RegexOptions.Compiled),
                new Regex(@"\b(calculate|compute|solve|equation|formula)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\$.*\$", RegexOptions.Compiled), // LaTeX math
                new Regex(@"\b(\d+)\s*(hz|khz|mhz|ghz|db|decibel)", RegexOptions.Compiled | RegexOptions.IgnoreCase), // Frequency/audio measurements
                new Regex(@"\b(dimension|vector|matrix|768|384|1536)\s*(dimension|embed)?", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "calculate", "compute", "math", "equation", "formula", "solve", "proof", "theorem", "algebra", "calculus", "statistics", "probability", "derivative", "integral", "frequency", "hertz", "hz", "decibel", "db", "spectrum", "fourier", "transform", "dimension" }
        ),

        [SpecializedRole.Creative] = (
            new[]
            {
                new Regex(@"\b(write|create|generate|compose)\s+(a|an|the)?\s*(story|poem|song|essay|article)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(imagine|creative|brainstorm)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(soundscape|ambient|atmosphere|mood)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "create", "write", "story", "poem", "creative", "imagine", "brainstorm", "ideate", "generate", "compose", "narrative", "fiction", "artistic", "soundscape", "ambient", "atmosphere", "mood", "aesthetic" }
        ),

        [SpecializedRole.Planner] = (
            new[]
            {
                new Regex(@"\b(plan|steps|how to|strategy)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(break down|decompose|outline)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(improve|enhance|upgrade|level up|optimize)\s*(my|the|your)?", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "plan", "steps", "strategy", "approach", "decompose", "break down", "outline", "roadmap", "schedule", "procedure", "workflow", "architecture", "design", "improve", "enhance", "upgrade", "level up", "optimize", "better" }
        ),

        [SpecializedRole.Synthesizer] = (
            new[]
            {
                new Regex(@"\b(summarize|summary|tldr|brief)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(key points|main ideas|overview)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(remember|recall|memory|memories)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "summarize", "summary", "brief", "tldr", "overview", "condense", "extract", "key points", "main ideas", "essence", "remember", "recall", "memory", "memories", "context", "history" }
        ),

        [SpecializedRole.Analyst] = (
            new[]
            {
                new Regex(@"\b(analyze|analyse|critique|review|evaluate)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(pros and cons|strengths and weaknesses)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(acoustic|audio|sound|signal)\s*(analysis|processing|pattern)", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "analyze", "critique", "review", "evaluate", "assess", "examine", "pros", "cons", "strengths", "weaknesses", "comparison", "acoustic", "audio", "sound", "signal", "urban", "noise", "pattern" }
        ),

        [SpecializedRole.Verifier] = (
            new[]
            {
                new Regex(@"\b(verify|check|validate|confirm)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(is it true|fact check|correct)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "verify", "check", "validate", "confirm", "fact-check", "correct", "accurate", "true", "false" }
        ),

        [SpecializedRole.MetaCognitive] = (
            new[]
            {
                new Regex(@"\b(self|yourself|your own)\s*(improve|enhance|modify|change)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(capabilities|abilities|features)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new Regex(@"\b(memory|collection|vector\s*store)\s*(system|database)?", RegexOptions.Compiled | RegexOptions.IgnoreCase)
            },
            new[] { "self-improve", "capabilities", "abilities", "features", "memory", "collection", "vector store", "neural", "model", "personality", "behavior", "mode", "configuration", "settings" }
        )
    };

    // Complexity indicators
    private static readonly string[] HighComplexityIndicators = new[]
    {
        "complex", "difficult", "challenging", "comprehensive", "detailed", "thorough",
        "multi-step", "advanced", "sophisticated", "intricate", "elaborate"
    };

    private static readonly string[] LowComplexityIndicators = new[]
    {
        "simple", "quick", "brief", "short", "basic", "easy", "straightforward"
    };

    /// <summary>
    /// Analyzes a prompt to determine optimal routing.
    /// </summary>
    /// <param name="prompt">The user prompt to analyze.</param>
    /// <returns>Analysis result with routing recommendations.</returns>
    public static TaskAnalysis Analyze(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new TaskAnalysis(
                SpecializedRole.QuickResponse,
                Array.Empty<SpecializedRole>(),
                Array.Empty<string>(),
                EstimatedComplexity: 0.0,
                RequiresThinking: false,
                RequiresVerification: false,
                Confidence: 1.0);
        }

        string lowerPrompt = prompt.ToLowerInvariant();
        int promptLength = prompt.Length;

        // Score each role
        var roleScores = new Dictionary<SpecializedRole, double>();
        var matchedCapabilities = new HashSet<string>();

        foreach (var (role, (patterns, keywords)) in RolePatterns)
        {
            double score = 0.0;

            // Check patterns (higher weight)
            foreach (var pattern in patterns)
            {
                if (pattern.IsMatch(prompt))
                {
                    score += 2.0;
                }
            }

            // Check keywords
            foreach (var keyword in keywords)
            {
                if (lowerPrompt.Contains(keyword))
                {
                    score += 1.0;
                    matchedCapabilities.Add(keyword);
                }
            }

            if (score > 0)
            {
                roleScores[role] = score;
            }
        }

        // Determine primary role
        SpecializedRole primaryRole;
        double confidence;

        if (roleScores.Count == 0)
        {
            // No specific patterns matched - use QuickResponse for short, DeepReasoning for long
            primaryRole = promptLength < 200 ? SpecializedRole.QuickResponse : SpecializedRole.DeepReasoning;
            confidence = 0.5;
        }
        else
        {
            var sorted = roleScores.OrderByDescending(kvp => kvp.Value).ToList();
            primaryRole = sorted[0].Key;
            double maxScore = sorted[0].Value;
            double totalScore = sorted.Sum(kvp => kvp.Value);
            confidence = Math.Min(1.0, maxScore / Math.Max(totalScore * 0.5, 1.0));
        }

        // Determine secondary roles
        var secondaryRoles = roleScores
            .Where(kvp => kvp.Key != primaryRole && kvp.Value > 1.0)
            .OrderByDescending(kvp => kvp.Value)
            .Take(2)
            .Select(kvp => kvp.Key)
            .ToArray();

        // Estimate complexity
        double complexity = EstimateComplexity(prompt, lowerPrompt, promptLength);

        // Determine if thinking mode is needed
        bool requiresThinking = complexity > 0.6 ||
            primaryRole == SpecializedRole.DeepReasoning ||
            primaryRole == SpecializedRole.Mathematical ||
            primaryRole == SpecializedRole.Planner;

        // Determine if verification is needed
        bool requiresVerification = complexity > 0.7 ||
            primaryRole == SpecializedRole.CodeExpert ||
            primaryRole == SpecializedRole.Mathematical ||
            lowerPrompt.Contains("important") ||
            lowerPrompt.Contains("critical") ||
            lowerPrompt.Contains("production");

        return new TaskAnalysis(
            primaryRole,
            secondaryRoles,
            matchedCapabilities.ToArray(),
            complexity,
            requiresThinking,
            requiresVerification,
            confidence);
    }

    /// <summary>
    /// Estimates the complexity of a task.
    /// </summary>
    private static double EstimateComplexity(string prompt, string lowerPrompt, int length)
    {
        double complexity = 0.0;

        // Length-based complexity
        complexity += Math.Min(0.3, length / 2000.0);

        // High complexity indicators
        foreach (var indicator in HighComplexityIndicators)
        {
            if (lowerPrompt.Contains(indicator))
            {
                complexity += 0.15;
            }
        }

        // Low complexity indicators (reduce)
        foreach (var indicator in LowComplexityIndicators)
        {
            if (lowerPrompt.Contains(indicator))
            {
                complexity -= 0.1;
            }
        }

        // Question marks indicate queries (usually simpler)
        int questionMarks = prompt.Count(c => c == '?');
        if (questionMarks == 1 && length < 100)
        {
            complexity -= 0.1;
        }

        // Multiple steps indicated
        if (lowerPrompt.Contains("and then") || lowerPrompt.Contains("after that") ||
            Regex.IsMatch(lowerPrompt, @"\b(first|second|third|then|next|finally)\b"))
        {
            complexity += 0.2;
        }

        // Code blocks increase complexity
        if (prompt.Contains("```"))
        {
            complexity += 0.2;
        }

        return Math.Clamp(complexity, 0.0, 1.0);
    }

    /// <summary>
    /// Determines if a task should be split into sub-tasks.
    /// </summary>
    /// <param name="analysis">The task analysis result.</param>
    /// <returns>True if the task should be decomposed.</returns>
    public static bool ShouldDecompose(TaskAnalysis analysis)
    {
        return analysis.EstimatedComplexity > 0.7 ||
               analysis.SecondaryRoles.Length >= 2 ||
               analysis.RequiredCapabilities.Length > 5;
    }
}
