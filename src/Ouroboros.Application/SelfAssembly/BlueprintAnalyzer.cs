// <copyright file="BlueprintAnalyzer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Represents a capability gap identified by the system.
/// </summary>
public sealed record CapabilityGap
{
    /// <summary>Description of the missing capability.</summary>
    public required string Description { get; init; }

    /// <summary>Why this capability is needed.</summary>
    public required string Rationale { get; init; }

    /// <summary>Estimated importance (0-1).</summary>
    public double Importance { get; init; }

    /// <summary>Topics that would benefit from this capability.</summary>
    public required IReadOnlyList<string> AffectedTopics { get; init; }

    /// <summary>Suggested capabilities to implement.</summary>
    public required IReadOnlyList<NeuronCapability> SuggestedCapabilities { get; init; }

    /// <summary>The source that identified this gap.</summary>
    public required string IdentifiedBy { get; init; }

    /// <summary>When this gap was identified.</summary>
    public DateTime IdentifiedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Analyzes the neural network to identify capability gaps and generate blueprints.
/// </summary>
public sealed class BlueprintAnalyzer
{
    private readonly OuroborosNeuralNetwork _network;
    private readonly List<CapabilityGap> _identifiedGaps = [];

    /// <summary>Delegate for LLM-based gap analysis.</summary>
    public Func<string, CancellationToken, Task<string>>? LlmAnalyzer { get; set; }

    /// <summary>Gets identified capability gaps.</summary>
    public IReadOnlyList<CapabilityGap> IdentifiedGaps => _identifiedGaps.AsReadOnly();

    /// <summary>
    /// Creates a new blueprint analyzer.
    /// </summary>
    public BlueprintAnalyzer(OuroborosNeuralNetwork network)
    {
        _network = network ?? throw new ArgumentNullException(nameof(network));
    }

    /// <summary>
    /// Analyzes message patterns to identify capability gaps.
    /// </summary>
    public async Task<IReadOnlyList<CapabilityGap>> AnalyzeGapsAsync(
        IReadOnlyList<NeuronMessage> recentMessages,
        CancellationToken ct = default)
    {
        var gaps = new List<CapabilityGap>();

        // Analysis 1: Unhandled topics
        var unhandledTopics = FindUnhandledTopics(recentMessages);
        if (unhandledTopics.Count > 0)
        {
            gaps.Add(new CapabilityGap
            {
                Description = $"Messages on topics [{string.Join(", ", unhandledTopics)}] have no subscribers",
                Rationale = "Messages are being broadcast but no neuron is listening",
                Importance = 0.7,
                AffectedTopics = unhandledTopics,
                SuggestedCapabilities = [NeuronCapability.EventObservation],
                IdentifiedBy = "TopicAnalyzer",
            });
        }

        // Analysis 2: Slow response patterns
        var slowPatterns = FindSlowResponsePatterns(recentMessages);
        if (slowPatterns.Count > 0)
        {
            gaps.Add(new CapabilityGap
            {
                Description = "Some message types consistently have slow or no responses",
                Rationale = $"Topics [{string.Join(", ", slowPatterns)}] show response latency issues",
                Importance = 0.5,
                AffectedTopics = slowPatterns,
                SuggestedCapabilities = [NeuronCapability.TextProcessing, NeuronCapability.Reasoning],
                IdentifiedBy = "LatencyAnalyzer",
            });
        }

        // Analysis 3: LLM-based analysis (if available)
        if (LlmAnalyzer != null)
        {
            var llmGaps = await AnalyzeWithLlmAsync(recentMessages, ct);
            gaps.AddRange(llmGaps);
        }

        _identifiedGaps.AddRange(gaps);
        return gaps.AsReadOnly();
    }

    /// <summary>
    /// Generates a blueprint to address a capability gap.
    /// </summary>
    public async Task<NeuronBlueprint?> GenerateBlueprintForGapAsync(
        CapabilityGap gap,
        CancellationToken ct = default)
    {
        if (gap.AffectedTopics.Count == 0)
        {
            return null;
        }

        string neuronName;
        string description;
        var handlers = new List<MessageHandler>();

        // Generate name based on capabilities
        var primaryCapability = gap.SuggestedCapabilities.FirstOrDefault();
        neuronName = primaryCapability switch
        {
            NeuronCapability.TextProcessing => $"TextProcessor_{gap.AffectedTopics.First()}",
            NeuronCapability.ApiIntegration => $"ApiHandler_{gap.AffectedTopics.First()}",
            NeuronCapability.Reasoning => $"Reasoner_{gap.AffectedTopics.First()}",
            NeuronCapability.EventObservation => $"Observer_{gap.AffectedTopics.First()}",
            NeuronCapability.Orchestration => $"Coordinator_{gap.AffectedTopics.First()}",
            _ => $"CustomHandler_{gap.AffectedTopics.First()}",
        };

        description = $"Auto-generated neuron to address: {gap.Description}";

        // Create handlers for each affected topic
        foreach (var topic in gap.AffectedTopics)
        {
            handlers.Add(new MessageHandler
            {
                TopicPattern = topic,
                HandlingLogic = $"Process {topic} messages: {gap.Rationale}",
                SendsResponse = true,
                BroadcastsResult = false,
            });
        }

        // Calculate confidence based on gap analysis
        double confidence = Math.Min(0.9, gap.Importance + 0.2);

        // If we have an LLM, use it to refine the blueprint
        if (LlmAnalyzer != null)
        {
            var refinedBlueprint = await RefineWithLlmAsync(neuronName, gap, ct);
            if (refinedBlueprint != null)
            {
                return refinedBlueprint;
            }
        }

        return new NeuronBlueprint
        {
            Name = neuronName,
            Description = description,
            Rationale = gap.Rationale,
            Type = NeuronType.Custom,
            SubscribedTopics = gap.AffectedTopics,
            Capabilities = gap.SuggestedCapabilities,
            MessageHandlers = handlers,
            HasAutonomousTick = false,
            ConfidenceScore = confidence,
            IdentifiedBy = $"{GetType().Name}:{gap.IdentifiedBy}",
        };
    }

    private List<string> FindUnhandledTopics(IReadOnlyList<NeuronMessage> messages)
    {
        var allTopics = messages.Select(m => m.Topic).Distinct().ToHashSet();
        var handledTopics = new HashSet<string>();

        // Find topics that have subscribers
        foreach (var neuron in _network.Neurons.Values)
        {
            foreach (var subscribedTopic in neuron.SubscribedTopics)
            {
                // Handle wildcard patterns
                if (subscribedTopic.EndsWith(".*"))
                {
                    var prefix = subscribedTopic[..^2];
                    foreach (var topic in allTopics.Where(t => t.StartsWith(prefix)))
                    {
                        handledTopics.Add(topic);
                    }
                }
                else
                {
                    handledTopics.Add(subscribedTopic);
                }
            }
        }

        return allTopics.Except(handledTopics).Take(5).ToList();
    }

    private List<string> FindSlowResponsePatterns(IReadOnlyList<NeuronMessage> messages)
    {
        // Group request/response pairs and look for missing responses
        var requests = messages
            .Where(m => m.ExpectsResponse)
            .GroupBy(m => m.Topic)
            .ToList();

        var responses = messages
            .Where(m => m.CorrelationId.HasValue)
            .Select(m => m.CorrelationId!.Value)
            .ToHashSet();

        var slowTopics = new List<string>();
        foreach (var group in requests)
        {
            var unanswered = group.Count(r => !responses.Contains(r.Id));
            if (unanswered > group.Count() / 2) // More than 50% unanswered
            {
                slowTopics.Add(group.Key);
            }
        }

        return slowTopics;
    }

    private async Task<IReadOnlyList<CapabilityGap>> AnalyzeWithLlmAsync(
        IReadOnlyList<NeuronMessage> messages,
        CancellationToken ct)
    {
        if (LlmAnalyzer == null || messages.Count == 0)
        {
            return [];
        }

        var topicSummary = string.Join("\n",
            messages.GroupBy(m => m.Topic)
                .Select(g => $"- {g.Key}: {g.Count()} messages"));

        var neuronSummary = string.Join("\n",
            _network.Neurons.Values
                .Select(n => $"- {n.Name} ({n.Type}): subscribes to [{string.Join(", ", n.SubscribedTopics)}]"));

        var prompt = $@"Analyze this neural network state and identify capability gaps:

Current Neurons:
{neuronSummary}

Recent Message Topics:
{topicSummary}

Identify 1-3 capability gaps that would improve the system. For each gap:
1. What capability is missing?
2. Why is it needed?
3. What topics would it handle?
4. How important is it (0-1)?

Respond in JSON array format:
[{{""description"": ""..."", ""rationale"": ""..."", ""topics"": [""...""], ""importance"": 0.X, ""capabilities"": [""TextProcessing""|""Reasoning""|""EventObservation""]}}]";

        try
        {
            var response = await LlmAnalyzer(prompt, ct);
            return ParseLlmGaps(response);
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<CapabilityGap> ParseLlmGaps(string response)
    {
        try
        {
            // Extract JSON from response
            var start = response.IndexOf('[');
            var end = response.LastIndexOf(']');
            if (start < 0 || end < 0 || end <= start)
            {
                return [];
            }

            var json = response[start..(end + 1)];
            var parsed = JsonSerializer.Deserialize<List<LlmGapResponse>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (parsed == null)
            {
                return [];
            }

            return parsed.Select(g => new CapabilityGap
            {
                Description = g.Description ?? "LLM-identified gap",
                Rationale = g.Rationale ?? "Identified by LLM analysis",
                Importance = Math.Clamp(g.Importance, 0, 1),
                AffectedTopics = g.Topics ?? [],
                SuggestedCapabilities = ParseCapabilities(g.Capabilities),
                IdentifiedBy = "LlmAnalyzer",
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<NeuronCapability> ParseCapabilities(IReadOnlyList<string>? names)
    {
        if (names == null || names.Count == 0)
        {
            return [NeuronCapability.TextProcessing];
        }

        var capabilities = new List<NeuronCapability>();
        foreach (var name in names)
        {
            if (Enum.TryParse<NeuronCapability>(name, true, out var cap))
            {
                capabilities.Add(cap);
            }
        }

        return capabilities.Count > 0 ? capabilities : [NeuronCapability.TextProcessing];
    }

    private async Task<NeuronBlueprint?> RefineWithLlmAsync(
        string baseName,
        CapabilityGap gap,
        CancellationToken ct)
    {
        if (LlmAnalyzer == null)
        {
            return null;
        }

        var prompt = $@"Generate a detailed neuron blueprint for this capability gap:

Gap: {gap.Description}
Rationale: {gap.Rationale}
Topics: {string.Join(", ", gap.AffectedTopics)}
Suggested Capabilities: {string.Join(", ", gap.SuggestedCapabilities)}

Create a JSON blueprint with:
- name: descriptive PascalCase name
- description: clear purpose description
- handlers: array of message handlers with {{topic, logic, sendsResponse, broadcastsResult}}
- hasAutonomousTick: true if needs periodic behavior
- tickBehavior: description if autonomous

JSON format:
{{""name"": ""..."", ""description"": ""..."", ""handlers"": [{{""topic"": ""..."", ""logic"": ""..."", ""sendsResponse"": true, ""broadcastsResult"": false}}], ""hasAutonomousTick"": false, ""tickBehavior"": null}}";

        try
        {
            var response = await LlmAnalyzer(prompt, ct);
            return ParseLlmBlueprint(response, gap);
        }
        catch
        {
            return null;
        }
    }

    private static NeuronBlueprint? ParseLlmBlueprint(string response, CapabilityGap gap)
    {
        try
        {
            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            if (start < 0 || end < 0 || end <= start)
            {
                return null;
            }

            var json = response[start..(end + 1)];
            var parsed = JsonSerializer.Deserialize<LlmBlueprintResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (parsed == null)
            {
                return null;
            }

            var handlers = parsed.Handlers?.Select(h => new MessageHandler
            {
                TopicPattern = h.Topic ?? gap.AffectedTopics.FirstOrDefault() ?? "default",
                HandlingLogic = h.Logic ?? "Handle message",
                SendsResponse = h.SendsResponse,
                BroadcastsResult = h.BroadcastsResult,
            }).ToList() ?? [];

            return new NeuronBlueprint
            {
                Name = parsed.Name ?? $"Generated_{Guid.NewGuid():N}",
                Description = parsed.Description ?? gap.Description,
                Rationale = gap.Rationale,
                Type = NeuronType.Custom,
                SubscribedTopics = gap.AffectedTopics,
                Capabilities = gap.SuggestedCapabilities,
                MessageHandlers = handlers,
                HasAutonomousTick = parsed.HasAutonomousTick,
                TickBehaviorDescription = parsed.TickBehavior,
                ConfidenceScore = 0.8, // LLM-refined blueprints get higher confidence
                IdentifiedBy = "LlmRefinedAnalyzer",
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class LlmGapResponse
    {
        public string? Description { get; set; }
        public string? Rationale { get; set; }
        public List<string>? Topics { get; set; }
        public double Importance { get; set; }
        public List<string>? Capabilities { get; set; }
    }

    private sealed class LlmBlueprintResponse
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<LlmHandlerResponse>? Handlers { get; set; }
        public bool HasAutonomousTick { get; set; }
        public string? TickBehavior { get; set; }
    }

    private sealed class LlmHandlerResponse
    {
        public string? Topic { get; set; }
        public string? Logic { get; set; }
        public bool SendsResponse { get; set; }
        public bool BroadcastsResult { get; set; }
    }
}
