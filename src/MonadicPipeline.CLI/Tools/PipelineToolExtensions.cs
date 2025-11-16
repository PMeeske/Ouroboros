#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reflection;
using LangChainPipeline.CLI;

namespace LangChainPipeline.Tools;

/// <summary>
/// Extension methods for registering pipeline steps as tools, enabling meta-AI capabilities.
/// </summary>
public static class PipelineToolExtensions
{
    /// <summary>
    /// Registers all discovered CLI pipeline steps as tools in the registry.
    /// This enables the LLM to invoke pipeline operations, creating a meta-AI layer
    /// where the pipeline can reason about and modify its own execution.
    /// </summary>
    /// <param name="registry">The tool registry to add pipeline step tools to.</param>
    /// <param name="pipelineState">The pipeline state that tools will operate on.</param>
    /// <returns>A new ToolRegistry with all pipeline steps registered as tools.</returns>
    public static ToolRegistry WithPipelineSteps(this ToolRegistry registry, CliPipelineState pipelineState)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(pipelineState);

        ToolRegistry newRegistry = registry;

        // Get all token groups from the StepRegistry
        foreach ((MethodInfo method, IReadOnlyList<string> names) in StepRegistry.GetTokenGroups())
        {
            // Use the first name as the primary identifier
            string? primaryName = names.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(primaryName))
                continue;

            // Create a description from method info
            string description = BuildStepDescription(method, names);

            // Create and register the pipeline step tool
            PipelineStepTool? tool = PipelineStepTool.FromStepName(primaryName, description);
            if (tool != null)
            {
                tool.SetPipelineState(pipelineState);
                newRegistry = newRegistry.WithTool(tool);
            }
        }

        return newRegistry;
    }

    /// <summary>
    /// Registers selected pipeline steps as tools.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="pipelineState">The pipeline state.</param>
    /// <param name="stepNames">Names of steps to register as tools.</param>
    /// <returns>A new ToolRegistry with specified pipeline steps registered.</returns>
    public static ToolRegistry WithPipelineSteps(this ToolRegistry registry, CliPipelineState pipelineState, params string[] stepNames)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(pipelineState);
        ArgumentNullException.ThrowIfNull(stepNames);

        ToolRegistry newRegistry = registry;

        foreach (string stepName in stepNames)
        {
            // Try to resolve step info
            if (StepRegistry.TryResolveInfo(stepName, out MethodInfo? method) && method != null)
            {
                PipelineTokenAttribute? attr = method.GetCustomAttribute<PipelineTokenAttribute>();
                IReadOnlyList<string> names = attr?.Names ?? new[] { stepName };
                string description = BuildStepDescription(method, names);

                PipelineStepTool? tool = PipelineStepTool.FromStepName(stepName, description);
                if (tool != null)
                {
                    tool.SetPipelineState(pipelineState);
                    newRegistry = newRegistry.WithTool(tool);
                }
            }
        }

        return newRegistry;
    }

    /// <summary>
    /// Builds a human-readable description for a pipeline step.
    /// </summary>
    private static string BuildStepDescription(System.Reflection.MethodInfo method, IReadOnlyList<string> names)
    {
        string methodName = method.Name;
        string aliases = names.Count > 1 ? $" (aliases: {string.Join(", ", names.Skip(1))})" : "";

        // Try to extract summary from XML documentation if available
        string? summary = method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

        if (!string.IsNullOrWhiteSpace(summary))
        {
            return $"{summary}{aliases}";
        }

        // Fallback to method-based description
        return methodName switch
        {
            "UseIngest" => "Ingest documents from the current data source into the vector store",
            "UseDir" => "Ingest documents from a directory (supports filters for extensions, patterns, exclusions)",
            "UseSolution" => "Ingest .NET solution files (.cs, .razor) with smart filtering",
            "UseDraft" => "Generate an initial draft response using the LLM with retrieval context",
            "UseCritique" => "Analyze and critique the current draft using the LLM",
            "UseImprove" => "Improve the draft based on critique using the LLM",
            "UseRefinementLoop" => "Run a complete refinement cycle: draft -> critique -> improve (multiple iterations)",
            "SetPrompt" or "Set" => "Set the prompt/query for LLM processing",
            "SetTopic" => "Set the topic for the pipeline",
            "SetQuery" => "Set the retrieval query",
            "SetSource" => "Change the data source for document loading",
            "SetK" => "Set the number of documents to retrieve (k parameter)",
            "TraceOn" => "Enable trace logging for debugging",
            "TraceOff" => "Disable trace logging",
            "Zip" => "Ingest and process files from a ZIP archive",
            "ListVectors" => "List ingested vectors/documents in the vector store",
            "Retrieve" => "Perform semantic search/retrieval over ingested documents",
            "Template" => "Format a prompt template with context and variables",
            "LlmStep" or "LLM" => "Execute LLM generation with the current prompt",
            "EnhanceMarkdown" => "Improve markdown files using LLM with iterative refinement",
            "SwitchModel" => "Switch the LLM or embedding model being used",
            _ => $"Pipeline step: {methodName}{aliases}"
        };
    }
}
