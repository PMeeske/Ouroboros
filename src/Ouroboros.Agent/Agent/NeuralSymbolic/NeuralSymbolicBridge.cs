#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Neural-Symbolic Bridge Implementation
// Bridges neural (LLM) and symbolic (MeTTa) reasoning
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Implementation of neural-symbolic bridge for hybrid reasoning.
/// </summary>
public sealed class NeuralSymbolicBridge : INeuralSymbolicBridge
{
    private readonly IChatCompletionModel _llm;
    private readonly ISymbolicKnowledgeBase _knowledgeBase;

    /// <summary>
    /// Initializes a new instance of the <see cref="NeuralSymbolicBridge"/> class.
    /// </summary>
    /// <param name="llm">The language model for neural reasoning.</param>
    /// <param name="knowledgeBase">The symbolic knowledge base.</param>
    public NeuralSymbolicBridge(IChatCompletionModel llm, ISymbolicKnowledgeBase knowledgeBase)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _knowledgeBase = knowledgeBase ?? throw new ArgumentNullException(nameof(knowledgeBase));
    }

    /// <inheritdoc/>
    public async Task<Result<List<SymbolicRule>, string>> ExtractRulesFromSkillAsync(
        Skill skill,
        CancellationToken ct = default)
    {
        if (skill == null)
            return Result<List<SymbolicRule>, string>.Failure("Skill cannot be null");

        try
        {
            // Build prompt for rule extraction
            var prompt = BuildRuleExtractionPrompt(skill);
            var response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse rules from response
            var rules = ParseExtractedRules(response, skill);

            return Result<List<SymbolicRule>, string>.Success(rules);
        }
        catch (Exception ex)
        {
            return Result<List<SymbolicRule>, string>.Failure($"Rule extraction failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<MeTTaExpression, string>> NaturalLanguageToMeTTaAsync(
        string naturalLanguage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
            return Result<MeTTaExpression, string>.Failure("Natural language cannot be empty");

        try
        {
            var prompt = $@"Convert the following natural language statement to a MeTTa expression.
Use proper MeTTa syntax with S-expressions.

Natural language: {naturalLanguage}

MeTTa expression:";

            var response = await _llm.GenerateTextAsync(prompt, ct);
            var expression = ParseMeTTaExpression(response);

            return Result<MeTTaExpression, string>.Success(expression);
        }
        catch (Exception ex)
        {
            return Result<MeTTaExpression, string>.Failure($"Conversion failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> MeTTaToNaturalLanguageAsync(
        MeTTaExpression expression,
        CancellationToken ct = default)
    {
        if (expression == null)
            return Result<string, string>.Failure("Expression cannot be null");

        try
        {
            var prompt = $@"Explain the following MeTTa expression in clear natural language:

MeTTa: {expression.RawExpression}

Natural language explanation:";

            var response = await _llm.GenerateTextAsync(prompt, ct);
            var explanation = response.Trim();

            return Result<string, string>.Success(explanation);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Explanation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ReasoningResult, string>> HybridReasonAsync(
        string query,
        ReasoningMode mode = ReasoningMode.SymbolicFirst,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Result<ReasoningResult, string>.Failure("Query cannot be empty");

        var startTime = Stopwatch.StartNew();
        var steps = new List<ReasoningStep>();
        bool symbolicSucceeded = false;
        bool neuralSucceeded = false;
        string answer = string.Empty;
        double confidence = 0.0;

        try
        {
            switch (mode)
            {
                case ReasoningMode.SymbolicFirst:
                    // Try symbolic reasoning first
                    var symbolicResult = await TrySymbolicReasoning(query, ct);
                    if (symbolicResult.IsSuccess)
                    {
                        symbolicSucceeded = true;
                        answer = symbolicResult.Value.answer;
                        confidence = symbolicResult.Value.confidence;
                        steps.AddRange(symbolicResult.Value.steps);
                    }
                    else
                    {
                        // Fall back to neural
                        var neuralResult = await TryNeuralReasoning(query, ct);
                        neuralSucceeded = neuralResult.IsSuccess;
                        answer = neuralResult.IsSuccess ? neuralResult.Value.answer : query;
                        confidence = neuralResult.IsSuccess ? neuralResult.Value.confidence : 0.0;
                        if (neuralResult.IsSuccess)
                            steps.AddRange(neuralResult.Value.steps);
                    }
                    break;

                case ReasoningMode.NeuralFirst:
                    // Try neural first, verify with symbolic
                    var neuralFirst = await TryNeuralReasoning(query, ct);
                    neuralSucceeded = neuralFirst.IsSuccess;
                    if (neuralFirst.IsSuccess)
                    {
                        answer = neuralFirst.Value.answer;
                        steps.AddRange(neuralFirst.Value.steps);
                        
                        // Try to verify symbolically
                        var verification = await TrySymbolicReasoning(query, ct);
                        symbolicSucceeded = verification.IsSuccess;
                        confidence = symbolicSucceeded ? 0.9 : 0.6;
                    }
                    break;

                case ReasoningMode.Parallel:
                    // Run both in parallel
                    var symbolicTask = TrySymbolicReasoning(query, ct);
                    var neuralTask = TryNeuralReasoning(query, ct);
                    await Task.WhenAll(symbolicTask, neuralTask);
                    
                    var symbolicParallel = await symbolicTask;
                    var neuralParallel = await neuralTask;
                    
                    symbolicSucceeded = symbolicParallel.IsSuccess;
                    neuralSucceeded = neuralParallel.IsSuccess;
                    
                    if (symbolicSucceeded && neuralSucceeded)
                    {
                        // Combine results
                        answer = $"Symbolic: {symbolicParallel.Value.answer}\nNeural: {neuralParallel.Value.answer}";
                        confidence = 0.95;
                        steps.AddRange(symbolicParallel.Value.steps);
                        steps.AddRange(neuralParallel.Value.steps);
                    }
                    else if (symbolicSucceeded)
                    {
                        answer = symbolicParallel.Value.answer;
                        confidence = symbolicParallel.Value.confidence;
                        steps.AddRange(symbolicParallel.Value.steps);
                    }
                    else if (neuralSucceeded)
                    {
                        answer = neuralParallel.Value.answer;
                        confidence = neuralParallel.Value.confidence;
                        steps.AddRange(neuralParallel.Value.steps);
                    }
                    break;

                case ReasoningMode.SymbolicOnly:
                    var symbolicOnly = await TrySymbolicReasoning(query, ct);
                    symbolicSucceeded = symbolicOnly.IsSuccess;
                    if (symbolicOnly.IsSuccess)
                    {
                        answer = symbolicOnly.Value.answer;
                        confidence = symbolicOnly.Value.confidence;
                        steps.AddRange(symbolicOnly.Value.steps);
                    }
                    break;

                case ReasoningMode.NeuralOnly:
                    var neuralOnly = await TryNeuralReasoning(query, ct);
                    neuralSucceeded = neuralOnly.IsSuccess;
                    if (neuralOnly.IsSuccess)
                    {
                        answer = neuralOnly.Value.answer;
                        confidence = neuralOnly.Value.confidence;
                        steps.AddRange(neuralOnly.Value.steps);
                    }
                    break;
            }

            startTime.Stop();

            var result = new ReasoningResult(
                query,
                answer,
                mode,
                steps,
                confidence,
                symbolicSucceeded,
                neuralSucceeded,
                startTime.Elapsed);

            return Result<ReasoningResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<ReasoningResult, string>.Failure($"Hybrid reasoning failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ConsistencyReport, string>> CheckConsistencyAsync(
        MetaAI.Hypothesis hypothesis,
        IReadOnlyList<SymbolicRule> knowledgeBase,
        CancellationToken ct = default)
    {
        if (hypothesis == null)
            return Result<ConsistencyReport, string>.Failure("Hypothesis cannot be null");

        try
        {
            var conflicts = new List<LogicalConflict>();
            var missing = new List<string>();
            var suggestions = new List<string>();

            // Build prompt for consistency checking
            var prompt = BuildConsistencyCheckPrompt(hypothesis, knowledgeBase);
            var response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse consistency analysis
            var (isConsistent, parsedConflicts, parsedMissing) = ParseConsistencyAnalysis(response, knowledgeBase);
            conflicts.AddRange(parsedConflicts);
            missing.AddRange(parsedMissing);

            if (!isConsistent)
            {
                suggestions.Add("Resolve logical conflicts between hypothesis and existing rules");
                suggestions.Add("Add missing prerequisites to the knowledge base");
            }

            var score = isConsistent ? 1.0 : Math.Max(0.0, 1.0 - (conflicts.Count * 0.2));

            var report = new ConsistencyReport(
                isConsistent,
                conflicts,
                missing,
                suggestions,
                score);

            return Result<ConsistencyReport, string>.Success(report);
        }
        catch (Exception ex)
        {
            return Result<ConsistencyReport, string>.Failure($"Consistency check failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<GroundedConcept, string>> GroundConceptAsync(
        string conceptDescription,
        float[] embedding,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conceptDescription))
            return Result<GroundedConcept, string>.Failure("Concept description cannot be empty");

        if (embedding == null || embedding.Length == 0)
            return Result<GroundedConcept, string>.Failure("Embedding cannot be null or empty");

        try
        {
            var prompt = $@"Given the concept: {conceptDescription}

Provide:
1. A MeTTa type for this concept
2. Key properties (list 3-5)
3. Relations to other concepts (list 2-3)

Format your response as:
Type: <type>
Properties: <prop1>, <prop2>, ...
Relations: <rel1>, <rel2>, ...";

            var response = await _llm.GenerateTextAsync(prompt, ct);
            var (mettaType, properties, relations) = ParseGroundingResponse(response);

            var concept = new GroundedConcept(
                conceptDescription,
                mettaType,
                properties,
                relations,
                embedding,
                0.8); // Base confidence

            return Result<GroundedConcept, string>.Success(concept);
        }
        catch (Exception ex)
        {
            return Result<GroundedConcept, string>.Failure($"Concept grounding failed: {ex.Message}");
        }
    }

    #region Private Helper Methods

    private string BuildRuleExtractionPrompt(Skill skill)
    {
        var stepsText = string.Join("\n", skill.Steps.Select((s, i) => $"{i + 1}. {s}"));
        
        return $@"Extract symbolic rules from this learned skill:

Skill: {skill.Name}
Description: {skill.Description}
Steps:
{stepsText}

For each rule, provide:
1. Rule name
2. MeTTa representation (use S-expression syntax)
3. Natural language description
4. Preconditions
5. Effects

Format each rule as:
RULE: <name>
METTA: <metta-expression>
DESCRIPTION: <description>
PRECONDITIONS: <condition1>, <condition2>, ...
EFFECTS: <effect1>, <effect2>, ...
---";
    }

    private List<SymbolicRule> ParseExtractedRules(string response, Skill skill)
    {
        var rules = new List<SymbolicRule>();
        var ruleBlocks = response.Split("---", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in ruleBlocks)
        {
            try
            {
                var name = ExtractField(block, "RULE:");
                var metta = ExtractField(block, "METTA:");
                var description = ExtractField(block, "DESCRIPTION:");
                var preconditions = ExtractList(block, "PRECONDITIONS:");
                var effects = ExtractList(block, "EFFECTS:");

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(metta))
                {
                    rules.Add(new SymbolicRule(
                        name,
                        metta,
                        description,
                        preconditions,
                        effects,
                        skill.SuccessRate,
                        RuleSource.ExtractedFromSkill));
                }
            }
            catch
            {
                // Skip malformed rule blocks
                continue;
            }
        }

        return rules;
    }

    private MeTTaExpression ParseMeTTaExpression(string response)
    {
        var cleaned = response.Trim();
        
        // Extract S-expression
        var match = Regex.Match(cleaned, @"\([^)]+\)");
        var rawExpression = match.Success ? match.Value : cleaned;

        // Parse symbols and variables
        var symbols = new List<string>();
        var variables = new List<string>();
        var tokens = Regex.Matches(rawExpression, @"\$\w+|\w+");
        
        foreach (Match token in tokens)
        {
            if (token.Value.StartsWith("$"))
                variables.Add(token.Value);
            else if (!string.IsNullOrWhiteSpace(token.Value))
                symbols.Add(token.Value);
        }

        var type = DetermineExpressionType(rawExpression);

        return new MeTTaExpression(
            rawExpression,
            type,
            symbols,
            variables,
            new Dictionary<string, object>());
    }

    private ExpressionType DetermineExpressionType(string expression)
    {
        if (expression.Contains("$"))
            return ExpressionType.Variable;
        if (expression.Contains("->") || expression.Contains("=>"))
            return ExpressionType.Rule;
        if (expression.Contains("?"))
            return ExpressionType.Query;
        if (expression.StartsWith("(") && expression.Contains(" "))
            return ExpressionType.Expression;
        
        return ExpressionType.Atom;
    }

    private async Task<Result<(string answer, List<ReasoningStep> steps, double confidence), string>> TrySymbolicReasoning(
        string query, CancellationToken ct)
    {
        try
        {
            var result = await _knowledgeBase.ExecuteMeTTaQueryAsync(query, ct);
            if (result.IsFailure)
                return Result<(string, List<ReasoningStep>, double), string>.Failure(result.Error);

            var steps = new List<ReasoningStep>
            {
                new ReasoningStep(1, "Symbolic query execution", query, ReasoningStepType.SymbolicDeduction)
            };

            return Result<(string, List<ReasoningStep>, double), string>.Success((result.Value, steps, 0.85));
        }
        catch (Exception ex)
        {
            return Result<(string, List<ReasoningStep>, double), string>.Failure(ex.Message);
        }
    }

    private async Task<Result<(string answer, List<ReasoningStep> steps, double confidence), string>> TryNeuralReasoning(
        string query, CancellationToken ct)
    {
        try
        {
            var prompt = $"Answer the following question using your knowledge:\n\n{query}\n\nAnswer:";
            var response = await _llm.GenerateTextAsync(prompt, ct);

            var steps = new List<ReasoningStep>
            {
                new ReasoningStep(1, "Neural inference", "LLM reasoning", ReasoningStepType.NeuralInference)
            };

            return Result<(string, List<ReasoningStep>, double), string>.Success((response, steps, 0.7));
        }
        catch (Exception ex)
        {
            return Result<(string, List<ReasoningStep>, double), string>.Failure(ex.Message);
        }
    }

    private string BuildConsistencyCheckPrompt(MetaAI.Hypothesis hypothesis, IReadOnlyList<SymbolicRule> knowledgeBase)
    {
        var rulesText = string.Join("\n", knowledgeBase.Take(10).Select(r => 
            $"- {r.Name}: {r.NaturalLanguageDescription}"));

        return $@"Check if this hypothesis is logically consistent with the knowledge base:

Hypothesis: {hypothesis.Statement}
Domain: {hypothesis.Domain}

Knowledge Base Rules:
{rulesText}

Analyze:
1. Are there any logical conflicts?
2. Are there missing prerequisites?
3. Is the hypothesis consistent?

Respond with:
CONSISTENT: Yes/No
CONFLICTS: <conflict descriptions, if any>
MISSING: <missing prerequisites, if any>";
    }

    private (bool isConsistent, List<LogicalConflict> conflicts, List<string> missing) ParseConsistencyAnalysis(
        string response, IReadOnlyList<SymbolicRule> knowledgeBase)
    {
        var isConsistent = response.Contains("CONSISTENT: Yes", StringComparison.OrdinalIgnoreCase);
        var conflicts = new List<LogicalConflict>();
        var missing = new List<string>();

        // Extract conflicts
        var conflictsMatch = Regex.Match(response, @"CONFLICTS:\s*(.+?)(?=MISSING:|$)", RegexOptions.Singleline);
        if (conflictsMatch.Success)
        {
            var conflictText = conflictsMatch.Groups[1].Value.Trim();
            if (!conflictText.Contains("None", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(conflictText))
            {
                // Create a conflict with a descriptive message about the conflict
                // In a real implementation, the LLM response would ideally specify which rules conflict
                // For now, we create a general conflict with the description from the LLM
                var unknownRule = new SymbolicRule(
                    "ConflictingRule", 
                    "(conflict-detected)", 
                    "A rule in the knowledge base conflicts with the hypothesis", 
                    new List<string>(), 
                    new List<string>(), 
                    1.0, 
                    RuleSource.UserProvided);
                    
                conflicts.Add(new LogicalConflict(
                    conflictText,
                    unknownRule,
                    unknownRule,
                    "Review the conflicting rules and resolve inconsistencies manually. Consider refining the hypothesis or updating the knowledge base."));
            }
        }

        // Extract missing prerequisites
        var missingMatch = Regex.Match(response, @"MISSING:\s*(.+?)$", RegexOptions.Singleline);
        if (missingMatch.Success)
        {
            var missingText = missingMatch.Groups[1].Value.Trim();
            if (!missingText.Contains("None", StringComparison.OrdinalIgnoreCase))
            {
                missing.AddRange(missingText.Split(',').Select(m => m.Trim()));
            }
        }

        return (isConsistent, conflicts, missing);
    }

    private (string type, List<string> properties, List<string> relations) ParseGroundingResponse(string response)
    {
        var type = ExtractField(response, "Type:") ?? "Concept";
        var properties = ExtractList(response, "Properties:");
        var relations = ExtractList(response, "Relations:");

        return (type, properties, relations);
    }

    private string ExtractField(string text, string fieldName)
    {
        var match = Regex.Match(text, $@"{Regex.Escape(fieldName)}\s*(.+?)(?=\n[A-Z]+:|$)", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private List<string> ExtractList(string text, string fieldName)
    {
        var field = ExtractField(text, fieldName);
        if (string.IsNullOrWhiteSpace(field))
            return new List<string>();

        return field.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    #endregion
}
