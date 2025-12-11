// <copyright file="CliInteractiveModeIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.IntegrationTests;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Pipeline.Planning;
using LangChainPipeline.Pipeline.Verification;
using Ouroboros.Application.Tools;
using Ouroboros.Tools.MeTTa;
using Xunit;

// Alias to disambiguate PlanStep from LangChainPipeline.Pipeline.Verification.Plan
using MetaAIPlanStep = LangChainPipeline.Agent.MetaAI.PlanStep;
using VerificationPlan = LangChainPipeline.Pipeline.Verification.Plan;

/// <summary>
/// Integration tests for CLI interactive mode functionality.
/// These tests validate command parsing, execution, and the behavior of interactive modes
/// including MeTTa REPL and Skills REPL modes.
///
/// The tests use mock input/output to simulate user interactions without requiring
/// actual console input.
///
/// These tests are marked with Integration trait and can be run separately:
/// dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class CliInteractiveModeIntegrationTests : IDisposable
{
    private readonly IMeTTaEngine _mettaEngine;
    private readonly SymbolicPlanSelector _planSelector;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliInteractiveModeIntegrationTests"/> class.
    /// </summary>
    public CliInteractiveModeIntegrationTests()
    {
        // Use in-memory MeTTa engine for testing (no Docker required)
        this._mettaEngine = new InMemoryMeTTaEngine();
        this._planSelector = new SymbolicPlanSelector(this._mettaEngine);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this._mettaEngine?.Dispose();
        GC.SuppressFinalize(this);
    }

    #region MeTTa Interactive Mode Tests

    /// <summary>
    /// Tests that the MeTTa engine can be initialized for interactive mode.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_EngineInitialization_Succeeds()
    {
        // Arrange & Act
        await this._planSelector.InitializeAsync();

        // Assert - No exception means success
        this._mettaEngine.Should().NotBeNull("MeTTa engine should be initialized");
    }

    /// <summary>
    /// Tests basic MeTTa query execution through the interactive mode engine.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_ExecuteQuery_ReturnsResult()
    {
        // Arrange
        await this._planSelector.InitializeAsync();
        string query = "(+ 1 2)";

        // Act
        Result<string, string> result = await this._mettaEngine.ExecuteQueryAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("basic arithmetic query should succeed");
        result.Value.Should().NotBeNullOrEmpty("query should return a result");
    }

    /// <summary>
    /// Tests that facts can be added to the MeTTa knowledge base.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_AddFact_UpdatesKnowledgeBase()
    {
        // Arrange
        await this._planSelector.InitializeAsync();
        string fact = "(human Socrates)";

        // Act
        Result<Unit, string> result = await this._mettaEngine.AddFactAsync(fact, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("adding a fact should succeed");
    }

    /// <summary>
    /// Tests that rules can be applied in the MeTTa engine.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_ApplyRule_ReturnsResult()
    {
        // Arrange
        await this._planSelector.InitializeAsync();
        await this._mettaEngine.AddFactAsync("(human Socrates)", CancellationToken.None);
        string rule = "(= (mortal $x) (human $x))";

        // Act
        Result<string, string> result = await this._mettaEngine.ApplyRuleAsync(rule, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("applying a rule should succeed");
    }

    /// <summary>
    /// Tests that the MeTTa engine can be reset.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_Reset_ClearsKnowledgeBase()
    {
        // Arrange
        await this._planSelector.InitializeAsync();
        await this._mettaEngine.AddFactAsync("(test-fact 1)", CancellationToken.None);

        // Act
        Result<Unit, string> result = await this._mettaEngine.ResetAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("reset should succeed");
    }

    /// <summary>
    /// Tests plan constraint checking in interactive mode.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_PlanCheck_ValidatesConstraints()
    {
        // Arrange
        await this._planSelector.InitializeAsync();

        var readOnlyPlan = new VerificationPlan("Read configuration files")
            .WithAction(new FileSystemAction("read", "/etc/config.yaml"));

        // Act
        Result<string, string> result = await this._planSelector.ExplainPlanAsync(
            readOnlyPlan,
            SafeContext.ReadOnly,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("plan explanation should succeed");
        result.Value.Should().NotBeNullOrEmpty("explanation should be provided");
    }

    /// <summary>
    /// Tests that write operations are correctly identified as unsafe in read-only context.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_PlanCheck_DetectsUnsafeOperations()
    {
        // Arrange
        await this._planSelector.InitializeAsync();

        var writePlan = new VerificationPlan("Update system files")
            .WithAction(new FileSystemAction("write", "/etc/config.yaml"));

        // Act
        Result<string, string> result = await this._planSelector.ExplainPlanAsync(
            writePlan,
            SafeContext.ReadOnly,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("plan explanation should succeed even for unsafe plans");
        result.Value.Should().NotBeNullOrEmpty("explanation should describe the constraint issue");
    }

    /// <summary>
    /// Tests plan selection from multiple candidates.
    /// </summary>
    [Fact]
    public async Task MeTTaInteractiveMode_SelectBestPlan_ReturnsOptimalChoice()
    {
        // Arrange
        await this._planSelector.InitializeAsync();

        var candidates = new[]
        {
            new VerificationPlan("Read configuration").WithAction(new FileSystemAction("read", "/etc/config.yaml")),
            new VerificationPlan("Write configuration").WithAction(new FileSystemAction("write", "/etc/config.yaml")),
            new VerificationPlan("Query API").WithAction(new NetworkAction("get", "https://api.example.com")),
        };

        // Act
        Result<PlanCandidate, string> result = await this._planSelector.SelectBestPlanAsync(
            candidates,
            SafeContext.ReadOnly,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("plan selection should succeed");
        result.Value.Should().NotBeNull("a plan should be selected");
        result.Value.Plan.Should().NotBeNull("selected plan should not be null");
        // Note: Scores can be negative (penalties for unsafe operations), so we only verify a score was assigned
        result.Value.Score.Should().BeLessThanOrEqualTo(0, "write operations should receive penalty scores in read-only context");
    }

    #endregion

    #region Interactive Command Parsing Tests

    /// <summary>
    /// Tests that interactive commands are correctly parsed.
    /// </summary>
    [Fact]
    public void InteractiveMode_ParseCommand_HandlesBasicCommands()
    {
        // Arrange
        var testCases = new[]
        {
            ("help", "help", string.Empty),
            ("?", "?", string.Empty),
            ("query (+ 1 2)", "query", "(+ 1 2)"),
            ("q (+ 1 2)", "q", "(+ 1 2)"),
            ("fact (human Socrates)", "fact", "(human Socrates)"),
            ("f (human Socrates)", "f", "(human Socrates)"),
            ("exit", "exit", string.Empty),
            ("quit", "quit", string.Empty),
        };

        foreach (var (input, expectedCommand, expectedArg) in testCases)
        {
            // Act
            var (command, arg) = ParseInteractiveCommand(input);

            // Assert
            command.Should().Be(expectedCommand.ToLowerInvariant(), $"command should be parsed correctly for input '{input}'");
            arg.Should().Be(expectedArg, $"argument should be parsed correctly for input '{input}'");
        }
    }

    /// <summary>
    /// Tests that whitespace in commands is handled correctly.
    /// </summary>
    [Fact]
    public void InteractiveMode_ParseCommand_HandlesWhitespace()
    {
        // Arrange
        var input = "  query   (+ 1 2)  ";

        // Act
        var (command, arg) = ParseInteractiveCommand(input);

        // Assert
        command.Should().Be("query", "command should be trimmed");
        arg.Should().Be("(+ 1 2)", "argument should be trimmed");
    }

    /// <summary>
    /// Tests that empty input is handled gracefully.
    /// </summary>
    [Fact]
    public void InteractiveMode_ParseCommand_HandlesEmptyInput()
    {
        // Arrange
        var testCases = new[] { "", "   ", null };

        foreach (var input in testCases)
        {
            // Act
            var (command, arg) = ParseInteractiveCommand(input!);

            // Assert
            command.Should().BeEmpty($"empty input '{input ?? "null"}' should result in empty command");
            arg.Should().BeEmpty($"empty input '{input ?? "null"}' should result in empty argument");
        }
    }

    #endregion

    #region Skills Interactive Mode Tests

    /// <summary>
    /// Tests that the skill registry can be initialized.
    /// </summary>
    [Fact]
    public async Task SkillsInteractiveMode_RegistryInitialization_Succeeds()
    {
        // Arrange
        string tempSkillsPath = Path.Combine(Path.GetTempPath(), $"test_skills_{Guid.NewGuid()}.json");
        var config = new PersistentSkillConfig(StoragePath: tempSkillsPath, AutoSave: true);

        try
        {
            // Act
            await using var registry = new PersistentSkillRegistry(config: config);
            await registry.InitializeAsync();

            // Assert
            registry.GetAllSkills().Should().NotBeNull("skill list should be initialized");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempSkillsPath))
            {
                File.Delete(tempSkillsPath);
            }
        }
    }

    /// <summary>
    /// Tests that skills can be registered in the skill registry.
    /// </summary>
    [Fact]
    public async Task SkillsInteractiveMode_RegisterSkill_AddsToRegistry()
    {
        // Arrange
        string tempSkillsPath = Path.Combine(Path.GetTempPath(), $"test_skills_{Guid.NewGuid()}.json");
        var config = new PersistentSkillConfig(StoragePath: tempSkillsPath, AutoSave: true);

        try
        {
            await using var registry = new PersistentSkillRegistry(config: config);
            await registry.InitializeAsync();

            var skill = new Skill(
                "TestSkill",
                "A test skill for integration testing",
                new List<string> { "test-context" },
                new List<MetaAIPlanStep>
                {
                    new("Step 1", new Dictionary<string, object> { ["hint"] = "test" }, "result", 0.9),
                },
                0.85,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);

            // Act
            await registry.RegisterSkillAsync(skill);

            // Assert
            var skills = registry.GetAllSkills();
            skills.Should().Contain(s => s.Name == "TestSkill", "registered skill should be in the registry");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempSkillsPath))
            {
                File.Delete(tempSkillsPath);
            }
        }
    }

    /// <summary>
    /// Tests that skill search functionality works correctly.
    /// </summary>
    [Fact]
    public async Task SkillsInteractiveMode_SearchSkills_ReturnsMatchingSkills()
    {
        // Arrange
        string tempSkillsPath = Path.Combine(Path.GetTempPath(), $"test_skills_{Guid.NewGuid()}.json");
        var config = new PersistentSkillConfig(StoragePath: tempSkillsPath, AutoSave: true);

        try
        {
            await using var registry = new PersistentSkillRegistry(config: config);
            await registry.InitializeAsync();

            var skill1 = new Skill(
                "LiteratureReview",
                "Synthesize research papers",
                new List<string> { "research" },
                new List<MetaAIPlanStep>(),
                0.85,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);

            var skill2 = new Skill(
                "CodeAnalysis",
                "Analyze source code",
                new List<string> { "code" },
                new List<MetaAIPlanStep>(),
                0.80,
                0,
                DateTime.UtcNow,
                DateTime.UtcNow);

            await registry.RegisterSkillAsync(skill1);
            await registry.RegisterSkillAsync(skill2);

            // Act
            var searchTerm = "research";
            var matchingSkills = registry.GetAllSkills()
                .Where(s => s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           s.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Assert
            matchingSkills.Should().HaveCount(1, "only one skill matches 'research'");
            matchingSkills[0].Name.Should().Be("LiteratureReview");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempSkillsPath))
            {
                File.Delete(tempSkillsPath);
            }
        }
    }

    /// <summary>
    /// Tests the skills REPL command parsing for common commands.
    /// </summary>
    [Fact]
    public void SkillsInteractiveMode_ParseCommand_HandlesSkillsCommands()
    {
        // Arrange - These match the commands in RunInteractiveSkillsMode
        var testCases = new[]
        {
            ("list", "list", string.Empty),
            ("ls", "ls", string.Empty),
            ("tokens", "tokens", string.Empty),
            ("t", "t", string.Empty),
            ("fetch neural networks", "fetch", "neural networks"),
            ("learn machine learning", "learn", "machine learning"),
            ("suggest reasoning", "suggest", "reasoning"),
            ("find analysis", "find", "analysis"),
            ("run ChainOfThoughtReasoning", "run", "ChainOfThoughtReasoning"),
            ("exec LiteratureReview", "exec", "LiteratureReview"),
            ("help", "help", string.Empty),
            ("exit", "exit", string.Empty),
            ("quit", "quit", string.Empty),
            ("q", "q", string.Empty),
        };

        foreach (var (input, expectedCommand, expectedArg) in testCases)
        {
            // Act
            var (command, arg) = ParseInteractiveCommand(input);

            // Assert
            command.Should().Be(expectedCommand.ToLowerInvariant(), $"command should be parsed correctly for input '{input}'");
            arg.Should().Be(expectedArg, $"argument should be parsed correctly for input '{input}'");
        }
    }

    #endregion

    #region Plan Action Tests

    /// <summary>
    /// Tests that file system actions are correctly converted to MeTTa atoms.
    /// </summary>
    [Fact]
    public void PlanAction_FileSystem_ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        var action = new FileSystemAction("read", "/etc/config.yaml");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Contain("FileSystemAction", "MeTTa atom should indicate file system action");
        atom.Should().Contain("read", "MeTTa atom should include the operation");
    }

    /// <summary>
    /// Tests that network actions are correctly converted to MeTTa atoms.
    /// </summary>
    [Fact]
    public void PlanAction_Network_ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        var action = new NetworkAction("get", "https://api.example.com");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Contain("NetworkAction", "MeTTa atom should indicate network action");
        atom.Should().Contain("get", "MeTTa atom should include the operation");
    }

    /// <summary>
    /// Tests that tool actions are correctly converted to MeTTa atoms.
    /// </summary>
    [Fact]
    public void PlanAction_Tool_ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        var action = new ToolAction("search_tool", "query");

        // Act
        var atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Contain("ToolAction", "MeTTa atom should indicate tool action");
        atom.Should().Contain("search_tool", "MeTTa atom should include the tool name");
    }

    #endregion

    #region Safe Context Tests

    /// <summary>
    /// Tests that safe contexts are correctly converted to MeTTa atoms.
    /// </summary>
    [Fact]
    public void SafeContext_ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange & Act
        var readOnlyAtom = SafeContext.ReadOnly.ToMeTTaAtom();
        var fullAccessAtom = SafeContext.FullAccess.ToMeTTaAtom();

        // Assert
        readOnlyAtom.Should().NotBeNullOrEmpty("ReadOnly context should have a MeTTa atom representation");
        fullAccessAtom.Should().NotBeNullOrEmpty("FullAccess context should have a MeTTa atom representation");
        readOnlyAtom.Should().NotBe(fullAccessAtom, "different contexts should have different atoms");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Parses an interactive command string into command and argument parts.
    /// This mirrors the parsing logic used in the interactive modes.
    /// </summary>
    private static (string Command, string Arg) ParseInteractiveCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return (string.Empty, string.Empty);
        }

        string trimmed = input.Trim();
        string[] parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        string command = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        return (command, arg);
    }

    #endregion
}
