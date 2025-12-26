// <copyright file="litellm-optimization-scenario.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

/*
 * Complex CLI Optimization Scenario using LiteLLM gpt-oss-120b-sovereign
 *
 * This example demonstrates a multi-stage optimization pipeline that:
 * 1. Analyzes codebase performance bottlenecks
 * 2. Generates optimization proposals
 * 3. Critiques proposals for correctness
 * 4. Iteratively refines the solution
 * 5. Validates against constraints
 *
 * Usage:
 *   $Env:CHAT_ENDPOINT = "https://adesso-ai-hub.3asabc.de"
 *   $Env:CHAT_API_KEY = "sk-your-api-key"
 *   $Env:CHAT_ENDPOINT_TYPE = "LiteLLM"
 *   dotnet run --project examples/OptimizationScenario
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Reasoning;
using Ouroboros.Providers;
using Ouroboros.Tools;
using Ouroboros.VectorStores;

namespace Ouroboros.Examples;

/// <summary>
/// Represents an optimization proposal with metrics.
/// </summary>
public record OptimizationProposal(
    string Description,
    string CodeChange,
    double ExpectedSpeedup,
    List<string> Risks,
    string Rationale);

/// <summary>
/// Represents validation results.
/// </summary>
public record ValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings,
    double ConfidenceScore);

/// <summary>
/// Complex optimization scenario using LiteLLM with monadic pipeline composition.
/// </summary>
public static class LiteLLMOptimizationScenario
{
    /// <summary>
    /// Main entry point for optimization scenario.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Ouroboros: LiteLLM Optimization Scenario ===\n");

        // Parse CLI arguments
        var options = ParseArguments(args);
        if (options is null)
        {
            PrintUsage();
            return 1;
        }

        // Setup LiteLLM model
        var settings = new ChatRuntimeSettings(
            temperature: 0.3f,  // Lower for more deterministic optimization
            maxTokens: 2048,
            timeout: 180,
            stream: false);

        var litellm = new LiteLLMChatModel(
            endpoint: options.Endpoint,
            apiKey: options.ApiKey,
            model: options.Model,
            settings: settings);

        // Setup tools for code analysis
        var tools = new ToolRegistry()
            .RegisterTool<FileSystemTool>()
            .RegisterTool<WebSearchTool>();

        var toolAwareLLM = new ToolAwareChatModel(litellm, tools);

        // Setup RAG for codebase context
        var vectorStore = new TrackedVectorStore();
        var dataSource = new FileSystemDataSource(options.CodebasePath);

        try
        {
            // Execute multi-stage optimization pipeline
            var result = await ExecuteOptimizationPipeline(
                toolAwareLLM,
                tools,
                vectorStore,
                dataSource,
                options);

            Console.WriteLine($"\n✓ Optimization complete: {result.Description}");
            Console.WriteLine($"  Expected speedup: {result.ExpectedSpeedup:P}");
            Console.WriteLine($"  Risks identified: {result.Risks.Count}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Executes the multi-stage optimization pipeline using monadic composition.
    /// </summary>
    private static async Task<OptimizationProposal> ExecuteOptimizationPipeline(
        ToolAwareChatModel llm,
        ToolRegistry tools,
        IVectorStore vectorStore,
        IDataSource dataSource,
        OptimizationOptions options)
    {
        Console.WriteLine("[Stage 1] Analyzing codebase for bottlenecks...");

        // Stage 1: Bottleneck Analysis
        var analysisStep = Step.Pure<string>()
            .Bind(async target =>
            {
                // Load codebase context with RAG
                var context = await LoadCodebaseContext(
                    vectorStore,
                    dataSource,
                    target,
                    topK: 10);

                var prompt = $@"Analyze the following codebase for performance bottlenecks.
Focus on: {target}

Context:
{context}

Identify:
1. Critical performance bottlenecks
2. Root causes
3. Profiling data patterns
4. Potential optimization strategies

Provide detailed technical analysis.";

                return await llm.GenerateWithToolsAsync(prompt, CancellationToken.None);
            });

        string bottleneckAnalysis = await analysisStep(options.OptimizationTarget);

        Console.WriteLine($"  Found {CountBottlenecks(bottleneckAnalysis)} bottleneck(s)");
        Console.WriteLine("\n[Stage 2] Generating optimization proposals...");

        // Stage 2: Proposal Generation
        var proposalStep = Step.Pure<(string analysis, string target)>()
            .Bind(async input =>
            {
                var prompt = $@"Based on this bottleneck analysis:
{input.analysis}

Generate 3 optimization proposals for: {input.target}

For each proposal, provide:
1. Specific code changes (with before/after examples)
2. Expected performance improvement (percentage)
3. Implementation complexity (low/medium/high)
4. Potential risks and side effects
5. Testing strategy

Format as structured data for parsing.";

                return await llm.GenerateWithToolsAsync(prompt, CancellationToken.None);
            })
            .Map(ParseProposals);

        var proposals = await proposalStep((bottleneckAnalysis, options.OptimizationTarget));

        Console.WriteLine($"  Generated {proposals.Count} proposal(s)");
        Console.WriteLine("\n[Stage 3] Critiquing proposals for correctness...");

        // Stage 3: Iterative Refinement using Draft-Critique-Improve pattern
        var refinementPipeline = proposals.Select(proposal =>
            CreateRefinementPipeline(llm, tools, proposal, options)).ToList();

        var refinedProposals = await Task.WhenAll(
            refinementPipeline.Select(pipeline => pipeline(proposal => proposal)));

        Console.WriteLine($"  Refined {refinedProposals.Length} proposal(s)");
        Console.WriteLine("\n[Stage 4] Validating against constraints...");

        // Stage 4: Constraint Validation
        var validationStep = Step.Pure<OptimizationProposal>()
            .Bind(async proposal =>
            {
                var prompt = $@"Validate this optimization proposal against constraints:

Proposal:
{proposal.Description}

Code Change:
{proposal.CodeChange}

Constraints:
- Must not break existing tests
- Must maintain API compatibility
- Must not introduce memory leaks
- Must improve performance by at least {options.MinSpeedup:P}
- Must have acceptable risk level: {options.MaxRisk}

Provide detailed validation report with:
1. Pass/fail for each constraint
2. Confidence score (0-1)
3. Specific issues found
4. Recommendations";

                string validationReport = await llm.GenerateWithToolsAsync(
                    prompt,
                    CancellationToken.None);

                return (proposal, ParseValidation(validationReport));
            })
            .Map(result =>
            {
                if (result.Item2.IsValid && result.Item2.ConfidenceScore >= 0.8)
                {
                    return Result<OptimizationProposal, string>.Ok(result.Item1);
                }

                return Result<OptimizationProposal, string>.Error(
                    $"Validation failed: {string.Join(", ", result.Item2.Errors)}");
            });

        // Execute validation on all refined proposals
        var validationResults = await Task.WhenAll(
            refinedProposals.Select(p => validationStep(p)));

        // Select best validated proposal
        var bestProposal = validationResults
            .Where(r => r.Match(_ => true, _ => false))
            .Select(r => r.Match(p => p, _ => null!))
            .OrderByDescending(p => p?.ExpectedSpeedup ?? 0)
            .FirstOrDefault();

        if (bestProposal is null)
        {
            throw new InvalidOperationException(
                "No proposals passed validation. " +
                string.Join("; ", validationResults
                    .Select(r => r.Match(_ => "", e => e))
                    .Where(e => !string.IsNullOrEmpty(e))));
        }

        Console.WriteLine($"  Selected proposal with {bestProposal.ExpectedSpeedup:P} speedup");

        // Stage 5: Generate implementation plan
        Console.WriteLine("\n[Stage 5] Generating implementation plan...");

        var implementationStep = Step.Pure<OptimizationProposal>()
            .Bind(async proposal =>
            {
                var prompt = $@"Create a detailed implementation plan for this optimization:

{proposal.Description}

Code changes:
{proposal.CodeChange}

Provide:
1. Step-by-step implementation guide
2. Required refactoring
3. Test cases to add/modify
4. Deployment strategy
5. Rollback plan
6. Monitoring and metrics";

                string plan = await llm.GenerateWithToolsAsync(prompt, CancellationToken.None);

                Console.WriteLine($"\n--- Implementation Plan ---");
                Console.WriteLine(plan);

                return proposal;
            });

        return await implementationStep(bestProposal);
    }

    /// <summary>
    /// Creates a refinement pipeline using Draft-Critique-Improve pattern.
    /// </summary>
    private static Step<Func<OptimizationProposal, OptimizationProposal>, OptimizationProposal>
        CreateRefinementPipeline(
            ToolAwareChatModel llm,
            ToolRegistry tools,
            OptimizationProposal initialProposal,
            OptimizationOptions options)
    {
        return Step.Pure<Func<OptimizationProposal, OptimizationProposal>>()
            .Bind(async selector =>
            {
                var currentProposal = selector(initialProposal);
                var iterations = 0;
                const int maxIterations = 3;

                while (iterations < maxIterations)
                {
                    iterations++;
                    Console.WriteLine($"    Iteration {iterations}: Critiquing proposal...");

                    // Critique
                    var critiquePrompt = $@"Critically analyze this optimization proposal:

{currentProposal.Description}

Code:
{currentProposal.CodeChange}

Expected speedup: {currentProposal.ExpectedSpeedup:P}

Identify:
1. Logical errors or bugs
2. Edge cases not handled
3. Performance assumptions that may not hold
4. Alternative approaches
5. Improvements needed

Be rigorous and specific.";

                    string critique = await llm.GenerateWithToolsAsync(
                        critiquePrompt,
                        CancellationToken.None);

                    if (IsGoodEnough(critique, currentProposal))
                    {
                        Console.WriteLine($"    Converged after {iterations} iteration(s)");
                        break;
                    }

                    // Improve
                    Console.WriteLine($"    Iteration {iterations}: Improving based on critique...");

                    var improvePrompt = $@"Improve this proposal based on critique:

Original Proposal:
{currentProposal.Description}

Critique:
{critique}

Provide improved version with:
1. Fixed issues
2. Better code implementation
3. Updated performance estimate
4. Additional risk mitigation";

                    string improved = await llm.GenerateWithToolsAsync(
                        improvePrompt,
                        CancellationToken.None);

                    currentProposal = ParseSingleProposal(improved, currentProposal);
                }

                return currentProposal;
            });
    }

    /// <summary>
    /// Loads codebase context using RAG.
    /// </summary>
    private static async Task<string> LoadCodebaseContext(
        IVectorStore vectorStore,
        IDataSource dataSource,
        string target,
        int topK)
    {
        // Load and embed documents
        var documents = await dataSource.LoadDocumentsAsync(CancellationToken.None);
        foreach (var doc in documents.Take(100))  // Limit for demo
        {
            await vectorStore.AddDocumentAsync(doc, CancellationToken.None);
        }

        // Retrieve relevant context
        var results = await vectorStore.SearchAsync(target, topK, CancellationToken.None);

        return string.Join("\n\n---\n\n", results.Select(r =>
            $"File: {r.Metadata.GetValueOrDefault("file", "unknown")}\n{r.Content}"));
    }

    /// <summary>
    /// Parses multiple proposals from LLM response.
    /// </summary>
    private static List<OptimizationProposal> ParseProposals(string response)
    {
        // Simplified parsing - in production, use structured output
        var proposals = new List<OptimizationProposal>();

        // Split by proposal markers
        var sections = response.Split(new[] { "Proposal ", "## Proposal" },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections.Skip(1).Take(3))
        {
            proposals.Add(new OptimizationProposal(
                Description: ExtractSection(section, "Description", "Expected"),
                CodeChange: ExtractSection(section, "Code", "Expected"),
                ExpectedSpeedup: ParseSpeedup(section),
                Risks: ExtractRisks(section),
                Rationale: ExtractSection(section, "Rationale", "###")));
        }

        return proposals.Any() ? proposals :
            new List<OptimizationProposal>
            {
                ParseSingleProposal(response, null!)
            };
    }

    /// <summary>
    /// Parses a single proposal from text.
    /// </summary>
    private static OptimizationProposal ParseSingleProposal(
        string text,
        OptimizationProposal? fallback)
    {
        try
        {
            return new OptimizationProposal(
                Description: ExtractSection(text, "Description", "Code") ??
                           ExtractSection(text, "Proposal", "Code") ??
                           "Optimized implementation",
                CodeChange: ExtractSection(text, "Code", "Performance") ??
                          ExtractSection(text, "```", "```") ??
                          text.Substring(0, Math.Min(500, text.Length)),
                ExpectedSpeedup: ParseSpeedup(text),
                Risks: ExtractRisks(text),
                Rationale: ExtractSection(text, "Rationale", "###") ?? "Analysis-based optimization");
        }
        catch
        {
            return fallback ?? new OptimizationProposal(
                "Generic optimization",
                text,
                0.1,
                new List<string> { "Parsing incomplete" },
                "Fallback proposal");
        }
    }

    /// <summary>
    /// Parses validation result from LLM response.
    /// </summary>
    private static ValidationResult ParseValidation(string report)
    {
        var isValid = report.Contains("PASS", StringComparison.OrdinalIgnoreCase) ||
                     report.Contains("Valid", StringComparison.OrdinalIgnoreCase);

        var confidence = ExtractConfidence(report);

        var errors = ExtractList(report, "Error", "Warning");
        var warnings = ExtractList(report, "Warning", "Recommendation");

        return new ValidationResult(isValid, errors, warnings, confidence);
    }

    /// <summary>
    /// Determines if proposal is good enough to stop iterating.
    /// </summary>
    private static bool IsGoodEnough(string critique, OptimizationProposal proposal)
    {
        var issueCount = CountIssues(critique);
        return issueCount < 2 && proposal.ExpectedSpeedup > 0.15;
    }

    // Helper methods for parsing
    private static string? ExtractSection(string text, string start, string end)
    {
        var startIdx = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return null;

        startIdx += start.Length;
        var endIdx = text.IndexOf(end, startIdx, StringComparison.OrdinalIgnoreCase);

        if (endIdx < 0) endIdx = Math.Min(startIdx + 500, text.Length);

        return text.Substring(startIdx, endIdx - startIdx).Trim();
    }

    private static double ParseSpeedup(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(\d+(?:\.\d+)?)\s*%|\b(\d+(?:\.\d+)?)\s*x\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var value = double.Parse(match.Groups[1].Success ?
                match.Groups[1].Value : match.Groups[2].Value);

            return match.Value.Contains("x") ? value : value / 100.0;
        }

        return 0.2; // Default assumption
    }

    private static List<string> ExtractRisks(string text)
    {
        var risks = new List<string>();
        var riskSection = ExtractSection(text, "Risk", "Test") ??
                         ExtractSection(text, "Risk", "###");

        if (riskSection != null)
        {
            risks.AddRange(riskSection
                .Split('\n')
                .Where(line => line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("*"))
                .Select(line => line.TrimStart('-', '*', ' '))
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        return risks.Any() ? risks : new List<string> { "Standard implementation risks" };
    }

    private static List<string> ExtractList(string text, string section, string nextSection)
    {
        var list = new List<string>();
        var content = ExtractSection(text, section, nextSection) ?? "";

        list.AddRange(content
            .Split('\n')
            .Where(line => line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("*"))
            .Select(line => line.TrimStart('-', '*', ' ').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line)));

        return list;
    }

    private static double ExtractConfidence(string text)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"confidence[:\s]+(\d+(?:\.\d+)?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && double.TryParse(match.Groups[1].Value, out double confidence))
        {
            return confidence > 1 ? confidence / 100.0 : confidence;
        }

        return 0.7; // Default
    }

    private static int CountBottlenecks(string analysis)
    {
        return System.Text.RegularExpressions.Regex.Matches(
            analysis,
            @"bottleneck|slow|inefficient|performance issue",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
    }

    private static int CountIssues(string critique)
    {
        return System.Text.RegularExpressions.Regex.Matches(
            critique,
            @"issue|problem|error|bug|concern",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
    }

    // CLI argument parsing
    private static OptimizationOptions? ParseArguments(string[] args)
    {
        var endpoint = Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        var apiKey = Environment.GetEnvironmentVariable("CHAT_API_KEY");
        var model = "gpt-oss-120b-sovereign";
        var codebasePath = Environment.CurrentDirectory;
        var target = "database queries";
        var minSpeedup = 0.2;
        var maxRisk = "medium";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--endpoint":
                    endpoint = args[++i];
                    break;
                case "--api-key":
                    apiKey = args[++i];
                    break;
                case "--model":
                    model = args[++i];
                    break;
                case "--codebase":
                    codebasePath = args[++i];
                    break;
                case "--target":
                    target = args[++i];
                    break;
                case "--min-speedup":
                    minSpeedup = double.Parse(args[++i]);
                    break;
                case "--max-risk":
                    maxRisk = args[++i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        return new OptimizationOptions(
            endpoint!, apiKey!, model, codebasePath, target, minSpeedup, maxRisk);
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
Usage: dotnet run [options]

Required (or set via environment variables):
  --endpoint <url>         LiteLLM endpoint URL (CHAT_ENDPOINT)
  --api-key <key>          API key (CHAT_API_KEY)

Optional:
  --model <name>           Model name (default: gpt-oss-120b-sovereign)
  --codebase <path>        Path to codebase (default: current directory)
  --target <description>   Optimization target (default: 'database queries')
  --min-speedup <decimal>  Minimum speedup required (default: 0.2 = 20%)
  --max-risk <level>       Maximum risk level: low|medium|high (default: medium)

Example:
  $Env:CHAT_ENDPOINT='https://adesso-ai-hub.3asabc.de'
  $Env:CHAT_API_KEY='sk-...'
  dotnet run -- --target 'loop optimizations' --min-speedup 0.3
");
    }
}

/// <summary>
/// Options for optimization scenario.
/// </summary>
public record OptimizationOptions(
    string Endpoint,
    string ApiKey,
    string Model,
    string CodebasePath,
    string OptimizationTarget,
    double MinSpeedup,
    string MaxRisk);

/// <summary>
/// Result type for monadic error handling.
/// </summary>
public record Result<TOk, TError>
{
    private readonly TOk? _ok;
    private readonly TError? _error;
    private readonly bool _isOk;

    private Result(TOk ok)
    {
        _ok = ok;
        _error = default;
        _isOk = true;
    }

    private Result(TError error)
    {
        _ok = default;
        _error = error;
        _isOk = false;
    }

    public static Result<TOk, TError> Ok(TOk value) => new(value);

    public static Result<TOk, TError> Error(TError error) => new(error);

    public TResult Match<TResult>(Func<TOk, TResult> onOk, Func<TError, TResult> onError) =>
        _isOk ? onOk(_ok!) : onError(_error!);
}
