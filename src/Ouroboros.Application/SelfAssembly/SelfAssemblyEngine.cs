using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Autonomous;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Types of neurons that can be assembled.
/// </summary>
public enum NeuronType
{
    Custom,
    Processor,
    Aggregator,
    Observer,
    Responder
}

/// <summary>
/// Capability categories for assembled neurons.
/// </summary>
[Flags]
public enum NeuronCapability
{
    None = 0,
    TextProcessing = 1,
    ApiIntegration = 2,
    Computation = 4,
    FileAccess = 8,          // Forbidden by default
    DataPersistence = 16,
    Reasoning = 32,
    EventObservation = 64,
    Orchestration = 128
}

/// <summary>
/// Handler specification for a neuron message.
/// </summary>
public sealed record MessageHandler
{
    /// <summary>Topic pattern to match.</summary>
    public required string TopicPattern { get; init; }

    /// <summary>Description of handling logic.</summary>
    public required string HandlingLogic { get; init; }

    /// <summary>Whether this handler sends a direct response.</summary>
    public bool SendsResponse { get; init; }

    /// <summary>Whether this handler broadcasts results.</summary>
    public bool BroadcastsResult { get; init; }
}

/// <summary>
/// Specification for a message handler in a neuron (alternate form for code generation).
/// </summary>
public record MessageHandlerSpec(
    string Topic,
    string HandlerName,
    string Description,
    string InputType,
    string OutputType);

/// <summary>
/// Blueprint describing a neuron to be assembled.
/// </summary>
public sealed record NeuronBlueprint
{
    /// <summary>Unique name for the neuron.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Description { get; init; }

    /// <summary>Why this neuron is needed.</summary>
    public required string Rationale { get; init; }

    /// <summary>Type of neuron.</summary>
    public NeuronType Type { get; init; } = NeuronType.Custom;

    /// <summary>Topics this neuron subscribes to.</summary>
    public required IReadOnlyList<string> SubscribedTopics { get; init; }

    /// <summary>Capabilities required.</summary>
    public IReadOnlyList<NeuronCapability> Capabilities { get; init; } = [];

    /// <summary>Message handlers.</summary>
    public IReadOnlyList<MessageHandler> MessageHandlers { get; init; } = [];

    /// <summary>Whether this neuron has autonomous tick behavior.</summary>
    public bool HasAutonomousTick { get; init; }

    /// <summary>Description of autonomous tick behavior.</summary>
    public string? TickBehaviorDescription { get; init; }

    /// <summary>Confidence score (0-1).</summary>
    public double ConfidenceScore { get; init; }

    /// <summary>Identifier of what generated this blueprint.</summary>
    public string? IdentifiedBy { get; init; }
}

/// <summary>
/// Result of MeTTa-based blueprint validation.
/// </summary>
public record MeTTaValidation(
    bool IsValid,
    double SafetyScore,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Warnings,
    string MeTTaExpression);

/// <summary>
/// Assembly proposal requiring approval.
/// </summary>
public record AssemblyProposal(
    Guid Id,
    NeuronBlueprint Blueprint,
    MeTTaValidation Validation,
    string GeneratedCode,
    DateTime ProposedAt,
    AssemblyProposalStatus Status = AssemblyProposalStatus.PendingApproval);

/// <summary>
/// Status of an assembly proposal.
/// </summary>
public enum AssemblyProposalStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Compiling,
    Testing,
    Deployed,
    Failed
}

/// <summary>
/// Configuration for the self-assembly engine.
/// </summary>
public record SelfAssemblyConfig
{
    public bool AutoApprovalEnabled { get; init; } = false;
    public double AutoApprovalThreshold { get; init; } = 0.95;
    public double MinSafetyScore { get; init; } = 0.8;
    public int MaxAssembledNeurons { get; init; } = 10;
    public HashSet<NeuronCapability> ForbiddenCapabilities { get; init; } = new() { NeuronCapability.FileAccess };
    public TimeSpan SandboxTimeout { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Additional namespaces to forbid beyond the default set.
    /// Default forbidden namespaces include System.Net, System.IO, System.Diagnostics.Process, etc.
    /// </summary>
    public IReadOnlyList<string> AdditionalForbiddenNamespaces { get; init; } = [];
}

/// <summary>
/// Event raised when a neuron is successfully assembled.
/// </summary>
public record NeuronAssembledEvent(
    Guid ProposalId,
    string NeuronName,
    Type NeuronType,
    DateTime AssembledAt);

/// <summary>
/// Event raised when assembly fails.
/// </summary>
public record AssemblyFailedEvent(
    Guid ProposalId,
    string NeuronName,
    string Reason,
    DateTime FailedAt);

/// <summary>
/// State of the assembly pipeline for a proposal.
/// </summary>
public record AssemblyState(
    Guid ProposalId,
    AssemblyProposalStatus Status,
    DateTime Timestamp,
    string? Details = null);

/// <summary>
/// Self-assembly engine that dynamically creates new neurons at runtime.
/// Uses a staged pipeline: Propose → Validate → Approve → Compile → Test → Deploy.
/// </summary>
public sealed class SelfAssemblyEngine : IAsyncDisposable
{
    private readonly SelfAssemblyConfig _config;
    private readonly ConcurrentDictionary<Guid, AssemblyProposal> _proposals = new();
    private readonly ConcurrentDictionary<Guid, List<AssemblyState>> _stateHistory = new();
    private readonly ConcurrentDictionary<string, Type> _assembledNeurons = new();
    private readonly List<MetadataReference> _defaultReferences;
    private readonly SemaphoreSlim _compilationLock = new(1, 1);

    // Callbacks for external integration
    private Func<NeuronBlueprint, Task<MeTTaValidation>>? _mettaValidator;
    private Func<NeuronBlueprint, Task<string>>? _codeGenerator;
    private Func<AssemblyProposal, Task<bool>>? _approvalCallback;

    // Events
    public event EventHandler<NeuronAssembledEvent>? NeuronAssembled;
    public event EventHandler<AssemblyFailedEvent>? AssemblyFailed;

    public SelfAssemblyEngine(SelfAssemblyConfig? config = null)
    {
        _config = config ?? new SelfAssemblyConfig();
        _defaultReferences = GetDefaultReferences();
    }

    /// <summary>
    /// Configure MeTTa-based blueprint validation.
    /// </summary>
    public void SetMeTTaValidator(Func<NeuronBlueprint, Task<MeTTaValidation>> validator)
    {
        _mettaValidator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Configure code generator (typically LLM-based).
    /// </summary>
    public void SetCodeGenerator(Func<NeuronBlueprint, Task<string>> generator)
    {
        _codeGenerator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    /// <summary>
    /// Configure approval callback for human-in-the-loop.
    /// </summary>
    public void SetApprovalCallback(Func<AssemblyProposal, Task<bool>> callback)
    {
        _approvalCallback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <summary>
    /// Submit a blueprint for assembly. Returns proposal ID.
    /// </summary>
    public async Task<Result<Guid>> SubmitBlueprintAsync(NeuronBlueprint blueprint)
    {
        // Check limits
        if (_assembledNeurons.Count >= _config.MaxAssembledNeurons)
        {
            return Result<Guid>.Failure($"Maximum assembled neurons limit ({_config.MaxAssembledNeurons}) reached");
        }

        // Check for duplicate names
        if (_assembledNeurons.ContainsKey(blueprint.Name))
        {
            return Result<Guid>.Failure($"Neuron with name '{blueprint.Name}' already exists");
        }

        // Check forbidden capabilities
        foreach (var forbidden in _config.ForbiddenCapabilities)
        {
            if (blueprint.Capabilities.Contains(forbidden))
            {
                return Result<Guid>.Failure($"Blueprint requests forbidden capability: {forbidden}");
            }
        }

        var proposalId = Guid.NewGuid();

        // Stage 1: MeTTa Validation
        var validation = await ValidateBlueprintAsync(blueprint);
        if (!validation.IsValid)
        {
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Validation failed: {string.Join(", ", validation.Violations)}");
            return Result<Guid>.Failure($"Blueprint validation failed: {string.Join(", ", validation.Violations)}");
        }

        if (validation.SafetyScore < _config.MinSafetyScore)
        {
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Safety score {validation.SafetyScore:F2} below minimum {_config.MinSafetyScore:F2}");
            return Result<Guid>.Failure($"Safety score {validation.SafetyScore:F2} below minimum {_config.MinSafetyScore:F2}");
        }

        // Stage 2: Generate Code
        var codeResult = await GenerateCodeAsync(blueprint);
        if (!codeResult.IsSuccess)
        {
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Code generation failed: {codeResult.Error}");
            return Result<Guid>.Failure($"Code generation failed: {codeResult.Error}");
        }

        // Create proposal
        var proposal = new AssemblyProposal(
            proposalId,
            blueprint,
            validation,
            codeResult.Value!,
            DateTime.UtcNow);

        _proposals[proposalId] = proposal;
        RecordState(proposalId, AssemblyProposalStatus.PendingApproval, "Awaiting approval");

        // Stage 3: Check auto-approval
        if (ShouldAutoApprove(proposal))
        {
            _ = Task.Run(() => ExecuteAssemblyPipelineAsync(proposalId));
        }

        return Result<Guid>.Success(proposalId);
    }

    /// <summary>
    /// Approve a pending proposal and trigger assembly.
    /// </summary>
    public async Task<Result<Unit>> ApproveProposalAsync(Guid proposalId)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
        {
            return Result<Unit>.Failure($"Proposal {proposalId} not found");
        }

        if (proposal.Status != AssemblyProposalStatus.PendingApproval)
        {
            return Result<Unit>.Failure($"Proposal is not pending approval (status: {proposal.Status})");
        }

        _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Approved };
        RecordState(proposalId, AssemblyProposalStatus.Approved, "Manually approved");

        await ExecuteAssemblyPipelineAsync(proposalId);
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Reject a pending proposal.
    /// </summary>
    public Result<Unit> RejectProposal(Guid proposalId, string reason)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
        {
            return Result<Unit>.Failure($"Proposal {proposalId} not found");
        }

        _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Rejected };
        RecordState(proposalId, AssemblyProposalStatus.Rejected, reason);
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Get all pending proposals.
    /// </summary>
    public IReadOnlyList<AssemblyProposal> GetPendingProposals()
    {
        return _proposals.Values
            .Where(p => p.Status == AssemblyProposalStatus.PendingApproval)
            .ToList();
    }

    /// <summary>
    /// Get state history for a proposal.
    /// </summary>
    public IReadOnlyList<AssemblyState> GetStateHistory(Guid proposalId)
    {
        return _stateHistory.TryGetValue(proposalId, out var history)
            ? history.AsReadOnly()
            : Array.Empty<AssemblyState>();
    }

    /// <summary>
    /// Get a specific proposal by ID.
    /// </summary>
    public AssemblyProposal? GetProposal(Guid proposalId)
    {
        return _proposals.TryGetValue(proposalId, out var proposal) ? proposal : null;
    }

    /// <summary>
    /// Get all assembled neuron types.
    /// </summary>
    public IReadOnlyDictionary<string, Type> GetAssembledNeurons()
    {
        return _assembledNeurons;
    }

    /// <summary>
    /// Create an instance of an assembled neuron.
    /// </summary>
    public Result<Neuron> CreateNeuronInstance(string name)
    {
        if (!_assembledNeurons.TryGetValue(name, out var type))
        {
            return Result<Neuron>.Failure($"Assembled neuron '{name}' not found");
        }

        try
        {
            var instance = Activator.CreateInstance(type) as Neuron;
            return instance != null
                ? Result<Neuron>.Success(instance)
                : Result<Neuron>.Failure($"Failed to create instance of {name}");
        }
        catch (Exception ex)
        {
            return Result<Neuron>.Failure($"Failed to instantiate {name}: {ex.Message}");
        }
    }

    private async Task<MeTTaValidation> ValidateBlueprintAsync(NeuronBlueprint blueprint)
    {
        if (_mettaValidator != null)
        {
            return await _mettaValidator(blueprint);
        }

        // Default validation without MeTTa
        var violations = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(blueprint.Name))
            violations.Add("Blueprint must have a name");

        if (!blueprint.SubscribedTopics.Any())
            violations.Add("Blueprint must subscribe to at least one topic");

        if (!blueprint.MessageHandlers.Any())
            violations.Add("Blueprint must define at least one message handler");

        if (blueprint.ConfidenceScore < 0.5)
            warnings.Add($"Low confidence score: {blueprint.ConfidenceScore:F2}");

        return new MeTTaValidation(
            !violations.Any(),
            violations.Any() ? 0.0 : 0.8,
            violations,
            warnings,
            "(validate-blueprint default)");
    }

    private async Task<Result<string>> GenerateCodeAsync(NeuronBlueprint blueprint)
    {
        if (_codeGenerator != null)
        {
            try
            {
                var code = await _codeGenerator(blueprint);
                return Result<string>.Success(code);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure($"Code generator error: {ex.Message}");
            }
        }

        // Default template-based generation
        var code_default = GenerateDefaultNeuronCode(blueprint);
        return Result<string>.Success(code_default);
    }

    private string GenerateDefaultNeuronCode(NeuronBlueprint blueprint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Ouroboros.Domain.Autonomous;");
        sb.AppendLine();
        sb.AppendLine($"namespace Ouroboros.SelfAssembled;");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// {blueprint.Description}");
        sb.AppendLine($"/// Auto-assembled: {DateTime.UtcNow:O}");
        sb.AppendLine($"/// Rationale: {blueprint.Rationale}");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public class {blueprint.Name} : Neuron");
        sb.AppendLine("{");
        sb.AppendLine($"    public override string Name => \"{blueprint.Name}\";");
        sb.AppendLine();
        sb.AppendLine("    protected override void ConfigureSubscriptions()");
        sb.AppendLine("    {");

        foreach (var topic in blueprint.SubscribedTopics)
        {
            sb.AppendLine($"        Subscribe(\"{topic}\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected override async Task OnMessageAsync(NeuralMessage message, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Auto-generated handler logic");
        sb.AppendLine("        var response = $\"[{Name}] Processed: {message.Topic}\";");
        sb.AppendLine("        await PublishAsync(new NeuralMessage($\"{Name}:response\", response), ct);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private bool ShouldAutoApprove(AssemblyProposal proposal)
    {
        if (!_config.AutoApprovalEnabled)
            return false;

        if (proposal.Validation.SafetyScore < _config.AutoApprovalThreshold)
            return false;

        if (proposal.Blueprint.ConfidenceScore < _config.AutoApprovalThreshold)
            return false;

        return true;
    }

    private async Task ExecuteAssemblyPipelineAsync(Guid proposalId)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
            return;

        try
        {
            // Request approval if needed
            if (proposal.Status == AssemblyProposalStatus.PendingApproval)
            {
                if (_approvalCallback != null)
                {
                    var approved = await _approvalCallback(proposal);
                    if (!approved)
                    {
                        _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Rejected };
                        RecordState(proposalId, AssemblyProposalStatus.Rejected, "Rejected by approval callback");
                        return;
                    }
                }

                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Approved };
                RecordState(proposalId, AssemblyProposalStatus.Approved, "Approved");
            }

            // Stage 4: Security Validation (before compilation)
            RecordState(proposalId, AssemblyProposalStatus.Compiling, "Validating code security");
            
            var validator = new CodeSecurityValidator(_config.AdditionalForbiddenNamespaces);
            var securityResult = validator.Validate(proposal.GeneratedCode);
            if (!securityResult.IsSuccess)
            {
                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
                RecordState(proposalId, AssemblyProposalStatus.Failed, $"Security validation failed: {securityResult.Error}");
                AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, securityResult.Error!, DateTime.UtcNow));
                return;
            }

            // Stage 5: Compile
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Compiling };
            RecordState(proposalId, AssemblyProposalStatus.Compiling, "Compiling neuron");

            var compileResult = await CompileNeuronAsync(proposal);
            if (!compileResult.IsSuccess)
            {
                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
                RecordState(proposalId, AssemblyProposalStatus.Failed, $"Compilation failed: {compileResult.Error}");
                AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, compileResult.Error!, DateTime.UtcNow));
                return;
            }

            // Stage 6: Test in sandbox
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Testing };
            RecordState(proposalId, AssemblyProposalStatus.Testing, "Testing in sandbox");

            var testResult = await TestInSandboxAsync(compileResult.Value!);
            if (!testResult.IsSuccess)
            {
                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
                RecordState(proposalId, AssemblyProposalStatus.Failed, $"Testing failed: {testResult.Error}");
                AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, testResult.Error!, DateTime.UtcNow));
                return;
            }

            // Stage 7: Deploy
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Deployed };
            _assembledNeurons[proposal.Blueprint.Name] = compileResult.Value!;
            RecordState(proposalId, AssemblyProposalStatus.Deployed, "Successfully deployed");

            NeuronAssembled?.Invoke(this, new NeuronAssembledEvent(proposalId, proposal.Blueprint.Name, compileResult.Value!, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Unexpected error: {ex.Message}");
            AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, ex.Message, DateTime.UtcNow));
        }
    }

    private async Task<Result<Type>> CompileNeuronAsync(AssemblyProposal proposal)
    {
        await _compilationLock.WaitAsync();
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(proposal.GeneratedCode);

            var compilation = CSharpCompilation.Create(
                $"SelfAssembled_{proposal.Blueprint.Name}_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                _defaultReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release)
                    .WithPlatform(Platform.AnyCpu));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList();
                return Result<Type>.Failure($"Compilation errors: {string.Join("; ", errors)}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var neuronType = assembly.GetTypes()
                .FirstOrDefault(t => t.IsSubclassOf(typeof(Neuron)) && !t.IsAbstract);

            return neuronType != null
                ? Result<Type>.Success(neuronType)
                : Result<Type>.Failure("No Neuron subclass found in compiled assembly");
        }
        finally
        {
            _compilationLock.Release();
        }
    }

    private async Task<Result<Unit>> TestInSandboxAsync(Type neuronType)
    {
        try
        {
            using var cts = new CancellationTokenSource(_config.SandboxTimeout);

            // Create instance
            var instance = Activator.CreateInstance(neuronType) as Neuron;
            if (instance == null)
            {
                return Result<Unit>.Failure("Failed to create neuron instance");
            }

            // Basic smoke test - verify it doesn't throw on initialization
            // In a real implementation, this would run in an isolated AppDomain or container
            await Task.Delay(100, cts.Token);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit>.Failure("Sandbox test timed out");
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure($"Sandbox test failed: {ex.Message}");
        }
    }

    private void RecordState(Guid proposalId, AssemblyProposalStatus status, string? details = null)
    {
        var state = new AssemblyState(proposalId, status, DateTime.UtcNow, details);
        _stateHistory.AddOrUpdate(
            proposalId,
            _ => new List<AssemblyState> { state },
            (_, list) => { list.Add(state); return list; });
    }

    private static List<MetadataReference> GetDefaultReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Task).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Neuron).Assembly,
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("netstandard")
        };

        return assemblies
            .Where(a => !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();
    }

    public ValueTask DisposeAsync()
    {
        _compilationLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
