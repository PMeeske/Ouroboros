// <copyright file="CausalModels.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Reasoning;

/// <summary>
/// Represents a causal graph with variables, edges, and structural equations.
/// Immutable record type for functional programming style.
/// </summary>
/// <param name="Variables">The variables in the causal graph.</param>
/// <param name="Edges">The causal edges between variables.</param>
/// <param name="Equations">Structural equations defining causal relationships.</param>
public sealed record CausalGraph(
    List<Variable> Variables,
    List<CausalEdge> Edges,
    Dictionary<string, StructuralEquation> Equations);

/// <summary>
/// Represents a variable in the causal graph.
/// </summary>
/// <param name="Name">The name of the variable.</param>
/// <param name="Type">The type of the variable (binary, categorical, continuous, ordinal).</param>
/// <param name="PossibleValues">The possible values this variable can take.</param>
public sealed record Variable(
    string Name,
    VariableType Type,
    List<object> PossibleValues);

/// <summary>
/// Defines the type of a variable in the causal graph.
/// </summary>
public enum VariableType
{
    /// <summary>
    /// Binary variable (true/false, 0/1).
    /// </summary>
    Binary,

    /// <summary>
    /// Categorical variable with discrete categories.
    /// </summary>
    Categorical,

    /// <summary>
    /// Continuous variable with real values.
    /// </summary>
    Continuous,

    /// <summary>
    /// Ordinal variable with ordered categories.
    /// </summary>
    Ordinal,
}

/// <summary>
/// Represents a causal edge between two variables.
/// </summary>
/// <param name="Cause">The causing variable.</param>
/// <param name="Effect">The effect variable.</param>
/// <param name="Strength">The strength of the causal relationship.</param>
/// <param name="Type">The type of edge (direct, confounded, mediated, collider).</param>
public sealed record CausalEdge(
    string Cause,
    string Effect,
    double Strength,
    EdgeType Type);

/// <summary>
/// Defines the type of causal edge.
/// </summary>
public enum EdgeType
{
    /// <summary>
    /// Direct causal relationship.
    /// </summary>
    Direct,

    /// <summary>
    /// Relationship through a shared confounder.
    /// </summary>
    Confounded,

    /// <summary>
    /// Relationship mediated through another variable.
    /// </summary>
    Mediated,

    /// <summary>
    /// Common effect (collider).
    /// </summary>
    Collider,
}

/// <summary>
/// Represents a structural equation in the causal model.
/// Defines how a variable's value is determined by its parents.
/// </summary>
/// <param name="Outcome">The outcome variable.</param>
/// <param name="Parents">The parent variables that influence the outcome.</param>
/// <param name="Function">The function mapping parent values to outcome value.</param>
/// <param name="NoiseVariance">The variance of the noise term.</param>
public sealed record StructuralEquation(
    string Outcome,
    List<string> Parents,
    Func<Dictionary<string, object>, object> Function,
    double NoiseVariance);

/// <summary>
/// Represents an observation of variables at a point in time.
/// </summary>
/// <param name="Values">The observed values for each variable.</param>
/// <param name="Timestamp">When the observation was made.</param>
/// <param name="Context">Optional context information about the observation.</param>
public sealed record Observation(
    Dictionary<string, object> Values,
    DateTime Timestamp,
    string? Context);

/// <summary>
/// Represents a probability distribution.
/// </summary>
/// <param name="Type">The type of distribution.</param>
/// <param name="Mean">The mean of the distribution.</param>
/// <param name="Variance">The variance of the distribution.</param>
/// <param name="Samples">Empirical samples from the distribution.</param>
/// <param name="Probabilities">Probability mass function for discrete distributions.</param>
public sealed record Distribution(
    DistributionType Type,
    double Mean,
    double Variance,
    List<double> Samples,
    Dictionary<object, double> Probabilities);

/// <summary>
/// Defines the type of probability distribution.
/// </summary>
public enum DistributionType
{
    /// <summary>
    /// Normal (Gaussian) distribution.
    /// </summary>
    Normal,

    /// <summary>
    /// Bernoulli distribution (binary outcomes).
    /// </summary>
    Bernoulli,

    /// <summary>
    /// Categorical distribution (discrete categories).
    /// </summary>
    Categorical,

    /// <summary>
    /// Empirical distribution from samples.
    /// </summary>
    Empirical,
}

/// <summary>
/// Represents a causal explanation for an effect.
/// </summary>
/// <param name="Effect">The effect being explained.</param>
/// <param name="CausalPaths">The causal paths from causes to the effect.</param>
/// <param name="Attributions">Attribution scores for each potential cause.</param>
/// <param name="NarrativeExplanation">A human-readable narrative explanation.</param>
public sealed record Explanation(
    string Effect,
    List<CausalPath> CausalPaths,
    Dictionary<string, double> Attributions,
    string NarrativeExplanation);

/// <summary>
/// Represents a causal path from causes to an effect.
/// </summary>
/// <param name="Variables">The variables along the path.</param>
/// <param name="TotalEffect">The total causal effect along this path.</param>
/// <param name="IsDirect">Whether this is a direct path (no mediators).</param>
/// <param name="Edges">The edges comprising this path.</param>
public sealed record CausalPath(
    List<string> Variables,
    double TotalEffect,
    bool IsDirect,
    List<CausalEdge> Edges);

/// <summary>
/// Represents a planned intervention on the causal graph.
/// </summary>
/// <param name="TargetVariable">The variable to intervene on.</param>
/// <param name="NewValue">The value to set the variable to.</param>
/// <param name="ExpectedEffect">The expected effect size of the intervention.</param>
/// <param name="Confidence">Confidence level in the intervention effect.</param>
/// <param name="SideEffects">List of variables that may be affected as side effects.</param>
public sealed record Intervention(
    string TargetVariable,
    object NewValue,
    double ExpectedEffect,
    double Confidence,
    List<string> SideEffects);

/// <summary>
/// Defines the algorithm used for causal structure discovery.
/// </summary>
public enum DiscoveryAlgorithm
{
    /// <summary>
    /// Peter-Clark algorithm for constraint-based causal discovery.
    /// </summary>
    PC,

    /// <summary>
    /// Fast Causal Inference algorithm.
    /// </summary>
    FCI,

    /// <summary>
    /// Greedy Equivalence Search algorithm.
    /// </summary>
    GES,

    /// <summary>
    /// Neural network based (NO TEARS) algorithm.
    /// </summary>
    NOTEARS,

    /// <summary>
    /// Continuous optimization DAGs with no curl constraint.
    /// </summary>
    DAGsNoCurl,
}
