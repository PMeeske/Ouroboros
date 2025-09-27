namespace LangChainPipeline.Pipeline.Reasoning;

public sealed class PromptTemplate(string template)
{
    public string Format(Dictionary<string, string> vars)
    {
        string result = template;
        foreach (KeyValuePair<string, string> kv in vars)
            result = result.Replace("{" + kv.Key + "}", kv.Value);
        return result;
    }
    
    /// <summary>
    /// Extracts placeholder names from the template string.
    /// </summary>
    /// <param name="template">The template string to parse.</param>
    /// <returns>A list of unique placeholder names found in the template.</returns>
    private static List<string> ExtractPlaceholders(string template)
    {
        var placeholders = new List<string>();
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
    
    public override string ToString() => template;
}
