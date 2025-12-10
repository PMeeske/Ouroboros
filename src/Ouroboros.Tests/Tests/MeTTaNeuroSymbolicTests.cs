// <copyright file="MeTTaNeuroSymbolicTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using LangChainPipeline.Pipeline.Verification;
using LangChainPipeline.Pipeline.Extraction;
using LangChainPipeline.Pipeline.Retrieval;
using LangChainPipeline.Pipeline.Planning;

/// <summary>
/// Unit tests for MeTTa Neuro-Symbolic Integration components.
/// Tests the three core components:
/// 1. Symbolic Guard Rails (MeTTaVerificationStep)
/// 2. Grounded Knowledge Graph (SymbolicIngestionStep, TripleExtractionStep, SymbolicRetrievalStep)
/// 3. Neuro-Symbolic Tool Discovery (MeTTaPlanner, ToolBinder)
/// </summary>
public class MeTTaNeuroSymbolicTests
{
    #region Component 1: Symbolic Guard Rails Tests

    [Fact]
    public void SafeContext_ToMeTTaAtom_ReturnsCorrectAtom()
    {
        // Arrange & Act
        string readOnlyAtom = SafeContext.ReadOnly.ToMeTTaAtom();
        string fullAccessAtom = SafeContext.FullAccess.ToMeTTaAtom();

        // Assert
        readOnlyAtom.Should().Be("ReadOnly");
        fullAccessAtom.Should().Be("FullAccess");
    }

    [Fact]
    public void Plan_WithAction_ReturnsNewPlanWithAction()
    {
        // Arrange
        Plan plan = new("Test plan");
        FileSystemAction action = new("write", "/tmp/test.txt");

        // Act
        Plan newPlan = plan.WithAction(action);

        // Assert
        newPlan.Actions.Should().HaveCount(1);
        newPlan.Actions[0].Should().Be(action);
        plan.Actions.Should().BeEmpty(); // Original unchanged
    }

    [Fact]
    public void FileSystemAction_ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        FileSystemAction readAction = new("read");
        FileSystemAction writeAction = new("write", "/path");

        // Act
        string readAtom = readAction.ToMeTTaAtom();
        string writeAtom = writeAction.ToMeTTaAtom();

        // Assert
        readAtom.Should().Be("(FileSystemAction \"read\")");
        writeAtom.Should().Be("(FileSystemAction \"write\")");
    }

    [Fact]
    public void NetworkAction_ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        NetworkAction getAction = new("get", "https://api.example.com");
        NetworkAction postAction = new("post");

        // Act
        string getAtom = getAction.ToMeTTaAtom();
        string postAtom = postAction.ToMeTTaAtom();

        // Assert
        getAtom.Should().Be("(NetworkAction \"get\")");
        postAtom.Should().Be("(NetworkAction \"post\")");
    }

    [Fact]
    public void ToolAction_ToMeTTaAtom_ReturnsCorrectFormat()
    {
        // Arrange
        ToolAction action = new("summarize_tool", "some args");

        // Act
        string atom = action.ToMeTTaAtom();

        // Assert
        atom.Should().Be("(ToolAction \"summarize_tool\")");
    }

    [Fact]
    public async Task MeTTaVerificationStep_ReadAction_AllowedInReadOnly()
    {
        // Arrange
        var engine = new VerificationMockMeTTaEngine(allowedInReadOnly: true);
        var verifier = new MeTTaVerificationStep(engine, SafeContext.ReadOnly);
        var plan = new Plan("Read file").WithAction(new FileSystemAction("read"));

        // Act
        var result = await verifier.VerifyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(plan);
    }

    [Fact]
    public async Task MeTTaVerificationStep_WriteAction_BlockedInReadOnly()
    {
        // Arrange
        var engine = new VerificationMockMeTTaEngine(allowedInReadOnly: false);
        var verifier = new MeTTaVerificationStep(engine, SafeContext.ReadOnly);
        var plan = new Plan("Write file").WithAction(new FileSystemAction("write"));

        // Act
        var result = await verifier.VerifyAsync(plan);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<SecurityException>();
        result.Error.Message.Should().Contain("Guard Rail Violation");
    }

    [Fact]
    public async Task MeTTaVerificationStep_AllActions_AllowedInFullAccess()
    {
        // Arrange
        var engine = new VerificationMockMeTTaEngine(allowedInReadOnly: true);
        var verifier = new MeTTaVerificationStep(engine, SafeContext.FullAccess);
        var plan = new Plan("Write file")
            .WithAction(new FileSystemAction("write"))
            .WithAction(new FileSystemAction("delete"));

        // Act
        var result = await verifier.VerifyAsync(plan);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Component 2: Grounded Knowledge Graph Tests

    [Fact]
    public void SemanticTriple_ToMeTTaFact_ReturnsCorrectFormat()
    {
        // Arrange
        var authorTriple = new SemanticTriple("deployment.md", "Author", "PMeeske");
        var statusTriple = new SemanticTriple("deployment.md", "Status", "Outdated");
        var topicTriple = new SemanticTriple("deployment.md", "Topic", "Kubernetes");

        // Act
        string authorFact = authorTriple.ToMeTTaFact();
        string statusFact = statusTriple.ToMeTTaFact();
        string topicFact = topicTriple.ToMeTTaFact();

        // Assert
        authorFact.Should().Contain("Author");
        authorFact.Should().Contain("Doc \"deployment.md\"");
        authorFact.Should().Contain("User \"PMeeske\"");

        statusFact.Should().Contain("Status");
        statusFact.Should().Contain("State \"Outdated\"");

        topicFact.Should().Contain("Topic");
        topicFact.Should().Contain("Concept \"Kubernetes\"");
    }

    [Fact]
    public void HybridRetrievalResult_AllDocumentIds_CombinesBothSources()
    {
        // Arrange
        var symbolicMatches = new List<string> { "doc1.md", "doc2.md" };
        var semanticMatches = new List<LangChain.DocumentLoaders.Document>
        {
            new LangChain.DocumentLoaders.Document("content3", new Dictionary<string, object> { ["id"] = "doc3.md" }),
        };
        var result = new HybridRetrievalResult("test query", symbolicMatches, semanticMatches);

        // Act
        var allIds = result.AllDocumentIds.ToList();

        // Assert
        allIds.Should().Contain("doc1.md");
        allIds.Should().Contain("doc2.md");
        allIds.Should().Contain("doc3.md");
    }

    [Fact]
    public async Task SymbolicRetrievalStep_RetrieveByStatus_ExecutesCorrectQuery()
    {
        // Arrange
        var engine = new RetrievalMockMeTTaEngine(new[] { "deployment.md", "config.md" });
        var retriever = new SymbolicRetrievalStep(engine);

        // Act
        var result = await retriever.RetrieveByStatusAsync("Outdated");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task SymbolicRetrievalStep_RetrieveByStatusAndTopic_ExecutesCompoundQuery()
    {
        // Arrange
        var engine = new RetrievalMockMeTTaEngine(new[] { "deployment.md" });
        var retriever = new SymbolicRetrievalStep(engine);

        // Act
        var result = await retriever.RetrieveByStatusAndTopicAsync("Outdated", "Kubernetes");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Compound query should return filtered results
    }

    #endregion

    #region Component 3: Neuro-Symbolic Tool Discovery Tests

    [Fact]
    public void MeTTaType_StaticInstances_HaveCorrectNames()
    {
        // Assert
        MeTTaType.Text.Name.Should().Be("Text");
        MeTTaType.Summary.Name.Should().Be("Summary");
        MeTTaType.Code.Name.Should().Be("Code");
        MeTTaType.TestResult.Name.Should().Be("TestResult");
    }

    [Fact]
    public void ToolChain_IsEmpty_ReturnsTrueForEmptyChain()
    {
        // Arrange
        var emptyChain = ToolChain.Empty;
        var nonEmptyChain = new ToolChain(new[] { "tool1" });

        // Assert
        emptyChain.IsEmpty.Should().BeTrue();
        nonEmptyChain.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task MeTTaPlanner_Plan_ReturnsToolChain()
    {
        // Arrange
        var engine = new PlannerMockMeTTaEngine("(chain summarize_tool generate_code_tool)");
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.PlanAsync(MeTTaType.Text, MeTTaType.Code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tools.Should().Contain("summarize_tool");
        result.Value.Tools.Should().Contain("generate_code_tool");
    }

    [Fact]
    public async Task MeTTaPlanner_RegisterToolSignature_AddsToEngine()
    {
        // Arrange
        var engine = new PlannerMockMeTTaEngine("added");
        var planner = new MeTTaPlanner(engine);

        // Act
        var result = await planner.RegisterToolSignatureAsync(
            "custom_tool",
            new MeTTaType("Input"),
            new MeTTaType("Output"));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ToolBinder_Bind_ReturnsExecutablePipeline()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithFunction("echo_tool", "Echoes input", s => s.ToUpper());
        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "echo_tool" });

        // Act
        var result = binder.Bind(chain);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ToolBinder_BoundPipeline_ExecutesTools()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithFunction("echo_tool", "Echoes input", s => s.ToUpper());
        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "echo_tool" });

        // Act
        var bindResult = binder.Bind(chain);
        bindResult.IsSuccess.Should().BeTrue();

        var output = await bindResult.Value("hello");

        // Assert
        output.Should().Be("HELLO");
    }

    [Fact]
    public void ToolBinder_Bind_FailsForUnknownTool()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "nonexistent_tool" });

        // Act
        var result = binder.Bind(chain);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Tool not found");
    }

    [Fact]
    public void ToolBinder_Bind_FailsForEmptyChain()
    {
        // Arrange
        var registry = new ToolRegistry();
        var binder = new ToolBinder(registry);

        // Act
        var result = binder.Bind(ToolChain.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Cannot bind empty tool chain");
    }

    [Fact]
    public async Task ToolBinder_BindSafe_ReturnsResultBasedPipeline()
    {
        // Arrange
        var registry = new ToolRegistry()
            .WithFunction("echo_tool", "Echoes input", s => s.ToUpper());
        var binder = new ToolBinder(registry);
        var chain = new ToolChain(new[] { "echo_tool" });

        // Act
        var bindResult = binder.BindSafe(chain);
        bindResult.IsSuccess.Should().BeTrue();

        var pipelineResult = await bindResult.Value("hello");

        // Assert
        pipelineResult.IsSuccess.Should().BeTrue();
        pipelineResult.Value.Should().Be("HELLO");
    }

    #endregion

    #region Mock Implementations

    /// <summary>
    /// Mock MeTTa engine for verification tests.
    /// </summary>
    private sealed class VerificationMockMeTTaEngine : IMeTTaEngine
    {
        private readonly bool _allowedInReadOnly;

        public VerificationMockMeTTaEngine(bool allowedInReadOnly)
        {
            _allowedInReadOnly = allowedInReadOnly;
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            // Check if query is asking about FullAccess - always allowed
            if (query.Contains("FullAccess"))
            {
                return Task.FromResult(Result<string, string>.Success("True"));
            }

            // For ReadOnly context, return based on configuration
            string result = _allowedInReadOnly ? "True" : "False";
            return Task.FromResult(Result<string, string>.Success(result));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock MeTTa engine for retrieval tests.
    /// </summary>
    private sealed class RetrievalMockMeTTaEngine : IMeTTaEngine
    {
        private readonly string[] _documentIds;

        public RetrievalMockMeTTaEngine(string[] documentIds)
        {
            _documentIds = documentIds;
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            // Return document IDs in MeTTa format
            var docs = string.Join(" ", _documentIds.Select(id => $"(Doc \"{id}\")"));
            return Task.FromResult(Result<string, string>.Success($"[{docs}]"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock MeTTa engine for planner tests.
    /// </summary>
    private sealed class PlannerMockMeTTaEngine : IMeTTaEngine
    {
        private readonly string _planResponse;

        public PlannerMockMeTTaEngine(string planResponse)
        {
            _planResponse = planResponse;
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success(_planResponse));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    #endregion
}
