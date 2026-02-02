// <copyright file="ExperienceReplay.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;

/// <summary>
/// Represents a single experience for replay-based learning.
/// Captures the state-action-reward-next_state tuple essential for reinforcement learning.
/// </summary>
/// <param name="Id">Unique identifier for this experience.</param>
/// <param name="State">The input state or context before the action.</param>
/// <param name="Action">The action taken in response to the state.</param>
/// <param name="Reward">The feedback score received for the action (typically in range [-1, 1] or [0, 1]).</param>
/// <param name="NextState">The resulting state after the action was taken.</param>
/// <param name="Timestamp">When this experience was recorded.</param>
/// <param name="Metadata">Additional contextual information about the experience.</param>
/// <param name="Priority">Priority weight for prioritized replay sampling (higher = more likely to be sampled).</param>
public sealed record Experience(
    Guid Id,
    string State,
    string Action,
    double Reward,
    string NextState,
    DateTime Timestamp,
    ImmutableDictionary<string, object> Metadata,
    double Priority)
{
    /// <summary>
    /// Creates a new experience with auto-generated ID and current timestamp.
    /// </summary>
    /// <param name="state">The input state or context.</param>
    /// <param name="action">The action taken.</param>
    /// <param name="reward">The feedback score.</param>
    /// <param name="nextState">The resulting state.</param>
    /// <param name="priority">Priority for replay sampling (default: 1.0).</param>
    /// <param name="metadata">Optional metadata dictionary.</param>
    /// <returns>A new Experience instance.</returns>
    public static Experience Create(
        string state,
        string action,
        double reward,
        string nextState,
        double priority = 1.0,
        ImmutableDictionary<string, object>? metadata = null)
        => new(
            Guid.NewGuid(),
            state,
            action,
            reward,
            nextState,
            DateTime.UtcNow,
            metadata ?? ImmutableDictionary<string, object>.Empty,
            priority);

    /// <summary>
    /// Creates a new experience with adjusted priority based on TD-error.
    /// </summary>
    /// <param name="tdError">The temporal difference error magnitude.</param>
    /// <param name="epsilon">Small constant to ensure non-zero priority (default: 0.01).</param>
    /// <returns>A new Experience with updated priority.</returns>
    public Experience WithTDErrorPriority(double tdError, double epsilon = 0.01)
        => this with { Priority = Math.Abs(tdError) + epsilon };

    /// <summary>
    /// Creates a copy with updated metadata.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new Experience with the added metadata.</returns>
    public Experience WithMetadata(string key, object value)
        => this with { Metadata = Metadata.SetItem(key, value) };
}

/// <summary>
/// Interface for experience buffer operations supporting replay-based learning.
/// </summary>
public interface IExperienceBuffer
{
    /// <summary>
    /// Gets the current number of experiences in the buffer.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Adds an experience to the buffer.
    /// If the buffer is at capacity, the oldest experience is evicted (FIFO).
    /// </summary>
    /// <param name="experience">The experience to add.</param>
    void Add(Experience experience);

    /// <summary>
    /// Samples a batch of experiences uniformly at random.
    /// </summary>
    /// <param name="batchSize">The number of experiences to sample.</param>
    /// <returns>A list of randomly sampled experiences.</returns>
    IReadOnlyList<Experience> Sample(int batchSize);

    /// <summary>
    /// Samples a batch of experiences using prioritized replay.
    /// Uses softmax distribution over priorities weighted by alpha.
    /// </summary>
    /// <param name="batchSize">The number of experiences to sample.</param>
    /// <param name="alpha">Temperature parameter controlling priority influence (0 = uniform, 1 = fully prioritized).</param>
    /// <returns>A list of priority-weighted sampled experiences.</returns>
    IReadOnlyList<Experience> SamplePrioritized(int batchSize, double alpha = 0.6);

    /// <summary>
    /// Clears all experiences from the buffer.
    /// </summary>
    void Clear();

    /// <summary>
    /// Updates the priority of an experience by ID.
    /// </summary>
    /// <param name="experienceId">The ID of the experience to update.</param>
    /// <param name="newPriority">The new priority value.</param>
    /// <returns>True if the experience was found and updated.</returns>
    bool UpdatePriority(Guid experienceId, double newPriority);
}

/// <summary>
/// Thread-safe experience buffer with fixed capacity and FIFO eviction.
/// Supports both uniform and prioritized experience replay sampling.
/// </summary>
public sealed class ExperienceBuffer : IExperienceBuffer
{
    private readonly object _lock = new();
    private readonly LinkedList<Experience> _experiences = new();
    private readonly Dictionary<Guid, LinkedListNode<Experience>> _index = new();
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperienceBuffer"/> class.
    /// </summary>
    /// <param name="capacity">Maximum number of experiences to store.</param>
    /// <param name="seed">Optional random seed for reproducible sampling.</param>
    public ExperienceBuffer(int capacity = 10000, int? seed = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        }

        Capacity = capacity;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _experiences.Count;
            }
        }
    }

    /// <inheritdoc/>
    public int Capacity { get; }

    /// <inheritdoc/>
    public void Add(Experience experience)
    {
        ArgumentNullException.ThrowIfNull(experience);

        lock (_lock)
        {
            // FIFO eviction when at capacity
            while (_experiences.Count >= Capacity)
            {
                var oldest = _experiences.First;
                if (oldest is not null)
                {
                    _index.Remove(oldest.Value.Id);
                    _experiences.RemoveFirst();
                }
            }

            // Add new experience at the end
            var node = _experiences.AddLast(experience);
            _index[experience.Id] = node;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Experience> Sample(int batchSize)
    {
        if (batchSize <= 0)
        {
            return Array.Empty<Experience>();
        }

        lock (_lock)
        {
            if (_experiences.Count == 0)
            {
                return Array.Empty<Experience>();
            }

            var effectiveBatchSize = Math.Min(batchSize, _experiences.Count);
            var experiences = _experiences.ToArray();
            var sampled = new List<Experience>(effectiveBatchSize);
            var usedIndices = new HashSet<int>();

            while (sampled.Count < effectiveBatchSize)
            {
                var index = _random.Next(experiences.Length);
                if (usedIndices.Add(index))
                {
                    sampled.Add(experiences[index]);
                }
            }

            return sampled;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Experience> SamplePrioritized(int batchSize, double alpha = 0.6)
    {
        if (batchSize <= 0)
        {
            return Array.Empty<Experience>();
        }

        lock (_lock)
        {
            if (_experiences.Count == 0)
            {
                return Array.Empty<Experience>();
            }

            var effectiveBatchSize = Math.Min(batchSize, _experiences.Count);
            var experiences = _experiences.ToArray();

            // Compute softmax probabilities based on priorities
            var probabilities = ComputePrioritizedProbabilities(experiences, alpha);

            var sampled = new List<Experience>(effectiveBatchSize);
            var usedIndices = new HashSet<int>();

            while (sampled.Count < effectiveBatchSize)
            {
                var index = SampleFromDistribution(probabilities);
                if (usedIndices.Add(index))
                {
                    sampled.Add(experiences[index]);
                }
            }

            return sampled;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_lock)
        {
            _experiences.Clear();
            _index.Clear();
        }
    }

    /// <inheritdoc/>
    public bool UpdatePriority(Guid experienceId, double newPriority)
    {
        lock (_lock)
        {
            if (!_index.TryGetValue(experienceId, out var node))
            {
                return false;
            }

            var updated = node.Value with { Priority = newPriority };
            node.Value = updated;
            return true;
        }
    }

    /// <summary>
    /// Gets all experiences as a read-only list (primarily for testing/debugging).
    /// </summary>
    /// <returns>A list of all experiences in the buffer.</returns>
    public IReadOnlyList<Experience> GetAll()
    {
        lock (_lock)
        {
            return _experiences.ToList();
        }
    }

    /// <summary>
    /// Computes prioritized sampling probabilities using softmax with temperature alpha.
    /// </summary>
    private static double[] ComputePrioritizedProbabilities(Experience[] experiences, double alpha)
    {
        var priorities = experiences.Select(e => Math.Pow(e.Priority, alpha)).ToArray();
        var sum = priorities.Sum();

        // Avoid division by zero
        if (sum <= 0)
        {
            var uniformProb = 1.0 / experiences.Length;
            return Enumerable.Repeat(uniformProb, experiences.Length).ToArray();
        }

        return priorities.Select(p => p / sum).ToArray();
    }

    /// <summary>
    /// Samples an index from a discrete probability distribution.
    /// </summary>
    private int SampleFromDistribution(double[] probabilities)
    {
        var value = _random.NextDouble();
        var cumulative = 0.0;

        for (int i = 0; i < probabilities.Length; i++)
        {
            cumulative += probabilities[i];
            if (value <= cumulative)
            {
                return i;
            }
        }

        // Fallback for floating-point edge cases
        return probabilities.Length - 1;
    }
}

/// <summary>
/// Provides Kleisli arrow factories for experience replay pipeline integration.
/// </summary>
public static class ExperienceReplayArrows
{
    /// <summary>
    /// Creates an arrow that adds an experience to the buffer.
    /// </summary>
    /// <param name="buffer">The experience buffer to add to.</param>
    /// <returns>A step that adds the experience and returns Unit on success.</returns>
    public static Step<Experience, Result<Unit, string>> AddExperienceArrow(IExperienceBuffer buffer)
        => experience => Task.FromResult(AddExperience(buffer, experience));

    /// <summary>
    /// Creates an arrow that samples experiences uniformly from the buffer.
    /// </summary>
    /// <param name="buffer">The experience buffer to sample from.</param>
    /// <returns>A step that samples the specified batch size of experiences.</returns>
    public static Step<int, Result<IReadOnlyList<Experience>, string>> SampleExperiencesArrow(IExperienceBuffer buffer)
        => batchSize => Task.FromResult(SampleExperiences(buffer, batchSize));

    /// <summary>
    /// Creates an arrow that samples experiences using prioritized replay.
    /// </summary>
    /// <param name="buffer">The experience buffer to sample from.</param>
    /// <param name="alpha">Temperature parameter for priority weighting.</param>
    /// <returns>A step that samples the specified batch size with priority weighting.</returns>
    public static Step<int, Result<IReadOnlyList<Experience>, string>> SamplePrioritizedArrow(
        IExperienceBuffer buffer,
        double alpha = 0.6)
        => batchSize => Task.FromResult(SamplePrioritized(buffer, batchSize, alpha));

    /// <summary>
    /// Creates an arrow that updates the priority of an experience.
    /// </summary>
    /// <param name="buffer">The experience buffer to update.</param>
    /// <returns>A step that updates priority and returns Unit on success.</returns>
    public static Step<(Guid Id, double NewPriority), Result<Unit, string>> UpdatePriorityArrow(IExperienceBuffer buffer)
        => input => Task.FromResult(UpdatePriority(buffer, input.Id, input.NewPriority));

    /// <summary>
    /// Creates an arrow that records an experience from state-action-reward-next_state tuple.
    /// </summary>
    /// <param name="buffer">The experience buffer to add to.</param>
    /// <param name="priority">Initial priority for the experience.</param>
    /// <returns>A step that creates and stores an experience from the input tuple.</returns>
    public static Step<(string State, string Action, double Reward, string NextState), Result<Experience, string>> RecordExperienceArrow(
        IExperienceBuffer buffer,
        double priority = 1.0)
        => input => Task.FromResult(RecordExperience(buffer, input.State, input.Action, input.Reward, input.NextState, priority));

    /// <summary>
    /// Creates an arrow that clears all experiences from the buffer.
    /// </summary>
    /// <param name="buffer">The experience buffer to clear.</param>
    /// <returns>A step that clears the buffer and returns Unit.</returns>
    public static Step<Unit, Result<Unit, string>> ClearBufferArrow(IExperienceBuffer buffer)
        => _ =>
        {
            buffer.Clear();
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        };

    /// <summary>
    /// Creates an arrow that returns the current buffer statistics.
    /// </summary>
    /// <param name="buffer">The experience buffer to query.</param>
    /// <returns>A step that returns buffer count and capacity.</returns>
    public static Step<Unit, (int Count, int Capacity)> GetBufferStatsArrow(IExperienceBuffer buffer)
        => _ => Task.FromResult((buffer.Count, buffer.Capacity));

    private static Result<Unit, string> AddExperience(IExperienceBuffer buffer, Experience experience)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(experience);
            buffer.Add(experience);
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to add experience: {ex.Message}");
        }
    }

    private static Result<IReadOnlyList<Experience>, string> SampleExperiences(IExperienceBuffer buffer, int batchSize)
    {
        try
        {
            if (batchSize <= 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Batch size must be positive.");
            }

            if (buffer.Count == 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Buffer is empty.");
            }

            var samples = buffer.Sample(batchSize);
            return Result<IReadOnlyList<Experience>, string>.Success(samples);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Experience>, string>.Failure($"Failed to sample experiences: {ex.Message}");
        }
    }

    private static Result<IReadOnlyList<Experience>, string> SamplePrioritized(
        IExperienceBuffer buffer,
        int batchSize,
        double alpha)
    {
        try
        {
            if (batchSize <= 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Batch size must be positive.");
            }

            if (buffer.Count == 0)
            {
                return Result<IReadOnlyList<Experience>, string>.Failure("Buffer is empty.");
            }

            var samples = buffer.SamplePrioritized(batchSize, alpha);
            return Result<IReadOnlyList<Experience>, string>.Success(samples);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Experience>, string>.Failure($"Failed to sample prioritized experiences: {ex.Message}");
        }
    }

    private static Result<Unit, string> UpdatePriority(IExperienceBuffer buffer, Guid experienceId, double newPriority)
    {
        try
        {
            if (buffer.UpdatePriority(experienceId, newPriority))
            {
                return Result<Unit, string>.Success(Unit.Value);
            }

            return Result<Unit, string>.Failure($"Experience with ID {experienceId} not found.");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to update priority: {ex.Message}");
        }
    }

    private static Result<Experience, string> RecordExperience(
        IExperienceBuffer buffer,
        string state,
        string action,
        double reward,
        string nextState,
        double priority)
    {
        try
        {
            var experience = Experience.Create(state, action, reward, nextState, priority);
            buffer.Add(experience);
            return Result<Experience, string>.Success(experience);
        }
        catch (Exception ex)
        {
            return Result<Experience, string>.Failure($"Failed to record experience: {ex.Message}");
        }
    }
}

/// <summary>
/// Unit type representing void in functional programming.
/// Used to signal successful completion without meaningful return value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The singleton instance of Unit.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    /// Determines equality with another Unit instance.
    /// </summary>
    /// <param name="other">The other Unit instance.</param>
    /// <returns>Always true.</returns>
    public bool Equals(Unit other) => true;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;

    /// <inheritdoc/>
    public override string ToString() => "()";

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
