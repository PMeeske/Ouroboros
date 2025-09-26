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
    public override string ToString() => template;
}
