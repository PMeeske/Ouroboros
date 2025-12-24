// <copyright file="Form.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Represents a Form in George Spencer-Brown's Laws of Form calculus.
/// A Form represents the fundamental act of drawing a distinction, creating
/// a boundary between what is inside (marked) and what is outside (unmarked).
///
/// The calculus is built on two fundamental axioms:
/// 1. Law of Calling (Condensation): Calling a name twice is the same as calling it once.
///    In notation: ⌐⌐ = ⌐ (or Mark(Mark(x)) = Mark(x))
/// 2. Law of Crossing (Cancellation): Crossing from unmarked to marked and back yields unmarked.
///    In notation: ⌐⌐void = void (or Mark(Mark(Void)) = Void)
///
/// Extended with Imaginary Forms (Chapter 11 of Laws of Form):
/// When a form re-enters itself (contains a reference to itself), it creates
/// an "imaginary" value that oscillates between marked and unmarked states.
/// This is analogous to the imaginary unit i in mathematics where i² = -1.
/// In LoF: if f = ⌐f, then f is imaginary (neither purely marked nor void).
///
/// This implementation models Forms as an algebraic data type that can be:
/// - Void: The unmarked state (absence of distinction)
/// - Mark: A distinction/boundary containing an inner form
/// - Imaginary: A self-referential form that oscillates (re-entry)
///
/// The Form calculus is equivalent to Boolean algebra with:
/// - Void = True (or False, depending on convention)
/// - Mark(Void) = False (or True)
/// - Mark(Mark(x)) simplifies according to the laws
/// - Imaginary values extend beyond Boolean into oscillating/wave-like states
/// </summary>
public abstract record Form
{
    /// <summary>
    /// Prevents external inheritance.
    /// </summary>
    private Form()
    {
    }

    /// <summary>
    /// Gets the void form - the unmarked state representing no distinction.
    /// In Boolean terms, this is typically interpreted as True (the ground state).
    /// </summary>
    public static Form Void { get; } = new VoidForm();

    /// <summary>
    /// Gets the imaginary form - the self-referential state that oscillates.
    /// In Spencer-Brown's notation, this is the form f where f = ⌐f.
    /// Analogous to the imaginary unit i in complex numbers.
    /// </summary>
    public static Form Imaginary { get; } = new ImaginaryForm();

    /// <summary>
    /// Creates a marked form (distinction) containing the given inner form.
    /// The mark represents crossing from the unmarked state to the marked state.
    /// </summary>
    /// <param name="inner">The form contained within this distinction.</param>
    /// <returns>A marked form containing the inner form.</returns>
    public static Form Mark(Form inner) => new MarkForm(inner);

    /// <summary>
    /// Creates a simple mark (distinction around void).
    /// This represents the basic marked state: ⌐ or Mark(Void).
    /// </summary>
    /// <returns>A marked form containing void.</returns>
    public static Form Cross() => Mark(Void);

    /// <summary>
    /// Creates an imaginary form with a specific phase.
    /// Phase represents the position in the oscillation cycle.
    /// </summary>
    /// <param name="phase">The phase angle (0 to 2π) in the oscillation.</param>
    /// <returns>An imaginary form at the specified phase.</returns>
    public static Form Imagine(double phase) => new ImaginaryForm(phase);

    /// <summary>
    /// Creates a re-entry form - a form that references itself.
    /// This is the fundamental source of imaginary values in LoF.
    /// The equation f = ⌐f has no solution in {Void, Mark} but is
    /// satisfied by the imaginary value.
    /// </summary>
    /// <param name="name">Optional name for the self-reference.</param>
    /// <returns>A re-entry form representing self-reference.</returns>
    public static Form ReEntry(string? name = null) => new ReEntryForm(name ?? "f");

    /// <summary>
    /// Evaluates this form to its simplest equivalent form by applying
    /// the Laws of Form (calling and crossing) recursively.
    /// </summary>
    /// <returns>The reduced form.</returns>
    public abstract Form Eval();

    /// <summary>
    /// Determines if this form is equivalent to the marked state.
    /// After evaluation, a form is marked if it reduces to Mark(Void).
    /// Imaginary forms are neither marked nor void.
    /// </summary>
    /// <returns>True if this form evaluates to the marked state.</returns>
    public bool IsMarked() => this.Eval() is MarkForm { Inner: VoidForm };

    /// <summary>
    /// Determines if this form is equivalent to the unmarked/void state.
    /// After evaluation, a form is void if it reduces to Void.
    /// Imaginary forms are neither marked nor void.
    /// </summary>
    /// <returns>True if this form evaluates to void.</returns>
    public bool IsVoid() => this.Eval() is VoidForm;

    /// <summary>
    /// Determines if this form is imaginary (self-referential/oscillating).
    /// An imaginary form transcends the marked/unmarked distinction.
    /// </summary>
    /// <returns>True if this form evaluates to an imaginary state.</returns>
    public bool IsImaginary() => this.Eval() is ImaginaryForm or ReEntryForm;

    /// <summary>
    /// Indicates (calls) this form with another form.
    /// In Spencer-Brown notation, this is juxtaposition of forms.
    /// Calling represents the combining of distinctions.
    /// </summary>
    /// <param name="other">The form to indicate with.</param>
    /// <returns>A form representing the indication.</returns>
    public Form Call(Form other) => new IndicationForm(this, other);

    /// <summary>
    /// The void form - represents no distinction, the ground state.
    /// </summary>
    internal sealed record VoidForm : Form
    {
        /// <inheritdoc/>
        public override Form Eval() => this;

        /// <inheritdoc/>
        public override string ToString() => "∅";
    }

    /// <summary>
    /// A marked form - represents a distinction containing an inner form.
    /// The mark crosses the boundary from unmarked to marked.
    /// </summary>
    /// <param name="Inner">The form contained within this distinction.</param>
    internal sealed record MarkForm(Form Inner) : Form
    {
        /// <inheritdoc/>
        public override Form Eval()
        {
            var evalInner = this.Inner.Eval();

            // Law of Crossing: Mark(Mark(x)) = x
            // When we have nested marks, they cancel out
            if (evalInner is MarkForm innerMark)
            {
                return innerMark.Inner.Eval();
            }

            // Mark of Imaginary shifts the phase by π (half cycle)
            // This models the oscillation: ⌐i at time t equals i at time t+1
            if (evalInner is ImaginaryForm imaginary)
            {
                return new ImaginaryForm(imaginary.Phase + Math.PI);
            }

            // Mark of ReEntry resolves to Imaginary
            // Since f = ⌐f implies f is imaginary
            if (evalInner is ReEntryForm)
            {
                return Imaginary;
            }

            return new MarkForm(evalInner);
        }

        /// <inheritdoc/>
        public override string ToString() => $"⌐{this.Inner}";
    }

    /// <summary>
    /// An indication form - represents two forms placed together (juxtaposition).
    /// This models the "calling" or "indication" operation.
    /// </summary>
    /// <param name="Left">The left form.</param>
    /// <param name="Right">The right form.</param>
    internal sealed record IndicationForm(Form Left, Form Right) : Form
    {
        /// <inheritdoc/>
        public override Form Eval()
        {
            var left = this.Left.Eval();
            var right = this.Right.Eval();

            // Law of Calling: If either side is marked, the result is marked
            // Mark placed next to anything yields Mark
            // (This models Boolean OR in the standard interpretation)
            if (left is MarkForm { Inner: VoidForm } || right is MarkForm { Inner: VoidForm })
            {
                return Cross();
            }

            // Void next to anything is just that thing (void is the identity for indication)
            if (left is VoidForm)
            {
                return right;
            }

            if (right is VoidForm)
            {
                return left;
            }

            // Imaginary interactions: combining imaginaries produces interference
            if (left is ImaginaryForm leftImag && right is ImaginaryForm rightImag)
            {
                // Phase combination (like wave interference)
                var combinedPhase = (leftImag.Phase + rightImag.Phase) / 2;
                return new ImaginaryForm(combinedPhase);
            }

            // Imaginary with real: imaginary dominates (like complex + real)
            if (left is ImaginaryForm || right is ImaginaryForm)
            {
                return left is ImaginaryForm ? left : right;
            }

            // If both sides evaluate to the same thing, apply condensation
            if (left.Equals(right))
            {
                return left;
            }

            return new IndicationForm(left, right);
        }

        /// <inheritdoc/>
        public override string ToString() => $"({this.Left} {this.Right})";
    }

    /// <summary>
    /// An imaginary form - represents an oscillating, self-referential state.
    /// This is Spencer-Brown's extension to handle re-entry equations like f = ⌐f.
    /// The imaginary form oscillates between marked and unmarked with each "crossing".
    ///
    /// In the time domain interpretation:
    /// - At even times, the imaginary form appears as void
    /// - At odd times, the imaginary form appears as marked
    ///
    /// The phase represents the position in this oscillation cycle.
    /// </summary>
    /// <param name="Phase">The phase angle in radians (0 to 2π).</param>
    internal sealed record ImaginaryForm(double Phase = 0) : Form
    {
        /// <inheritdoc/>
        public override Form Eval() => this;

        /// <summary>
        /// Gets the apparent value at a given discrete time step.
        /// The imaginary form oscillates between Void and Mark.
        /// </summary>
        /// <param name="time">The discrete time step.</param>
        /// <returns>Void at even times, Mark at odd times (adjusted by phase).</returns>
        public Form AtTime(int time)
        {
            var effectivePhase = this.Phase + (time * Math.PI);
            var normalizedPhase = effectivePhase % (2 * Math.PI);

            // If phase is in [0, π), appears as Void; in [π, 2π), appears as Marked
            return normalizedPhase < Math.PI ? Void : Cross();
        }

        /// <summary>
        /// Determines if this imaginary form appears marked at a given time.
        /// </summary>
        /// <param name="time">The discrete time step.</param>
        /// <returns>True if marked at this time.</returns>
        public bool IsMarkedAtTime(int time) => this.AtTime(time).IsMarked();

        /// <inheritdoc/>
        public override string ToString() =>
            this.Phase == 0 ? "i" : $"i∠{this.Phase:F2}";
    }

    /// <summary>
    /// A re-entry form - represents a form that contains a reference to itself.
    /// This is the source of imaginary values: the equation f = ⌐f.
    ///
    /// Re-entry captures the essence of self-reference, recursion, and the
    /// Ouroboros symbol - the serpent consuming its own tail.
    ///
    /// When evaluated in the context of its own definition, a re-entry form
    /// produces an imaginary (oscillating) value.
    /// </summary>
    /// <param name="Name">The name of the self-referential variable.</param>
    internal sealed record ReEntryForm(string Name) : Form
    {
        /// <inheritdoc/>
        public override Form Eval()
        {
            // A re-entry form evaluates to Imaginary
            // because f = ⌐f has no solution in {Void, Mark}
            // The imaginary value is the "eigenform" of the crossing operation
            return Imaginary;
        }

        /// <summary>
        /// Creates the defining equation for this re-entry: f = ⌐f.
        /// </summary>
        /// <returns>A tuple of (left side, right side) of the equation.</returns>
        public (Form Left, Form Right) GetEquation() => (this, Mark(this));

        /// <inheritdoc/>
        public override string ToString() => $"↻{this.Name}";
    }
}
