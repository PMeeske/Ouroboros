// <copyright file="IAdvancedMeTTaEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools.MeTTa;

/// <summary>
/// Advanced MeTTa engine with rule learning, theorem proving, and abductive reasoning.
/// </summary>
public interface IAdvancedMeTTaEngine : IMeTTaEngine
{
    /// <summary>
    /// Induces rules from observations using specified induction strategy.
    /// </summary>
    /// <param name="observations">List of facts to learn from.</param>
    /// <param name="strategy">Induction strategy to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing induced rules or an error message.</returns>
    Task<Result<List<Rule>, string>> InduceRulesAsync(
        List<Fact> observations,
        InductionStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Proves a theorem using specified proof strategy.
    /// </summary>
    /// <param name="theorem">The theorem to prove.</param>
    /// <param name="axioms">List of axioms to use.</param>
    /// <param name="strategy">Proof strategy to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the proof trace or an error message.</returns>
    Task<Result<ProofTrace, string>> ProveTheoremAsync(
        string theorem,
        List<string> axioms,
        ProofStrategy strategy,
        CancellationToken ct = default);

    /// <summary>
    /// Generates hypotheses to explain an observation.
    /// </summary>
    /// <param name="observation">The observation to explain.</param>
    /// <param name="backgroundKnowledge">Background knowledge to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing generated hypotheses or an error message.</returns>
    Task<Result<List<Hypothesis>, string>> GenerateHypothesesAsync(
        string observation,
        List<string> backgroundKnowledge,
        CancellationToken ct = default);

    /// <summary>
    /// Infers the type of an atom in a given context.
    /// </summary>
    /// <param name="atom">The atom to type.</param>
    /// <param name="context">The type context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the typed atom or an error message.</returns>
    Task<Result<TypedAtom, string>> InferTypeAsync(
        string atom,
        TypeContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Applies forward chaining to derive new facts.
    /// </summary>
    /// <param name="rules">Rules to apply.</param>
    /// <param name="facts">Initial facts.</param>
    /// <param name="maxSteps">Maximum inference steps.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing derived facts or an error message.</returns>
    Task<Result<List<Fact>, string>> ForwardChainAsync(
        List<Rule> rules,
        List<Fact> facts,
        int maxSteps = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Applies backward chaining to prove a goal.
    /// </summary>
    /// <param name="goal">Goal to prove.</param>
    /// <param name="rules">Rules to use.</param>
    /// <param name="knownFacts">Known facts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing facts needed to prove the goal or an error message.</returns>
    Task<Result<List<Fact>, string>> BackwardChainAsync(
        Fact goal,
        List<Rule> rules,
        List<Fact> knownFacts,
        CancellationToken ct = default);
}

/// <summary>
/// Represents a logical rule with premises and a conclusion.
/// </summary>
/// <param name="Name">Name of the rule.</param>
/// <param name="Premises">Premises that must be satisfied.</param>
/// <param name="Conclusion">Conclusion when premises are satisfied.</param>
/// <param name="Confidence">Confidence level (0.0 to 1.0).</param>
public sealed record Rule(
    string Name,
    List<Pattern> Premises,
    Pattern Conclusion,
    double Confidence = 1.0);

/// <summary>
/// Represents a symbolic fact.
/// </summary>
/// <param name="Predicate">Predicate name.</param>
/// <param name="Arguments">List of arguments.</param>
/// <param name="Confidence">Confidence level (0.0 to 1.0).</param>
public sealed record Fact(
    string Predicate,
    List<string> Arguments,
    double Confidence = 1.0);

/// <summary>
/// Represents a proof trace with steps.
/// </summary>
/// <param name="Steps">Proof steps.</param>
/// <param name="Proved">Whether the theorem was proved.</param>
/// <param name="CounterExample">Counter-example if not proved.</param>
public sealed record ProofTrace(
    List<ProofStep> Steps,
    bool Proved,
    string? CounterExample = null);

/// <summary>
/// Represents a single proof step.
/// </summary>
/// <param name="Inference">Description of the inference.</param>
/// <param name="RuleApplied">Rule applied in this step.</param>
/// <param name="UsedFacts">Facts used in this step.</param>
public sealed record ProofStep(
    string Inference,
    Rule RuleApplied,
    List<Fact> UsedFacts);

/// <summary>
/// Represents a hypothesis with supporting evidence.
/// </summary>
/// <param name="Statement">The hypothesis statement.</param>
/// <param name="Plausibility">Plausibility score (0.0 to 1.0).</param>
/// <param name="SupportingEvidence">Supporting facts.</param>
public sealed record Hypothesis(
    string Statement,
    double Plausibility,
    List<Fact> SupportingEvidence);

/// <summary>
/// Represents a pattern with variables.
/// </summary>
/// <param name="Template">Pattern template.</param>
/// <param name="Variables">List of variable names.</param>
public sealed record Pattern(
    string Template,
    List<string> Variables);

/// <summary>
/// Represents a typed atom.
/// </summary>
/// <param name="Atom">The atom expression.</param>
/// <param name="Type">The inferred type.</param>
/// <param name="TypeParameters">Type parameters if polymorphic.</param>
public sealed record TypedAtom(
    string Atom,
    string Type,
    Dictionary<string, string> TypeParameters);

/// <summary>
/// Represents a type context for type inference.
/// </summary>
/// <param name="Bindings">Variable to type bindings.</param>
/// <param name="Constraints">Type constraints.</param>
public sealed record TypeContext(
    Dictionary<string, string> Bindings,
    List<string> Constraints);

/// <summary>
/// Strategy for rule induction.
/// </summary>
public enum InductionStrategy
{
    /// <summary>First Order Inductive Learner.</summary>
    FOIL,

    /// <summary>General purpose learning algorithm.</summary>
    GOLEM,

    /// <summary>Progol algorithm.</summary>
    Progol,

    /// <summary>Inductive Logic Programming.</summary>
    ILP,
}

/// <summary>
/// Strategy for theorem proving.
/// </summary>
public enum ProofStrategy
{
    /// <summary>Resolution-based proving.</summary>
    Resolution,

    /// <summary>Tableaux method.</summary>
    Tableaux,

    /// <summary>Natural deduction.</summary>
    NaturalDeduction,
}
