// <copyright file="FormStateMachine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

using Ouroboros.Core.Monads;
using System.Globalization;

/// <summary>
/// A state machine that explicitly models indeterminate states using Laws of Form.
/// Useful for distributed systems where states can genuinely oscillate during
/// consensus periods, leader elections, or network partitions.
/// </summary>
/// <typeparam name="TState">The type representing possible certain states.</typeparam>
public sealed class FormStateMachine<TState>
    where TState : notnull
{
    private readonly List<StateTransition<TState>> history;
    private Form currentForm;
    private Option<TState> currentCertainState;
    private double oscillationPhase;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormStateMachine{TState}"/> class.
    /// Starts in a certain state.
    /// </summary>
    /// <param name="initialState">The initial certain state.</param>
    public FormStateMachine(TState initialState)
    {
        this.currentCertainState = Option<TState>.Some(initialState);
        this.currentForm = Form.Mark;
        this.oscillationPhase = 0.0;
        this.history = new List<StateTransition<TState>>
        {
            new(DateTimeOffset.UtcNow, Form.Mark, Option<TState>.Some(initialState), "Initial state"),
        };
    }

    /// <summary>
    /// Gets the current form state (Mark = certain, Void = impossible, Imaginary = indeterminate).
    /// </summary>
    public Form CurrentForm => this.currentForm;

    /// <summary>
    /// Gets the current certain state, if the machine is in a certain state.
    /// Returns None if the machine is in an indeterminate (Imaginary) state.
    /// </summary>
    public Option<TState> CurrentState => this.currentCertainState;

    /// <summary>
    /// Gets the oscillation phase for indeterminate states (0.0 to 1.0).
    /// </summary>
    public double OscillationPhase => this.oscillationPhase;

    /// <summary>
    /// Gets a value indicating whether the machine is in a certain state.
    /// </summary>
    public bool IsCertain => this.currentForm.IsCertain() && this.currentCertainState.HasValue;

    /// <summary>
    /// Gets a value indicating whether the machine is in an indeterminate state.
    /// </summary>
    public bool IsIndeterminate => this.currentForm == Form.Imaginary;

    /// <summary>
    /// Gets the history of state transitions.
    /// </summary>
    public IReadOnlyList<StateTransition<TState>> History => this.history;

    /// <summary>
    /// Transitions to a new certain state.
    /// </summary>
    /// <param name="newState">The new certain state.</param>
    /// <param name="reason">The reason for the transition.</param>
    /// <example>
    /// var machine = new FormStateMachine&lt;ServerRole&gt;(ServerRole.Follower);
    /// machine.TransitionTo(ServerRole.Leader, "Won election");
    /// </example>
    public void TransitionTo(TState newState, string reason)
    {
        this.currentCertainState = Option<TState>.Some(newState);
        this.currentForm = Form.Mark;
        this.oscillationPhase = 0.0;

        this.history.Add(new StateTransition<TState>(
            DateTimeOffset.UtcNow,
            Form.Mark,
            this.currentCertainState,
            reason));
    }

    /// <summary>
    /// Enters an indeterminate state (e.g., during leader election, network partition).
    /// The machine oscillates between possible states until resolution.
    /// </summary>
    /// <param name="phase">Initial oscillation phase (0.0 to 1.0).</param>
    /// <param name="reason">The reason for indeterminacy.</param>
    /// <example>
    /// machine.EnterIndeterminateState(0.5, "Leader election in progress");
    /// </example>
    public void EnterIndeterminateState(double phase, string reason)
    {
        if (phase is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(phase), "Phase must be between 0.0 and 1.0");
        }

        this.currentForm = Form.Imaginary;
        this.oscillationPhase = phase;
        this.currentCertainState = Option<TState>.None();

        this.history.Add(new StateTransition<TState>(
            DateTimeOffset.UtcNow,
            Form.Imaginary,
            Option<TState>.None(),
            reason));
    }

    /// <summary>
    /// Resolves an indeterminate state to a certain state.
    /// Call this when consensus is achieved or certainty is restored.
    /// </summary>
    /// <param name="definitiveState">The resolved certain state.</param>
    /// <param name="reason">The reason for resolution.</param>
    /// <example>
    /// machine.ResolveState(ServerRole.Leader, "Election completed, won majority");
    /// </example>
    public void ResolveState(TState definitiveState, string reason)
    {
        if (!this.IsIndeterminate)
        {
            throw new InvalidOperationException("Can only resolve from an indeterminate state");
        }

        this.TransitionTo(definitiveState, $"Resolved: {reason}");
    }

    /// <summary>
    /// Updates the oscillation phase for an indeterminate state.
    /// Use this to track confidence or oscillation over time.
    /// </summary>
    /// <param name="newPhase">The new phase value (0.0 to 1.0).</param>
    public void UpdatePhase(double newPhase)
    {
        if (newPhase is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(newPhase), "Phase must be between 0.0 and 1.0");
        }

        if (!this.IsIndeterminate)
        {
            throw new InvalidOperationException("Can only update phase in indeterminate state");
        }

        this.oscillationPhase = newPhase;
    }

    /// <summary>
    /// Executes an action only if the machine is in a certain state.
    /// Returns None if the machine is indeterminate.
    /// </summary>
    /// <typeparam name="TResult">The result type of the action.</typeparam>
    /// <param name="action">The action to execute with the certain state.</param>
    /// <returns>Some(result) if certain, None if indeterminate.</returns>
    /// <example>
    /// var result = machine.WhenCertain(state =&gt;
    /// {
    ///     Console.WriteLine($"Current state: {state}");
    ///     return PerformOperation(state);
    /// });
    /// </example>
    public Option<TResult> WhenCertain<TResult>(Func<TState, TResult> action)
    {
        if (this.IsCertain && this.currentCertainState.HasValue)
        {
            return Option<TResult>.Some(action(this.currentCertainState.Value!));
        }

        return Option<TResult>.None();
    }

    /// <summary>
    /// Samples the state at a given time step for oscillating states.
    /// Uses the oscillation phase to determine which of two states to sample.
    /// </summary>
    /// <param name="state1">The first possible state.</param>
    /// <param name="state2">The second possible state.</param>
    /// <param name="timeStep">The time step for sampling (affects phase).</param>
    /// <returns>One of the two states based on oscillation.</returns>
    /// <example>
    /// // During leader election, oscillate between possible leaders
    /// var sampledState = machine.SampleAt(ServerRole.Leader, ServerRole.Follower, currentTime);
    /// </example>
    public TState SampleAt(TState state1, TState state2, double timeStep)
    {
        if (!this.IsIndeterminate)
        {
            throw new InvalidOperationException("Sampling only applies to indeterminate states");
        }

        // Use sine wave based on phase and time step
        // Use a small epsilon to handle floating-point precision at boundary values
        var sampleValue = Math.Sin((this.oscillationPhase + timeStep) * 2 * Math.PI);
        const double epsilon = 1e-10;
        return sampleValue > epsilon ? state1 : state2;
    }

    /// <summary>
    /// Gets the current state description for debugging.
    /// </summary>
    /// <returns>A string describing the current state.</returns>
    public override string ToString()
    {
        if (this.IsCertain && this.currentCertainState.HasValue)
        {
            return $"Certain: {this.currentCertainState.Value}";
        }

        return $"Indeterminate (phase: {this.oscillationPhase.ToString("F2", CultureInfo.InvariantCulture)})";
    }
}

/// <summary>
/// Represents a state transition in the machine's history.
/// </summary>
/// <typeparam name="TState">The state type.</typeparam>
/// <param name="Timestamp">When the transition occurred.</param>
/// <param name="Form">The form state after transition.</param>
/// <param name="State">The certain state (if applicable).</param>
/// <param name="Reason">The reason for the transition.</param>
public record StateTransition<TState>(
    DateTimeOffset Timestamp,
    Form Form,
    Option<TState> State,
    string Reason)
    where TState : notnull;
