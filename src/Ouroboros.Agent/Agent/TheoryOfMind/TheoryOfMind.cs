// <copyright file="TheoryOfMind.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Concurrent;
using System.Text.Json;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;
using Ouroboros.Providers;
using Unit = Ouroboros.Core.Learning.Unit;

namespace Ouroboros.Agent.TheoryOfMind;

/// <summary>
/// Implementation of Theory of Mind capabilities.
/// Maintains models of other agents and predicts their behavior.
/// </summary>
public sealed class TheoryOfMind : ITheoryOfMind
{
    private readonly IChatCompletionModel _llm;
    private readonly ConcurrentDictionary<string, AgentModel> _agentModels = new();
    private readonly ConcurrentDictionary<string, List<PredictionAccuracy>> _predictionHistory = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TheoryOfMind"/> class.
    /// </summary>
    /// <param name="llm">LLM for inference and reasoning</param>
    public TheoryOfMind(IChatCompletionModel llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    /// <summary>
    /// Infers another agent's beliefs from observations.
    /// </summary>
    public async Task<Result<BeliefState, string>> InferBeliefsAsync(
        string agentId,
        IReadOnlyList<AgentObservation> observations,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return Result<BeliefState, string>.Failure("Agent ID cannot be empty");

            if (observations == null || observations.Count == 0)
                return Result<BeliefState, string>.Success(BeliefState.Empty(agentId));

            // Get existing model or create new one
            AgentModel existingModel = _agentModels.GetOrAdd(agentId, AgentModel.Create);

            // Build prompt for belief inference
            string observationsText = string.Join("\n",
                observations.Select((o, i) =>
                    $"{i + 1}. [{o.ObservationType}] {o.Content} (at {o.ObservedAt:HH:mm:ss})"));

            string existingBeliefs = existingModel.Beliefs.Beliefs.Any()
                ? string.Join("\n", existingModel.Beliefs.Beliefs.Select(b =>
                    $"- {b.Key}: {b.Value.Proposition} (confidence: {b.Value.Probability:P0})"))
                : "No prior beliefs recorded";

            string prompt = $@"You are analyzing observations of Agent '{agentId}' to infer their beliefs about the world.

OBSERVATIONS:
{observationsText}

EXISTING BELIEFS:
{existingBeliefs}

Based on these observations, infer what Agent '{agentId}' believes. Consider:
1. What information is the agent acting on?
2. What assumptions are they making?
3. What do they seem to know or not know?

Provide a JSON response with this structure:
{{
  ""beliefs"": [
    {{
      ""key"": ""belief_key"",
      ""proposition"": ""description of belief"",
      ""probability"": 0.85,
      ""source"": ""inference""
    }}
  ],
  ""overall_confidence"": 0.75
}}

Focus on actionable beliefs that would influence the agent's behavior. Limit to 5 most important beliefs.";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse the LLM response
            BeliefState beliefs = ParseBeliefStateFromLLM(agentId, response, existingModel.Beliefs);

            return Result<BeliefState, string>.Success(beliefs);
        }
        catch (Exception ex)
        {
            return Result<BeliefState, string>.Failure($"Belief inference failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Predicts another agent's likely intention or goal.
    /// </summary>
    public async Task<Result<IntentionPrediction, string>> PredictIntentionAsync(
        string agentId,
        BeliefState beliefs,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return Result<IntentionPrediction, string>.Failure("Agent ID cannot be empty");

            if (beliefs == null)
                return Result<IntentionPrediction, string>.Success(IntentionPrediction.Unknown(agentId));

            AgentModel? model = GetAgentModel(agentId);
            if (model == null || model.ObservationHistory.Count < 2)
                return Result<IntentionPrediction, string>.Success(IntentionPrediction.Unknown(agentId));

            // Build prompt for intention prediction
            string beliefsText = beliefs.Beliefs.Any()
                ? string.Join("\n", beliefs.Beliefs.Select(b =>
                    $"- {b.Key}: {b.Value.Proposition} (confidence: {b.Value.Probability:P0})"))
                : "No beliefs identified";

            string recentActions = string.Join("\n",
                model.ObservationHistory.TakeLast(5).Select((o, i) =>
                    $"{i + 1}. [{o.ObservationType}] {o.Content}"));

            string prompt = $@"You are predicting the intention/goal of Agent '{agentId}'.

AGENT'S BELIEFS:
{beliefsText}

RECENT ACTIONS:
{recentActions}

KNOWN GOALS: {string.Join(", ", model.InferredGoals.DefaultIfEmpty("None"))}
CAPABILITIES: {string.Join(", ", model.InferredCapabilities.DefaultIfEmpty("Unknown"))}

Based on this information, predict what Agent '{agentId}' is trying to achieve. Provide a JSON response:
{{
  ""predicted_goal"": ""clear description of the main goal"",
  ""confidence"": 0.80,
  ""supporting_evidence"": [""reason 1"", ""reason 2""],
  ""alternative_goals"": [""alternative 1"", ""alternative 2""]
}}

Consider both explicit statements and implicit behavior patterns.";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            IntentionPrediction prediction = ParseIntentionFromLLM(agentId, response);

            return Result<IntentionPrediction, string>.Success(prediction);
        }
        catch (Exception ex)
        {
            return Result<IntentionPrediction, string>.Failure($"Intention prediction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Predicts what action another agent will take next.
    /// </summary>
    public async Task<Result<ActionPrediction, string>> PredictNextActionAsync(
        string agentId,
        BeliefState beliefs,
        IReadOnlyList<EmbodiedAction> availableActions,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return Result<ActionPrediction, string>.Failure("Agent ID cannot be empty");

            AgentModel? model = GetAgentModel(agentId);
            if (model == null || model.ObservationHistory.Count < 1)
                return Result<ActionPrediction, string>.Success(
                    ActionPrediction.NoOp(agentId, "Insufficient observation history"));

            // Build prompt for action prediction
            string actionsText = availableActions?.Any() == true
                ? string.Join("\n", availableActions.Select((a, i) =>
                    $"{i + 1}. {a.ActionName ?? "Unnamed"}: Movement={a.Movement}, Rotation={a.Rotation}"))
                : "No specific actions listed - agent has general action capabilities";

            string recentBehavior = string.Join("\n",
                model.ObservationHistory.Where(o => o.ObservationType == "action")
                    .TakeLast(3)
                    .Select((o, i) => $"{i + 1}. {o.Content}"));

            string prompt = $@"You are predicting the next action of Agent '{agentId}'.

AGENT'S BELIEFS:
{string.Join("\n", beliefs.Beliefs.Take(3).Select(b => $"- {b.Key}: {b.Value.Proposition}"))}

RECENT ACTIONS:
{recentBehavior}

INFERRED GOALS: {string.Join(", ", model.InferredGoals.Take(2).DefaultIfEmpty("Unknown"))}

AVAILABLE ACTIONS:
{actionsText}

Based on this context, predict what action the agent will take next. Provide JSON response:
{{
  ""action_index"": 0,
  ""action_name"": ""action name or description"",
  ""confidence"": 0.70,
  ""reasoning"": ""why this action makes sense given their beliefs and goals""
}}

Consider the agent's goals, beliefs, and recent behavior patterns.";

            string response = await _llm.GenerateTextAsync(prompt, ct);

            ActionPrediction prediction = ParseActionFromLLM(agentId, response, availableActions);

            return Result<ActionPrediction, string>.Success(prediction);
        }
        catch (Exception ex)
        {
            return Result<ActionPrediction, string>.Failure($"Action prediction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the model of a specific agent based on new observation.
    /// </summary>
    public async Task<Result<Unit, string>> UpdateAgentModelAsync(
        string agentId,
        AgentObservation observation,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(agentId))
                return Result<Unit, string>.Failure("Agent ID cannot be empty");

            if (observation == null)
                return Result<Unit, string>.Failure("Observation cannot be null");

            // Get or create agent model
            AgentModel model = _agentModels.AddOrUpdate(
                agentId,
                _ => AgentModel.Create(agentId).WithObservation(observation),
                (_, existing) => existing.WithObservation(observation));

            // Infer beliefs from recent observations
            List<AgentObservation> recentObs = model.ObservationHistory.TakeLast(10).ToList();
            Result<BeliefState, string> beliefsResult = await InferBeliefsAsync(
                agentId,
                recentObs,
                ct);

            if (beliefsResult.IsSuccess)
            {
                model = model.WithBeliefs(beliefsResult.Value);
                _agentModels[agentId] = model;
            }

            await Task.CompletedTask;
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Model update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current model of a specific agent.
    /// </summary>
    public AgentModel? GetAgentModel(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            return null;

        return _agentModels.TryGetValue(agentId, out AgentModel? model) ? model : null;
    }

    /// <summary>
    /// Evaluates how well the agent understands another agent.
    /// </summary>
    public async Task<double> GetModelConfidenceAsync(string agentId, CancellationToken ct = default)
    {
        await Task.CompletedTask;

        AgentModel? model = GetAgentModel(agentId);
        if (model == null)
            return 0.0;

        // Calculate confidence based on multiple factors
        double observationScore = Math.Min(model.ObservationHistory.Count / 20.0, 1.0);
        double beliefScore = model.Beliefs.Confidence;
        double recencyScore = CalculateRecencyScore(model.LastInteraction);

        // Check prediction accuracy if available
        double accuracyScore = 0.5; // Default neutral
        if (_predictionHistory.TryGetValue(agentId, out List<PredictionAccuracy>? history) && history.Count >= 3)
        {
            accuracyScore = history.TakeLast(10).Average(p => p.WasAccurate ? 1.0 : 0.0);
        }

        // Weighted average
        double confidence = (observationScore * 0.3) +
                           (beliefScore * 0.3) +
                           (recencyScore * 0.2) +
                           (accuracyScore * 0.2);

        return Math.Clamp(confidence, 0.0, 1.0);
    }

    // Private helper methods

    private BeliefState ParseBeliefStateFromLLM(string agentId, string llmResponse, BeliefState existingBeliefs)
    {
        try
        {
            // Try to parse JSON response
            using JsonDocument doc = JsonDocument.Parse(llmResponse);
            JsonElement root = doc.RootElement;

            Dictionary<string, BeliefValue> beliefs = new();

            if (root.TryGetProperty("beliefs", out JsonElement beliefsArray))
            {
                foreach (JsonElement beliefElement in beliefsArray.EnumerateArray())
                {
                    string key = beliefElement.GetProperty("key").GetString() ?? "unknown";
                    string proposition = beliefElement.GetProperty("proposition").GetString() ?? "";
                    double probability = beliefElement.GetProperty("probability").GetDouble();
                    string source = beliefElement.GetProperty("source").GetString() ?? "inference";

                    beliefs[key] = new BeliefValue(proposition, probability, source);
                }
            }

            double confidence = root.TryGetProperty("overall_confidence", out JsonElement confElement)
                ? confElement.GetDouble()
                : 0.5;

            return new BeliefState(agentId, beliefs, confidence, DateTime.UtcNow);
        }
        catch
        {
            // Fallback: return existing beliefs with slightly reduced confidence
            return existingBeliefs.WithConfidence(existingBeliefs.Confidence * 0.9);
        }
    }

    private IntentionPrediction ParseIntentionFromLLM(string agentId, string llmResponse)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(llmResponse);
            JsonElement root = doc.RootElement;

            string goal = root.GetProperty("predicted_goal").GetString() ?? "Unknown";
            double confidence = root.GetProperty("confidence").GetDouble();

            List<string> evidence = new();
            if (root.TryGetProperty("supporting_evidence", out JsonElement evidenceArray))
            {
                evidence.AddRange(evidenceArray.EnumerateArray().Select(e => e.GetString() ?? ""));
            }

            List<string> alternatives = new();
            if (root.TryGetProperty("alternative_goals", out JsonElement altArray))
            {
                alternatives.AddRange(altArray.EnumerateArray().Select(e => e.GetString() ?? ""));
            }

            return IntentionPrediction.Create(agentId, goal, confidence, evidence, alternatives);
        }
        catch
        {
            return IntentionPrediction.Unknown(agentId);
        }
    }

    private ActionPrediction ParseActionFromLLM(
        string agentId,
        string llmResponse,
        IReadOnlyList<EmbodiedAction>? availableActions)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(llmResponse);
            JsonElement root = doc.RootElement;

            int actionIndex = root.TryGetProperty("action_index", out JsonElement indexElement)
                ? indexElement.GetInt32()
                : -1;

            string actionName = root.TryGetProperty("action_name", out JsonElement nameElement)
                ? nameElement.GetString() ?? "Unknown"
                : "Unknown";

            double confidence = root.GetProperty("confidence").GetDouble();
            string reasoning = root.GetProperty("reasoning").GetString() ?? "";

            EmbodiedAction action;
            if (actionIndex >= 0 && availableActions != null && actionIndex < availableActions.Count)
            {
                action = availableActions[actionIndex];
            }
            else
            {
                action = EmbodiedAction.NoOp();
                reasoning = $"{reasoning} (No specific action selected)";
            }

            return ActionPrediction.Create(agentId, action, confidence, reasoning);
        }
        catch
        {
            return ActionPrediction.NoOp(agentId, "Failed to parse LLM response");
        }
    }

    private double CalculateRecencyScore(DateTime lastInteraction)
    {
        TimeSpan timeSinceInteraction = DateTime.UtcNow - lastInteraction;
        double hoursSince = timeSinceInteraction.TotalHours;

        // Decay function: recent interactions get higher scores
        if (hoursSince < 1) return 1.0;
        if (hoursSince < 24) return 0.8;
        if (hoursSince < 168) return 0.5; // 1 week
        return 0.2;
    }

    private sealed record PredictionAccuracy(
        DateTime PredictedAt,
        bool WasAccurate,
        string PredictionType);
}
