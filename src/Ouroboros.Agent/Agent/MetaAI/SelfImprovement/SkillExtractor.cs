#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill Extractor Implementation
// Automatic extraction of reusable skills from successful executions
// ==========================================================

using Ouroboros.Core.Ethics;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of automatic skill extraction from successful executions.
/// Analyzes execution patterns and creates reusable skills with confidence scoring.
/// </summary>
public sealed class SkillExtractor : ISkillExtractor
{
    private readonly IChatCompletionModel _llm;
    private readonly ISkillRegistry _skillRegistry;
    private readonly IEthicsFramework _ethics;

    public SkillExtractor(IChatCompletionModel llm, ISkillRegistry skillRegistry, IEthicsFramework ethics)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        _ethics = ethics ?? throw new ArgumentNullException(nameof(ethics));
    }

    /// <summary>
    /// Determines if a skill should be extracted from the given verification result.
    /// </summary>
    public async Task<bool> ShouldExtractSkillAsync(
        VerificationResult verification,
        SkillExtractionConfig? config = null)
    {
        config ??= new SkillExtractionConfig();

        // Check quality threshold
        if (!verification.Verified || verification.QualityScore < config.MinQualityThreshold)
            return false;

        // Check if execution has enough steps
        int stepCount = verification.Execution.StepResults.Count;
        if (stepCount < config.MinStepsForExtraction)
            return false;

        // Check if execution was successful
        if (!verification.Execution.Success)
            return false;

        return await Task.FromResult(true);
    }

    /// <summary>
    /// Extracts a skill from a successful execution.
    /// </summary>
    public async Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        VerificationResult verification,
        SkillExtractionConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new SkillExtractionConfig();

        try
        {
            // Validate inputs
            if (execution == null)
                return Result<Skill, string>.Failure("Execution cannot be null");

            if (verification == null)
                return Result<Skill, string>.Failure("Verification cannot be null");

            // Check if skill should be extracted
            if (!await ShouldExtractSkillAsync(verification, config))
                return Result<Skill, string>.Failure(
                    $"Execution does not meet extraction criteria (Quality: {verification.QualityScore:P0}, Steps: {execution.StepResults.Count})");

            // Generate skill name and description using LLM
            string skillName = await GenerateSkillNameAsync(execution, ct);
            string description = await GenerateSkillDescriptionAsync(execution, ct);

            // Check if similar skill already exists
            Skill? existingSkill = _skillRegistry.GetSkill(skillName);
            if (existingSkill != null)
            {
                // Update existing skill with new execution data
                return await UpdateExistingSkillAsync(existingSkill, execution, verification, config);
            }

            // Extract prerequisites from successful steps
            List<string> prerequisites = ExtractPrerequisites(execution, config);

            // Extract and parameterize steps
            List<PlanStep> steps = config.EnableAutoParameterization
                ? ParameterizeSteps(execution.Plan.Steps)
                : execution.Plan.Steps;

            // Limit steps to configured maximum
            if (steps.Count > config.MaxStepsPerSkill)
            {
                steps = steps.Take(config.MaxStepsPerSkill).ToList();
            }

            // Calculate initial success rate from verification quality
            double initialSuccessRate = verification.QualityScore;

            // Create new skill
            Skill skill = new Skill(
                Name: skillName,
                Description: description,
                Prerequisites: prerequisites,
                Steps: steps,
                SuccessRate: initialSuccessRate,
                UsageCount: 1, // First usage is the extraction itself
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow);

            // Ethics evaluation - validate skill before registration
            var skillContext = new SkillUsageContext
            {
                Skill = new Core.Ethics.Skill
                {
                    Name = skill.Name,
                    Description = skill.Description,
                    Prerequisites = skill.Prerequisites,
                    Steps = skill.Steps.Select(s => new Core.Ethics.PlanStep
                    {
                        Action = s.Action,
                        Parameters = s.Parameters,
                        ExpectedOutcome = s.ExpectedOutcome,
                        ConfidenceScore = s.ConfidenceScore
                    }).ToArray(),
                    SuccessRate = skill.SuccessRate,
                    UsageCount = skill.UsageCount,
                    CreatedAt = skill.CreatedAt,
                    LastUsed = skill.LastUsed
                },
                ActionContext = new ActionContext
                {
                    AgentId = "skill-extractor",
                    UserId = null,
                    Environment = "skill_extraction",
                    State = new Dictionary<string, object>
                    {
                        ["goal"] = execution.Plan.Goal,
                        ["quality_score"] = verification.QualityScore
                    }
                },
                Goal = execution.Plan.Goal,
                HistoricalSuccessRate = initialSuccessRate
            };

            var ethicsResult = await _ethics.EvaluateSkillAsync(skillContext, ct);

            if (ethicsResult.IsFailure)
            {
                return Result<Skill, string>.Failure(
                    $"Skill rejected by ethics evaluation: {ethicsResult.Error}");
            }

            if (!ethicsResult.Value.IsPermitted)
            {
                return Result<Skill, string>.Failure(
                    $"Skill rejected by ethics framework: {ethicsResult.Value.Reasoning}");
            }

            if (ethicsResult.Value.Level == EthicalClearanceLevel.RequiresHumanApproval)
            {
                return Result<Skill, string>.Failure(
                    $"Skill requires human approval before registration: {ethicsResult.Value.Reasoning}");
            }

            // Register the skill
            _skillRegistry.RegisterSkill(skill);

            return Result<Skill, string>.Success(skill);
        }
        catch (Exception ex)
        {
            return Result<Skill, string>.Failure($"Skill extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a descriptive name for the extracted skill using LLM.
    /// </summary>
    public async Task<string> GenerateSkillNameAsync(
        ExecutionResult execution,
        CancellationToken ct = default)
    {
        try
        {
            string prompt = $@"Generate a concise, descriptive name for a reusable skill based on this execution:

Goal: {execution.Plan.Goal}
Steps:
{string.Join("\n", execution.Plan.Steps.Select((s, i) => $"{i + 1}. {s.Action}"))}

Requirements:
- Use lowercase with underscores (e.g., 'analyze_data', 'generate_report')
- Be specific but concise (2-4 words max)
- Focus on the main action/capability

Skill name:";

            string skillName = await _llm.GenerateTextAsync(prompt, ct);
            skillName = skillName?.Trim() ?? "extracted_skill";

            // Sanitize the name
            skillName = SanitizeSkillName(skillName);

            return skillName;
        }
        catch
        {
            // Fallback to automatic name generation
            return GenerateFallbackSkillName(execution);
        }
    }

    /// <summary>
    /// Generates a description for the extracted skill using LLM.
    /// </summary>
    public async Task<string> GenerateSkillDescriptionAsync(
        ExecutionResult execution,
        CancellationToken ct = default)
    {
        try
        {
            string prompt = $@"Generate a clear description of what this skill does:

Goal: {execution.Plan.Goal}
Steps:
{string.Join("\n", execution.Plan.Steps.Select((s, i) => $"{i + 1}. {s.Action}: {s.ExpectedOutcome}"))}

Results:
{string.Join("\n", execution.StepResults.Where(r => r.Success).Select((r, i) => $"{i + 1}. {r.Output}"))}

Write a 1-2 sentence description of this skill's capability:";

            string description = await _llm.GenerateTextAsync(prompt, ct);
            description = description?.Trim() ?? $"Skill for: {execution.Plan.Goal}";

            return description;
        }
        catch
        {
            // Fallback to simple description
            return $"Reusable skill for: {execution.Plan.Goal}";
        }
    }

    /// <summary>
    /// Extracts prerequisites from successful execution steps.
    /// </summary>
    private List<string> ExtractPrerequisites(ExecutionResult execution, SkillExtractionConfig config)
    {
        List<string> prerequisites = new List<string>();

        // Extract actions from high-confidence successful steps
        List<string> successfulSteps = execution.StepResults
            .Where(r => r.Success && r.Step.ConfidenceScore > 0.7)
            .Select(r => r.Step.Action)
            .Distinct()
            .ToList();

        prerequisites.AddRange(successfulSteps);

        return prerequisites;
    }

    /// <summary>
    /// Parameterizes plan steps to make them more reusable.
    /// Identifies common patterns and replaces specific values with parameter placeholders.
    /// </summary>
    private List<PlanStep> ParameterizeSteps(List<PlanStep> steps)
    {
        List<PlanStep> parameterizedSteps = new List<PlanStep>();

        foreach (PlanStep step in steps)
        {
            Dictionary<string, object> newParams = new Dictionary<string, object>();

            // Keep parameters but mark those that could be parameterized
            foreach (KeyValuePair<string, object> param in step.Parameters)
            {
                // Simple heuristic: if value is a string or number, it might be parameterizable
                if (param.Value is string || param.Value is int || param.Value is double)
                {
                    newParams[$"param_{param.Key}"] = param.Value;
                }
                else
                {
                    newParams[param.Key] = param.Value;
                }
            }

            PlanStep parameterizedStep = new PlanStep(
                Action: step.Action,
                Parameters: newParams.Any() ? newParams : step.Parameters,
                ExpectedOutcome: step.ExpectedOutcome,
                ConfidenceScore: step.ConfidenceScore);

            parameterizedSteps.Add(parameterizedStep);
        }

        return parameterizedSteps;
    }

    /// <summary>
    /// Updates an existing skill with new execution data.
    /// </summary>
    private async Task<Result<Skill, string>> UpdateExistingSkillAsync(
        Skill existingSkill,
        ExecutionResult execution,
        VerificationResult verification,
        SkillExtractionConfig config)
    {
        try
        {
            // Calculate updated success rate (weighted average)
            int totalUsages = existingSkill.UsageCount + 1;
            double updatedSuccessRate =
                (existingSkill.SuccessRate * existingSkill.UsageCount + verification.QualityScore) / totalUsages;

            // Create updated skill
            Skill updatedSkill = existingSkill with
            {
                SuccessRate = updatedSuccessRate,
                UsageCount = totalUsages,
                LastUsed = DateTime.UtcNow
            };

            // Update in registry
            _skillRegistry.RegisterSkill(updatedSkill);

            return await Task.FromResult(Result<Skill, string>.Success(updatedSkill));
        }
        catch (Exception ex)
        {
            return Result<Skill, string>.Failure($"Failed to update existing skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Sanitizes skill name to follow naming conventions.
    /// </summary>
    private string SanitizeSkillName(string name)
    {
        // Remove quotes and extra whitespace
        name = name.Trim('"', '\'', ' ', '\n', '\r');

        // Convert to lowercase
        name = name.ToLowerInvariant();

        // Replace spaces and special chars with underscores
        name = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-z0-9_]", "_");

        // Remove duplicate underscores
        name = System.Text.RegularExpressions.Regex.Replace(name, @"_+", "_");

        // Remove leading/trailing underscores
        name = name.Trim('_');

        // Ensure it's not empty
        if (string.IsNullOrWhiteSpace(name))
            name = "extracted_skill";

        return name;
    }

    /// <summary>
    /// Generates a fallback skill name when LLM generation fails.
    /// </summary>
    private string GenerateFallbackSkillName(ExecutionResult execution)
    {
        // Extract first action as basis for name
        string firstAction = execution.Plan.Steps.FirstOrDefault()?.Action ?? "skill";
        string sanitized = SanitizeSkillName(firstAction);

        // Add timestamp to ensure uniqueness
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        return $"{sanitized}_{timestamp}";
    }
}
