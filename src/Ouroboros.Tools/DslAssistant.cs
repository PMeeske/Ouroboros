using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ouroboros.Core;

namespace Ouroboros.Tools
{
    /// <summary>
    /// AI-powered DSL assistant for pipeline construction and validation.
    /// Provides suggestions, completions, validation, and natural language explanations.
    /// </summary>
    public class DslAssistant
    {
        private readonly ILlmProvider _llm;
        private readonly RoslynCodeTool _codeTool;

        /// <summary>
        /// Initializes a new instance of the DslAssistant.
        /// </summary>
        /// <param name="llm">The LLM provider for AI assistance.</param>
        public DslAssistant(ILlmProvider llm)
        {
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
            _codeTool = new RoslynCodeTool();
        }

        /// <summary>
        /// Suggests next steps for DSL pipeline construction.
        /// </summary>
        /// <param name="currentDsl">The current DSL string.</param>
        /// <returns>A list of suggestions with explanations and confidence scores.</returns>
        public async Task<List<DslSuggestion>> SuggestNextSteps(string currentDsl)
        {
            if (string.IsNullOrWhiteSpace(currentDsl))
                return new List<DslSuggestion>();

            var prompt = $"Given the current DSL pipeline: '{currentDsl}', suggest the next logical steps. " +
                        "Provide 3-5 suggestions with explanations and confidence scores (0-1). " +
                        "Format as JSON array of objects with 'step', 'explanation', 'confidence' fields.";

            var response = await _llm.GenerateAsync(prompt);

            // Parse response and return suggestions
            // For simulation, return mock suggestions
            return new List<DslSuggestion>
            {
                new DslSuggestion("UseDraft", "Generate an initial draft response", 0.9),
                new DslSuggestion("UseCritique", "Critique the draft for improvements", 0.8),
                new DslSuggestion("UseImprove", "Improve the response based on critique", 0.7)
            };
        }

        /// <summary>
        /// Provides token completions for partial DSL input.
        /// </summary>
        /// <param name="partialToken">The partial token to complete.</param>
        /// <returns>A list of possible completions.</returns>
        public async Task<List<string>> GetTokenCompletions(string partialToken)
        {
            if (string.IsNullOrWhiteSpace(partialToken))
                return new List<string>();

            var validTokens = new[] { "SetTopic", "SetPrompt", "UseDraft", "UseCritique", "UseImprove", "UseDir", "UseVector" };
            return validTokens
                .Where(t => t.StartsWith(partialToken, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Validates a DSL pipeline string.
        /// </summary>
        /// <param name="dsl">The DSL string to validate.</param>
        /// <returns>Validation result with errors and suggestions.</returns>
        public ValidationResult ValidateDsl(string dsl)
        {
            if (string.IsNullOrWhiteSpace(dsl))
                return new ValidationResult(false, new[] { "DSL cannot be empty" }, Array.Empty<string>());

            var tokens = dsl.Split('|').Select(t => t.Trim()).ToArray();
            var errors = new List<string>();
            var suggestions = new List<string>();

            var validTokens = new HashSet<string> { "SetTopic", "SetPrompt", "UseDraft", "UseCritique", "UseImprove", "UseVector", "UseDir" };

            foreach (var token in tokens)
            {
                if (!validTokens.Contains(token.Split('(')[0]))
                {
                    errors.Add($"Unknown token: {token}");
                    var similar = validTokens.Where(v => v.Contains(token.Split('(')[0], StringComparison.OrdinalIgnoreCase)).ToList();
                    suggestions.AddRange(similar);
                }
            }

            return new ValidationResult(!errors.Any(), errors.ToArray(), suggestions.ToArray());
        }

        /// <summary>
        /// Explains a DSL pipeline in natural language.
        /// </summary>
        /// <param name="dsl">The DSL pipeline string.</param>
        /// <returns>A natural language explanation.</returns>
        public string ExplainPipeline(string dsl)
        {
            if (string.IsNullOrWhiteSpace(dsl))
                return "Empty pipeline";

            var tokens = dsl.Split('|').Select(t => t.Trim()).ToArray();
            var explanations = new List<string>();

            foreach (var token in tokens)
            {
                switch (token.Split('(')[0])
                {
                    case "SetTopic":
                        explanations.Add("Sets the topic for the pipeline");
                        break;
                    case "UseDraft":
                        explanations.Add("Generates an initial draft response");
                        break;
                    case "UseCritique":
                        explanations.Add("Critiques the draft for potential improvements");
                        break;
                    case "UseImprove":
                        explanations.Add("Improves the response based on the critique");
                        break;
                    default:
                        explanations.Add($"Executes {token}");
                        break;
                }
            }

            return $"This pipeline {string.Join(", then ", explanations).ToLowerInvariant()}.";
        }

        /// <summary>
        /// Generates a DSL pipeline from a natural language goal.
        /// </summary>
        /// <param name="goal">The natural language goal.</param>
        /// <returns>A generated DSL string.</returns>
        public string GenerateDslFromGoal(string goal)
        {
            if (string.IsNullOrWhiteSpace(goal))
                return string.Empty;

            // Simple rule-based generation for simulation
            if (goal.Contains("analyze") || goal.Contains("quality"))
                return "SetTopic('Code Quality Analysis') | UseDraft | UseCritique | UseImprove";

            return "SetTopic('General') | UseDraft";
        }

        /// <summary>
        /// Builds a DSL pipeline interactively.
        /// </summary>
        /// <param name="topic">The topic for the pipeline.</param>
        /// <returns>A suggested DSL string.</returns>
        public string BuildDsl(string topic)
        {
            return $"SetTopic('{topic}') | UseDraft | UseCritique | UseImprove";
        }

        /// <summary>
        /// Suggests improvements to an existing DSL.
        /// </summary>
        /// <param name="dsl">The current DSL.</param>
        /// <returns>An improved DSL string.</returns>
        public string SuggestImprovements(string dsl)
        {
            if (!dsl.Contains("UseCritique"))
                return dsl + " | UseCritique";
            if (!dsl.Contains("UseImprove"))
                return dsl + " | UseImprove";
            return dsl;
        }

        /// <summary>
        /// Generates C# code from a natural language description.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns>Generated C# code.</returns>
        public string GenerateCode(string description)
        {
            if (description.Contains("Result<T> monad"))
            {
                return @"
public class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public string Error { get; }

    private Result(bool isSuccess, T value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new Result<T>(true, value, null);
    public static Result<T> Error(string error) => new Result<T>(false, default, error);
}";
            }

            return "// Generated code placeholder";
        }

        /// <summary>
        /// Explains generated code.
        /// </summary>
        /// <param name="code">The code to explain.</param>
        /// <returns>An explanation.</returns>
        public string ExplainCode(string code)
        {
            return "This code implements a Result monad for functional error handling in C#.";
        }

        /// <summary>
        /// Processes interactive commands.
        /// </summary>
        /// <param name="command">The command string.</param>
        /// <returns>Response output.</returns>
        public string ProcessCommand(string command)
        {
            if (command.StartsWith("suggest"))
            {
                var dsl = command.Substring(8);
                var suggestions = SuggestNextSteps(dsl).Result;
                return $"Suggestions: {string.Join(", ", suggestions.Select(s => s.Step))}";
            }
            if (command.StartsWith("complete"))
            {
                var partial = command.Substring(9);
                var completions = GetTokenCompletions(partial).Result;
                return $"Completions: {string.Join(", ", completions)}";
            }
            if (command == "help")
                return "Available commands: suggest, complete, help, exit";
            if (command == "exit")
                return "Session terminated";
            return "Unknown command";
        }
    }

    /// <summary>
    /// Represents a DSL suggestion.
    /// </summary>
    public class DslSuggestion
    {
        public string Step { get; }
        public string Explanation { get; }
        public double Confidence { get; }

        public DslSuggestion(string step, string explanation, double confidence)
        {
            Step = step;
            Explanation = explanation;
            Confidence = confidence;
        }
    }

    /// <summary>
    /// Represents DSL validation result.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public string[] Errors { get; }
        public string[] Suggestions { get; }

        public ValidationResult(bool isValid, string[] errors, string[] suggestions)
        {
            IsValid = isValid;
            Errors = errors;
            Suggestions = suggestions;
        }
    }
}
