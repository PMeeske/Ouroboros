// <copyright file="TransferLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using System.Collections.Concurrent;

/// <summary>
/// Implementation of transfer learning for cross-domain skill adaptation.
/// </summary>
public sealed class TransferLearner : ITransferLearner
{
    private readonly IChatCompletionModel llm;
    private readonly ISkillRegistry skills;
    private readonly IMemoryStore memory;
    private readonly TransferLearningConfig config;
    private readonly ConcurrentDictionary<string, List<TransferResult>> transferHistory = new();

    public TransferLearner(
        IChatCompletionModel llm,
        ISkillRegistry skills,
        IMemoryStore memory,
        TransferLearningConfig? config = null)
    {
        this.llm = llm ?? throw new ArgumentNullException(nameof(llm));
        this.skills = skills ?? throw new ArgumentNullException(nameof(skills));
        this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
        this.config = config ?? new TransferLearningConfig();
    }

    /// <summary>
    /// Adapts a skill from one domain to another.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<TransferResult, string>> AdaptSkillToDomainAsync(
        Skill sourceSkill,
        string targetDomain,
        TransferLearningConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= this.config;

        if (sourceSkill == null)
        {
            return Result<TransferResult, string>.Failure("Source skill cannot be null");
        }

        if (string.IsNullOrWhiteSpace(targetDomain))
        {
            return Result<TransferResult, string>.Failure("Target domain cannot be empty");
        }

        try
        {
            // Estimate transferability first
            var transferability = await this.EstimateTransferabilityAsync(sourceSkill, targetDomain, ct);

            if (transferability < config.MinTransferabilityThreshold)
            {
                return Result<TransferResult, string>.Failure(
                    $"Transferability too low: {transferability:P0} < {config.MinTransferabilityThreshold:P0}");
            }

            // Find analogies to guide adaptation
            var sourceDomain = this.InferDomainFromSkill(sourceSkill);
            var analogies = await this.FindAnalogiesAsync(sourceDomain, targetDomain, ct);

            // Adapt the skill using LLM
            var adaptationPrompt = this.BuildAdaptationPrompt(sourceSkill, targetDomain, analogies);
            var adaptationResponse = await this.llm.GenerateTextAsync(adaptationPrompt, ct);

            // Parse the adapted skill
            var adaptedSteps = this.ParseAdaptedSteps(adaptationResponse, sourceSkill.Steps);
            var adaptations = this.ExtractAdaptations(adaptationResponse);

            // Create adapted skill
            var adaptedSkillName = $"{sourceSkill.Name}_adapted_{targetDomain.ToLowerInvariant().Replace(" ", "_")}";
            var adaptedSkill = new Skill(
                adaptedSkillName,
                $"Adapted from {sourceSkill.Name} for {targetDomain}: {sourceSkill.Description}",
                sourceSkill.Prerequisites,
                adaptedSteps,
                SuccessRate: sourceSkill.SuccessRate * transferability, // Adjust success rate by transferability
                UsageCount: 0,
                DateTime.UtcNow,
                DateTime.UtcNow);

            // Register the adapted skill
            this.skills.RegisterSkill(adaptedSkill);

            var result = new TransferResult(
                adaptedSkill,
                transferability,
                sourceDomain,
                targetDomain,
                adaptations,
                DateTime.UtcNow);

            // Track transfer history
            if (config.TrackTransferHistory)
            {
                this.transferHistory.AddOrUpdate(
                    sourceSkill.Name,
                    _ => new List<TransferResult> { result },
                    (_, list) => { list.Add(result);
                        return list; });
            }

            return Result<TransferResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<TransferResult, string>.Failure($"Transfer learning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Estimates how well a skill can transfer to a new domain.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<double> EstimateTransferabilityAsync(
        Skill skill,
        string targetDomain,
        CancellationToken ct = default)
    {
        if (skill == null || string.IsNullOrWhiteSpace(targetDomain))
        {
            return 0.0;
        }

        try
        {
            var sourceDomain = this.InferDomainFromSkill(skill);

            var prompt = $@"Estimate how well a skill can transfer from one domain to another.

Source Domain: {sourceDomain}
Skill: {skill.Name}
Description: {skill.Description}
Steps: {skill.Steps.Count}

Target Domain: {targetDomain}

Consider:
1. Structural similarity between domains
2. Abstraction level of the skill
3. Domain-specific dependencies
4. Conceptual overlap

Provide a transferability score from 0.0 (cannot transfer) to 1.0 (perfect transfer).
Respond with just the number.";

            var response = await this.llm.GenerateTextAsync(prompt, ct);

            // Extract numeric score
            var scoreMatch = System.Text.RegularExpressions.Regex.Match(response, @"0?\.\d+|1\.0");
            if (scoreMatch.Success && double.TryParse(scoreMatch.Value, out var score))
            {
                return Math.Clamp(score, 0.0, 1.0);
            }

            // Fallback: use historical data if available
            if (this.transferHistory.TryGetValue(skill.Name, out var history))
            {
                var similarTransfers = history
                    .Where(t => t.TargetDomain.Contains(targetDomain, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (similarTransfers.Any())
                {
                    return similarTransfers.Average(t => t.TransferabilityScore);
                }
            }

            // Default conservative estimate
            return 0.5;
        }
        catch
        {
            return 0.5; // Conservative default
        }
    }

    /// <summary>
    /// Finds analogies between domains to guide transfer.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<List<(string source, string target, double confidence)>> FindAnalogiesAsync(
        string sourceDomain,
        string targetDomain,
        CancellationToken ct = default)
    {
        var analogies = new List<(string source, string target, double confidence)>();

        if (string.IsNullOrWhiteSpace(sourceDomain) || string.IsNullOrWhiteSpace(targetDomain))
        {
            return analogies;
        }

        try
        {
            var prompt = $@"Identify analogical mappings between two domains.

Source Domain: {sourceDomain}
Target Domain: {targetDomain}

Find conceptual mappings, such as:
- Objects/entities that serve similar roles
- Processes that have similar structures
- Relationships that map across domains

Format each mapping as:
SOURCE_CONCEPT -> TARGET_CONCEPT (confidence: 0.0-1.0)

Example:
database_query -> library_search (confidence: 0.8)
";

            var response = await this.llm.GenerateTextAsync(prompt, ct);
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(.+?)\s*->\s*(.+?)\s*\(confidence:\s*(0?\.\d+|1\.0)\)");

                if (match.Success)
                {
                    var source = match.Groups[1].Value.Trim();
                    var target = match.Groups[2].Value.Trim();
                    var confidence = double.Parse(match.Groups[3].Value);

                    analogies.Add((source, target, confidence));
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return analogies;
    }

    /// <summary>
    /// Gets the transfer history for a skill.
    /// </summary>
    /// <returns></returns>
    public List<TransferResult> GetTransferHistory(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return new List<TransferResult>();
        }

        return this.transferHistory.TryGetValue(skillName, out var history)
            ? new List<TransferResult>(history)
            : new List<TransferResult>();
    }

    /// <summary>
    /// Validates if a transferred skill works in the target domain.
    /// </summary>
    public void RecordTransferValidation(TransferResult transferResult, bool success)
    {
        if (transferResult == null)
        {
            return;
        }

        // Update the adapted skill's success rate
        var skillName = transferResult.AdaptedSkill.Name;
        this.skills.RecordSkillExecution(skillName, success);

        // Could also update transferability estimates based on validation
    }

    // Private helper methods
    private string InferDomainFromSkill(Skill skill)
    {
        // Extract domain hints from skill name and description
        var words = skill.Name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var domainHints = words.Take(2);

        return string.Join(" ", domainHints);
    }

    private string BuildAdaptationPrompt(
        Skill sourceSkill,
        string targetDomain,
        List<(string source, string target, double confidence)> analogies)
    {
        var analogyText = analogies.Any()
            ? string.Join("\n", analogies.Select(a => $"- {a.source} → {a.target} (confidence: {a.confidence:F2})"))
            : "No specific analogies identified.";

        return $@"Adapt a skill from its source domain to a target domain.

Source Skill: {sourceSkill.Name}
Description: {sourceSkill.Description}

Original Steps:
{string.Join("\n", sourceSkill.Steps.Select((s, i) => $"{i + 1}. {s.Action}"))}

Target Domain: {targetDomain}

Analogical Mappings:
{analogyText}

Adapt each step to work in the target domain while preserving the core logic.
For each step, specify:
1. Adapted action (how it changes for target domain)
2. Parameters (updated for target domain)
3. Expected outcome

Format as:
STEP 1: [adapted action]
PARAMETERS: [adapted parameters]
EXPECTED: [expected outcome]
";
    }

    private List<PlanStep> ParseAdaptedSteps(string response, List<PlanStep> originalSteps)
    {
        var adaptedSteps = new List<PlanStep>();
        var lines = response.Split('\n');

        string? currentAction = null;
        Dictionary<string, object>? currentParams = null;
        string? currentExpected = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    adaptedSteps.Add(new PlanStep(
                        currentAction,
                        currentParams ?? new Dictionary<string, object>(),
                        currentExpected ?? string.Empty,
                        0.7)); // Default confidence for adapted steps
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;
                currentParams = new Dictionary<string, object>();
                currentExpected = string.Empty;
            }
            else if (trimmed.StartsWith("PARAMETERS:"))
            {
                var paramsStr = trimmed.Substring("PARAMETERS:".Length).Trim();
                currentParams = new Dictionary<string, object> { ["description"] = paramsStr };
            }
            else if (trimmed.StartsWith("EXPECTED:"))
            {
                currentExpected = trimmed.Substring("EXPECTED:".Length).Trim();
            }
        }

        if (currentAction != null)
        {
            adaptedSteps.Add(new PlanStep(
                currentAction,
                currentParams ?? new Dictionary<string, object>(),
                currentExpected ?? string.Empty,
                0.7));
        }

        // Fallback to original if parsing failed
        return adaptedSteps.Any() ? adaptedSteps : originalSteps;
    }

    private List<string> ExtractAdaptations(string response)
    {
        var adaptations = new List<string>();

        // Look for lines that describe adaptations
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("adapted", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("modified", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("changed", StringComparison.OrdinalIgnoreCase))
            {
                adaptations.Add(line.Trim());
            }
        }

        return adaptations.Take(5).ToList();
    }
}
