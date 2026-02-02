// <copyright file="Affordance.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Core.EmbodiedInteraction;

using Ouroboros.Core.Monads;

/// <summary>
/// Affordance type classification based on ecological psychology.
/// </summary>
public enum AffordanceType
{
    /// <summary>Surface can be traversed (walked on, driven on).</summary>
    Traversable,

    /// <summary>Object can be grasped and manipulated.</summary>
    Graspable,

    /// <summary>Object can be pushed or moved.</summary>
    Pushable,

    /// <summary>Object can be pulled toward agent.</summary>
    Pullable,

    /// <summary>Object can be lifted.</summary>
    Liftable,

    /// <summary>Object can be rotated.</summary>
    Rotatable,

    /// <summary>Space can be entered or occupied.</summary>
    Enterable,

    /// <summary>Object can be climbed.</summary>
    Climbable,

    /// <summary>Object is sittable (chair, bench).</summary>
    Sittable,

    /// <summary>Container can hold objects.</summary>
    Containable,

    /// <summary>Surface provides support for placing objects.</summary>
    Supportive,

    /// <summary>Object can be broken or destroyed.</summary>
    Breakable,

    /// <summary>Object can be combined with others.</summary>
    Combinable,

    /// <summary>Object can be activated (button, lever).</summary>
    Activatable,

    /// <summary>Object blocks movement or view.</summary>
    Obstructive,

    /// <summary>Surface is slippery.</summary>
    Slippery,

    /// <summary>Surface is dangerous.</summary>
    Hazardous,

    /// <summary>Custom affordance type.</summary>
    Custom,
}

/// <summary>
/// Represents an affordance - a potential for action that an object or surface offers.
/// Based on J.J. Gibson's ecological psychology concept of affordances.
/// </summary>
/// <param name="Id">Unique identifier for the affordance.</param>
/// <param name="Type">Classification of the affordance.</param>
/// <param name="TargetObjectId">ID of the object offering the affordance.</param>
/// <param name="ActionVerb">The action verb (grasp, push, sit, etc.).</param>
/// <param name="RequiredCapabilities">Capabilities agent needs to use this affordance.</param>
/// <param name="Confidence">Confidence in affordance detection (0.0-1.0).</param>
/// <param name="Constraints">Physical constraints on the action.</param>
/// <param name="RiskLevel">Estimated risk of using this affordance (0.0-1.0).</param>
/// <param name="EnergyRequired">Estimated energy/effort required.</param>
/// <param name="DetectedAt">When the affordance was detected.</param>
public sealed record Affordance(
    Guid Id,
    AffordanceType Type,
    string TargetObjectId,
    string ActionVerb,
    IReadOnlyList<string> RequiredCapabilities,
    double Confidence,
    AffordanceConstraints Constraints,
    double RiskLevel,
    double EnergyRequired,
    DateTime DetectedAt)
{
    /// <summary>
    /// Creates a new affordance with auto-generated ID and timestamp.
    /// </summary>
    public static Affordance Create(
        AffordanceType type,
        string targetObjectId,
        string actionVerb,
        IReadOnlyList<string>? requiredCapabilities = null,
        double confidence = 1.0,
        AffordanceConstraints? constraints = null,
        double riskLevel = 0.0,
        double energyRequired = 1.0) =>
        new(
            Guid.NewGuid(),
            type,
            targetObjectId,
            actionVerb,
            requiredCapabilities ?? Array.Empty<string>(),
            confidence,
            constraints ?? AffordanceConstraints.None,
            riskLevel,
            energyRequired,
            DateTime.UtcNow);

    /// <summary>
    /// Creates a traversable surface affordance.
    /// </summary>
    public static Affordance Traversable(string surfaceId, double confidence = 1.0) =>
        Create(AffordanceType.Traversable, surfaceId, "walk", confidence: confidence);

    /// <summary>
    /// Creates a graspable object affordance.
    /// </summary>
    public static Affordance Graspable(
        string objectId,
        double confidence = 1.0,
        GripRequirement? grip = null) =>
        Create(
            AffordanceType.Graspable,
            objectId,
            "grasp",
            new[] { "manipulator", "gripper" },
            confidence,
            new AffordanceConstraints(
                MinApproachDistance: grip?.MinApproachDistance ?? 0.1,
                MaxApproachDistance: grip?.MaxApproachDistance ?? 0.5,
                RequiredOrientation: null,
                ForceRange: grip?.ForceRange,
                TimeConstraint: null,
                CustomConstraints: null));

    /// <summary>
    /// Creates an activatable object affordance (button, lever, etc.).
    /// </summary>
    public static Affordance Activatable(string objectId, string activationType = "press") =>
        Create(AffordanceType.Activatable, objectId, activationType);

    /// <summary>
    /// Checks if the agent has the required capabilities.
    /// </summary>
    public bool CanBeUsedBy(IReadOnlySet<string> agentCapabilities)
    {
        foreach (var required in RequiredCapabilities)
        {
            if (!agentCapabilities.Contains(required))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a risk-adjusted confidence score.
    /// </summary>
    public double RiskAdjustedConfidence => Confidence * (1.0 - RiskLevel);
}

/// <summary>
/// Constraints on how an affordance can be used.
/// </summary>
/// <param name="MinApproachDistance">Minimum distance to approach from.</param>
/// <param name="MaxApproachDistance">Maximum distance to approach from.</param>
/// <param name="RequiredOrientation">Required agent orientation (if any).</param>
/// <param name="ForceRange">Required force range (min, max) in Newtons.</param>
/// <param name="TimeConstraint">Time window for action (if any).</param>
/// <param name="CustomConstraints">Additional custom constraints.</param>
public sealed record AffordanceConstraints(
    double? MinApproachDistance,
    double? MaxApproachDistance,
    (double X, double Y, double Z)? RequiredOrientation,
    (double Min, double Max)? ForceRange,
    TimeSpan? TimeConstraint,
    IReadOnlyDictionary<string, object>? CustomConstraints)
{
    /// <summary>
    /// No constraints.
    /// </summary>
    public static AffordanceConstraints None => new(null, null, null, null, null, null);
}

/// <summary>
/// Requirements for grasping an object.
/// </summary>
/// <param name="MinApproachDistance">Minimum approach distance.</param>
/// <param name="MaxApproachDistance">Maximum approach distance.</param>
/// <param name="ForceRange">Required grip force range.</param>
public sealed record GripRequirement(
    double MinApproachDistance,
    double MaxApproachDistance,
    (double Min, double Max)? ForceRange);

/// <summary>
/// Collection of affordances detected in the environment.
/// </summary>
public sealed class AffordanceMap
{
    private readonly Dictionary<string, List<Affordance>> _objectAffordances = new();
    private readonly Dictionary<AffordanceType, List<Affordance>> _typeIndex = new();

    /// <summary>
    /// Gets all affordances in the map.
    /// </summary>
    public IEnumerable<Affordance> All => _objectAffordances.Values.SelectMany(x => x);

    /// <summary>
    /// Gets the count of affordances.
    /// </summary>
    public int Count => _objectAffordances.Values.Sum(x => x.Count);

    /// <summary>
    /// Adds an affordance to the map.
    /// </summary>
    public void Add(Affordance affordance)
    {
        if (!_objectAffordances.TryGetValue(affordance.TargetObjectId, out var objectList))
        {
            objectList = new List<Affordance>();
            _objectAffordances[affordance.TargetObjectId] = objectList;
        }

        objectList.Add(affordance);

        if (!_typeIndex.TryGetValue(affordance.Type, out var typeList))
        {
            typeList = new List<Affordance>();
            _typeIndex[affordance.Type] = typeList;
        }

        typeList.Add(affordance);
    }

    /// <summary>
    /// Gets affordances for a specific object.
    /// </summary>
    public Option<IReadOnlyList<Affordance>> GetForObject(string objectId) =>
        _objectAffordances.TryGetValue(objectId, out var list)
            ? Option<IReadOnlyList<Affordance>>.Some(list)
            : Option<IReadOnlyList<Affordance>>.None();

    /// <summary>
    /// Gets affordances of a specific type.
    /// </summary>
    public IReadOnlyList<Affordance> GetByType(AffordanceType type) =>
        _typeIndex.TryGetValue(type, out var list) ? list : Array.Empty<Affordance>();

    /// <summary>
    /// Finds affordances matching criteria.
    /// </summary>
    public IEnumerable<Affordance> Find(
        Func<Affordance, bool> predicate,
        int? limit = null)
    {
        var query = All.Where(predicate).OrderByDescending(a => a.RiskAdjustedConfidence);
        return limit.HasValue ? query.Take(limit.Value) : query;
    }

    /// <summary>
    /// Gets the most confident affordance for an action verb.
    /// </summary>
    public Option<Affordance> GetBestForAction(string actionVerb) =>
        All.Where(a => a.ActionVerb.Equals(actionVerb, StringComparison.OrdinalIgnoreCase))
           .OrderByDescending(a => a.RiskAdjustedConfidence)
           .FirstOrDefault()
           ?.Let(a => Option<Affordance>.Some(a)) ?? Option<Affordance>.None();

    /// <summary>
    /// Clears all affordances.
    /// </summary>
    public void Clear()
    {
        _objectAffordances.Clear();
        _typeIndex.Clear();
    }

    /// <summary>
    /// Removes stale affordances older than the specified age.
    /// </summary>
    public int RemoveStale(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var removed = 0;

        foreach (var list in _objectAffordances.Values)
        {
            removed += list.RemoveAll(a => a.DetectedAt < cutoff);
        }

        foreach (var list in _typeIndex.Values)
        {
            list.RemoveAll(a => a.DetectedAt < cutoff);
        }

        return removed;
    }
}

/// <summary>
/// Extension methods for affordances.
/// </summary>
public static class AffordanceExtensions
{
    /// <summary>
    /// Functional let binding.
    /// </summary>
    public static TResult Let<T, TResult>(this T value, Func<T, TResult> func) => func(value);
}
