#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Hypothesis Engine Implementation
// Scientific reasoning, hypothesis generation and testing
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of hypothesis generation and scientific reasoning.
/// </summary>
public sealed class HypothesisEngine : IHypothesisEngine
{
    private readonly IChatCompletionModel _llm;
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private readonly IMemoryStore _memory;
    private readonly HypothesisEngineConfig _config;
    private readonly ConcurrentDictionary<Guid, Hypothesis> _hypotheses = new();
    private readonly ConcurrentDictionary<Guid, List<(DateTime time, double confidence)>> _confidenceTrends = new();

    public HypothesisEngine(
        IChatCompletionModel llm,
        IMetaAIPlannerOrchestrator orchestrator,
        IMemoryStore memory,
        HypothesisEngineConfig? config = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _config = config ?? new HypothesisEngineConfig();
    }

    /// <summary>
    /// Generates a hypothesis to explain an observation or pattern.
    /// </summary>
    public async Task<Result<Hypothesis, string>> GenerateHypothesisAsync(
        string observation,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(observation))
            return Result<Hypothesis, string>.Failure("Observation cannot be empty");

        try
        {
            // Gather relevant past experiences
            MemoryQuery query = new MemoryQuery(
                observation,
                context,
                MaxResults: 10,
                MinSimilarity: 0.6);

            List<Experience> experiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);

            // Build hypothesis generation prompt
            string prompt = BuildHypothesisPrompt(observation, experiences, context);
            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse hypothesis from response
            Hypothesis hypothesis = ParseHypothesis(response, observation, context);

            // Store hypothesis
            _hypotheses[hypothesis.Id] = hypothesis;
            _confidenceTrends[hypothesis.Id] = new List<(DateTime, double)>
            {
                (DateTime.UtcNow, hypothesis.Confidence)
            };

            return Result<Hypothesis, string>.Success(hypothesis);
        }
        catch (Exception ex)
        {
            return Result<Hypothesis, string>.Failure($"Hypothesis generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Designs an experiment to test a hypothesis.
    /// </summary>
    public async Task<Result<Experiment, string>> DesignExperimentAsync(
        Hypothesis hypothesis,
        CancellationToken ct = default)
    {
        if (hypothesis == null)
            return Result<Experiment, string>.Failure("Hypothesis cannot be null");

        try
        {
            string prompt = $@"Design an experiment to test this hypothesis:

Hypothesis: {hypothesis.Statement}
Domain: {hypothesis.Domain}
Current Confidence: {hypothesis.Confidence:P0}

Design a concrete experiment that will provide evidence for or against this hypothesis.
Include:
1. Clear experimental steps
2. Expected outcomes if hypothesis is true
3. Expected outcomes if hypothesis is false
4. Control variables
5. Measurable criteria

Format:
STEP 1: [action]
EXPECTED_IF_TRUE: [outcome]
EXPECTED_IF_FALSE: [outcome]

STEP 2: ...

CRITERIA: [how to measure success]";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse experiment design
            List<PlanStep> steps = ParseExperimentSteps(response);
            Dictionary<string, object> expectedOutcomes = ParseExpectedOutcomes(response);

            Experiment experiment = new Experiment(
                Guid.NewGuid(),
                hypothesis,
                $"Test for: {hypothesis.Statement}",
                steps,
                expectedOutcomes,
                DateTime.UtcNow);

            return Result<Experiment, string>.Success(experiment);
        }
        catch (Exception ex)
        {
            return Result<Experiment, string>.Failure($"Experiment design failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests a hypothesis by executing an experiment.
    /// </summary>
    public async Task<Result<HypothesisTestResult, string>> TestHypothesisAsync(
        Hypothesis hypothesis,
        Experiment experiment,
        CancellationToken ct = default)
    {
        if (hypothesis == null)
            return Result<HypothesisTestResult, string>.Failure("Hypothesis cannot be null");

        if (experiment == null)
            return Result<HypothesisTestResult, string>.Failure("Experiment cannot be null");

        try
        {
            // Create plan from experiment steps
            Plan plan = new Plan(
                $"Test: {hypothesis.Statement}",
                experiment.Steps,
                new Dictionary<string, double> { ["experimental"] = 0.8 },
                DateTime.UtcNow);

            // Execute experiment
            Result<ExecutionResult, string> execResult = await _orchestrator.ExecuteAsync(plan, ct);

            if (!execResult.IsSuccess)
            {
                return Result<HypothesisTestResult, string>.Failure(
                    $"Experiment execution failed: {execResult.Error}");
            }

            ExecutionResult execution = execResult.Value;

            // Analyze results against expected outcomes
            bool supported = AnalyzeExperimentResults(execution, experiment.ExpectedOutcomes);
            double confidenceAdjustment = CalculateConfidenceAdjustment(execution, supported);

            string explanation = GenerateExplanation(hypothesis, execution, supported);

            // Update hypothesis
            Hypothesis updatedHypothesis = hypothesis with
            {
                Confidence = Math.Clamp(hypothesis.Confidence + confidenceAdjustment, 0.0, 1.0),
                Tested = true,
                Validated = supported
            };

            _hypotheses[hypothesis.Id] = updatedHypothesis;

            // Track confidence trend
            if (_confidenceTrends.TryGetValue(hypothesis.Id, out List<(DateTime time, double confidence)>? trend))
            {
                trend.Add((DateTime.UtcNow, updatedHypothesis.Confidence));
            }

            HypothesisTestResult result = new HypothesisTestResult(
                updatedHypothesis,
                experiment,
                execution,
                supported,
                confidenceAdjustment,
                explanation,
                DateTime.UtcNow);

            return Result<HypothesisTestResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<HypothesisTestResult, string>.Failure($"Hypothesis testing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Uses abductive reasoning to infer the best explanation for observations.
    /// </summary>
    public async Task<Result<Hypothesis, string>> AbductiveReasoningAsync(
        List<string> observations,
        CancellationToken ct = default)
    {
        if (observations == null || !observations.Any())
            return Result<Hypothesis, string>.Failure("Observations cannot be empty");

        try
        {
            string prompt = $@"Use abductive reasoning to find the best explanation for these observations:

Observations:
{string.Join("\n", observations.Select((o, i) => $"{i + 1}. {o}"))}

Provide:
1. The most likely hypothesis that explains ALL observations
2. Confidence level (0-1)
3. Supporting evidence from the observations
4. Alternative hypotheses considered

Format:
HYPOTHESIS: [statement]
CONFIDENCE: [0-1]
DOMAIN: [domain]
EVIDENCE: [supporting points]
ALTERNATIVES: [other possibilities]";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse the best explanation
            Hypothesis hypothesis = ParseHypothesis(response, string.Join("; ", observations), null);

            // Store hypothesis
            _hypotheses[hypothesis.Id] = hypothesis;
            _confidenceTrends[hypothesis.Id] = new List<(DateTime, double)>
            {
                (DateTime.UtcNow, hypothesis.Confidence)
            };

            return Result<Hypothesis, string>.Success(hypothesis);
        }
        catch (Exception ex)
        {
            return Result<Hypothesis, string>.Failure($"Abductive reasoning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all hypotheses for a specific domain.
    /// </summary>
    public List<Hypothesis> GetHypothesesByDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return new List<Hypothesis>();

        return _hypotheses.Values
            .Where(h => h.Domain.Contains(domain, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Confidence)
            .ToList();
    }

    /// <summary>
    /// Updates a hypothesis based on new evidence.
    /// </summary>
    public void UpdateHypothesis(Guid hypothesisId, string evidence, bool supports)
    {
        if (!_hypotheses.TryGetValue(hypothesisId, out Hypothesis? hypothesis))
            return;

        Hypothesis updatedHypothesis = supports
            ? hypothesis with
            {
                SupportingEvidence = new List<string>(hypothesis.SupportingEvidence) { evidence },
                Confidence = Math.Min(hypothesis.Confidence + 0.1, 1.0)
            }
            : hypothesis with
            {
                CounterEvidence = new List<string>(hypothesis.CounterEvidence) { evidence },
                Confidence = Math.Max(hypothesis.Confidence - 0.15, 0.0)
            };

        _hypotheses[hypothesisId] = updatedHypothesis;

        // Track confidence change
        if (_confidenceTrends.TryGetValue(hypothesisId, out List<(DateTime time, double confidence)>? trend))
        {
            trend.Add((DateTime.UtcNow, updatedHypothesis.Confidence));
        }
    }

    /// <summary>
    /// Gets the confidence trend for a hypothesis over time.
    /// </summary>
    public List<(DateTime time, double confidence)> GetConfidenceTrend(Guid hypothesisId)
    {
        return _confidenceTrends.TryGetValue(hypothesisId, out List<(DateTime time, double confidence)>? trend)
            ? new List<(DateTime, double)>(trend)
            : new List<(DateTime, double)>();
    }

    // Private helper methods

    private string BuildHypothesisPrompt(
        string observation,
        List<Experience> experiences,
        Dictionary<string, object>? context)
    {
        string contextText = context != null && context.Any()
            ? $"\nContext: {System.Text.Json.JsonSerializer.Serialize(context)}"
            : "";

        string experienceText = experiences.Any()
            ? $"\nRelevant Past Experiences:\n{string.Join("\n", experiences.Take(3).Select(e => $"- {e.Goal}: {(e.Verification.Verified ? "Success" : "Failed")}"))}"
            : "";

        return $@"Generate a hypothesis to explain this observation:

Observation: {observation}{contextText}{experienceText}

Provide:
1. A clear hypothesis statement
2. Confidence level (0-1)
3. Domain/category
4. Initial supporting evidence

Format:
HYPOTHESIS: [statement]
CONFIDENCE: [0-1]
DOMAIN: [domain]
EVIDENCE: [supporting points]";
    }

    private Hypothesis ParseHypothesis(string response, string observation, Dictionary<string, object>? context)
    {
        string[] lines = response.Split('\n');

        string statement = observation;
        double confidence = 0.5;
        string domain = "general";
        List<string> supportingEvidence = new List<string>();

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("HYPOTHESIS:", StringComparison.OrdinalIgnoreCase))
            {
                statement = trimmed.Substring("HYPOTHESIS:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                string confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, out double conf))
                {
                    confidence = Math.Clamp(conf, 0.0, 1.0);
                }
            }
            else if (trimmed.StartsWith("DOMAIN:", StringComparison.OrdinalIgnoreCase))
            {
                domain = trimmed.Substring("DOMAIN:".Length).Trim();
            }
            else if (trimmed.StartsWith("EVIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                string evidence = trimmed.Substring("EVIDENCE:".Length).Trim();
                supportingEvidence.Add(evidence);
            }
        }

        return new Hypothesis(
            Guid.NewGuid(),
            statement,
            domain,
            confidence,
            supportingEvidence,
            new List<string>(),
            DateTime.UtcNow,
            Tested: false,
            Validated: null);
    }

    private List<PlanStep> ParseExperimentSteps(string response)
    {
        List<PlanStep> steps = new List<PlanStep>();
        string[] lines = response.Split('\n');

        string? currentAction = null;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    steps.Add(new PlanStep(
                        currentAction,
                        new Dictionary<string, object>(),
                        "",
                        0.8));
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
        }

        if (currentAction != null)
        {
            steps.Add(new PlanStep(
                currentAction,
                new Dictionary<string, object>(),
                "",
                0.8));
        }

        return steps;
    }

    private Dictionary<string, object> ParseExpectedOutcomes(string response)
    {
        Dictionary<string, object> outcomes = new Dictionary<string, object>();
        string[] lines = response.Split('\n');

        foreach (string line in lines)
        {
            if (line.Contains("EXPECTED_IF_TRUE:", StringComparison.OrdinalIgnoreCase))
            {
                outcomes["if_true"] = line.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
            else if (line.Contains("EXPECTED_IF_FALSE:", StringComparison.OrdinalIgnoreCase))
            {
                outcomes["if_false"] = line.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
            else if (line.Contains("CRITERIA:", StringComparison.OrdinalIgnoreCase))
            {
                outcomes["criteria"] = line.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
            }
        }

        return outcomes;
    }

    private bool AnalyzeExperimentResults(ExecutionResult execution, Dictionary<string, object> expectedOutcomes)
    {
        // Check if execution was successful
        if (!execution.Success)
            return false;

        // Check if there are any step results to analyze
        if (execution.StepResults.Count == 0)
            return false;

        // Simple heuristic: if most steps succeeded, hypothesis is likely supported
        double successRate = execution.StepResults.Count(r => r.Success) / (double)execution.StepResults.Count;

        return successRate >= 0.7;
    }

    private double CalculateConfidenceAdjustment(ExecutionResult execution, bool supported)
    {
        // Handle empty step results
        if (execution.StepResults.Count == 0)
            return supported ? 0.05 : -0.05;

        double successRate = execution.StepResults.Count(r => r.Success) / (double)execution.StepResults.Count;

        if (supported)
        {
            // Increase confidence based on how clean the success was
            return 0.1 + (successRate - 0.7) * 0.2;
        }
        else
        {
            // Decrease confidence
            return -0.15 - ((1.0 - successRate) * 0.1);
        }
    }

    private string GenerateExplanation(Hypothesis hypothesis, ExecutionResult execution, bool supported)
    {
        if (supported)
        {
            return $"Experiment supports the hypothesis. " +
                   $"{execution.StepResults.Count(r => r.Success)}/{execution.StepResults.Count} steps succeeded.";
        }
        else
        {
            return $"Experiment does not support the hypothesis. " +
                   $"Only {execution.StepResults.Count(r => r.Success)}/{execution.StepResults.Count} steps succeeded.";
        }
    }
}
