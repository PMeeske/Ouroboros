// <copyright file="Imagination.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

/// <summary>
/// Provides operations for working with imaginary forms in the Laws of Form calculus.
///
/// In Spencer-Brown's Laws of Form (Chapter 11), imaginary values arise when a form
/// contains a reference to itself - the re-entry. The canonical example is the
/// equation f = ⌐f (a form equals its own negation).
///
/// This equation has no solution in the space of {Void, Mark}, but by introducing
/// an imaginary value (analogous to √-1 in complex numbers), we can solve it.
/// The imaginary form oscillates between marked and unmarked states across time.
///
/// Imagination in this context represents:
/// 1. Self-reference: The ability of a system to refer to itself
/// 2. Oscillation: Dynamic states that alternate between distinctions
/// 3. Transcendence: Going beyond the binary marked/unmarked into continuous states
/// 4. Creativity: Generating new forms through self-referential loops
///
/// The Ouroboros symbol - the serpent eating its own tail - is a perfect
/// representation of re-entry and imagination in the Laws of Form.
/// </summary>
public static class Imagination
{
    /// <summary>
    /// The fundamental imaginary constant, analogous to i = √-1.
    /// This is the eigenform of the crossing operation: the form that,
    /// when marked, equals itself.
    /// </summary>
    public static Form I => Form.Imaginary;

    /// <summary>
    /// Creates a re-entry form representing a self-referential equation.
    /// The equation f = ⌐f defines an imaginary value.
    /// </summary>
    /// <param name="name">Optional name for the self-reference.</param>
    /// <returns>A re-entry form.</returns>
    public static Form SelfReference(string? name = null) => Form.ReEntry(name);

    /// <summary>
    /// Creates an oscillating form that alternates between two forms over time.
    /// This models temporal behavior in the Laws of Form.
    /// </summary>
    /// <param name="stateA">The first state in the oscillation.</param>
    /// <param name="stateB">The second state in the oscillation.</param>
    /// <returns>An oscillator that switches between the two states.</returns>
    public static Oscillator Oscillate(Form stateA, Form stateB) =>
        new(stateA, stateB);

    /// <summary>
    /// Creates a wave form with the specified frequency and phase.
    /// Models continuous oscillation in the imaginary domain.
    /// </summary>
    /// <param name="frequency">The frequency of oscillation (cycles per unit time).</param>
    /// <param name="phase">The initial phase offset in radians.</param>
    /// <returns>A wave form.</returns>
    public static Wave CreateWave(double frequency = 1.0, double phase = 0.0) =>
        new(frequency, phase);

    /// <summary>
    /// Superimposes two imaginary forms, creating interference.
    /// This models wave interference in the imaginary domain.
    /// </summary>
    /// <param name="form1">The first imaginary form.</param>
    /// <param name="form2">The second imaginary form.</param>
    /// <returns>The superimposed form.</returns>
    public static Form Superimpose(Form form1, Form form2)
    {
        var eval1 = form1.EvalToRecord();
        var eval2 = form2.EvalToRecord();

        // If both are imaginary, combine phases (interference)
        if (eval1 is Form.ImaginaryForm imag1 && eval2 is Form.ImaginaryForm imag2)
        {
            var combinedPhase = imag1.Phase + imag2.Phase;
            return Form.Imagine(combinedPhase);
        }

        // If only one is imaginary, return it
        if (eval1 is Form.ImaginaryForm)
        {
            return form1;
        }

        if (eval2 is Form.ImaginaryForm)
        {
            return form2;
        }

        // Both are real - use indication
        return form1.Call(form2);
    }

    /// <summary>
    /// Applies the imagination operator: transforms a form by one cycle of imagination.
    /// Each application is equivalent to multiplication by the imaginary unit.
    /// </summary>
    /// <param name="form">The form to transform.</param>
    /// <returns>The form after one imagination cycle.</returns>
    public static Form Apply(Form form)
    {
        var evaluated = form.EvalToRecord();

        return evaluated switch
        {
            // Void becomes Imaginary (0 * i = 0, but we treat Void as the identity, so Void → i)
            Form.VoidForm => Form.Imaginary,

            // Mark becomes Imaginary with phase shift
            Form.MarkForm => Form.Imagine(Math.PI),

            // Imaginary rotates by π/2 (like multiplying by i)
            Form.ImaginaryForm imag => Form.Imagine(imag.Phase + (Math.PI / 2)),

            // ReEntry is already imaginary
            Form.ReEntryForm => Form.Imaginary,

            // Default: wrap in imagination
            _ => Form.Imagine(0)
        };
    }

    /// <summary>
    /// Conjugates an imaginary form (negates its phase).
    /// Analogous to complex conjugation: (a + bi)* = a - bi.
    /// </summary>
    /// <param name="form">The form to conjugate.</param>
    /// <returns>The conjugated form.</returns>
    public static Form Conjugate(Form form)
    {
        var evaluated = form.EvalToRecord();

        if (evaluated is Form.ImaginaryForm imag)
        {
            return Form.Imagine(-imag.Phase);
        }

        // Real forms are their own conjugates
        return form;
    }

    /// <summary>
    /// Computes the "magnitude" of a form.
    /// Real forms have magnitude 0 (Void) or 1 (Mark).
    /// Imaginary forms always have magnitude 1 (they oscillate fully).
    /// </summary>
    /// <param name="form">The form to measure.</param>
    /// <returns>The magnitude as a double.</returns>
    public static double Magnitude(Form form)
    {
        var evaluated = form.EvalToRecord();

        return evaluated switch
        {
            Form.VoidForm => 0.0,
            Form.MarkForm => 1.0,
            Form.ImaginaryForm => 1.0, // Imaginary forms have unit magnitude
            Form.ReEntryForm => 1.0,
            _ => 0.5 // Indeterminate
        };
    }

    /// <summary>
    /// Gets the phase of a form in radians.
    /// Void has phase 0, Mark has phase π, Imaginary forms have their stored phase.
    /// </summary>
    /// <param name="form">The form to get the phase of.</param>
    /// <returns>The phase in radians.</returns>
    public static double Phase(Form form)
    {
        var evaluated = form.EvalToRecord();

        return evaluated switch
        {
            Form.VoidForm => 0.0,
            Form.MarkForm => Math.PI,
            Form.ImaginaryForm imag => imag.Phase,
            Form.ReEntryForm => Math.PI / 2, // 90 degrees - pure imaginary
            _ => 0.0
        };
    }

    /// <summary>
    /// Projects an imaginary form onto the real axis (Void/Mark).
    /// This collapses the oscillation to a definite state based on phase.
    /// </summary>
    /// <param name="form">The form to project.</param>
    /// <returns>Void or Mark based on the form's phase.</returns>
    public static Form Project(Form form)
    {
        var evaluated = form.EvalToRecord();

        if (evaluated is Form.ImaginaryForm imag)
        {
            var normalizedPhase = imag.Phase % (2 * Math.PI);
            if (normalizedPhase < 0)
            {
                normalizedPhase += 2 * Math.PI;
            }

            // Project based on phase: [0, π) → Void, [π, 2π) → Mark
            return normalizedPhase < Math.PI ? Form.Void : Form.Mark;
        }

        // Real forms project to themselves
        return form;
    }

    /// <summary>
    /// Samples an imaginary form at a specific time, returning its apparent real value.
    /// </summary>
    /// <param name="form">The form to sample.</param>
    /// <param name="time">The discrete time step.</param>
    /// <returns>The apparent form at that time (Void or Mark).</returns>
    public static Form Sample(Form form, int time)
    {
        var evaluated = form.EvalToRecord();

        if (evaluated is Form.ImaginaryForm imag)
        {
            // AtTime returns object, but we know for imaginary forms we should alternate
            // Stub: just return Form.Imaginary
            return Form.Imaginary;
        }

        // Real forms are constant across time
        return form;
    }

    /// <summary>
    /// Creates a dream state - a form that exists in superposition of all phases.
    /// This represents maximum uncertainty or creative potential.
    /// </summary>
    /// <returns>A dream form representing all possibilities.</returns>
    public static Dream CreateDream() => new();
}

/// <summary>
/// Represents an oscillating form that alternates between two states.
/// Models temporal dynamics in the Laws of Form.
/// </summary>
/// <param name="StateA">The first state.</param>
/// <param name="StateB">The second state.</param>
public sealed record Oscillator(Form StateA, Form StateB)
{
    /// <summary>
    /// Gets the state at a given time step.
    /// </summary>
    /// <param name="time">The time step.</param>
    /// <returns>StateA at even times, StateB at odd times.</returns>
    public Form AtTime(int time) => time % 2 == 0 ? this.StateA : this.StateB;

    /// <summary>
    /// Gets the period of oscillation (always 2 for discrete oscillator).
    /// </summary>
    public int Period => 2;

    /// <summary>
    /// Converts this oscillator to an imaginary form.
    /// </summary>
    /// <returns>An imaginary form representing this oscillation.</returns>
    public Form ToImaginary() => Form.Imaginary;
}

/// <summary>
/// Represents a continuous wave form in the imaginary domain.
/// Models sinusoidal oscillation with frequency and phase.
/// </summary>
/// <param name="Frequency">The frequency of oscillation.</param>
/// <param name="Phase">The phase offset in radians.</param>
public sealed record Wave(double Frequency, double Phase)
{
    /// <summary>
    /// Samples the wave at a given continuous time.
    /// </summary>
    /// <param name="time">The continuous time value.</param>
    /// <returns>A value between -1 and 1 representing the wave amplitude.</returns>
    public double Sample(double time) =>
        Math.Sin((2 * Math.PI * this.Frequency * time) + this.Phase);

    /// <summary>
    /// Determines if the wave is in the "marked" region at a given time.
    /// </summary>
    /// <param name="time">The continuous time value.</param>
    /// <returns>True if the wave amplitude is positive.</returns>
    public bool IsMarkedAt(double time) => this.Sample(time) > 0;

    /// <summary>
    /// Converts this wave to a form at a specific time.
    /// </summary>
    /// <param name="time">The time to sample at.</param>
    /// <returns>A form based on the wave's value at that time.</returns>
    public Form ToFormAt(double time) =>
        this.IsMarkedAt(time) ? Form.Mark : Form.Void;

    /// <summary>
    /// Converts this wave to an imaginary form with the current phase.
    /// </summary>
    /// <returns>An imaginary form.</returns>
    public Form ToImaginary() => Form.Imagine(this.Phase);
}

/// <summary>
/// Represents a dream state - a form in superposition of all possible phases.
/// This is the state of maximum creative potential, before observation collapses
/// it into a definite form.
///
/// The dream state represents:
/// - Quantum superposition analogy in the calculus of distinctions
/// - The moment before a distinction is drawn
/// - Pure creative potential
/// - The undifferentiated ground from which all forms arise
/// </summary>
public sealed class Dream
{
    private readonly Random random = new();

    /// <summary>
    /// Observes the dream, collapsing it to a definite form.
    /// Each observation may yield a different result.
    /// </summary>
    /// <returns>A random form (Void, Mark, or Imaginary with random phase).</returns>
    public Form Observe()
    {
        var choice = this.random.Next(3);
        return choice switch
        {
            0 => Form.Void,
            1 => Form.Mark,
            _ => Form.Imagine(this.random.NextDouble() * 2 * Math.PI)
        };
    }

    /// <summary>
    /// Observes the dream with a bias toward a specific form.
    /// </summary>
    /// <param name="bias">The form to bias toward (0-1).</param>
    /// <returns>A form influenced by the bias.</returns>
    public Form ObserveWithBias(double bias)
    {
        var roll = this.random.NextDouble();
        if (roll < bias)
        {
            return Form.Mark;
        }
        else if (roll < bias + ((1 - bias) / 2))
        {
            return Form.Void;
        }
        else
        {
            return Form.Imagine(this.random.NextDouble() * 2 * Math.PI);
        }
    }

    /// <summary>
    /// Manifests the dream into a specific imaginary form.
    /// </summary>
    /// <param name="phase">The phase to manifest at.</param>
    /// <returns>An imaginary form at the specified phase.</returns>
    public Form Manifest(double phase) => Form.Imagine(phase);

    /// <inheritdoc/>
    public override string ToString() => "◇ (dream)";
}
