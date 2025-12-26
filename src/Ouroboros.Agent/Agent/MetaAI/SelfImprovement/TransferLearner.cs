#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Transfer Learning Implementation
// Domain adaptation and analogical reasoning for skill transfer
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of transfer learning for cross-domain skill adaptation.
/// </summary>
public sealed class TransferLearner : ITransferLearner
{
    private readonly IChatCompletionModel _llm;
    private readonly ISkillRegistry _skills;
    private readonly IMemoryStore _memory;
    private readonly TransferLearningConfig _config;
    private readonly ConcurrentDictionary<string, List<TransferResult>> _transferHistory = new();

    public TransferLearner(
        IChatCompletionModel llm,
        ISkillRegistry skills,
        IMemoryStore memory,
        TransferLearningConfig? config = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _config = config ?? new TransferLearningConfig();
    }

    /// <summary>
    /// Adapts a skill from one domain to another.
    /// </summary>
    public async Task<Result<TransferResult, string>> AdaptSkillToDomainAsync(
        Skill sourceSkill,
        string targetDomain,
        TransferLearningConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= _config;

        if (sourceSkill == null)
            return Result<TransferResult, string>.Failure("Source skill cannot be null");

        if (string.IsNullOrWhiteSpace(targetDomain))
            return Result<TransferResult, string>.Failure("Target domain cannot be empty");

        try
        {
            // Estimate transferability first
            double transferability = await EstimateTransferabilityAsync(sourceSkill, targetDomain, ct);

            if (transferability < config.MinTransferabilityThreshold)
            {
                return Result<TransferResult, string>.Failure(
                    $"Transferability too low: {transferability:P0} < {config.MinTransferabilityThreshold:P0}");
            }

            // Find analogies to guide adaptation
            string sourceDomain = InferDomainFromSkill(sourceSkill);
            List<(string source, string target, double confidence)> analogies = await FindAnalogiesAsync(sourceDomain, targetDomain, ct);

            // Adapt the skill using LLM
            string adaptationPrompt = BuildAdaptationPrompt(sourceSkill, targetDomain, analogies);
            string adaptationResponse = await _llm.GenerateTextAsync(adaptationPrompt, ct);

            // Parse the adapted skill
            List<PlanStep> adaptedSteps = ParseAdaptedSteps(adaptationResponse, sourceSkill.Steps);
            List<string> adaptations = ExtractAdaptations(adaptationResponse);

            // Create adapted skill
            string adaptedSkillName = $"{sourceSkill.Name}_adapted_{targetDomain.ToLowerInvariant().Replace(" ", "_")}";
            Skill adaptedSkill = new Skill(
                adaptedSkillName,
                $"Adapted from {sourceSkill.Name} for {targetDomain}: {sourceSkill.Description}",
                sourceSkill.Prerequisites,
                adaptedSteps,
                SuccessRate: sourceSkill.SuccessRate * transferability, // Adjust success rate by transferability
                UsageCount: 0,
                DateTime.UtcNow,
                DateTime.UtcNow);

            // Register the adapted skill
            _skills.RegisterSkill(adaptedSkill);

            TransferResult result = new TransferResult(
                adaptedSkill,
                transferability,
                sourceDomain,
                targetDomain,
                adaptations,
                DateTime.UtcNow);

            // Track transfer history
            if (config.TrackTransferHistory)
            {
                _transferHistory.AddOrUpdate(
                    sourceSkill.Name,
                    _ => new List<TransferResult> { result },
                    (_, list) => { list.Add(result); return list; });
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
    public async Task<double> EstimateTransferabilityAsync(
        Skill skill,
        string targetDomain,
        CancellationToken ct = default)
    {
        if (skill == null || string.IsNullOrWhiteSpace(targetDomain))
            return 0.0;

        try
        {
            string sourceDomain = InferDomainFromSkill(skill);

            string prompt = $@"Estimate how well a skill can transfer from one domain to another.

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

            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Extract numeric score
            System.Text.RegularExpressions.Match scoreMatch = System.Text.RegularExpressions.Regex.Match(response, @"0?\.\d+|1\.0");
            if (scoreMatch.Success && double.TryParse(scoreMatch.Value, out double score))
            {
                return Math.Clamp(score, 0.0, 1.0);
            }

            // Fallback: use historical data if available
            if (_transferHistory.TryGetValue(skill.Name, out List<TransferResult>? history))
            {
                List<TransferResult> similarTransfers = history
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
    public async Task<List<(string source, string target, double confidence)>> FindAnalogiesAsync(
        string sourceDomain,
        string targetDomain,
        CancellationToken ct = default)
    {
        List<(string source, string target, double confidence)> analogies = new List<(string source, string target, double confidence)>();

        if (string.IsNullOrWhiteSpace(sourceDomain) || string.IsNullOrWhiteSpace(targetDomain))
            return analogies;

        try
        {
            string prompt = $@"Identify analogical mappings between two domains.

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

            string response = await _llm.GenerateTextAsync(prompt, ct);
            string[] lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(.+?)\s*->\s*(.+?)\s*\(confidence:\s*(0?\.\d+|1\.0)\)");

                if (match.Success)
                {
                    string source = match.Groups[1].Value.Trim();
                    string target = match.Groups[2].Value.Trim();
                    double confidence = double.Parse(match.Groups[3].Value);

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
    public List<TransferResult> GetTransferHistory(string skillName)
    {
        if (string.IsNullOrWhiteSpace(skillName))
            return new List<TransferResult>();

        return _transferHistory.TryGetValue(skillName, out List<TransferResult>? history)
            ? new List<TransferResult>(history)
            : new List<TransferResult>();
    }

    /// <summary>
    /// Validates if a transferred skill works in the target domain.
    /// </summary>
    public void RecordTransferValidation(TransferResult transferResult, bool success)
    {
        if (transferResult == null)
            return;

        // Update the adapted skill's success rate
        string skillName = transferResult.AdaptedSkill.Name;
        _skills.RecordSkillExecution(skillName, success);

        // Could also update transferability estimates based on validation
    }

    // Private helper methods

    private string InferDomainFromSkill(Skill skill)
    {
        // Extract domain hints from skill name and description
        string[] words = skill.Name.Split('_', StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> domainHints = words.Take(2);

        return string.Join(" ", domainHints);
    }

    private string BuildAdaptationPrompt(
        Skill sourceSkill,
        string targetDomain,
        List<(string source, string target, double confidence)> analogies)
    {
        string analogyText = analogies.Any()
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
        List<PlanStep> adaptedSteps = new List<PlanStep>();
        string[] lines = response.Split('\n');

        string? currentAction = null;
        Dictionary<string, object>? currentParams = null;
        string? currentExpected = null;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    adaptedSteps.Add(new PlanStep(
                        currentAction,
                        currentParams ?? new Dictionary<string, object>(),
                        currentExpected ?? "",
                        0.7)); // Default confidence for adapted steps
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
                currentParams = new Dictionary<string, object>();
                currentExpected = "";
            }
            else if (trimmed.StartsWith("PARAMETERS:"))
            {
                string paramsStr = trimmed.Substring("PARAMETERS:".Length).Trim();
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
                currentExpected ?? "",
                0.7));
        }

        // Fallback to original if parsing failed
        return adaptedSteps.Any() ? adaptedSteps : originalSteps;
    }

    private List<string> ExtractAdaptations(string response)
    {
        List<string> adaptations = new List<string>();

        // Look for lines that describe adaptations
        string[] lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
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
