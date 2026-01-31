// <copyright file="TemporalReasoner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Globalization;
using Ouroboros.Core.Monads;
using Ouroboros.Providers;

namespace Ouroboros.Agent.TemporalReasoning;

/// <summary>
/// Implementation of temporal reasoning engine.
/// Enables reasoning about time, sequences, causality, and temporal relationships between events.
/// </summary>
public sealed class TemporalReasoner : ITemporalReasoner
{
    private readonly IChatCompletionModel? llm;
    private readonly ConcurrentDictionary<Guid, TemporalEvent> eventStore = new();
    private readonly ConcurrentDictionary<string, List<TemporalEvent>> eventsByType = new();
    private readonly ConcurrentDictionary<(Guid, Guid), TemporalRelation> relationCache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporalReasoner"/> class.
    /// </summary>
    /// <param name="llm">Optional LLM for causal inference and pattern recognition.</param>
    public TemporalReasoner(IChatCompletionModel? llm = null)
    {
        this.llm = llm;
    }

    /// <summary>
    /// Determines temporal relationship between two events using Allen Interval Algebra.
    /// </summary>
    public Task<Result<TemporalRelation, string>> GetRelationAsync(
        TemporalEvent event1,
        TemporalEvent event2,
        CancellationToken ct = default)
    {
        if (event1 == null)
        {
            return Task.FromResult(Result<TemporalRelation, string>.Failure("Event1 cannot be null"));
        }

        if (event2 == null)
        {
            return Task.FromResult(Result<TemporalRelation, string>.Failure("Event2 cannot be null"));
        }

        try
        {
            // Check cache
            var cacheKey = (event1.Id, event2.Id);
            if (this.relationCache.TryGetValue(cacheKey, out var cachedRelation))
            {
                return Task.FromResult(Result<TemporalRelation, string>.Success(cachedRelation));
            }

            // Compute Allen interval relation
            var relationType = this.ComputeAllenRelation(event1, event2);
            var relation = new TemporalRelation(event1, event2, relationType, 1.0);

            // Cache the result
            this.relationCache[cacheKey] = relation;

            return Task.FromResult(Result<TemporalRelation, string>.Success(relation));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<TemporalRelation, string>.Failure($"Failed to compute relation: {ex.Message}"));
        }
    }

    /// <summary>
    /// Queries events matching temporal constraints.
    /// </summary>
    public Task<Result<List<TemporalEvent>, string>> QueryEventsAsync(
        TemporalQuery query,
        CancellationToken ct = default)
    {
        if (query == null)
        {
            return Task.FromResult(Result<List<TemporalEvent>, string>.Failure("Query cannot be null"));
        }

        try
        {
            var events = this.eventStore.Values.AsEnumerable();

            // Filter by time range
            if (query.After.HasValue)
            {
                events = events.Where(e => e.StartTime >= query.After.Value);
            }

            if (query.Before.HasValue)
            {
                events = events.Where(e => e.StartTime <= query.Before.Value);
            }

            // Filter by event type
            if (!string.IsNullOrWhiteSpace(query.EventType))
            {
                events = events.Where(e => e.EventType.Equals(query.EventType, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by duration
            if (query.Duration.HasValue)
            {
                events = events.Where(e =>
                {
                    if (e.EndTime.HasValue)
                    {
                        var duration = e.EndTime.Value - e.StartTime;
                        return Math.Abs(duration.TotalSeconds - query.Duration.Value.TotalSeconds) < 1.0;
                    }

                    return false;
                });
            }

            // Filter by temporal relation to another event
            if (query.RelationTo.HasValue && query.RelatedEventId.HasValue)
            {
                if (this.eventStore.TryGetValue(query.RelatedEventId.Value, out var relatedEvent))
                {
                    events = events.Where(e =>
                    {
                        var relation = this.ComputeAllenRelation(e, relatedEvent);
                        return relation == query.RelationTo.Value;
                    });
                }
            }

            var results = events.Take(query.MaxResults).ToList();
            return Task.FromResult(Result<List<TemporalEvent>, string>.Success(results));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<List<TemporalEvent>, string>.Failure($"Query failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Infers causal relationships from temporal patterns using LLM.
    /// </summary>
    public async Task<Result<List<CausalRelation>, string>> InferCausalityAsync(
        IReadOnlyList<TemporalEvent> events,
        CancellationToken ct = default)
    {
        if (events == null || events.Count == 0)
        {
            return Result<List<CausalRelation>, string>.Failure("Events list cannot be null or empty");
        }

        if (this.llm == null)
        {
            // Without LLM, use simple temporal correlation
            return Result<List<CausalRelation>, string>.Success(this.InferSimpleCausality(events));
        }

        try
        {
            // Build prompt for LLM
            var prompt = this.BuildCausalInferencePrompt(events);
            var response = await this.llm.GenerateTextAsync(prompt, ct);

            // Parse causal relations from response
            var causalRelations = this.ParseCausalRelations(response, events);

            return Result<List<CausalRelation>, string>.Success(causalRelations);
        }
        catch (Exception ex)
        {
            return Result<List<CausalRelation>, string>.Failure($"Causal inference failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Predicts future events based on temporal patterns.
    /// </summary>
    public async Task<Result<List<PredictedEvent>, string>> PredictFutureEventsAsync(
        IReadOnlyList<TemporalEvent> history,
        TimeSpan horizon,
        CancellationToken ct = default)
    {
        if (history == null || history.Count == 0)
        {
            return Result<List<PredictedEvent>, string>.Failure("History cannot be null or empty");
        }

        if (horizon <= TimeSpan.Zero)
        {
            return Result<List<PredictedEvent>, string>.Failure("Horizon must be positive");
        }

        try
        {
            if (this.llm != null)
            {
                return await this.PredictWithLLMAsync(history, horizon, ct);
            }

            // Fallback to pattern-based prediction
            var predictions = this.PredictWithPatterns(history, horizon);
            return Result<List<PredictedEvent>, string>.Success(predictions);
        }
        catch (Exception ex)
        {
            return Result<List<PredictedEvent>, string>.Failure($"Prediction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Constructs a timeline from a set of events.
    /// </summary>
    public Result<Timeline, string> ConstructTimeline(IReadOnlyList<TemporalEvent> events)
    {
        if (events == null || events.Count == 0)
        {
            return Result<Timeline, string>.Failure("Events list cannot be null or empty");
        }

        try
        {
            // Store events
            foreach (var evt in events)
            {
                this.eventStore[evt.Id] = evt;

                if (!this.eventsByType.ContainsKey(evt.EventType))
                {
                    this.eventsByType[evt.EventType] = new List<TemporalEvent>();
                }

                this.eventsByType[evt.EventType].Add(evt);
            }

            // Sort events by start time
            var sortedEvents = events.OrderBy(e => e.StartTime).ToList();

            // Compute relations between consecutive and overlapping events
            var relations = new List<TemporalRelation>();
            for (int i = 0; i < sortedEvents.Count; i++)
            {
                for (int j = i + 1; j < Math.Min(i + 5, sortedEvents.Count); j++)
                {
                    var relation = this.ComputeAllenRelation(sortedEvents[i], sortedEvents[j]);
                    relations.Add(new TemporalRelation(
                        sortedEvents[i],
                        sortedEvents[j],
                        relation,
                        1.0));
                }
            }

            // Find earliest and latest times
            var earliestTime = sortedEvents.Min(e => e.StartTime);
            var latestTime = sortedEvents.Max(e => e.EndTime ?? e.StartTime);

            // Group events by type
            var eventsByType = sortedEvents
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.ToList());

            var timeline = new Timeline(
                sortedEvents,
                relations,
                earliestTime,
                latestTime,
                eventsByType);

            return Result<Timeline, string>.Success(timeline);
        }
        catch (Exception ex)
        {
            return Result<Timeline, string>.Failure($"Timeline construction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a temporal constraint is satisfiable.
    /// </summary>
    public Task<Result<bool, string>> CheckConstraintSatisfiabilityAsync(
        List<TemporalConstraint> constraints,
        CancellationToken ct = default)
    {
        if (constraints == null)
        {
            return Task.FromResult(Result<bool, string>.Failure("Constraints cannot be null"));
        }

        try
        {
            // Build constraint graph
            var constraintMap = new Dictionary<(Guid, Guid), TemporalRelationType>();

            foreach (var constraint in constraints)
            {
                var key = (constraint.Event1Id, constraint.Event2Id);
                if (constraintMap.TryGetValue(key, out var existing))
                {
                    // Check for conflicts
                    if (existing != constraint.RequiredRelation)
                    {
                        return Task.FromResult(Result<bool, string>.Success(false));
                    }
                }
                else
                {
                    constraintMap[key] = constraint.RequiredRelation;
                }
            }

            // Check for consistency using path consistency
            var satisfiable = this.CheckPathConsistency(constraintMap);

            return Task.FromResult(Result<bool, string>.Success(satisfiable));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<bool, string>.Failure($"Constraint checking failed: {ex.Message}"));
        }
    }

    // Private helper methods
    private TemporalRelationType ComputeAllenRelation(TemporalEvent event1, TemporalEvent event2)
    {
        var start1 = event1.StartTime;
        var end1 = event1.EndTime ?? event1.StartTime;
        var start2 = event2.StartTime;
        var end2 = event2.EndTime ?? event2.StartTime;

        // Check for point events (no duration)
        var isPoint1 = start1 == end1;
        var isPoint2 = start2 == end2;

        // Equals
        if (start1 == start2 && end1 == end2)
        {
            return TemporalRelationType.Equals;
        }

        // Before / After
        if (end1 < start2)
        {
            return end1 == start2 ? TemporalRelationType.Meets : TemporalRelationType.Before;
        }

        if (start1 > end2)
        {
            return start1 == end2 ? TemporalRelationType.MetBy : TemporalRelationType.After;
        }

        // Overlaps / OverlappedBy
        if (start1 < start2 && end1 > start2 && end1 < end2)
        {
            return TemporalRelationType.Overlaps;
        }

        if (start2 < start1 && end2 > start1 && end2 < end1)
        {
            return TemporalRelationType.OverlappedBy;
        }

        // During / Contains
        if (start1 > start2 && end1 < end2)
        {
            return TemporalRelationType.During;
        }

        if (start2 > start1 && end2 < end1)
        {
            return TemporalRelationType.Contains;
        }

        // Starts / StartedBy
        if (start1 == start2)
        {
            return end1 < end2 ? TemporalRelationType.Starts : TemporalRelationType.StartedBy;
        }

        // Finishes / FinishedBy
        if (end1 == end2)
        {
            return start1 > start2 ? TemporalRelationType.Finishes : TemporalRelationType.FinishedBy;
        }

        // Default to Before if no other relation matches
        return TemporalRelationType.Before;
    }

    private List<CausalRelation> InferSimpleCausality(IReadOnlyList<TemporalEvent> events)
    {
        var causalRelations = new List<CausalRelation>();
        var sortedEvents = events.OrderBy(e => e.StartTime).ToList();

        // Simple heuristic: events that occur close in time may be causally related
        for (int i = 0; i < sortedEvents.Count - 1; i++)
        {
            for (int j = i + 1; j < Math.Min(i + 3, sortedEvents.Count); j++)
            {
                var timeDiff = (sortedEvents[j].StartTime - sortedEvents[i].StartTime).TotalMinutes;

                // If events are within 60 minutes and not overlapping, consider potential causality
                if (timeDiff > 0 && timeDiff <= 60)
                {
                    var strength = Math.Max(0.1, 1.0 - (timeDiff / 60.0));
                    causalRelations.Add(new CausalRelation(
                        sortedEvents[i],
                        sortedEvents[j],
                        strength,
                        "Temporal proximity",
                        new List<string>()));
                }
            }
        }

        return causalRelations;
    }

    private string BuildCausalInferencePrompt(IReadOnlyList<TemporalEvent> events)
    {
        var eventDescriptions = events
            .OrderBy(e => e.StartTime)
            .Select((e, i) => $"{i + 1}. [{e.EventType}] {e.Description} at {e.StartTime:yyyy-MM-dd HH:mm}")
            .ToList();

        return $@"Analyze the following sequence of events and identify causal relationships:

Events:
{string.Join("\n", eventDescriptions)}

For each potential causal relationship, provide:
1. Cause event number
2. Effect event number
3. Causal strength (0-1)
4. Mechanism explaining the causality
5. Confounding factors (if any)

Format your response as:
CAUSE: [event_number]
EFFECT: [event_number]
STRENGTH: [0-1]
MECHANISM: [explanation]
CONFOUNDS: [comma-separated factors or 'none']

---

Provide multiple causal relationships if they exist.";
    }

    private List<CausalRelation> ParseCausalRelations(string response, IReadOnlyList<TemporalEvent> events)
    {
        var relations = new List<CausalRelation>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int? causeIdx = null;
        int? effectIdx = null;
        double strength = 0.5;
        string mechanism = "Unknown";
        var confounds = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("CAUSE:", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = trimmed.Substring("CAUSE:".Length).Trim();
                if (int.TryParse(numStr, out var num) && num > 0 && num <= events.Count)
                {
                    causeIdx = num - 1;
                }
            }
            else if (trimmed.StartsWith("EFFECT:", StringComparison.OrdinalIgnoreCase))
            {
                var numStr = trimmed.Substring("EFFECT:".Length).Trim();
                if (int.TryParse(numStr, out var num) && num > 0 && num <= events.Count)
                {
                    effectIdx = num - 1;
                }
            }
            else if (trimmed.StartsWith("STRENGTH:", StringComparison.OrdinalIgnoreCase))
            {
                var strStr = trimmed.Substring("STRENGTH:".Length).Trim();
                if (double.TryParse(strStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var str))
                {
                    strength = Math.Clamp(str, 0.0, 1.0);
                }
            }
            else if (trimmed.StartsWith("MECHANISM:", StringComparison.OrdinalIgnoreCase))
            {
                mechanism = trimmed.Substring("MECHANISM:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFOUNDS:", StringComparison.OrdinalIgnoreCase))
            {
                var confoundStr = trimmed.Substring("CONFOUNDS:".Length).Trim();
                if (!confoundStr.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    confounds = confoundStr.Split(',').Select(s => s.Trim()).ToList();
                }
            }
            else if (trimmed == "---" && causeIdx.HasValue && effectIdx.HasValue)
            {
                // Complete relation
                relations.Add(new CausalRelation(
                    events[causeIdx.Value],
                    events[effectIdx.Value],
                    strength,
                    mechanism,
                    confounds));

                // Reset for next relation
                causeIdx = null;
                effectIdx = null;
                strength = 0.5;
                mechanism = "Unknown";
                confounds = new List<string>();
            }
        }

        // Add last relation if exists
        if (causeIdx.HasValue && effectIdx.HasValue)
        {
            relations.Add(new CausalRelation(
                events[causeIdx.Value],
                events[effectIdx.Value],
                strength,
                mechanism,
                confounds));
        }

        return relations;
    }

    private async Task<Result<List<PredictedEvent>, string>> PredictWithLLMAsync(
        IReadOnlyList<TemporalEvent> history,
        TimeSpan horizon,
        CancellationToken ct)
    {
        var prompt = this.BuildPredictionPrompt(history, horizon);
        var response = await this.llm!.GenerateTextAsync(prompt, ct);
        var predictions = this.ParsePredictions(response, history);
        return Result<List<PredictedEvent>, string>.Success(predictions);
    }

    private string BuildPredictionPrompt(IReadOnlyList<TemporalEvent> history, TimeSpan horizon)
    {
        var eventDescriptions = history
            .OrderBy(e => e.StartTime)
            .Select(e => $"- [{e.EventType}] {e.Description} at {e.StartTime:yyyy-MM-dd HH:mm}")
            .ToList();

        var latestTime = history.Max(e => e.EndTime ?? e.StartTime);
        var targetTime = latestTime + horizon;

        return $@"Based on the following event history, predict future events that may occur by {targetTime:yyyy-MM-dd HH:mm}:

Historical Events:
{string.Join("\n", eventDescriptions)}

Prediction horizon: {horizon.TotalHours:F1} hours from the last event.

For each predicted event, provide:
1. Event type
2. Description
3. Predicted time (as hours from now)
4. Confidence (0-1)
5. Reasoning

Format:
TYPE: [event_type]
DESCRIPTION: [description]
TIME: [hours_from_now]
CONFIDENCE: [0-1]
REASONING: [explanation]
---";
    }

    private List<PredictedEvent> ParsePredictions(string response, IReadOnlyList<TemporalEvent> history)
    {
        var predictions = new List<PredictedEvent>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? eventType = null;
        string? description = null;
        double hoursFromNow = 0;
        double confidence = 0.5;
        string reasoning = string.Empty;

        var latestTime = history.Max(e => e.EndTime ?? e.StartTime);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
            {
                eventType = trimmed.Substring("TYPE:".Length).Trim();
            }
            else if (trimmed.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
            {
                description = trimmed.Substring("DESCRIPTION:".Length).Trim();
            }
            else if (trimmed.StartsWith("TIME:", StringComparison.OrdinalIgnoreCase))
            {
                var timeStr = trimmed.Substring("TIME:".Length).Trim();
                if (double.TryParse(timeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var hours))
                {
                    hoursFromNow = hours;
                }
            }
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                var confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var conf))
                {
                    confidence = Math.Clamp(conf, 0.0, 1.0);
                }
            }
            else if (trimmed.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                reasoning = trimmed.Substring("REASONING:".Length).Trim();
            }
            else if (trimmed == "---" && eventType != null && description != null)
            {
                // Complete prediction
                var predictedTime = latestTime.AddHours(hoursFromNow);
                predictions.Add(new PredictedEvent(
                    eventType,
                    description,
                    predictedTime,
                    confidence,
                    history.ToList(),
                    reasoning));

                // Reset for next prediction
                eventType = null;
                description = null;
                hoursFromNow = 0;
                confidence = 0.5;
                reasoning = string.Empty;
            }
        }

        // Add last prediction if exists
        if (eventType != null && description != null)
        {
            var predictedTime = latestTime.AddHours(hoursFromNow);
            predictions.Add(new PredictedEvent(
                eventType,
                description,
                predictedTime,
                confidence,
                history.ToList(),
                reasoning));
        }

        return predictions;
    }

    private List<PredictedEvent> PredictWithPatterns(IReadOnlyList<TemporalEvent> history, TimeSpan horizon)
    {
        var predictions = new List<PredictedEvent>();

        // Group events by type to find patterns
        var eventsByType = history.GroupBy(e => e.EventType).ToList();

        var latestTime = history.Max(e => e.EndTime ?? e.StartTime);
        var targetTime = latestTime + horizon;

        foreach (var group in eventsByType)
        {
            var events = group.OrderBy(e => e.StartTime).ToList();
            if (events.Count < 2)
            {
                continue;
            }

            // Calculate average time between events of this type
            var intervals = new List<TimeSpan>();
            for (int i = 1; i < events.Count; i++)
            {
                intervals.Add(events[i].StartTime - events[i - 1].StartTime);
            }

            var avgInterval = TimeSpan.FromTicks((long)intervals.Average(t => t.Ticks));

            // Predict next occurrence
            var lastEvent = events.Last();
            var predictedTime = (lastEvent.EndTime ?? lastEvent.StartTime) + avgInterval;

            if (predictedTime <= targetTime)
            {
                predictions.Add(new PredictedEvent(
                    group.Key,
                    $"Predicted {group.Key} based on historical pattern",
                    predictedTime,
                    0.6,
                    events,
                    $"Based on average interval of {avgInterval.TotalHours:F1} hours between {events.Count} historical events"));
            }
        }

        return predictions;
    }

    private bool CheckPathConsistency(Dictionary<(Guid, Guid), TemporalRelationType> constraints)
    {
        // Simple path consistency check
        // A more complete implementation would use Allen's composition table
        // For now, we check for obvious contradictions

        var eventIds = constraints.Keys
            .SelectMany(k => new[] { k.Item1, k.Item2 })
            .Distinct()
            .ToList();

        // Check for direct contradictions
        foreach (var key in constraints.Keys)
        {
            var reverseKey = (key.Item2, key.Item1);
            if (constraints.TryGetValue(reverseKey, out var reverseRelation))
            {
                // Check if relations are consistent (inverse of each other)
                if (!this.AreInverseRelations(constraints[key], reverseRelation))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool AreInverseRelations(TemporalRelationType rel1, TemporalRelationType rel2)
    {
        return (rel1, rel2) switch
        {
            (TemporalRelationType.Before, TemporalRelationType.After) => true,
            (TemporalRelationType.After, TemporalRelationType.Before) => true,
            (TemporalRelationType.Meets, TemporalRelationType.MetBy) => true,
            (TemporalRelationType.MetBy, TemporalRelationType.Meets) => true,
            (TemporalRelationType.Overlaps, TemporalRelationType.OverlappedBy) => true,
            (TemporalRelationType.OverlappedBy, TemporalRelationType.Overlaps) => true,
            (TemporalRelationType.During, TemporalRelationType.Contains) => true,
            (TemporalRelationType.Contains, TemporalRelationType.During) => true,
            (TemporalRelationType.Starts, TemporalRelationType.StartedBy) => true,
            (TemporalRelationType.StartedBy, TemporalRelationType.Starts) => true,
            (TemporalRelationType.Finishes, TemporalRelationType.FinishedBy) => true,
            (TemporalRelationType.FinishedBy, TemporalRelationType.Finishes) => true,
            (TemporalRelationType.Equals, TemporalRelationType.Equals) => true,
            _ => false,
        };
    }
}
