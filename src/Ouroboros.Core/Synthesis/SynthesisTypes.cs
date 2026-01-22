// <copyright file="SynthesisTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Synthesis;

/// <summary>
/// Represents a synthesized program with its source code, AST, and metadata.
/// </summary>
/// <param name="SourceCode">The source code representation of the program.</param>
/// <param name="AST">The abstract syntax tree of the program.</param>
/// <param name="Language">The domain-specific language used to write the program.</param>
/// <param name="LogProbability">The log probability of this program under the learned model.</param>
/// <param name="Trace">Optional execution trace for debugging and analysis.</param>
public sealed record Program(
    string SourceCode,
    AbstractSyntaxTree AST,
    DomainSpecificLanguage Language,
    double LogProbability,
    ExecutionTrace? Trace = null);

/// <summary>
/// Represents the abstract syntax tree of a program.
/// </summary>
/// <param name="Root">The root node of the AST.</param>
/// <param name="Depth">The maximum depth of the tree.</param>
/// <param name="NodeCount">The total number of nodes in the tree.</param>
public sealed record AbstractSyntaxTree(
    ASTNode Root,
    int Depth,
    int NodeCount);

/// <summary>
/// Represents a node in an abstract syntax tree.
/// </summary>
/// <param name="NodeType">The type of this node (e.g., "Apply", "Lambda", "Variable").</param>
/// <param name="Value">The value associated with this node (e.g., primitive name, variable name).</param>
/// <param name="Children">The child nodes of this node.</param>
public sealed record ASTNode(
    string NodeType,
    string Value,
    List<ASTNode> Children);

/// <summary>
/// Represents an input-output example for program synthesis.
/// </summary>
/// <param name="Input">The input value for the example.</param>
/// <param name="ExpectedOutput">The expected output value for the given input.</param>
/// <param name="TimeoutSeconds">Optional timeout for execution of this example.</param>
public sealed record InputOutputExample(
    object Input,
    object ExpectedOutput,
    double? TimeoutSeconds = null);

/// <summary>
/// Represents a domain-specific language for program synthesis.
/// </summary>
/// <param name="Name">The name of the DSL.</param>
/// <param name="Primitives">The primitive operations available in the DSL.</param>
/// <param name="TypeRules">The type rules governing the DSL.</param>
/// <param name="Optimizations">Rewrite rules for optimizing programs in the DSL.</param>
public sealed record DomainSpecificLanguage(
    string Name,
    List<Primitive> Primitives,
    List<TypeRule> TypeRules,
    List<RewriteRule> Optimizations);

/// <summary>
/// Represents a primitive operation in a DSL.
/// </summary>
/// <param name="Name">The name of the primitive.</param>
/// <param name="Type">The type signature of the primitive.</param>
/// <param name="Implementation">The executable implementation of the primitive.</param>
/// <param name="LogPrior">The log prior probability of using this primitive.</param>
public sealed record Primitive(
    string Name,
    string Type,
    Func<object[], object> Implementation,
    double LogPrior);

/// <summary>
/// Represents a type rule in a DSL.
/// </summary>
/// <param name="Name">The name of the type rule.</param>
/// <param name="InputTypes">The input types for this rule.</param>
/// <param name="OutputType">The output type produced by this rule.</param>
public sealed record TypeRule(
    string Name,
    List<string> InputTypes,
    string OutputType);

/// <summary>
/// Represents a rewrite rule for AST optimization.
/// </summary>
/// <param name="Name">The name of the rewrite rule.</param>
/// <param name="Pattern">The AST pattern to match.</param>
/// <param name="Replacement">The replacement AST pattern.</param>
public sealed record RewriteRule(
    string Name,
    ASTNode Pattern,
    ASTNode Replacement);

/// <summary>
/// Represents a synthesis task with examples and a DSL.
/// </summary>
/// <param name="Description">A description of the synthesis task.</param>
/// <param name="Examples">The input-output examples defining the task.</param>
/// <param name="DSL">The domain-specific language to use for synthesis.</param>
public sealed record SynthesisTask(
    string Description,
    List<InputOutputExample> Examples,
    DomainSpecificLanguage DSL);

/// <summary>
/// Represents usage statistics for primitives in the DSL.
/// </summary>
/// <param name="PrimitiveUseCounts">Count of how many times each primitive was used.</param>
/// <param name="PrimitiveSuccessRates">Success rate for programs using each primitive.</param>
/// <param name="TotalProgramsSynthesized">Total number of programs successfully synthesized.</param>
public sealed record UsageStatistics(
    Dictionary<string, int> PrimitiveUseCounts,
    Dictionary<string, double> PrimitiveSuccessRates,
    int TotalProgramsSynthesized);

/// <summary>
/// Represents an execution trace of a program.
/// </summary>
/// <param name="Steps">The execution steps taken during program execution.</param>
/// <param name="FinalResult">The final result produced by the program.</param>
/// <param name="Duration">The time taken to execute the program.</param>
public sealed record ExecutionTrace(
    List<ExecutionStep> Steps,
    object FinalResult,
    TimeSpan Duration);

/// <summary>
/// Represents a single step in program execution.
/// </summary>
/// <param name="PrimitiveName">The name of the primitive executed.</param>
/// <param name="Inputs">The input values to the primitive.</param>
/// <param name="Output">The output value produced by the primitive.</param>
public sealed record ExecutionStep(
    string PrimitiveName,
    List<object> Inputs,
    object Output);

/// <summary>
/// Compression strategy for library learning.
/// </summary>
public enum CompressionStrategy
{
    /// <summary>
    /// Find common patterns via anti-unification.
    /// </summary>
    AntiUnification,

    /// <summary>
    /// E-graph based compression.
    /// </summary>
    EGraph,

    /// <summary>
    /// Grammar-based fragment extraction.
    /// </summary>
    FragmentGrammar,
}
