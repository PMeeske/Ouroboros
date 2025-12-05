#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Dynamic Tool Selector
// Intelligent tool selection based on use case and context
// ==========================================================

using Ouroboros.Tools;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Provides dynamic tool selection based on use case, context, and tool capabilities.
/// Selects the most relevant subset of tools for each operation to improve response quality.
/// </summary>
public sealed class DynamicToolSelector
{
    private readonly ToolRegistry _baseTools;
    private readonly Dictionary<ToolCategory, List<ITool>> _categorizedTools;
    private readonly Dictionary<UseCaseType, List<ToolCategory>> _useCaseMappings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicToolSelector"/> class.
    /// </summary>
    /// <param name="baseTools">The base tool registry containing all available tools.</param>
    public DynamicToolSelector(ToolRegistry baseTools)
    {
        ArgumentNullException.ThrowIfNull(baseTools);
        _baseTools = baseTools;
        _categorizedTools = CategorizeTools(baseTools);
        _useCaseMappings = InitializeUseCaseMappings();
    }

    /// <summary>
    /// Selects appropriate tools for the given use case and context.
    /// </summary>
    /// <param name="useCase">The classified use case.</param>
    /// <param name="context">Optional context for more precise selection.</param>
    /// <returns>A ToolRegistry containing the selected tools.</returns>
    public ToolRegistry SelectToolsForUseCase(UseCase useCase, ToolSelectionContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(useCase);

        // Get relevant categories for this use case
        var relevantCategories = GetRelevantCategories(useCase.Type);

        // Start with base tools
        var selectedTools = new ToolRegistry();

        // Add tools from relevant categories
        foreach (var category in relevantCategories)
        {
            if (_categorizedTools.TryGetValue(category, out var tools))
            {
                foreach (var tool in tools)
                {
                    selectedTools = selectedTools.WithTool(tool);
                }
            }
        }

        // Apply context-based filtering if provided
        if (context != null)
        {
            selectedTools = ApplyContextFilter(selectedTools, context);
        }

        // If no specific tools selected, return base tools
        if (selectedTools.Count == 0)
        {
            return _baseTools;
        }

        return selectedTools;
    }

    /// <summary>
    /// Selects tools based on a natural language prompt analysis.
    /// </summary>
    /// <param name="prompt">The user prompt to analyze.</param>
    /// <returns>A ToolRegistry containing the selected tools.</returns>
    public ToolRegistry SelectToolsForPrompt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var detectedCategories = DetectCategoriesFromPrompt(prompt);
        var selectedTools = new ToolRegistry();

        foreach (var category in detectedCategories)
        {
            if (_categorizedTools.TryGetValue(category, out var tools))
            {
                foreach (var tool in tools)
                {
                    selectedTools = selectedTools.WithTool(tool);
                }
            }
        }

        return selectedTools.Count > 0 ? selectedTools : _baseTools;
    }

    /// <summary>
    /// Gets a recommendation of tools with confidence scores.
    /// </summary>
    /// <param name="useCase">The classified use case.</param>
    /// <param name="prompt">The original prompt.</param>
    /// <returns>List of recommended tools with relevance scores.</returns>
    public List<ToolRecommendation> GetToolRecommendations(UseCase useCase, string prompt)
    {
        var recommendations = new List<ToolRecommendation>();
        var promptLower = prompt.ToLowerInvariant();

        foreach (var tool in _baseTools.All)
        {
            var score = CalculateToolRelevance(tool, useCase, promptLower);
            recommendations.Add(new ToolRecommendation(tool.Name, tool.Description, score, GetToolCategory(tool)));
        }

        return recommendations
            .OrderByDescending(r => r.RelevanceScore)
            .ToList();
    }

    /// <summary>
    /// Gets tool statistics by category.
    /// </summary>
    public Dictionary<ToolCategory, int> GetToolStatsByCategory()
    {
        return _categorizedTools.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
    }

    private List<ToolCategory> GetRelevantCategories(UseCaseType useCaseType)
    {
        if (_useCaseMappings.TryGetValue(useCaseType, out var categories))
        {
            return categories;
        }

        // Default: return general and utility tools
        return new List<ToolCategory> { ToolCategory.General, ToolCategory.Utility };
    }

    private Dictionary<UseCaseType, List<ToolCategory>> InitializeUseCaseMappings()
    {
        return new Dictionary<UseCaseType, List<ToolCategory>>
        {
            [UseCaseType.CodeGeneration] = new List<ToolCategory>
            {
                ToolCategory.Code,
                ToolCategory.FileSystem,
                ToolCategory.Validation,
                ToolCategory.General
            },
            [UseCaseType.Reasoning] = new List<ToolCategory>
            {
                ToolCategory.Knowledge,
                ToolCategory.Analysis,
                ToolCategory.Reasoning,
                ToolCategory.General
            },
            [UseCaseType.Creative] = new List<ToolCategory>
            {
                ToolCategory.Creative,
                ToolCategory.Text,
                ToolCategory.General
            },
            [UseCaseType.Summarization] = new List<ToolCategory>
            {
                ToolCategory.Text,
                ToolCategory.Analysis,
                ToolCategory.General
            },
            [UseCaseType.ToolUse] = new List<ToolCategory>
            {
                ToolCategory.General,
                ToolCategory.Utility,
                ToolCategory.FileSystem,
                ToolCategory.Web
            },
            [UseCaseType.Conversation] = new List<ToolCategory>
            {
                ToolCategory.General,
                ToolCategory.Knowledge
            }
        };
    }

    private Dictionary<ToolCategory, List<ITool>> CategorizeTools(ToolRegistry registry)
    {
        var categorized = new Dictionary<ToolCategory, List<ITool>>();

        // Initialize all categories
        foreach (ToolCategory category in Enum.GetValues<ToolCategory>())
        {
            categorized[category] = new List<ITool>();
        }

        // Categorize each tool
        foreach (var tool in registry.All)
        {
            var category = GetToolCategory(tool);
            categorized[category].Add(tool);
        }

        return categorized;
    }

    private ToolCategory GetToolCategory(ITool tool)
    {
        var nameLower = tool.Name.ToLowerInvariant();
        var descLower = tool.Description?.ToLowerInvariant() ?? string.Empty;

        // Code-related tools
        if (ContainsAny(nameLower, descLower, "code", "compile", "syntax", "lint", "format", "refactor", "debug"))
        {
            return ToolCategory.Code;
        }

        // File system tools
        if (ContainsAny(nameLower, descLower, "file", "directory", "folder", "read", "write", "path"))
        {
            return ToolCategory.FileSystem;
        }

        // Web/API tools
        if (ContainsAny(nameLower, descLower, "http", "api", "web", "fetch", "request", "url", "download"))
        {
            return ToolCategory.Web;
        }

        // Knowledge/search tools
        if (ContainsAny(nameLower, descLower, "search", "query", "knowledge", "database", "lookup", "find"))
        {
            return ToolCategory.Knowledge;
        }

        // Analysis tools
        if (ContainsAny(nameLower, descLower, "analyze", "analysis", "metrics", "statistics", "measure"))
        {
            return ToolCategory.Analysis;
        }

        // Validation tools
        if (ContainsAny(nameLower, descLower, "validate", "verify", "check", "test", "assert"))
        {
            return ToolCategory.Validation;
        }

        // Text processing tools
        if (ContainsAny(nameLower, descLower, "text", "string", "parse", "extract", "summarize", "translate"))
        {
            return ToolCategory.Text;
        }

        // Reasoning tools
        if (ContainsAny(nameLower, descLower, "reason", "logic", "infer", "deduce", "conclude"))
        {
            return ToolCategory.Reasoning;
        }

        // Creative tools
        if (ContainsAny(nameLower, descLower, "generate", "create", "compose", "design", "art", "image"))
        {
            return ToolCategory.Creative;
        }

        // Utility tools
        if (ContainsAny(nameLower, descLower, "util", "helper", "convert", "transform", "format"))
        {
            return ToolCategory.Utility;
        }

        return ToolCategory.General;
    }

    private static bool ContainsAny(string name, string description, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private List<ToolCategory> DetectCategoriesFromPrompt(string prompt)
    {
        var categories = new HashSet<ToolCategory>();
        var promptLower = prompt.ToLowerInvariant();

        // Detect code-related needs
        if (ContainsAny(promptLower, string.Empty, "code", "programming", "function", "class", "implement", "debug", "fix", "compile"))
        {
            categories.Add(ToolCategory.Code);
        }

        // Detect file operations
        if (ContainsAny(promptLower, string.Empty, "file", "read", "write", "save", "load", "directory", "folder"))
        {
            categories.Add(ToolCategory.FileSystem);
        }

        // Detect web/API needs
        if (ContainsAny(promptLower, string.Empty, "api", "http", "fetch", "download", "url", "web", "request"))
        {
            categories.Add(ToolCategory.Web);
        }

        // Detect search/knowledge needs
        if (ContainsAny(promptLower, string.Empty, "search", "find", "lookup", "query", "information", "what is", "how to"))
        {
            categories.Add(ToolCategory.Knowledge);
        }

        // Detect analysis needs
        if (ContainsAny(promptLower, string.Empty, "analyze", "examine", "inspect", "metrics", "statistics", "evaluate"))
        {
            categories.Add(ToolCategory.Analysis);
        }

        // Detect validation needs
        if (ContainsAny(promptLower, string.Empty, "validate", "verify", "check", "test", "ensure", "confirm"))
        {
            categories.Add(ToolCategory.Validation);
        }

        // Detect text processing needs
        if (ContainsAny(promptLower, string.Empty, "summarize", "extract", "parse", "translate", "format text"))
        {
            categories.Add(ToolCategory.Text);
        }

        // Detect creative needs
        if (ContainsAny(promptLower, string.Empty, "create", "generate", "design", "compose", "write a story", "imagine"))
        {
            categories.Add(ToolCategory.Creative);
        }

        // Always include general tools
        categories.Add(ToolCategory.General);

        return categories.ToList();
    }

    private double CalculateToolRelevance(ITool tool, UseCase useCase, string promptLower)
    {
        double score = 0.0;

        // Base score from category matching
        var toolCategory = GetToolCategory(tool);
        var relevantCategories = GetRelevantCategories(useCase.Type);

        if (relevantCategories.Contains(toolCategory))
        {
            score += 0.5;
        }

        // Keyword matching in prompt
        var nameLower = tool.Name.ToLowerInvariant();
        var descLower = tool.Description?.ToLowerInvariant() ?? string.Empty;

        // Check if tool name words appear in prompt
        var nameWords = nameLower.Split('_', ' ', '-');
        foreach (var word in nameWords.Where(w => w.Length > 2))
        {
            if (promptLower.Contains(word))
            {
                score += 0.2;
            }
        }

        // Check if description keywords appear in prompt
        var descWords = descLower.Split(' ', '.', ',')
            .Where(w => w.Length > 3)
            .Take(10);
        foreach (var word in descWords)
        {
            if (promptLower.Contains(word))
            {
                score += 0.1;
            }
        }

        return Math.Min(score, 1.0);
    }

    private ToolRegistry ApplyContextFilter(ToolRegistry tools, ToolSelectionContext context)
    {
        var filtered = new ToolRegistry();

        foreach (var tool in tools.All)
        {
            bool include = true;

            // Apply max tools limit
            if (context.MaxTools.HasValue && filtered.Count >= context.MaxTools.Value)
            {
                break;
            }

            // Apply required categories filter
            if (context.RequiredCategories?.Count > 0)
            {
                var toolCategory = GetToolCategory(tool);
                if (!context.RequiredCategories.Contains(toolCategory))
                {
                    include = false;
                }
            }

            // Apply excluded categories filter
            if (context.ExcludedCategories?.Count > 0)
            {
                var toolCategory = GetToolCategory(tool);
                if (context.ExcludedCategories.Contains(toolCategory))
                {
                    include = false;
                }
            }

            // Apply required tool names filter
            if (context.RequiredToolNames?.Count > 0)
            {
                if (!context.RequiredToolNames.Contains(tool.Name))
                {
                    include = false;
                }
            }

            if (include)
            {
                filtered = filtered.WithTool(tool);
            }
        }

        return filtered;
    }
}

/// <summary>
/// Categories of tools for dynamic selection.
/// </summary>
public enum ToolCategory
{
    /// <summary>General purpose tools.</summary>
    General,

    /// <summary>Code-related tools (compilation, formatting, analysis).</summary>
    Code,

    /// <summary>File system operations.</summary>
    FileSystem,

    /// <summary>Web and API tools.</summary>
    Web,

    /// <summary>Knowledge and search tools.</summary>
    Knowledge,

    /// <summary>Analysis and metrics tools.</summary>
    Analysis,

    /// <summary>Validation and testing tools.</summary>
    Validation,

    /// <summary>Text processing tools.</summary>
    Text,

    /// <summary>Reasoning and logic tools.</summary>
    Reasoning,

    /// <summary>Creative generation tools.</summary>
    Creative,

    /// <summary>Utility and helper tools.</summary>
    Utility
}

/// <summary>
/// Context for fine-tuned tool selection.
/// </summary>
public sealed record ToolSelectionContext
{
    /// <summary>Maximum number of tools to select.</summary>
    public int? MaxTools { get; init; }

    /// <summary>Required tool categories.</summary>
    public List<ToolCategory>? RequiredCategories { get; init; }

    /// <summary>Excluded tool categories.</summary>
    public List<ToolCategory>? ExcludedCategories { get; init; }

    /// <summary>Required tool names.</summary>
    public List<string>? RequiredToolNames { get; init; }

    /// <summary>Whether to prioritize fast tools.</summary>
    public bool PreferFastTools { get; init; }

    /// <summary>Whether to prioritize reliable tools.</summary>
    public bool PreferReliableTools { get; init; }
}

/// <summary>
/// Represents a tool recommendation with relevance scoring.
/// </summary>
public sealed record ToolRecommendation(
    string ToolName,
    string Description,
    double RelevanceScore,
    ToolCategory Category)
{
    /// <summary>Whether the tool is highly recommended (score > 0.7).</summary>
    public bool IsHighlyRecommended => RelevanceScore > 0.7;

    /// <summary>Whether the tool is recommended (score > 0.4).</summary>
    public bool IsRecommended => RelevanceScore > 0.4;
}
