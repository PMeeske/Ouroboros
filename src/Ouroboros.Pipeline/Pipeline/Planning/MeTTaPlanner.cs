// <copyright file="MeTTaPlanner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Planning;

using System.Text.RegularExpressions;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Represents a type in the MeTTa type system.
/// </summary>
/// <param name="Name">The name of the type.</param>
public sealed record MeTTaType(string Name)
{
    /// <summary>
    /// Common input types.
    /// </summary>
    public static readonly MeTTaType Text = new("Text");
    
    /// <summary>
    /// Summary type.
    /// </summary>
    public static readonly MeTTaType Summary = new("Summary");
    
    /// <summary>
    /// Code type.
    /// </summary>
    public static readonly MeTTaType Code = new("Code");
    
    /// <summary>
    /// Test result type.
    /// </summary>
    public static readonly MeTTaType TestResult = new("TestResult");
    
    /// <summary>
    /// Query type.
    /// </summary>
    public static readonly MeTTaType Query = new("Query");
    
    /// <summary>
    /// Answer type.
    /// </summary>
    public static readonly MeTTaType Answer = new("Answer");

    /// <inheritdoc/>
    public override string ToString() => this.Name;
}

/// <summary>
/// Represents a tool chain derived from MeTTa backward chaining.
/// </summary>
/// <param name="Tools">The ordered list of tools to execute.</param>
public sealed record ToolChain(IReadOnlyList<string> Tools)
{
    /// <summary>
    /// Gets whether this chain is empty.
    /// </summary>
    public bool IsEmpty => this.Tools.Count == 0;

    /// <summary>
    /// Creates an empty tool chain.
    /// </summary>
    public static ToolChain Empty => new(Array.Empty<string>());
}

/// <summary>
/// MeTTa-based planner that uses backward chaining to discover tool chains.
/// Replaces imperative tool routing with symbolic reasoning.
/// </summary>
public sealed class MeTTaPlanner
{
    private readonly IMeTTaEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaPlanner"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine for backward chaining.</param>
    public MeTTaPlanner(IMeTTaEngine engine)
    {
        this._engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Plans a tool chain from input type to output type using backward chaining.
    /// </summary>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The desired output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the tool chain or an error.</returns>
    public async Task<Result<ToolChain, string>> PlanAsync(
        MeTTaType startType,
        MeTTaType endType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(startType);
        ArgumentNullException.ThrowIfNull(endType);

        try
        {
            // Execute backward chaining query
            string query = $"!(solve {startType.Name} {endType.Name})";
            Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct);

            return result.Match(
                success => this.ParseToolChain(success),
                error => Result<ToolChain, string>.Failure($"Planning failed: {error}"));
        }
        catch (Exception ex)
        {
            return Result<ToolChain, string>.Failure($"Planning exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all tools that can accept a given input type.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tool names.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> GetToolsAcceptingAsync(
        MeTTaType inputType,
        CancellationToken ct = default)
    {
        string query = $"!(tools-accepting {inputType.Name})";
        Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct);

        return result.Match(
            success => Result<IReadOnlyList<string>, string>.Success(this.ParseToolNames(success)),
            error => Result<IReadOnlyList<string>, string>.Failure(error));
    }

    /// <summary>
    /// Gets all tools that can produce a given output type.
    /// </summary>
    /// <param name="outputType">The output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tool names.</returns>
    public async Task<Result<IReadOnlyList<string>, string>> GetToolsProducingAsync(
        MeTTaType outputType,
        CancellationToken ct = default)
    {
        string query = $"!(tools-producing {outputType.Name})";
        Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct);

        return result.Match(
            success => Result<IReadOnlyList<string>, string>.Success(this.ParseToolNames(success)),
            error => Result<IReadOnlyList<string>, string>.Failure(error));
    }

    /// <summary>
    /// Registers a tool signature with the MeTTa engine.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="inputType">The input type.</param>
    /// <param name="outputType">The output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> RegisterToolSignatureAsync(
        string toolName,
        MeTTaType inputType,
        MeTTaType outputType,
        CancellationToken ct = default)
    {
        string fact = $"(: {toolName} (-> {inputType.Name} {outputType.Name}))";
        return await this._engine.AddFactAsync(fact, ct);
    }

    /// <summary>
    /// Initializes the planner with standard tool signatures.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> InitializeAsync(CancellationToken ct = default)
    {
        string[] signatures =
        [
            "; Define Base Types",
            "(: Text Type)",
            "(: Summary Type)",
            "(: Code Type)",
            "(: TestResult Type)",
            "(: Query Type)",
            "(: Answer Type)",
            "",
            "; Tool Signatures",
            "(: summarize_tool (-> Text Summary))",
            "(: generate_code_tool (-> Summary Code))",
            "(: run_tests_tool (-> Code TestResult))",
            "(: answer_question_tool (-> Query Answer))",
            "",
            "; Chaining Logic",
            "(= (solve $in $out) (match &self (: $tool (-> $in $out)) $tool))",
            "(= (solve $in $out) (match &self (: $tool (-> $mid $out)) (chain (solve $in $mid) $tool)))",
            "(= (tools-accepting $input_type) (match &self (: $tool (-> $input_type $any)) $tool))",
            "(= (tools-producing $output_type) (match &self (: $tool (-> $any $output_type)) $tool))",
        ];

        foreach (string line in signatures)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(';'))
            {
                continue;
            }

            Result<Unit, string> result = await this._engine.AddFactAsync(line, ct);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    /// <summary>
    /// Parses a tool chain from MeTTa output.
    /// </summary>
    private Result<ToolChain, string> ParseToolChain(string mettaOutput)
    {
        List<string> tools = new();

        // Handle chain format: (chain (chain tool1 tool2) tool3)
        // Flatten the nested chains
        string normalized = mettaOutput.Trim();

        if (string.IsNullOrEmpty(normalized) || normalized == "[]" || normalized == "()")
        {
            return Result<ToolChain, string>.Failure("No tool chain found");
        }

        // Extract tool names from the chain expression
        Regex toolPattern = new(@"\b([a-z_]+_tool)\b", RegexOptions.Compiled);
        
        foreach (Match match in toolPattern.Matches(normalized))
        {
            tools.Add(match.Value);
        }

        if (tools.Count == 0)
        {
            // Try to extract any identifier that looks like a tool
            Regex identPattern = new(@"\b([a-zA-Z_][a-zA-Z0-9_]*)\b", RegexOptions.Compiled);
            foreach (Match match in identPattern.Matches(normalized))
            {
                string value = match.Value;
                // Skip MeTTa keywords
                if (value != "chain" && value != "solve" && value != "match")
                {
                    tools.Add(value);
                }
            }
        }

        return tools.Count > 0
            ? Result<ToolChain, string>.Success(new ToolChain(tools))
            : Result<ToolChain, string>.Failure($"Could not parse tool chain from: {normalized}");
    }

    /// <summary>
    /// Parses tool names from MeTTa list output.
    /// </summary>
    private IReadOnlyList<string> ParseToolNames(string mettaOutput)
    {
        List<string> tools = new();

        Regex toolPattern = new(@"\b([a-z_]+_tool)\b", RegexOptions.Compiled);
        
        foreach (Match match in toolPattern.Matches(mettaOutput))
        {
            if (!tools.Contains(match.Value))
            {
                tools.Add(match.Value);
            }
        }

        return tools;
    }
}
