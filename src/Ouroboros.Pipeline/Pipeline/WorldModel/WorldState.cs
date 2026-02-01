// <copyright file="WorldState.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;

/// <summary>
/// Represents an observation in the world state with associated metadata.
/// </summary>
/// <param name="Value">The observed value.</param>
/// <param name="Confidence">Confidence score between 0.0 and 1.0.</param>
/// <param name="Timestamp">When the observation was recorded.</param>
public sealed record Observation(
    object Value,
    double Confidence,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a new observation with the current timestamp.
    /// </summary>
    /// <param name="value">The observed value.</param>
    /// <param name="confidence">Confidence score between 0.0 and 1.0.</param>
    /// <returns>A new observation.</returns>
    public static Observation Create(object value, double confidence)
    {
        ArgumentNullException.ThrowIfNull(value);

        double clampedConfidence = Math.Clamp(confidence, 0.0, 1.0);
        return new Observation(value, clampedConfidence, DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new observation with full confidence.
    /// </summary>
    /// <param name="value">The observed value.</param>
    /// <returns>A new observation with confidence 1.0.</returns>
    public static Observation Certain(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Observation(value, 1.0, DateTime.UtcNow);
    }

    /// <summary>
    /// Gets the value as a specific type if possible.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>Option containing the typed value if cast succeeds.</returns>
    public Option<T> GetValueAs<T>()
    {
        return Value is T typedValue
            ? Option<T>.Some(typedValue)
            : Option<T>.None();
    }
}

/// <summary>
/// Represents a capability that the system can perform.
/// </summary>
/// <param name="Name">Unique identifier for the capability.</param>
/// <param name="Description">Human-readable description of what the capability does.</param>
/// <param name="RequiredTools">List of tool names required to perform this capability.</param>
public sealed record Capability(
    string Name,
    string Description,
    ImmutableList<string> RequiredTools)
{
    /// <summary>
    /// Creates a capability with no required tools.
    /// </summary>
    /// <param name="name">Capability name.</param>
    /// <param name="description">Capability description.</param>
    /// <returns>A new capability.</returns>
    public static Capability Create(string name, string description)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        return new Capability(name, description, ImmutableList<string>.Empty);
    }

    /// <summary>
    /// Creates a capability with required tools.
    /// </summary>
    /// <param name="name">Capability name.</param>
    /// <param name="description">Capability description.</param>
    /// <param name="requiredTools">Tools required for this capability.</param>
    /// <returns>A new capability.</returns>
    public static Capability Create(string name, string description, params string[] requiredTools)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(requiredTools);

        return new Capability(name, description, requiredTools.ToImmutableList());
    }

    /// <summary>
    /// Checks if all required tools are available in the provided tool set.
    /// </summary>
    /// <param name="availableTools">Set of available tool names.</param>
    /// <returns>True if all required tools are available.</returns>
    public bool CanExecuteWith(IReadOnlySet<string> availableTools)
    {
        ArgumentNullException.ThrowIfNull(availableTools);

        return RequiredTools.All(availableTools.Contains);
    }
}

/// <summary>
/// Represents a constraint or rule that limits system behavior.
/// </summary>
/// <param name="Name">Unique identifier for the constraint.</param>
/// <param name="Rule">Description or expression of the constraint rule.</param>
/// <param name="Priority">Priority level (higher values = higher priority).</param>
public sealed record Constraint(
    string Name,
    string Rule,
    int Priority)
{
    /// <summary>
    /// Creates a constraint with default priority (0).
    /// </summary>
    /// <param name="name">Constraint name.</param>
    /// <param name="rule">Constraint rule.</param>
    /// <returns>A new constraint.</returns>
    public static Constraint Create(string name, string rule)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rule);

        return new Constraint(name, rule, 0);
    }

    /// <summary>
    /// Creates a constraint with specified priority.
    /// </summary>
    /// <param name="name">Constraint name.</param>
    /// <param name="rule">Constraint rule.</param>
    /// <param name="priority">Priority level.</param>
    /// <returns>A new constraint.</returns>
    public static Constraint Create(string name, string rule, int priority)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rule);

        return new Constraint(name, rule, priority);
    }

    /// <summary>
    /// Creates a high-priority constraint (priority 100).
    /// </summary>
    /// <param name="name">Constraint name.</param>
    /// <param name="rule">Constraint rule.</param>
    /// <returns>A new high-priority constraint.</returns>
    public static Constraint Critical(string name, string rule)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rule);

        return new Constraint(name, rule, 100);
    }
}

/// <summary>
/// Represents the current state of the world as understood by the AI system.
/// Immutable record that tracks observations, capabilities, and constraints.
/// Follows functional programming principles with monadic operations.
/// </summary>
/// <param name="Observations">Dictionary of key-value observations about the world.</param>
/// <param name="Capabilities">List of capabilities the system can perform.</param>
/// <param name="Constraints">List of active constraints limiting behavior.</param>
/// <param name="LastUpdated">Timestamp of the last state update.</param>
public sealed record WorldState(
    ImmutableDictionary<string, Observation> Observations,
    ImmutableList<Capability> Capabilities,
    ImmutableList<Constraint> Constraints,
    DateTime LastUpdated)
{
    /// <summary>
    /// Creates an empty world state.
    /// </summary>
    /// <returns>A new empty world state.</returns>
    public static WorldState Empty() => new(
        ImmutableDictionary<string, Observation>.Empty,
        ImmutableList<Capability>.Empty,
        ImmutableList<Constraint>.Empty,
        DateTime.UtcNow);

    /// <summary>
    /// Creates a world state with initial observations.
    /// </summary>
    /// <param name="initialObservations">Initial key-value observations.</param>
    /// <returns>A new world state with observations.</returns>
    public static WorldState FromObservations(IEnumerable<KeyValuePair<string, object>> initialObservations)
    {
        ArgumentNullException.ThrowIfNull(initialObservations);

        ImmutableDictionary<string, Observation>.Builder builder = ImmutableDictionary.CreateBuilder<string, Observation>();

        foreach (KeyValuePair<string, object> kvp in initialObservations)
        {
            builder[kvp.Key] = Observation.Certain(kvp.Value);
        }

        return new WorldState(
            builder.ToImmutable(),
            ImmutableList<Capability>.Empty,
            ImmutableList<Constraint>.Empty,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Adds or updates an observation in the world state.
    /// </summary>
    /// <param name="key">The observation key.</param>
    /// <param name="value">The observed value.</param>
    /// <param name="confidence">Confidence score between 0.0 and 1.0.</param>
    /// <returns>A new world state with the observation added or updated.</returns>
    public WorldState WithObservation(string key, object value, double confidence)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        Observation observation = Observation.Create(value, confidence);
        return this with
        {
            Observations = Observations.SetItem(key, observation),
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Adds or updates an observation with full confidence.
    /// </summary>
    /// <param name="key">The observation key.</param>
    /// <param name="value">The observed value.</param>
    /// <returns>A new world state with the observation added or updated.</returns>
    public WorldState WithObservation(string key, object value)
    {
        return WithObservation(key, value, 1.0);
    }

    /// <summary>
    /// Adds a capability to the world state.
    /// </summary>
    /// <param name="capability">The capability to add.</param>
    /// <returns>A new world state with the capability added.</returns>
    public WorldState WithCapability(Capability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        // Check if capability already exists by name
        bool exists = Capabilities.Any(c => c.Name == capability.Name);
        if (exists)
        {
            // Replace existing capability
            ImmutableList<Capability> updated = Capabilities
                .Select(c => c.Name == capability.Name ? capability : c)
                .ToImmutableList();

            return this with
            {
                Capabilities = updated,
                LastUpdated = DateTime.UtcNow,
            };
        }

        return this with
        {
            Capabilities = Capabilities.Add(capability),
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Adds a constraint to the world state.
    /// </summary>
    /// <param name="constraint">The constraint to add.</param>
    /// <returns>A new world state with the constraint added.</returns>
    public WorldState WithConstraint(Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        // Check if constraint already exists by name
        bool exists = Constraints.Any(c => c.Name == constraint.Name);
        if (exists)
        {
            // Replace existing constraint
            ImmutableList<Constraint> updated = Constraints
                .Select(c => c.Name == constraint.Name ? constraint : c)
                .ToImmutableList();

            return this with
            {
                Constraints = updated,
                LastUpdated = DateTime.UtcNow,
            };
        }

        return this with
        {
            Constraints = Constraints.Add(constraint),
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Removes an observation from the world state.
    /// </summary>
    /// <param name="key">The observation key to remove.</param>
    /// <returns>A new world state without the observation.</returns>
    public WorldState WithoutObservation(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return this with
        {
            Observations = Observations.Remove(key),
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Removes a capability from the world state.
    /// </summary>
    /// <param name="capabilityName">The name of the capability to remove.</param>
    /// <returns>A new world state without the capability.</returns>
    public WorldState WithoutCapability(string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(capabilityName);

        return this with
        {
            Capabilities = Capabilities.RemoveAll(c => c.Name == capabilityName),
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Removes a constraint from the world state.
    /// </summary>
    /// <param name="constraintName">The name of the constraint to remove.</param>
    /// <returns>A new world state without the constraint.</returns>
    public WorldState WithoutConstraint(string constraintName)
    {
        ArgumentNullException.ThrowIfNull(constraintName);

        return this with
        {
            Constraints = Constraints.RemoveAll(c => c.Name == constraintName),
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Gets an observation by key.
    /// </summary>
    /// <param name="key">The observation key.</param>
    /// <returns>Option containing the observation if found.</returns>
    public Option<Observation> GetObservation(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return Observations.TryGetValue(key, out Observation? observation)
            ? Option<Observation>.Some(observation)
            : Option<Observation>.None();
    }

    /// <summary>
    /// Gets an observation value as a specific type.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The observation key.</param>
    /// <returns>Option containing the typed value if found and castable.</returns>
    public Option<T> GetObservationValue<T>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return GetObservation(key).Bind(obs => obs.GetValueAs<T>());
    }

    /// <summary>
    /// Checks if a capability exists by name.
    /// </summary>
    /// <param name="capabilityName">The capability name to check.</param>
    /// <returns>True if the capability exists.</returns>
    public bool HasCapability(string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(capabilityName);

        return Capabilities.Any(c => c.Name == capabilityName);
    }

    /// <summary>
    /// Gets a capability by name.
    /// </summary>
    /// <param name="capabilityName">The capability name.</param>
    /// <returns>Option containing the capability if found.</returns>
    public Option<Capability> GetCapability(string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(capabilityName);

        Capability? capability = Capabilities.FirstOrDefault(c => c.Name == capabilityName);
        return capability is not null
            ? Option<Capability>.Some(capability)
            : Option<Capability>.None();
    }

    /// <summary>
    /// Checks if a constraint exists by name.
    /// </summary>
    /// <param name="constraintName">The constraint name to check.</param>
    /// <returns>True if the constraint exists.</returns>
    public bool HasConstraint(string constraintName)
    {
        ArgumentNullException.ThrowIfNull(constraintName);

        return Constraints.Any(c => c.Name == constraintName);
    }

    /// <summary>
    /// Gets a constraint by name.
    /// </summary>
    /// <param name="constraintName">The constraint name.</param>
    /// <returns>Option containing the constraint if found.</returns>
    public Option<Constraint> GetConstraint(string constraintName)
    {
        ArgumentNullException.ThrowIfNull(constraintName);

        Constraint? constraint = Constraints.FirstOrDefault(c => c.Name == constraintName);
        return constraint is not null
            ? Option<Constraint>.Some(constraint)
            : Option<Constraint>.None();
    }

    /// <summary>
    /// Gets all constraints ordered by priority (highest first).
    /// </summary>
    /// <returns>Constraints ordered by priority descending.</returns>
    public IEnumerable<Constraint> GetConstraintsByPriority()
    {
        return Constraints.OrderByDescending(c => c.Priority);
    }

    /// <summary>
    /// Gets all observations with confidence above a threshold.
    /// </summary>
    /// <param name="minimumConfidence">Minimum confidence threshold.</param>
    /// <returns>Key-value pairs of high-confidence observations.</returns>
    public IEnumerable<KeyValuePair<string, Observation>> GetHighConfidenceObservations(double minimumConfidence)
    {
        return Observations.Where(kvp => kvp.Value.Confidence >= minimumConfidence);
    }

    /// <summary>
    /// Gets the average confidence across all observations.
    /// </summary>
    /// <returns>Option containing average confidence, or None if no observations.</returns>
    public Option<double> GetAverageConfidence()
    {
        return Observations.Count > 0
            ? Option<double>.Some(Observations.Values.Average(o => o.Confidence))
            : Option<double>.None();
    }

    /// <summary>
    /// Converts this world state to an Option.
    /// Returns None if the state has no observations, capabilities, or constraints.
    /// </summary>
    /// <returns>Option containing this world state if it has content.</returns>
    public Option<WorldState> ToOption()
    {
        bool hasContent = Observations.Count > 0 || Capabilities.Count > 0 || Constraints.Count > 0;
        return hasContent
            ? Option<WorldState>.Some(this)
            : Option<WorldState>.None();
    }

    /// <summary>
    /// Merges another world state into this one.
    /// Observations, capabilities, and constraints from the other state take precedence.
    /// </summary>
    /// <param name="other">The world state to merge.</param>
    /// <returns>A new merged world state.</returns>
    public WorldState Merge(WorldState other)
    {
        ArgumentNullException.ThrowIfNull(other);

        ImmutableDictionary<string, Observation>.Builder observationsBuilder = Observations.ToBuilder();
        foreach (KeyValuePair<string, Observation> kvp in other.Observations)
        {
            observationsBuilder[kvp.Key] = kvp.Value;
        }

        HashSet<string> existingCapabilities = Capabilities.Select(c => c.Name).ToHashSet();
        ImmutableList<Capability>.Builder capabilitiesBuilder = Capabilities.ToBuilder();
        foreach (Capability capability in other.Capabilities)
        {
            if (existingCapabilities.Contains(capability.Name))
            {
                int index = capabilitiesBuilder.FindIndex(c => c.Name == capability.Name);
                capabilitiesBuilder[index] = capability;
            }
            else
            {
                capabilitiesBuilder.Add(capability);
            }
        }

        HashSet<string> existingConstraints = Constraints.Select(c => c.Name).ToHashSet();
        ImmutableList<Constraint>.Builder constraintsBuilder = Constraints.ToBuilder();
        foreach (Constraint constraint in other.Constraints)
        {
            if (existingConstraints.Contains(constraint.Name))
            {
                int index = constraintsBuilder.FindIndex(c => c.Name == constraint.Name);
                constraintsBuilder[index] = constraint;
            }
            else
            {
                constraintsBuilder.Add(constraint);
            }
        }

        return new WorldState(
            observationsBuilder.ToImmutable(),
            capabilitiesBuilder.ToImmutable(),
            constraintsBuilder.ToImmutable(),
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a snapshot of the current state with a new timestamp.
    /// </summary>
    /// <returns>A copy of this world state with current timestamp.</returns>
    public WorldState Snapshot()
    {
        return this with { LastUpdated = DateTime.UtcNow };
    }
}
