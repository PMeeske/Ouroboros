// <copyright file="Form.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents a three-valued form from Spencer-Brown's Laws of Form.
/// - Mark (Cross): A distinction is drawn, certain affirmative state
/// - Void: No distinction, certain negative state
/// - Imaginary: Re-entrant form, uncertain/paradoxical state
/// </summary>
public readonly struct Form : IEquatable<Form>
{
    /// <summary>
    /// Gets the certainty state of this form.
    /// </summary>
    public TriState State { get; }

    private Form(TriState state)
    {
        this.State = state;
    }

    /// <summary>
    /// Creates a marked form (Cross) - represents certainty, affirmative, or true.
    /// In notation: ⌐ or | |
    /// </summary>
    /// <returns>A marked form.</returns>
    public static Form Cross() => new(TriState.Mark);

    /// <summary>
    /// Gets a marked form (alias for Cross) - represents certainty, affirmative, or true.
    /// In notation: ⌐ or | |
    /// </summary>
    public static Form Mark => new(TriState.Mark);

    /// <summary>
    /// Creates a crossed/marked form containing another form (distinction).
    /// In Laws of Form, crossing is the fundamental operation that creates distinctions.
    /// CrossForm(CrossForm(Void)) = Void (double crossing cancels)
    /// CrossForm(Void) = Mark
    /// </summary>
    /// <param name="inner">The inner form to cross.</param>
    /// <returns>A new form with a crossing applied.</returns>
    public static Form CrossForm(Form inner) => inner.Not();

    /// <summary>
    /// Creates a void form - represents emptiness, negation, or false.
    /// In notation: (empty space)
    /// </summary>
    public static Form Void => new(TriState.Void);

    /// <summary>
    /// Creates an imaginary form - represents re-entry, uncertainty, or paradox.
    /// Occurs when f = ⌐f (self-negation/re-entry).
    /// </summary>
    public static Form Imaginary => new(TriState.Imaginary);

    /// <summary>
    /// Gets a value indicating whether this form is marked (certain affirmative).
    /// Alias for IsMark() for compatibility.
    /// </summary>
    /// <returns>True if the form is in the Mark state.</returns>
    public bool IsMarked() => this.IsMark();

    /// <summary>
    /// Gets a value indicating whether this form is certain (not imaginary).
    /// </summary>
    /// <returns>True if the form is Mark or Void, false if Imaginary.</returns>
    public bool IsCertain() => !this.IsImaginary();

    /// <summary>
    /// Gets a value indicating whether this form is marked (certain affirmative).
    /// </summary>
    /// <returns>True if the form is in the Mark state.</returns>
    public bool IsMark() => this.State == TriState.Mark;

    /// <summary>
    /// Gets a value indicating whether this form is void (certain negative).
    /// </summary>
    /// <returns>True if the form is in the Void state.</returns>
    public bool IsVoid() => this.State == TriState.Void;

    /// <summary>
    /// Gets a value indicating whether this form is imaginary (uncertain/paradoxical).
    /// </summary>
    /// <returns>True if the form is in the Imaginary state.</returns>
    public bool IsImaginary() => this.State == TriState.Imaginary;

    /// <summary>
    /// Negation operator - Cross the form.
    /// ⌐⌐ = void (double negation cancels)
    /// ⌐void = ⌐ (negating void gives mark)
    /// ⌐imaginary = imaginary (re-entry is self-negating)
    /// </summary>
    /// <returns>The negated form.</returns>
    public Form Not()
    {
        return this.State switch
        {
            TriState.Mark => Void,
            TriState.Void => Mark,
            TriState.Imaginary => Imaginary,
            _ => throw new InvalidOperationException("Unknown form state")
        };
    }

    /// <summary>
    /// Calling operator - idempotent operation that returns the form itself.
    /// In Laws of Form, calling a form is idempotent: f() = f.
    /// </summary>
    /// <returns>The form itself.</returns>
    public Form Calling() => this;

    /// <summary>
    /// Converts the form to a nullable boolean.
    /// Mark -> true, Void -> false, Imaginary -> null.
    /// </summary>
    /// <returns>A nullable boolean representing the form state.</returns>
    public bool? ToBool()
    {
        return this.State switch
        {
            TriState.Mark => true,
            TriState.Void => false,
            TriState.Imaginary => null,
            _ => null
        };
    }

    /// <summary>
    /// Conjunction (AND) - Juxtaposition in Laws of Form.
    /// Mark AND Mark = Mark
    /// Mark AND Void = Void
    /// Anything AND Imaginary = Imaginary
    /// </summary>
    /// <param name="other">The other form.</param>
    /// <returns>The conjunction of the two forms.</returns>
    public Form And(Form other)
    {
        if (this.IsImaginary() || other.IsImaginary())
        {
            return Imaginary;
        }

        if (this.IsVoid() || other.IsVoid())
        {
            return Void;
        }

        return Cross();
    }

    /// <summary>
    /// Disjunction (OR) - De Morgan dual of conjunction.
    /// Mark OR anything = Mark
    /// Void OR Void = Void
    /// Anything OR Imaginary = Imaginary (unless other is Mark)
    /// </summary>
    /// <param name="other">The other form.</param>
    /// <returns>The disjunction of the two forms.</returns>
    public Form Or(Form other)
    {
        if (this.IsMark() || other.IsMark())
        {
            return Cross();
        }

        if (this.IsImaginary() || other.IsImaginary())
        {
            return Imaginary;
        }

        return Void;
    }

    /// <summary>
    /// Pattern matching on the form state.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="onMark">Function to execute if marked.</param>
    /// <param name="onVoid">Function to execute if void.</param>
    /// <param name="onImaginary">Function to execute if imaginary.</param>
    /// <returns>The result of the matched function.</returns>
    public TResult Match<TResult>(
        Func<TResult> onMark,
        Func<TResult> onVoid,
        Func<TResult> onImaginary)
    {
        return this.State switch
        {
            TriState.Mark => onMark(),
            TriState.Void => onVoid(),
            TriState.Imaginary => onImaginary(),
            _ => throw new InvalidOperationException("Unknown form state")
        };
    }

    /// <summary>
    /// Pattern matching with actions.
    /// </summary>
    /// <param name="onMark">Action to execute if marked.</param>
    /// <param name="onVoid">Action to execute if void.</param>
    /// <param name="onImaginary">Action to execute if imaginary.</param>
    public void Match(
        Action onMark,
        Action onVoid,
        Action onImaginary)
    {
        switch (this.State)
        {
            case TriState.Mark:
                onMark();
                break;
            case TriState.Void:
                onVoid();
                break;
            case TriState.Imaginary:
                onImaginary();
                break;
            default:
                throw new InvalidOperationException("Unknown form state");
        }
    }

    /// <summary>
    /// Negation operator overload.
    /// </summary>
    /// <param name="form">The form to negate.</param>
    /// <returns>The negated form.</returns>
    public static Form operator !(Form form) => form.Not();

    /// <summary>
    /// Conjunction operator overload.
    /// </summary>
    /// <param name="left">The left form.</param>
    /// <param name="right">The right form.</param>
    /// <returns>The conjunction of the two forms.</returns>
    public static Form operator &(Form left, Form right) => left.And(right);

    /// <summary>
    /// Disjunction operator overload.
    /// </summary>
    /// <param name="left">The left form.</param>
    /// <param name="right">The right form.</param>
    /// <returns>The disjunction of the two forms.</returns>
    public static Form operator |(Form left, Form right) => left.Or(right);

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The other form.</param>
    /// <returns>True if the forms are equal.</returns>
    public bool Equals(Form other) => this.State == other.State;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Form other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => this.State.GetHashCode();

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Form left, Form right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Form left, Form right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.State switch
        {
            TriState.Mark => "⌐",
            TriState.Void => "∅",
            TriState.Imaginary => "i",
            _ => "?"
        };
    }

    /// <summary>
    /// Creates a re-entry form representing self-reference.
    /// This is a stub implementation that returns Imaginary.
    /// </summary>
    /// <param name="name">Optional name for the self-reference.</param>
    /// <returns>An imaginary form representing re-entry.</returns>
    public static Form ReEntry(string? name = null) => Imaginary;

    /// <summary>
    /// Creates an imaginary form with a specific phase.
    /// This is a stub implementation that returns Imaginary.
    /// </summary>
    /// <param name="phase">The phase angle in radians.</param>
    /// <returns>An imaginary form.</returns>
    public static Form Imagine(double phase) => Imaginary;

    /// <summary>
    /// Evaluates the form, returning the simplified canonical form.
    /// This method is idempotent - evaluating an already-evaluated form returns the same form.
    /// </summary>
    /// <returns>The evaluated form (Mark, Void, or Imaginary).</returns>
    public Form Eval() => this;

    /// <summary>
    /// Evaluates the form to a pattern-matchable record type.
    /// </summary>
    /// <returns>A record type representing the evaluated form.</returns>
    public object EvalToRecord()
    {
        return this.State switch
        {
            TriState.Void => new VoidForm(),
            TriState.Mark => new MarkForm(),
            TriState.Imaginary => new ImaginaryForm(0.0),
            _ => new VoidForm()
        };
    }

    /// <summary>
    /// Calls (applies) this form to another form.
    /// This implements the "calling" operation from Laws of Form.
    /// In calling:
    /// - Void is the identity element: f call Void = f
    /// - Imaginary dominates over real forms (Mark/Void)
    /// - Mark call Mark = Mark
    /// </summary>
    /// <param name="other">The form to apply to.</param>
    /// <returns>The result of the call.</returns>
    public Form Call(Form other)
    {
        // Imaginary dominates: if either is imaginary, result is imaginary
        if (this.IsImaginary() || other.IsImaginary())
        {
            return Imaginary;
        }

        // For real forms, use standard Or logic
        return this.Or(other);
    }

    /// <summary>
    /// Represents a void form for pattern matching.
    /// </summary>
    public record VoidForm;

    /// <summary>
    /// Represents a marked form for pattern matching.
    /// </summary>
    public record MarkForm;

    /// <summary>
    /// Represents an imaginary form with a phase for pattern matching.
    /// </summary>
    public record ImaginaryForm
    {
        /// <summary>
        /// Gets the phase angle in radians.
        /// </summary>
        public double Phase { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImaginaryForm"/> class.
        /// </summary>
        /// <param name="phase">The phase angle in radians.</param>
        public ImaginaryForm(double phase)
        {
            this.Phase = phase;
        }

        /// <summary>
        /// Projects the imaginary form at a specific time.
        /// Stub implementation that returns the imaginary form itself.
        /// </summary>
        /// <param name="time">The time value.</param>
        /// <returns>The form at the specified time.</returns>
        public object AtTime(double time) => this;
    }

    /// <summary>
    /// Represents a re-entry form with an optional name for pattern matching.
    /// </summary>
    /// <param name="Name">Optional name for the self-reference.</param>
    public record ReEntryForm(string? Name);
}
