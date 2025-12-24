// <copyright file="DistinctionArrow.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

using LangChainPipeline.Core.Kleisli;

/// <summary>
/// Provides Kleisli arrows for distinction-based reasoning using the Laws of Form.
/// These arrows allow pipeline composition where computations are gated by distinctions.
///
/// A DistinctionArrow embodies the fundamental principle that all computation
/// begins with an act of distinction - the separation of "this" from "not-this".
/// </summary>
public static class DistinctionArrow
{
    /// <summary>
    /// Creates an arrow that passes through if the predicate creates a marked distinction.
    /// This is the fundamental "gate" operation based on Laws of Form.
    /// </summary>
    /// <typeparam name="T">The input/output type.</typeparam>
    /// <param name="predicate">A function that creates a distinction from the input.</param>
    /// <returns>An arrow that returns the input if marked, or default if void.</returns>
    public static Step<T, T?> Gate<T>(Func<T, Form> predicate)
        where T : class
    {
        return input =>
        {
            var distinction = predicate(input);
            return Task.FromResult(distinction.IsMarked() ? input : default);
        };
    }

    /// <summary>
    /// Creates an arrow that transforms input based on a distinction.
    /// Applies one transformation if marked, another if void.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="predicate">A function that creates a distinction from the input.</param>
    /// <param name="onMarked">Transformation to apply if the distinction is marked.</param>
    /// <param name="onVoid">Transformation to apply if the distinction is void.</param>
    /// <returns>An arrow that applies the appropriate transformation.</returns>
    public static Step<TInput, TOutput> Branch<TInput, TOutput>(
        Func<TInput, Form> predicate,
        Func<TInput, TOutput> onMarked,
        Func<TInput, TOutput> onVoid)
    {
        return input =>
        {
            var distinction = predicate(input);
            var result = distinction.IsMarked() ? onMarked(input) : onVoid(input);
            return Task.FromResult(result);
        };
    }

    /// <summary>
    /// Creates an arrow that evaluates multiple distinctions in sequence,
    /// returning the input only if all distinctions are marked.
    /// This models conjunction through sequential marking.
    /// </summary>
    /// <typeparam name="T">The input/output type.</typeparam>
    /// <param name="predicates">Functions that create distinctions.</param>
    /// <returns>An arrow that returns input if all distinctions are marked.</returns>
    public static Step<T, T?> AllMarked<T>(params Func<T, Form>[] predicates)
        where T : class
    {
        return input =>
        {
            foreach (var predicate in predicates)
            {
                if (!predicate(input).IsMarked())
                {
                    return Task.FromResult<T?>(default);
                }
            }

            return Task.FromResult<T?>(input);
        };
    }

    /// <summary>
    /// Creates an arrow that evaluates multiple distinctions,
    /// returning the input if any distinction is marked.
    /// This models disjunction through the Law of Calling.
    /// </summary>
    /// <typeparam name="T">The input/output type.</typeparam>
    /// <param name="predicates">Functions that create distinctions.</param>
    /// <returns>An arrow that returns input if any distinction is marked.</returns>
    public static Step<T, T?> AnyMarked<T>(params Func<T, Form>[] predicates)
        where T : class
    {
        return input =>
        {
            foreach (var predicate in predicates)
            {
                if (predicate(input).IsMarked())
                {
                    return Task.FromResult<T?>(input);
                }
            }

            return Task.FromResult<T?>(default);
        };
    }

    /// <summary>
    /// Creates an arrow that applies the Law of Crossing (double negation elimination).
    /// Useful for cleaning up accumulated distinctions.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <param name="extractor">Function to extract a form from input.</param>
    /// <param name="combiner">Function to combine the evaluated form back with input.</param>
    /// <returns>An arrow that evaluates (simplifies) forms in the pipeline.</returns>
    public static Step<T, T> Evaluate<T>(
        Func<T, Form> extractor,
        Func<T, Form, T> combiner)
    {
        return input =>
        {
            var form = extractor(input);
            var evaluated = form.Eval();
            return Task.FromResult(combiner(input, evaluated));
        };
    }

    /// <summary>
    /// Creates an arrow that models re-entry - a form that refers to itself.
    /// This captures the self-referential nature of the Ouroboros symbol.
    ///
    /// Re-entry is a profound concept in Laws of Form where a form can
    /// contain a copy of itself, leading to oscillating or imaginary values.
    /// </summary>
    /// <typeparam name="T">The input/output type.</typeparam>
    /// <param name="selfReference">A function that takes the current state and returns a form representing self-reference.</param>
    /// <param name="maxDepth">Maximum recursion depth to prevent infinite loops.</param>
    /// <returns>An arrow implementing bounded self-reference.</returns>
    public static Step<T, Form> ReEntry<T>(
        Func<T, Form, Form> selfReference,
        int maxDepth = 10)
    {
        return input =>
        {
            var current = Form.Void;
            for (int i = 0; i < maxDepth; i++)
            {
                var next = selfReference(input, current);
                if (next.Eval().Equals(current.Eval()))
                {
                    // Fixed point reached
                    return Task.FromResult(current);
                }

                current = next;
            }

            // Return the last computed form (may be oscillating)
            return Task.FromResult(current);
        };
    }

    /// <summary>
    /// Lifts a boolean predicate into the distinction domain.
    /// </summary>
    /// <typeparam name="T">The input type.</typeparam>
    /// <param name="predicate">A boolean predicate.</param>
    /// <returns>A function that creates forms from predicates.</returns>
    public static Func<T, Form> LiftPredicate<T>(Func<T, bool> predicate)
    {
        return input => predicate(input).ToForm();
    }
}
