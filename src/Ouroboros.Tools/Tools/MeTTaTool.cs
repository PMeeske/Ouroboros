// <copyright file="MeTTaTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using Ouroboros.Tools.MeTTa;

/// <summary>
/// A general-purpose tool that allows the LLM to execute MeTTa symbolic reasoning queries.
/// This bridges the gap between the Neural (LLM) and Symbolic (MeTTa) layers in neuro-symbolic AI.
/// </summary>
public sealed class MeTTaTool : ITool
{
    private readonly IMeTTaEngine engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaTool"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine to use for symbolic reasoning.</param>
    public MeTTaTool(IMeTTaEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <inheritdoc />
    public string Name => "metta";

    /// <inheritdoc />
    public string Description => "Execute MeTTa symbolic reasoning queries. Bridges Neural (LLM) and Symbolic (MeTTa) computation for neuro-symbolic AI. Supports queries, facts, rules, and plan verification.";

    /// <inheritdoc />
    public string? JsonSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""expression"": {
                ""type"": ""string"",
                ""description"": ""The MeTTa expression to execute (e.g., '!(+ 2 3)', '!(match &self (fact $x) $x)')""
            },
            ""operation"": {
                ""type"": ""string"",
                ""enum"": [""query"", ""add_fact"", ""apply_rule"", ""verify_plan""],
                ""description"": ""The type of MeTTa operation to perform. Defaults to 'query' if not specified."",
                ""default"": ""query""
            }
        },
        ""required"": [""expression""]
    }";

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<string, string>.Failure("MeTTa expression cannot be empty");
        }

        try
        {
            // Parse JSON input if provided, otherwise treat as direct expression
            string expression;
            string operation = "query";

            if (input.TrimStart().StartsWith("{"))
            {
                try
                {
                    System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(input);
                    if (json.RootElement.TryGetProperty("expression", out System.Text.Json.JsonElement exprProp))
                    {
                        expression = exprProp.GetString() ?? input;
                    }
                    else
                    {
                        return Result<string, string>.Failure("JSON input must contain 'expression' property");
                    }

                    if (json.RootElement.TryGetProperty("operation", out System.Text.Json.JsonElement opProp))
                    {
                        operation = opProp.GetString()?.ToLowerInvariant() ?? "query";
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // If JSON parsing fails, treat as direct expression
                    expression = input;
                }
            }
            else
            {
                expression = input;
            }

            // Execute the appropriate operation based on type
            return operation switch
            {
                "query" => await this.engine.ExecuteQueryAsync(expression, ct),
                "add_fact" => await this.ExecuteAddFactAsync(expression, ct),
                "apply_rule" => await this.engine.ApplyRuleAsync(expression, ct),
                "verify_plan" => await this.ExecuteVerifyPlanAsync(expression, ct),
                _ => Result<string, string>.Failure($"Unknown operation: {operation}. Valid operations: query, add_fact, apply_rule, verify_plan")
            };
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"MeTTa execution failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> ExecuteAddFactAsync(string fact, CancellationToken ct)
    {
        Result<Unit, string> result = await this.engine.AddFactAsync(fact, ct);
        return result.Match(
            _ => Result<string, string>.Success($"Fact added successfully: {fact}"),
            error => Result<string, string>.Failure(error));
    }

    private async Task<Result<string, string>> ExecuteVerifyPlanAsync(string plan, CancellationToken ct)
    {
        Result<bool, string> result = await this.engine.VerifyPlanAsync(plan, ct);
        return result.Match(
            isValid => Result<string, string>.Success(isValid ? $"✓ Plan is valid: {plan}" : $"✗ Plan is invalid: {plan}"),
            error => Result<string, string>.Failure(error));
    }
}
