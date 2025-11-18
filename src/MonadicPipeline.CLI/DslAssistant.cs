#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text;
using LangChainPipeline.Agent.MetaAI;

namespace LangChainPipeline.CLI;

/// <summary>
/// GitHub Copilot-like assistant for MonadicPipeline CLI DSL.
/// Provides intelligent code completion, suggestions, and error recovery for pipeline DSL.
/// </summary>
public class DslAssistant
{
    private readonly ToolAwareChatModel _llm;
    private readonly ToolRegistry _tools;
    private readonly string _systemPrompt;

    public DslAssistant(ToolAwareChatModel llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
        _systemPrompt = BuildSystemPrompt();
    }

    /// <summary>
    /// Suggests the next pipeline step(s) based on the current DSL context.
    /// </summary>
    /// <param name="currentDsl">Current DSL string (may be incomplete)</param>
    /// <param name="cursorPosition">Position in the DSL where suggestions are needed (optional)</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <returns>List of suggested pipeline steps with explanations</returns>
    public async Task<Result<List<DslSuggestion>, string>> SuggestNextStepAsync(
        string currentDsl,
        int? cursorPosition = null,
        int maxSuggestions = 5)
    {
        try
        {
            // Get available tokens from registry
            IReadOnlyCollection<string> availableTokens = StepRegistry.Tokens;
            string tokenList = string.Join(", ", availableTokens.Take(50));

            // Build context-aware prompt
            string prompt = $@"You are a MonadicPipeline DSL assistant. The user is building a pipeline DSL.

Current DSL: {currentDsl}
Available tokens: {tokenList}

Based on the current pipeline, suggest the {maxSuggestions} most logical next steps. For each suggestion:
1. Provide the token name
2. Include example parameters if applicable
3. Explain why this step makes sense next

Format your response as a numbered list with:
- Token name (with example parameters if applicable)
- Brief explanation (1-2 sentences)

Example format:
1. UseDraft - Generate an initial draft response. This is typically the first step after SetTopic or SetPrompt.
2. UseCritique - Analyze and critique the current draft to identify areas for improvement.";

            (string response, List<ToolExecution> _) = await _llm.GenerateWithToolsAsync(prompt);

            // Parse response into suggestions
            List<DslSuggestion> suggestions = ParseSuggestions(response, maxSuggestions);

            return Result<List<DslSuggestion>, string>.Ok(suggestions);
        }
        catch (Exception ex)
        {
            return Result<List<DslSuggestion>, string>.Error($"Failed to generate suggestions: {ex.Message}");
        }
    }

    /// <summary>
    /// Completes partial DSL tokens with available options.
    /// </summary>
    /// <param name="partialToken">Partial token to complete (e.g., "UseD")</param>
    /// <param name="maxCompletions">Maximum number of completions to return</param>
    /// <returns>List of possible completions</returns>
    public Result<List<string>, string> CompleteToken(string partialToken, int maxCompletions = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(partialToken))
            {
                return Result<List<string>, string>.Ok(StepRegistry.Tokens.Take(maxCompletions).ToList());
            }

            IReadOnlyCollection<string> availableTokens = StepRegistry.Tokens;
            List<string> completions = availableTokens
                .Where(t => t.StartsWith(partialToken, StringComparison.OrdinalIgnoreCase))
                .Take(maxCompletions)
                .ToList();

            return Result<List<string>, string>.Ok(completions);
        }
        catch (Exception ex)
        {
            return Result<List<string>, string>.Error($"Failed to complete token: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates and suggests fixes for DSL syntax errors.
    /// </summary>
    /// <param name="dsl">DSL string to validate</param>
    /// <returns>Validation result with suggested fixes</returns>
    public async Task<Result<DslValidationResult, string>> ValidateAndFixAsync(string dsl)
    {
        try
        {
            // Tokenize to check for parsing errors
            string[] tokens = PipelineDsl.Tokenize(dsl);
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            List<string> suggestions = new List<string>();

            // Check each token
            foreach (string token in tokens)
            {
                (string name, string? args) = ParseToken(token);
                if (!StepRegistry.TryResolveInfo(name, out System.Reflection.MethodInfo? mi) || mi is null)
                {
                    errors.Add($"Unknown token: {name}");
                    
                    // Find similar tokens
                    List<string> similar = FindSimilarTokens(name, 3);
                    if (similar.Count > 0)
                    {
                        suggestions.Add($"Did you mean one of these? {string.Join(", ", similar)}");
                    }
                }
            }

            // If there are errors, ask LLM for fix suggestions
            string? fixedDsl = null;
            if (errors.Count > 0)
            {
                string prompt = $@"The following DSL has errors:
DSL: {dsl}
Errors: {string.Join(", ", errors)}

Available tokens: {string.Join(", ", StepRegistry.Tokens.Take(30))}

Please suggest a corrected version of the DSL that fixes these errors.
Respond with only the corrected DSL, no explanation.";

                (string response, List<ToolExecution> _) = await _llm.GenerateWithToolsAsync(prompt);
                fixedDsl = response.Trim();
            }

            DslValidationResult result = new DslValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                Suggestions = suggestions,
                FixedDsl = fixedDsl
            };

            return Result<DslValidationResult, string>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<DslValidationResult, string>.Error($"Validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Explains what a DSL pipeline does in natural language.
    /// </summary>
    /// <param name="dsl">DSL pipeline to explain</param>
    /// <returns>Natural language explanation</returns>
    public async Task<Result<string, string>> ExplainDslAsync(string dsl)
    {
        try
        {
            string explanation = PipelineDsl.Explain(dsl);
            
            string prompt = $@"You are explaining a MonadicPipeline DSL to a developer.

DSL: {dsl}

Technical breakdown:
{explanation}

Provide a clear, concise explanation of what this pipeline does in 2-3 sentences.
Focus on the high-level purpose and flow, not implementation details.";

            (string response, List<ToolExecution> _) = await _llm.GenerateWithToolsAsync(prompt);

            return Result<string, string>.Ok(response.Trim());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Error($"Failed to explain DSL: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a DSL pipeline interactively by asking clarifying questions.
    /// </summary>
    /// <param name="goal">High-level goal for the pipeline</param>
    /// <returns>Generated DSL pipeline</returns>
    public async Task<Result<string, string>> BuildDslInteractivelyAsync(string goal)
    {
        try
        {
            string availableTokensInfo = GetTokenGroupsDescription();
            
            string prompt = $@"You are a MonadicPipeline DSL assistant. Help build a pipeline for this goal:
Goal: {goal}

Available pipeline tokens and their purposes:
{availableTokensInfo}

Build a complete DSL pipeline using the | operator to chain steps.
Consider:
1. Start with SetTopic or SetPrompt to establish context
2. Use UseDraft to generate initial content
3. Use UseCritique to review quality
4. Use UseImprove to refine based on critique
5. Add UseIngest or UseDir if document processing is needed
6. Add Retrieve if RAG is needed

Respond with only the DSL pipeline, no explanation.
Example format: SetTopic('AI Ethics') | UseDraft | UseCritique | UseImprove";

            (string response, List<ToolExecution> _) = await _llm.GenerateWithToolsAsync(prompt);

            return Result<string, string>.Ok(response.Trim());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Error($"Failed to build DSL: {ex.Message}");
        }
    }

    // Private helper methods

    private string BuildSystemPrompt()
    {
        return @"You are an expert assistant for the MonadicPipeline CLI DSL. 
You help developers build type-safe, composable AI pipelines using functional programming patterns.
You provide intelligent code completion, error recovery, and guidance.";
    }

    private List<DslSuggestion> ParseSuggestions(string response, int maxSuggestions)
    {
        List<DslSuggestion> suggestions = new List<DslSuggestion>();
        string[] lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string line in lines.Take(maxSuggestions * 3)) // Process more lines than needed
        {
            // Match numbered list format: "1. TokenName - explanation"
            if (System.Text.RegularExpressions.Regex.Match(line, @"^\d+\.\s+(\w+)(?:\([^)]*\))?\s*-\s*(.+)$") is var match && match.Success)
            {
                string token = match.Groups[1].Value;
                string explanation = match.Groups[2].Value;
                
                suggestions.Add(new DslSuggestion
                {
                    Token = token,
                    Explanation = explanation,
                    Confidence = 1.0 - (suggestions.Count * 0.1) // Decrease confidence for later suggestions
                });

                if (suggestions.Count >= maxSuggestions)
                    break;
            }
        }

        return suggestions;
    }

    private (string name, string? args) ParseToken(string token)
    {
        string name = token;
        string? args = null;
        System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
            token, 
            @"^(?<name>[A-Za-z0-9_<>:, ]+)\s*\((?<args>.*)\)\s*$");
        
        if (m.Success)
        {
            name = m.Groups["name"].Value.Trim();
            args = m.Groups["args"].Value.Trim();
        }

        return (name, args);
    }

    private List<string> FindSimilarTokens(string target, int maxResults)
    {
        IReadOnlyCollection<string> availableTokens = StepRegistry.Tokens;
        
        // Simple similarity based on edit distance
        return availableTokens
            .Select(t => new { Token = t, Distance = LevenshteinDistance(target.ToLower(), t.ToLower()) })
            .Where(x => x.Distance <= 3)
            .OrderBy(x => x.Distance)
            .Take(maxResults)
            .Select(x => x.Token)
            .ToList();
    }

    private int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        int[,] d = new int[source.Length + 1, target.Length + 1];

        for (int i = 0; i <= source.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= target.Length; j++)
            d[0, j] = j;

        for (int j = 1; j <= target.Length; j++)
        {
            for (int i = 1; i <= source.Length; i++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[source.Length, target.Length];
    }

    private string GetTokenGroupsDescription()
    {
        StringBuilder sb = new StringBuilder();
        var groups = StepRegistry.GetTokenGroups().Take(20);
        
        foreach ((System.Reflection.MethodInfo method, IReadOnlyList<string> names) in groups)
        {
            string tokenNames = string.Join(", ", names);
            sb.AppendLine($"- {tokenNames}: {method.DeclaringType?.Name}.{method.Name}()");
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Represents a DSL suggestion with explanation.
/// </summary>
public class DslSuggestion
{
    public string Token { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// Result of DSL validation with errors and suggested fixes.
/// </summary>
public class DslValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> Suggestions { get; set; } = new List<string>();
    public string? FixedDsl { get; set; }
}
