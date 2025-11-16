#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace LangChainPipeline.Pipeline.Reasoning;

/// <summary>
/// Immutable template for generating prompts with variable substitution.
/// Enhanced with functional programming patterns and Result-based error handling.
/// </summary>
public sealed class PromptTemplate(string template)
{
    private readonly string _template = template ?? throw new ArgumentNullException(nameof(template));

    /// <summary>
    /// Formats the template with the provided variables using simple string replacement.
    /// </summary>
    /// <param name="vars">Dictionary of variable names to values</param>
    /// <returns>The formatted template string</returns>
    public string Format(Dictionary<string, string> vars)
    {
        ArgumentNullException.ThrowIfNull(vars);

        string result = _template;
        foreach (KeyValuePair<string, string> kv in vars)
            result = result.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);
        return result;
    }

    /// <summary>
    /// Safe formatting that returns a Result indicating success or failure.
    /// Validates that all required placeholders are provided.
    /// </summary>
    /// <param name="vars">Dictionary of variable names to values</param>
    /// <returns>Result containing the formatted string or an error message</returns>
    public Result<string> SafeFormat(Dictionary<string, string> vars)
    {
        if (vars == null)
            return Result<string>.Failure("Variables dictionary cannot be null");

        try
        {
            // Find all placeholders in the template
            List<string> placeholders = ExtractPlaceholders(_template);
            List<string> missing = placeholders.Where(p => !vars.ContainsKey(p)).ToList();

            if (missing.Any())
                return Result<string>.Failure($"Missing required variables: {string.Join(", ", missing)}");

            string result = _template;
            foreach (KeyValuePair<string, string> kv in vars)
                result = result.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);

            return Result<string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Template formatting failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts all placeholder names from the template.
    /// </summary>
    /// <param name="template">The template string</param>
    /// <returns>List of placeholder names</returns>
    private static List<string> ExtractPlaceholders(string template)
    {
        List<string> placeholders = new List<string>();
        int start = 0;

        while (true)
        {
            int openBrace = template.IndexOf('{', start);
            if (openBrace == -1) break;

            int closeBrace = template.IndexOf('}', openBrace);
            if (closeBrace == -1) break;

            string placeholder = template.Substring(openBrace + 1, closeBrace - openBrace - 1);
            if (!string.IsNullOrWhiteSpace(placeholder) && !placeholders.Contains(placeholder))
                placeholders.Add(placeholder);

            start = closeBrace + 1;
        }

        return placeholders;
    }

    /// <summary>
    /// Returns the raw template string.
    /// </summary>
    public override string ToString() => _template;

    /// <summary>
    /// Implicit conversion from string to PromptTemplate.
    /// </summary>
    public static implicit operator PromptTemplate(string template) => new(template);

    /// <summary>
    /// Gets the required variable names for this template.
    /// </summary>
    public IReadOnlyList<string> RequiredVariables => ExtractPlaceholders(_template);
}
