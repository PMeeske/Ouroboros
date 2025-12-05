// <copyright file="MeTTaTools.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools.MeTTa;

/// <summary>
/// Tool for executing MeTTa symbolic queries.
/// </summary>
public sealed class MeTTaQueryTool : ITool
{
    private readonly IMeTTaEngine engine;

    /// <inheritdoc />
    public string Name => "metta_query";

    /// <inheritdoc />
    public string Description => "Execute a MeTTa symbolic query against the knowledge base. Returns symbolic reasoning results.";

    /// <inheritdoc />
    public string? JsonSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""query"": {
                ""type"": ""string"",
                ""description"": ""The MeTTa query expression to execute""
            }
        },
        ""required"": [""query""]
    }";

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaQueryTool"/> class.
    /// Creates a new MeTTa query tool.
    /// </summary>
    /// <param name="engine">The MeTTa engine to use for query execution.</param>
    public MeTTaQueryTool(IMeTTaEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<string, string>.Failure("Query cannot be empty");
        }

        // Parse JSON if provided, otherwise treat as direct query
        string query;
        try
        {
            System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(input);
            if (json.RootElement.TryGetProperty("query", out System.Text.Json.JsonElement queryProp))
            {
                query = queryProp.GetString() ?? input;
            }
            else
            {
                query = input;
            }
        }
        catch
        {
            // If not valid JSON, use input directly
            query = input;
        }

        return await this.engine.ExecuteQueryAsync(query, ct);
    }
}

/// <summary>
/// Tool for applying MeTTa inference rules.
/// </summary>
public sealed class MeTTaRuleTool : ITool
{
    private readonly IMeTTaEngine engine;

    /// <inheritdoc />
    public string Name => "metta_rule";

    /// <inheritdoc />
    public string Description => "Apply a MeTTa inference rule to derive new knowledge from existing facts.";

    /// <inheritdoc />
    public string? JsonSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""rule"": {
                ""type"": ""string"",
                ""description"": ""The MeTTa rule expression to apply""
            }
        },
        ""required"": [""rule""]
    }";

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaRuleTool"/> class.
    /// Creates a new MeTTa rule tool.
    /// </summary>
    /// <param name="engine">The MeTTa engine to use for rule application.</param>
    public MeTTaRuleTool(IMeTTaEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<string, string>.Failure("Rule cannot be empty");
        }

        // Parse JSON if provided
        string rule;
        try
        {
            System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(input);
            if (json.RootElement.TryGetProperty("rule", out System.Text.Json.JsonElement ruleProp))
            {
                rule = ruleProp.GetString() ?? input;
            }
            else
            {
                rule = input;
            }
        }
        catch
        {
            rule = input;
        }

        return await this.engine.ApplyRuleAsync(rule, ct);
    }
}

/// <summary>
/// Tool for verifying plans using symbolic reasoning.
/// </summary>
public sealed class MeTTaPlanVerifierTool : ITool
{
    private readonly IMeTTaEngine engine;

    /// <inheritdoc />
    public string Name => "metta_verify_plan";

    /// <inheritdoc />
    public string Description => "Verify a plan using MeTTa symbolic reasoning. Returns true/false with reasoning.";

    /// <inheritdoc />
    public string? JsonSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""plan"": {
                ""type"": ""string"",
                ""description"": ""The plan to verify in MeTTa format""
            }
        },
        ""required"": [""plan""]
    }";

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaPlanVerifierTool"/> class.
    /// Creates a new MeTTa plan verifier tool.
    /// </summary>
    /// <param name="engine">The MeTTa engine to use for plan verification.</param>
    public MeTTaPlanVerifierTool(IMeTTaEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<string, string>.Failure("Plan cannot be empty");
        }

        // Parse JSON if provided
        string plan;
        try
        {
            System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(input);
            if (json.RootElement.TryGetProperty("plan", out System.Text.Json.JsonElement planProp))
            {
                plan = planProp.GetString() ?? input;
            }
            else
            {
                plan = input;
            }
        }
        catch
        {
            plan = input;
        }

        Result<bool, string> result = await this.engine.VerifyPlanAsync(plan, ct);

        return result.Match(
            isValid => Result<string, string>.Success(isValid ? "Plan is valid" : "Plan is invalid"),
            error => Result<string, string>.Failure(error));
    }
}

/// <summary>
/// Tool for adding facts to the MeTTa knowledge base.
/// </summary>
public sealed class MeTTaFactTool : ITool
{
    private readonly IMeTTaEngine engine;

    /// <inheritdoc />
    public string Name => "metta_add_fact";

    /// <inheritdoc />
    public string Description => "Add a fact to the MeTTa knowledge base for symbolic reasoning.";

    /// <inheritdoc />
    public string? JsonSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""fact"": {
                ""type"": ""string"",
                ""description"": ""The fact to add in MeTTa format""
            }
        },
        ""required"": [""fact""]
    }";

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaFactTool"/> class.
    /// Creates a new MeTTa fact tool.
    /// </summary>
    /// <param name="engine">The MeTTa engine to use.</param>
    public MeTTaFactTool(IMeTTaEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<string, string>.Failure("Fact cannot be empty");
        }

        // Parse JSON if provided
        string fact;
        try
        {
            System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(input);
            if (json.RootElement.TryGetProperty("fact", out System.Text.Json.JsonElement factProp))
            {
                fact = factProp.GetString() ?? input;
            }
            else
            {
                fact = input;
            }
        }
        catch
        {
            fact = input;
        }

        Result<Unit, string> result = await this.engine.AddFactAsync(fact, ct);

        return result.Match(
            _ => Result<string, string>.Success("Fact added successfully"),
            error => Result<string, string>.Failure(error));
    }
}
