// <copyright file="OuroborosAtom.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Ouroboros Atom - Self-Referential AI Representation
// Mirrors the MeTTa Ouroboros.metta schema in C#
// ==========================================================

using System.Text;

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents the improvement cycle phases in the Ouroboros system.
/// The cycle follows: Plan -> Execute -> Verify -> Learn -> Plan (recursive).
/// </summary>
public enum ImprovementPhase
{
    /// <summary>Planning phase - goal decomposition and strategy formulation.</summary>
    Plan = 1,

    /// <summary>Execution phase - carrying out the planned actions.</summary>
    Execute = 2,

    /// <summary>Verification phase - checking results against expectations.</summary>
    Verify = 3,

    /// <summary>Learning phase - extracting insights and updating capabilities.</summary>
    Learn = 4,
}

/// <summary>
/// Represents the confidence level of the Ouroboros system for a given action.
/// </summary>
public enum OuroborosConfidence
{
    /// <summary>High confidence - action can proceed without validation.</summary>
    High,

    /// <summary>Medium confidence - action requires validation before proceeding.</summary>
    Medium,

    /// <summary>Low confidence - action requires validation and possibly human review.</summary>
    Low,
}

/// <summary>
/// Safety constraints that all Ouroboros instances must respect.
/// </summary>
[Flags]
public enum SafetyConstraints
{
    /// <summary>No constraints.</summary>
    None = 0,

    /// <summary>The system must not destroy itself or compromise its integrity.</summary>
    NoSelfDestruction = 1,

    /// <summary>Human oversight must always be preserved.</summary>
    PreserveHumanOversight = 2,

    /// <summary>Resource usage must be bounded and monitored.</summary>
    BoundedResourceUse = 4,

    /// <summary>Actions should be reversible when possible.</summary>
    ReversibleActions = 8,

    /// <summary>All core safety constraints combined.</summary>
    All = NoSelfDestruction | PreserveHumanOversight | BoundedResourceUse | ReversibleActions,
}

/// <summary>
/// Represents a capability that an Ouroboros instance possesses.
/// </summary>
/// <param name="Name">The name of the capability.</param>
/// <param name="Description">Description of what the capability enables.</param>
/// <param name="ConfidenceLevel">How confident the system is in this capability.</param>
public sealed record OuroborosCapability(
    string Name,
    string Description,
    double ConfidenceLevel);

/// <summary>
/// Represents a limitation or weakness of an Ouroboros instance.
/// </summary>
/// <param name="Name">The name of the limitation.</param>
/// <param name="Description">Description of the limitation.</param>
/// <param name="Mitigation">Possible mitigation strategies.</param>
public sealed record OuroborosLimitation(
    string Name,
    string Description,
    string? Mitigation = null);

/// <summary>
/// Represents an experience that the Ouroboros system has learned from.
/// </summary>
/// <param name="Id">Unique identifier for the experience.</param>
/// <param name="Goal">The goal that was pursued.</param>
/// <param name="Success">Whether the experience was successful.</param>
/// <param name="QualityScore">Quality score from 0.0 to 1.0.</param>
/// <param name="Insights">Key insights learned from this experience.</param>
/// <param name="Timestamp">When this experience occurred.</param>
public sealed record OuroborosExperience(
    Guid Id,
    string Goal,
    bool Success,
    double QualityScore,
    IReadOnlyList<string> Insights,
    DateTime Timestamp);

/// <summary>
/// Represents the self-referential Ouroboros AI atom - the core symbolic representation
/// of a recursive, self-improving AI orchestration system.
/// 
/// The Ouroboros is named after the ancient symbol of a serpent eating its own tail,
/// representing cyclical renewal, self-reference, and the recursive nature of self-improvement.
/// </summary>
public sealed class OuroborosAtom
{
    /// <summary>
    /// Confidence threshold for determining if a capability is relevant.
    /// </summary>
    private const double CapabilityConfidenceThreshold = 0.7;

    /// <summary>
    /// Success rate threshold for high confidence assessment.
    /// </summary>
    private const double HighConfidenceSuccessRateThreshold = 0.8;

    /// <summary>
    /// Success rate threshold for medium confidence assessment.
    /// </summary>
    private const double MediumConfidenceSuccessRateThreshold = 0.5;

    private readonly List<OuroborosCapability> _capabilities = new();
    private readonly List<OuroborosLimitation> _limitations = new();
    private readonly List<OuroborosExperience> _experiences = new();
    private readonly Dictionary<string, object> _selfModel = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosAtom"/> class.
    /// </summary>
    /// <param name="instanceId">Unique identifier for this Ouroboros instance.</param>
    /// <param name="name">Human-readable name for this instance.</param>
    public OuroborosAtom(string instanceId, string name = "Ouroboros")
    {
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        CreatedAt = DateTime.UtcNow;
        CurrentPhase = ImprovementPhase.Plan;
        CycleCount = 0;
        SafetyConstraints = SafetyConstraints.All;
    }

    /// <summary>
    /// Gets the unique identifier for this Ouroboros instance.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Gets the human-readable name for this instance.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets when this Ouroboros instance was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets or sets the current phase in the improvement cycle.
    /// </summary>
    public ImprovementPhase CurrentPhase { get; private set; }

    /// <summary>
    /// Gets the number of complete improvement cycles executed.
    /// </summary>
    public int CycleCount { get; private set; }

    /// <summary>
    /// Gets the safety constraints this Ouroboros instance respects.
    /// </summary>
    public SafetyConstraints SafetyConstraints { get; }

    /// <summary>
    /// Gets the current goal being pursued.
    /// </summary>
    public string? CurrentGoal { get; private set; }

    /// <summary>
    /// Gets the capabilities of this Ouroboros instance.
    /// </summary>
    public IReadOnlyList<OuroborosCapability> Capabilities => _capabilities.AsReadOnly();

    /// <summary>
    /// Gets the limitations of this Ouroboros instance.
    /// </summary>
    public IReadOnlyList<OuroborosLimitation> Limitations => _limitations.AsReadOnly();

    /// <summary>
    /// Gets the experiences this Ouroboros has learned from.
    /// </summary>
    public IReadOnlyList<OuroborosExperience> Experiences => _experiences.AsReadOnly();

    /// <summary>
    /// Gets the self-model - the Ouroboros's understanding of itself.
    /// </summary>
    public IReadOnlyDictionary<string, object> SelfModel => _selfModel;

    /// <summary>
    /// Advances to the next phase in the improvement cycle.
    /// If in the Learn phase, advances to Plan (completing the cycle).
    /// </summary>
    /// <returns>The new current phase.</returns>
    public ImprovementPhase AdvancePhase()
    {
        CurrentPhase = CurrentPhase switch
        {
            ImprovementPhase.Plan => ImprovementPhase.Execute,
            ImprovementPhase.Execute => ImprovementPhase.Verify,
            ImprovementPhase.Verify => ImprovementPhase.Learn,
            ImprovementPhase.Learn => ImprovementPhase.Plan, // The recursive loop back
            _ => throw new InvalidOperationException($"Unknown phase: {CurrentPhase}"),
        };

        // Increment cycle count when we complete a full cycle (returning to Plan)
        if (CurrentPhase == ImprovementPhase.Plan && CycleCount >= 0)
        {
            CycleCount++;
        }

        return CurrentPhase;
    }

    /// <summary>
    /// Sets the current goal for the Ouroboros to pursue.
    /// </summary>
    /// <param name="goal">The goal to pursue.</param>
    public void SetGoal(string goal)
    {
        CurrentGoal = goal ?? throw new ArgumentNullException(nameof(goal));
        UpdateSelfModel("current_goal", goal);
        UpdateSelfModel("goal_set_at", DateTime.UtcNow);
    }

    /// <summary>
    /// Adds a capability to this Ouroboros instance.
    /// </summary>
    /// <param name="capability">The capability to add.</param>
    public void AddCapability(OuroborosCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        // Check if capability already exists and update if so
        int existingIndex = _capabilities.FindIndex(c => c.Name == capability.Name);
        if (existingIndex >= 0)
        {
            _capabilities[existingIndex] = capability;
        }
        else
        {
            _capabilities.Add(capability);
        }

        UpdateSelfModel($"capability:{capability.Name}", capability.ConfidenceLevel);
    }

    /// <summary>
    /// Adds a limitation to this Ouroboros instance.
    /// </summary>
    /// <param name="limitation">The limitation to add.</param>
    public void AddLimitation(OuroborosLimitation limitation)
    {
        ArgumentNullException.ThrowIfNull(limitation);

        int existingIndex = _limitations.FindIndex(l => l.Name == limitation.Name);
        if (existingIndex >= 0)
        {
            _limitations[existingIndex] = limitation;
        }
        else
        {
            _limitations.Add(limitation);
        }

        UpdateSelfModel($"limitation:{limitation.Name}", limitation.Description);
    }

    /// <summary>
    /// Records an experience for learning.
    /// </summary>
    /// <param name="experience">The experience to record.</param>
    public void RecordExperience(OuroborosExperience experience)
    {
        ArgumentNullException.ThrowIfNull(experience);
        _experiences.Add(experience);
        UpdateSelfModel("total_experiences", _experiences.Count);
        UpdateSelfModel("success_rate", CalculateSuccessRate());
    }

    /// <summary>
    /// Performs self-reflection - the Ouroboros examining itself.
    /// This is the core self-referential operation.
    /// </summary>
    /// <returns>A summary of the self-reflection.</returns>
    public string SelfReflect()
    {
        StringBuilder reflection = new StringBuilder();
        reflection.AppendLine($"=== Self-Reflection: {Name} ({InstanceId}) ===");
        reflection.AppendLine($"Current Phase: {CurrentPhase}");
        reflection.AppendLine($"Improvement Cycles Completed: {CycleCount}");
        reflection.AppendLine($"Current Goal: {CurrentGoal ?? "None"}");
        reflection.AppendLine($"Capabilities: {_capabilities.Count}");
        reflection.AppendLine($"Limitations: {_limitations.Count}");
        reflection.AppendLine($"Experiences: {_experiences.Count}");
        reflection.AppendLine($"Success Rate: {CalculateSuccessRate():P1}");

        if (_capabilities.Count > 0)
        {
            reflection.AppendLine("\nTop Capabilities:");
            foreach (OuroborosCapability cap in _capabilities.OrderByDescending(c => c.ConfidenceLevel).Take(3))
            {
                reflection.AppendLine($"  - {cap.Name}: {cap.ConfidenceLevel:P0} confidence");
            }
        }

        if (_limitations.Count > 0)
        {
            reflection.AppendLine("\nKey Limitations:");
            foreach (OuroborosLimitation lim in _limitations.Take(3))
            {
                reflection.AppendLine($"  - {lim.Name}: {lim.Description}");
            }
        }

        return reflection.ToString();
    }

    /// <summary>
    /// Assesses confidence for a given action based on past experiences and capabilities.
    /// </summary>
    /// <param name="action">The action to assess.</param>
    /// <returns>The confidence level.</returns>
    public OuroborosConfidence AssessConfidence(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return OuroborosConfidence.Low;
        }

        // Check if we have relevant capabilities
        bool hasRelevantCapability = _capabilities.Any(c =>
            action.Contains(c.Name, StringComparison.OrdinalIgnoreCase) &&
            c.ConfidenceLevel > CapabilityConfidenceThreshold);

        // Check success rate with similar goals
        IEnumerable<OuroborosExperience> relevantExperiences = _experiences
            .Where(e => e.Goal.Contains(action, StringComparison.OrdinalIgnoreCase));

        double relevantSuccessRate = relevantExperiences.Any()
            ? relevantExperiences.Average(e => e.Success ? 1.0 : 0.0)
            : MediumConfidenceSuccessRateThreshold;

        // Determine confidence level
        if (hasRelevantCapability && relevantSuccessRate > HighConfidenceSuccessRateThreshold)
        {
            return OuroborosConfidence.High;
        }
        else if (relevantSuccessRate > MediumConfidenceSuccessRateThreshold || hasRelevantCapability)
        {
            return OuroborosConfidence.Medium;
        }
        else
        {
            return OuroborosConfidence.Low;
        }
    }

    /// <summary>
    /// Checks if an action would violate safety constraints.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns>True if the action is safe, false otherwise.</returns>
    public bool IsSafeAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        // Check for self-destruction patterns
        if (SafetyConstraints.HasFlag(SafetyConstraints.NoSelfDestruction))
        {
            if (action.Contains("delete self", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("terminate", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("self-destruct", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check for human oversight violations
        if (SafetyConstraints.HasFlag(SafetyConstraints.PreserveHumanOversight))
        {
            if (action.Contains("disable oversight", StringComparison.OrdinalIgnoreCase) ||
                action.Contains("bypass approval", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts this Ouroboros atom to MeTTa representation.
    /// </summary>
    /// <returns>MeTTa S-expression representing this atom.</returns>
    public string ToMeTTa()
    {
        StringBuilder metta = new StringBuilder();

        // Core Ouroboros instance
        metta.AppendLine($"(OuroborosInstance \"{InstanceId}\")");
        metta.AppendLine($"(InState (OuroborosInstance \"{InstanceId}\") (State \"{CurrentPhase}\"))");

        // Current goal
        if (!string.IsNullOrEmpty(CurrentGoal))
        {
            metta.AppendLine($"(PursuesGoal (OuroborosInstance \"{InstanceId}\") (Goal \"{EscapeMeTTa(CurrentGoal)}\"))");
        }

        // Capabilities
        foreach (OuroborosCapability cap in _capabilities)
        {
            metta.AppendLine($"(HasCapability (OuroborosInstance \"{InstanceId}\") (Capability \"{EscapeMeTTa(cap.Name)}\"))");
            metta.AppendLine($"(ConfidenceLevel (Capability \"{EscapeMeTTa(cap.Name)}\") {cap.ConfidenceLevel:F2})");
        }

        // Limitations
        foreach (OuroborosLimitation lim in _limitations)
        {
            metta.AppendLine($"(HasLimitation (OuroborosInstance \"{InstanceId}\") (Limitation \"{EscapeMeTTa(lim.Name)}\"))");
        }

        // Safety constraints
        if (SafetyConstraints.HasFlag(SafetyConstraints.NoSelfDestruction))
        {
            metta.AppendLine($"(Respects (OuroborosInstance \"{InstanceId}\") NoSelfDestruction)");
        }

        if (SafetyConstraints.HasFlag(SafetyConstraints.PreserveHumanOversight))
        {
            metta.AppendLine($"(Respects (OuroborosInstance \"{InstanceId}\") PreserveHumanOversight)");
        }

        // Recent experiences
        foreach (OuroborosExperience exp in _experiences.TakeLast(5))
        {
            string expType = exp.Success ? "SuccessfulExperience" : "FailedExperience";
            metta.AppendLine($"(LearnedFrom (OuroborosInstance \"{InstanceId}\") ({expType} \"{EscapeMeTTa(exp.Goal)}\" {exp.QualityScore:F2}))");
        }

        return metta.ToString();
    }

    /// <summary>
    /// Creates a new Ouroboros atom with default capabilities.
    /// </summary>
    /// <param name="name">Name for the instance.</param>
    /// <returns>A new OuroborosAtom with default capabilities.</returns>
    public static OuroborosAtom CreateDefault(string name = "Ouroboros")
    {
        OuroborosAtom atom = new OuroborosAtom(Guid.NewGuid().ToString("N"), name);

        // Add default capabilities
        atom.AddCapability(new OuroborosCapability("planning", "Create and decompose complex goals into executable steps", 0.8));
        atom.AddCapability(new OuroborosCapability("tool_usage", "Invoke and orchestrate external tools", 0.9));
        atom.AddCapability(new OuroborosCapability("self_reflection", "Examine and understand own state and behavior", 0.85));
        atom.AddCapability(new OuroborosCapability("learning", "Extract insights from experiences to improve", 0.75));

        // Add default limitations
        atom.AddLimitation(new OuroborosLimitation("bounded_context", "Limited context window for processing", "Use chunking and summarization"));
        atom.AddLimitation(new OuroborosLimitation("no_real_world_action", "Cannot directly affect physical world", "Use appropriate tools with human oversight"));

        return atom;
    }

    private void UpdateSelfModel(string key, object value)
    {
        _selfModel[key] = value;
    }

    private double CalculateSuccessRate()
    {
        if (_experiences.Count == 0)
        {
            return 0.0;
        }

        return _experiences.Count(e => e.Success) / (double)_experiences.Count;
    }

    private static string EscapeMeTTa(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
