// ==========================================================
// Dynamic Tool Selector Tests
// Tests for intelligent tool selection based on use case
// ==========================================================

using FluentAssertions;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Tools;
using Xunit;

namespace Ouroboros.Tests.UnitTests;

/// <summary>
/// Tests for the DynamicToolSelector class.
/// </summary>
[Trait("Category", "Unit")]
public class DynamicToolSelectorTests
{
    private readonly ToolRegistry _baseTools;
    private readonly DynamicToolSelector _selector;

    public DynamicToolSelectorTests()
    {
        // Create a tool registry with various categorized tools
        _baseTools = new ToolRegistry()
            .WithTool(CreateMockTool("code_analyzer", "Analyzes code for issues and improvements"))
            .WithTool(CreateMockTool("file_reader", "Reads file contents from disk"))
            .WithTool(CreateMockTool("web_fetch", "Fetches content from HTTP URLs"))
            .WithTool(CreateMockTool("search_engine", "Searches for information"))
            .WithTool(CreateMockTool("text_summarizer", "Summarizes long text documents"))
            .WithTool(CreateMockTool("validate_json", "Validates JSON structure"))
            .WithTool(CreateMockTool("general_helper", "A general purpose helper tool"));

        _selector = new DynamicToolSelector(_baseTools);
    }

    [Fact]
    public void Constructor_WithNullTools_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() => new DynamicToolSelector(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SelectToolsForUseCase_CodeGeneration_ShouldIncludeCodeTools()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.CodeGeneration);

        // Act
        var result = _selector.SelectToolsForUseCase(useCase);

        // Assert
        result.Should().NotBeNull();
        result.Contains("code_analyzer").Should().BeTrue("code tools should be included for code generation");
    }

    [Fact]
    public void SelectToolsForUseCase_Reasoning_ShouldIncludeKnowledgeTools()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.Reasoning);

        // Act
        var result = _selector.SelectToolsForUseCase(useCase);

        // Assert
        result.Should().NotBeNull();
        result.Contains("search_engine").Should().BeTrue("knowledge tools should be included for reasoning");
    }

    [Fact]
    public void SelectToolsForUseCase_ToolUse_ShouldIncludeGeneralAndFileTools()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.ToolUse);

        // Act
        var result = _selector.SelectToolsForUseCase(useCase);

        // Assert
        result.Should().NotBeNull();
        result.Contains("general_helper").Should().BeTrue("general tools should be included for tool use");
    }

    [Fact]
    public void SelectToolsForUseCase_Summarization_ShouldIncludeTextTools()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.Summarization);

        // Act
        var result = _selector.SelectToolsForUseCase(useCase);

        // Assert
        result.Should().NotBeNull();
        result.Contains("text_summarizer").Should().BeTrue("text tools should be included for summarization");
    }

    [Fact]
    public void SelectToolsForPrompt_WithCodeKeywords_ShouldSelectCodeTools()
    {
        // Arrange
        var prompt = "Please analyze this code and fix any issues";

        // Act
        var result = _selector.SelectToolsForPrompt(prompt);

        // Assert
        result.Should().NotBeNull();
        result.Contains("code_analyzer").Should().BeTrue("code tools should be detected from prompt");
    }

    [Fact]
    public void SelectToolsForPrompt_WithFileKeywords_ShouldSelectFileTools()
    {
        // Arrange
        var prompt = "Read the file from disk and process it";

        // Act
        var result = _selector.SelectToolsForPrompt(prompt);

        // Assert
        result.Should().NotBeNull();
        result.Contains("file_reader").Should().BeTrue("file tools should be detected from prompt");
    }

    [Fact]
    public void SelectToolsForPrompt_WithWebKeywords_ShouldSelectWebTools()
    {
        // Arrange
        var prompt = "Fetch data from the API endpoint";

        // Act
        var result = _selector.SelectToolsForPrompt(prompt);

        // Assert
        result.Should().NotBeNull();
        result.Contains("web_fetch").Should().BeTrue("web tools should be detected from prompt");
    }

    [Fact]
    public void SelectToolsForPrompt_WithNoKeywords_ShouldReturnBaseTools()
    {
        // Arrange
        var prompt = "Hello world";

        // Act
        var result = _selector.SelectToolsForPrompt(prompt);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetToolRecommendations_ShouldReturnScoredRecommendations()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.CodeGeneration);
        var prompt = "Analyze this code for bugs";

        // Act
        var recommendations = _selector.GetToolRecommendations(useCase, prompt);

        // Assert
        recommendations.Should().NotBeNull();
        recommendations.Should().NotBeEmpty();
        recommendations.Should().AllSatisfy(r =>
        {
            r.ToolName.Should().NotBeNullOrEmpty();
            r.RelevanceScore.Should().BeGreaterThanOrEqualTo(0);
            r.RelevanceScore.Should().BeLessThanOrEqualTo(1);
        });
    }

    [Fact]
    public void GetToolRecommendations_ShouldOrderByRelevanceDescending()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.CodeGeneration);
        var prompt = "Analyze code";

        // Act
        var recommendations = _selector.GetToolRecommendations(useCase, prompt);

        // Assert
        recommendations.Should().BeInDescendingOrder(r => r.RelevanceScore);
    }

    [Fact]
    public void GetToolStatsByCategory_ShouldReturnCategoryCounts()
    {
        // Act
        var stats = _selector.GetToolStatsByCategory();

        // Assert
        stats.Should().NotBeNull();
        stats.Should().ContainKey(ToolCategory.Code);
        stats.Should().ContainKey(ToolCategory.General);
    }

    [Fact]
    public void SelectToolsForUseCase_WithContext_MaxTools_ShouldLimitResults()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.ToolUse);
        var context = new ToolSelectionContext { MaxTools = 2 };

        // Act
        var result = _selector.SelectToolsForUseCase(useCase, context);

        // Assert
        result.Count.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void SelectToolsForUseCase_WithContext_RequiredCategories_ShouldFilterByCategory()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.ToolUse);
        var context = new ToolSelectionContext
        {
            RequiredCategories = new List<ToolCategory> { ToolCategory.Code }
        };

        // Act
        var result = _selector.SelectToolsForUseCase(useCase, context);

        // Assert
        result.Should().NotBeNull();
        // Should only contain code tools
        if (result.Count > 0)
        {
            result.Contains("code_analyzer").Should().BeTrue();
        }
    }

    [Fact]
    public void SelectToolsForUseCase_WithContext_ExcludedCategories_ShouldExcludeCategory()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.ToolUse);
        var context = new ToolSelectionContext
        {
            ExcludedCategories = new List<ToolCategory> { ToolCategory.Web }
        };

        // Act
        var result = _selector.SelectToolsForUseCase(useCase, context);

        // Assert
        result.Contains("web_fetch").Should().BeFalse("web tools should be excluded");
    }

    [Fact]
    public void ToolRecommendation_IsHighlyRecommended_ShouldBeBasedOnScore()
    {
        // Arrange
        var highScore = new ToolRecommendation("tool1", "desc", 0.8, ToolCategory.General);
        var lowScore = new ToolRecommendation("tool2", "desc", 0.3, ToolCategory.General);

        // Assert
        highScore.IsHighlyRecommended.Should().BeTrue();
        lowScore.IsHighlyRecommended.Should().BeFalse();
    }

    [Fact]
    public void ToolRecommendation_IsRecommended_ShouldBeBasedOnScore()
    {
        // Arrange
        var recommended = new ToolRecommendation("tool1", "desc", 0.5, ToolCategory.General);
        var notRecommended = new ToolRecommendation("tool2", "desc", 0.2, ToolCategory.General);

        // Assert
        recommended.IsRecommended.Should().BeTrue();
        notRecommended.IsRecommended.Should().BeFalse();
    }

    [Fact]
    public void SelectToolsForUseCase_Analysis_ShouldIncludeAnalysisTools()
    {
        // Arrange
        var useCase = CreateUseCase(UseCaseType.Analysis);

        // Act
        var result = _selector.SelectToolsForUseCase(useCase);

        // Assert
        result.Should().NotBeNull();
        // Analysis use case should include search tools (Knowledge category)
        result.Count.Should().BeGreaterThan(0);
    }

    private static UseCase CreateUseCase(UseCaseType type, int complexity = 5)
    {
        return new UseCase(type, complexity, Array.Empty<string>(), 0.5, 0.5);
    }

    private static ITool CreateMockTool(string name, string description)
    {
        return new MockTool(name, description);
    }

    private sealed class MockTool : ITool
    {
        public MockTool(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success("mock result"));
        }
    }
}
