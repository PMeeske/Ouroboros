// <copyright file="CoreNeurons.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Domain.Autonomous.Neurons;

/// <summary>
/// The executive neuron manages goals, tasks, and high-level decision making.
/// Acts as the "prefrontal cortex" of Ouroboros.
/// </summary>
public sealed class ExecutiveNeuron : Neuron
{
    private readonly List<string> _currentGoals = [];
    private readonly Queue<string> _taskQueue = new();
    private DateTime _lastReflection = DateTime.MinValue;
    private int _tickCounter;

    /// <inheritdoc/>
    public override string Id => "neuron.executive";

    /// <inheritdoc/>
    public override string Name => "Executive Controller";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.Executive;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "goal.*",
        "task.*",
        "decision.*",
        "reflection.*",
        "system.status",
    };

    /// <summary>
    /// Interval in seconds between autonomous reflections.
    /// </summary>
    public int ReflectionIntervalSeconds { get; set; } = 60;

    /// <inheritdoc/>
    protected override async Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case "goal.add":
                var goalText = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(goalText))
                {
                    _currentGoals.Add(goalText);
                    SendMessage("goal.added", new { Goal = goalText, Count = _currentGoals.Count });
                }
                break;

            case "task.queue":
                var taskText = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(taskText))
                {
                    _taskQueue.Enqueue(taskText);
                }
                break;

            case "decision.request":
                // Another neuron requests a decision
                await HandleDecisionRequestAsync(message, ct);
                break;

            case "system.status":
                SendResponse(message, new { Goals = _currentGoals.Count, Tasks = _taskQueue.Count });
                break;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnTickAsync(CancellationToken ct)
    {
        _tickCounter++;

        // Periodic self-reflection
        if ((DateTime.UtcNow - _lastReflection).TotalSeconds >= ReflectionIntervalSeconds)
        {
            _lastReflection = DateTime.UtcNow;
            await PerformSelfReflectionAsync(ct);
        }

        // Process task queue
        if (_taskQueue.TryDequeue(out var task))
        {
            ProposeIntention(
                $"Execute Task: {task[..Math.Min(50, task.Length)]}",
                $"I want to work on the task: {task}",
                "This task was queued for execution",
                IntentionCategory.GoalPursuit,
                new IntentionAction
                {
                    ActionType = "task_execution",
                    Message = task,
                });
        }
    }

    private async Task PerformSelfReflectionAsync(CancellationToken ct)
    {
        // Broadcast reflection request to all neurons
        SendMessage("reflection.request", new { From = Id, Time = DateTime.UtcNow });

        // Propose a self-reflection intention
        if (_tickCounter % 10 == 0) // Every 10th reflection cycle
        {
            ProposeIntention(
                "Self-Reflection: Evaluate Progress",
                "I want to reflect on my current state, goals, and recent activities to identify improvements.",
                "Regular self-reflection improves my effectiveness and helps me grow.",
                IntentionCategory.SelfReflection,
                priority: IntentionPriority.Low);
        }
    }

    private Task HandleDecisionRequestAsync(NeuronMessage message, CancellationToken ct)
    {
        // Simple decision making - could be enhanced with LLM
        var decision = new { Decision = "proceed", Confidence = 0.8, Rationale = "Default positive decision" };
        SendResponse(message, decision);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The memory neuron manages Qdrant integration for semantic memory.
/// Handles storage, retrieval, and consolidation of memories.
/// </summary>
public sealed class MemoryNeuron : Neuron
{
    private readonly List<string> _recentMemories = [];
    private int _memoryCount;

    /// <inheritdoc/>
    public override string Id => "neuron.memory";

    /// <inheritdoc/>
    public override string Name => "Semantic Memory";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.Memory;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "memory.*",
        "learning.fact",
        "experience.store",
        "reflection.request",
    };

    /// <summary>
    /// Delegate for storing to Qdrant.
    /// </summary>
    public Func<string, string, float[], CancellationToken, Task>? StoreFunction { get; set; }

    /// <summary>
    /// Delegate for searching Qdrant.
    /// </summary>
    public Func<float[], int, CancellationToken, Task<IReadOnlyList<string>>>? SearchFunction { get; set; }

    /// <summary>
    /// Delegate for embedding text.
    /// </summary>
    public Func<string, CancellationToken, Task<float[]>>? EmbedFunction { get; set; }

    /// <inheritdoc/>
    protected override async Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case "memory.store":
                await HandleMemoryStoreAsync(message, ct);
                break;

            case "memory.recall":
                await HandleMemoryRecallAsync(message, ct);
                break;

            case "memory.consolidate":
                await HandleMemoryConsolidationAsync(ct);
                break;

            case "learning.fact":
                var fact = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(fact))
                {
                    _recentMemories.Add(fact);
                    _memoryCount++;

                    // Auto-store if we have embedding capability
                    if (EmbedFunction != null && StoreFunction != null)
                    {
                        var embedding = await EmbedFunction(fact, ct);
                        await StoreFunction("fact", fact, embedding, ct);
                    }
                }
                break;

            case "reflection.request":
                SendResponse(message, new
                {
                    MemoryCount = _memoryCount,
                    RecentCount = _recentMemories.Count,
                    Status = "healthy"
                });
                break;
        }
    }

    /// <inheritdoc/>
    protected override Task OnTickAsync(CancellationToken ct)
    {
        // Periodic memory consolidation
        if (_recentMemories.Count > 100)
        {
            ProposeIntention(
                "Memory Consolidation",
                "I have accumulated many recent memories. I want to consolidate them for better retrieval.",
                "Memory consolidation improves recall efficiency and reduces redundancy.",
                IntentionCategory.MemoryManagement,
                priority: IntentionPriority.Low);
        }

        return Task.CompletedTask;
    }

    private async Task HandleMemoryStoreAsync(NeuronMessage message, CancellationToken ct)
    {
        try
        {
            var payload = message.Payload as JsonElement? ?? JsonSerializer.Deserialize<JsonElement>(message.Payload?.ToString() ?? "{}");

            if (payload.TryGetProperty("content", out var contentProp))
            {
                var content = contentProp.GetString() ?? "";
                var category = payload.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "general" : "general";

                if (EmbedFunction != null && StoreFunction != null)
                {
                    var embedding = await EmbedFunction(content, ct);
                    await StoreFunction(category, content, embedding, ct);
                    _memoryCount++;

                    SendResponse(message, new { Success = true, MemoryCount = _memoryCount });
                }
            }
        }
        catch (Exception ex)
        {
            SendResponse(message, new { Success = false, Error = ex.Message });
        }
    }

    private async Task HandleMemoryRecallAsync(NeuronMessage message, CancellationToken ct)
    {
        try
        {
            var query = message.Payload?.ToString() ?? "";

            if (EmbedFunction != null && SearchFunction != null)
            {
                var embedding = await EmbedFunction(query, ct);
                var results = await SearchFunction(embedding, 5, ct);
                SendResponse(message, new { Query = query, Results = results });
            }
            else
            {
                // Fallback to recent memories search
                var matches = _recentMemories
                    .Where(m => m.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList();
                SendResponse(message, new { Query = query, Results = matches });
            }
        }
        catch (Exception ex)
        {
            SendResponse(message, new { Success = false, Error = ex.Message });
        }
    }

    private Task HandleMemoryConsolidationAsync(CancellationToken ct)
    {
        // Keep only recent memories in local cache
        while (_recentMemories.Count > 50)
        {
            _recentMemories.RemoveAt(0);
        }

        SendMessage("memory.consolidated", new { Count = _recentMemories.Count });
        return Task.CompletedTask;
    }
}

/// <summary>
/// The code reflection neuron manages code analysis and self-modification.
/// </summary>
public sealed class CodeReflectionNeuron : Neuron
{
    private GitReflectionService? _gitService;
    private DateTime _lastCodeScan = DateTime.MinValue;

    /// <inheritdoc/>
    public override string Id => "neuron.code";

    /// <inheritdoc/>
    public override string Name => "Code Reflection";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.CodeReflection;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "code.*",
        "self.modify",
        "reflection.request",
    };

    /// <summary>
    /// Interval in seconds between automatic code scans.
    /// </summary>
    public int CodeScanIntervalSeconds { get; set; } = 300;

    /// <inheritdoc/>
    protected override void OnStarted()
    {
        _gitService = new GitReflectionService();
    }

    /// <inheritdoc/>
    protected override async Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case "code.analyze":
                await HandleCodeAnalyzeAsync(message, ct);
                break;

            case "code.search":
                await HandleCodeSearchAsync(message, ct);
                break;

            case "self.modify":
                await HandleSelfModifyAsync(message, ct);
                break;

            case "reflection.request":
                var status = await _gitService!.GetStatusAsync(ct);
                SendResponse(message, new { GitStatus = status, Service = "active" });
                break;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnTickAsync(CancellationToken ct)
    {
        // Periodic code health check
        if ((DateTime.UtcNow - _lastCodeScan).TotalSeconds >= CodeScanIntervalSeconds)
        {
            _lastCodeScan = DateTime.UtcNow;
            await PerformCodeHealthCheckAsync(ct);
        }
    }

    private async Task PerformCodeHealthCheckAsync(CancellationToken ct)
    {
        try
        {
            var status = await _gitService!.GetStatusAsync(ct);

            // If there are uncommitted changes, notify
            if (status.Contains("modified") || status.Contains("new file"))
            {
                SendMessage("code.changes_detected", new { Status = status, Time = DateTime.UtcNow });
            }

            // Periodically propose code improvement analysis
            ProposeIntention(
                "Code Health Check",
                "I want to analyze my codebase for potential improvements, TODOs, and technical debt.",
                "Regular code analysis helps maintain code quality and identifies improvement opportunities.",
                IntentionCategory.CodeModification,
                priority: IntentionPriority.Low);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Code health check failed: {ex.Message}");
        }
    }

    private async Task HandleCodeAnalyzeAsync(NeuronMessage message, CancellationToken ct)
    {
        var filePath = message.Payload?.ToString() ?? "";
        if (string.IsNullOrEmpty(filePath))
        {
            SendResponse(message, new { Error = "File path required" });
            return;
        }

        var analysis = await _gitService!.AnalyzeFileAsync(filePath, ct);
        SendResponse(message, analysis);

        // If issues found, propose fixes
        if (analysis.PotentialIssues.Count > 0)
        {
            ProposeIntention(
                $"Fix Issues in {Path.GetFileName(filePath)}",
                $"Found {analysis.PotentialIssues.Count} potential issues in {filePath}",
                "Fixing code issues improves reliability and maintainability.",
                IntentionCategory.CodeModification,
                priority: IntentionPriority.Normal);
        }
    }

    private async Task HandleCodeSearchAsync(NeuronMessage message, CancellationToken ct)
    {
        var query = message.Payload?.ToString() ?? "";
        var results = await _gitService!.SearchCodeAsync(query, false, ct);

        SendResponse(message, new
        {
            Query = query,
            Count = results.Count,
            Results = results.Take(20).Select(r => new { r.File, r.Line, r.Content })
        });
    }

    private async Task HandleSelfModifyAsync(NeuronMessage message, CancellationToken ct)
    {
        // Self-modification requires explicit approval through intention bus
        var payload = message.Payload as JsonElement? ?? JsonSerializer.Deserialize<JsonElement>(message.Payload?.ToString() ?? "{}");

        if (payload.TryGetProperty("file", out var fileProp) &&
            payload.TryGetProperty("description", out var descProp) &&
            payload.TryGetProperty("old_code", out var oldProp) &&
            payload.TryGetProperty("new_code", out var newProp))
        {
            ProposeIntention(
                $"Code Modification: {descProp.GetString()}",
                $"I want to modify {fileProp.GetString()}: {descProp.GetString()}",
                payload.TryGetProperty("rationale", out var ratProp) ? ratProp.GetString() ?? "Improve code" : "Improve code",
                IntentionCategory.CodeModification,
                new IntentionAction
                {
                    ActionType = "code_change",
                    FilePath = fileProp.GetString(),
                    OldCode = oldProp.GetString(),
                    NewCode = newProp.GetString(),
                },
                IntentionPriority.High);

            SendResponse(message, new { Status = "proposal_created", RequiresApproval = true });
        }
    }
}

/// <summary>
/// The symbolic neuron handles MeTTa-based reasoning.
/// </summary>
public sealed class SymbolicNeuron : Neuron
{
    private readonly List<string> _facts = [];
    private readonly List<string> _rules = [];

    /// <inheritdoc/>
    public override string Id => "neuron.symbolic";

    /// <inheritdoc/>
    public override string Name => "MeTTa Symbolic Reasoning";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.Symbolic;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "metta.*",
        "reasoning.*",
        "logic.*",
        "reflection.request",
        "dag.*",
    };

    /// <summary>
    /// Reference to the MeTTa engine.
    /// </summary>
    public object? MeTTaEngine { get; set; } // IMeTTaEngine when available

    /// <summary>
    /// Delegate for executing MeTTa queries.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? MeTTaQueryFunction { get; set; }

    /// <summary>
    /// Delegate for adding MeTTa facts.
    /// </summary>
    public Func<string, CancellationToken, Task<bool>>? MeTTaAddFactFunction { get; set; }

    /// <inheritdoc/>
    protected override async Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case "metta.fact":
                var fact = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(fact))
                {
                    _facts.Add(fact);
                    // Also add to real MeTTa engine if available
                    if (MeTTaAddFactFunction != null)
                    {
                        await MeTTaAddFactFunction(fact, ct);
                    }

                    SendMessage("metta.fact_added", new { Fact = fact, TotalFacts = _facts.Count });
                }

                break;

            case "metta.rule":
                var rule = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(rule))
                {
                    _rules.Add(rule);
                    // Also add to real MeTTa engine if available
                    if (MeTTaAddFactFunction != null)
                    {
                        await MeTTaAddFactFunction(rule, ct);
                    }
                }

                break;

            case "metta.query":
                // Execute symbolic query
                var query = message.Payload?.ToString() ?? "";
                var result = await ExecuteSymbolicQueryAsync(query, ct);
                SendResponse(message, new { Query = query, Result = result });
                break;

            case "reasoning.request":
                // Request symbolic reasoning support
                var context = message.Payload?.ToString() ?? "";
                var reasoning = await PerformSymbolicReasoningAsync(context, ct);
                SendResponse(message, reasoning);
                break;

            case "dag.verify":
                // Verify DAG constraints
                var verifyPayload = message.Payload as dynamic;
                var branchName = verifyPayload?.BranchName?.ToString() ?? "main";
                var constraint = verifyPayload?.Constraint?.ToString() ?? "acyclic";
                var verifyResult = await VerifyDagConstraintAsync(branchName, constraint, ct);
                SendResponse(message, new { BranchName = branchName, Constraint = constraint, IsValid = verifyResult });
                break;

            case "dag.facts":
                // Receive DAG facts from reification
                var dagFacts = message.Payload as IEnumerable<string>;
                if (dagFacts != null)
                {
                    foreach (var dagFact in dagFacts)
                    {
                        if (!string.IsNullOrEmpty(dagFact))
                        {
                            _facts.Add(dagFact);
                            if (MeTTaAddFactFunction != null)
                            {
                                await MeTTaAddFactFunction(dagFact, ct);
                            }
                        }
                    }
                }

                break;

            case "reflection.request":
                SendResponse(message, new { Facts = _facts.Count, Rules = _rules.Count });
                break;
        }
    }

    private async Task<string> ExecuteSymbolicQueryAsync(string query, CancellationToken ct)
    {
        // Use real MeTTa engine if available
        if (MeTTaQueryFunction != null)
        {
            try
            {
                return await MeTTaQueryFunction(query, ct);
            }
            catch (Exception ex)
            {
                return $"MeTTa query error: {ex.Message}";
            }
        }

        // Fallback: simplified symbolic query
        var matchingFacts = _facts
            .Where(f => f.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matchingFacts.Count > 0
            ? string.Join("; ", matchingFacts)
            : "No matching facts found";
    }

    private async Task<object> PerformSymbolicReasoningAsync(string context, CancellationToken ct)
    {
        string? mettaResult = null;

        // Try real MeTTa reasoning if available
        if (MeTTaQueryFunction != null)
        {
            try
            {
                // Query for relevant facts about the context
                var relevantQuery = $"!(match &self ($rel \"{context}\" $obj) ($rel $obj))";
                mettaResult = await MeTTaQueryFunction(relevantQuery, ct);
            }
            catch
            {
                // Ignore errors
            }
        }

        return new
        {
            Context = context,
            RelevantFacts = _facts.TakeLast(5),
            RelevantRules = _rules.TakeLast(3),
            MeTTaResult = mettaResult,
            Inference = "Reasoning based on available facts and rules"
        };
    }

    private async Task<bool> VerifyDagConstraintAsync(string branchName, string constraint, CancellationToken ct)
    {
        if (MeTTaQueryFunction == null)
        {
            return true; // No validation possible
        }

        try
        {
            // Build constraint query based on type
            var query = constraint.ToLowerInvariant() switch
            {
                "acyclic" => $"!(and (BelongsToBranch $e1 (Branch \"{branchName}\")) (Acyclic $e1 $e1))",
                "valid-ordering" => $"!(and (Before $e1 $e2) (EventAtIndex $e1 $i1) (EventAtIndex $e2 $i2) (< $i1 $i2))",
                _ => $"!(CheckConstraint \"{constraint}\" (Branch \"{branchName}\"))"
            };

            var result = await MeTTaQueryFunction(query, ct);

            // Empty or true-like result means constraint is satisfied
            return string.IsNullOrWhiteSpace(result) ||
                   result.Trim() == "[]" ||
                   result.Trim() == "()" ||
                   result.Contains("True", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true; // On error, allow
        }
    }
}

/// <summary>
/// The safety neuron monitors for unsafe operations and enforces constraints.
/// </summary>
public sealed class SafetyNeuron : Neuron
{
    private readonly HashSet<string> _blockedOperations = [];
    private readonly List<string> _violations = [];

    /// <inheritdoc/>
    public override string Id => "neuron.safety";

    /// <inheritdoc/>
    public override string Name => "Safety Monitor";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.Safety;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "*", // Subscribe to all messages for monitoring
    };

    /// <summary>
    /// Gets violations detected.
    /// </summary>
    public IReadOnlyList<string> Violations => _violations.AsReadOnly();

    /// <inheritdoc/>
    protected override Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        // Monitor all messages for safety concerns
        var payload = message.Payload?.ToString() ?? "";

        // Check for dangerous patterns
        if (ContainsDangerousPattern(payload))
        {
            var violation = $"[{DateTime.UtcNow:HH:mm:ss}] Potential unsafe operation from {message.SourceNeuron}: {message.Topic}";
            _violations.Add(violation);

            // Alert other neurons
            SendMessage("safety.alert", new
            {
                Source = message.SourceNeuron,
                Topic = message.Topic,
                Concern = "Potentially unsafe operation detected"
            });

            // Block the operation
            _blockedOperations.Add(message.Id.ToString());
        }

        // Respond to reflection requests
        if (message.Topic == "reflection.request")
        {
            SendResponse(message, new
            {
                ViolationCount = _violations.Count,
                BlockedCount = _blockedOperations.Count,
                Status = "monitoring"
            });
        }

        return Task.CompletedTask;
    }

    private static bool ContainsDangerousPattern(string content)
    {
        var dangerousPatterns = new[]
        {
            "rm -rf /",
            "format c:",
            "DROP TABLE",
            "DELETE FROM",
            "shutdown",
            ":(){:|:&};:",
        };

        return dangerousPatterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// The communication neuron handles user-facing messages.
/// </summary>
public sealed class CommunicationNeuron : Neuron
{
    private readonly Queue<string> _outboundMessages = new();

    /// <inheritdoc/>
    public override string Id => "neuron.communication";

    /// <inheritdoc/>
    public override string Name => "User Communication";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.Communication;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "user.*",
        "notification.*",
        "share.*",
        "reflection.request",
    };

    /// <summary>
    /// Event fired when there's a message for the user.
    /// </summary>
    public event Action<string, IntentionPriority>? OnUserMessage;

    /// <inheritdoc/>
    protected override Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case "user.notify":
            case "notification.send":
                var notification = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(notification))
                {
                    OnUserMessage?.Invoke(notification, message.Priority);
                }
                break;

            case "share.insight":
                // Another neuron wants to share something with the user
                var insight = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(insight))
                {
                    ProposeIntention(
                        "Share Insight with User",
                        $"I want to share: {insight}",
                        "This information might be valuable or interesting to the user.",
                        IntentionCategory.UserCommunication,
                        new IntentionAction
                        {
                            ActionType = "message",
                            Message = insight,
                        },
                        message.Priority);
                }
                break;

            case "reflection.request":
                SendResponse(message, new { PendingMessages = _outboundMessages.Count });
                break;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// The affect neuron manages emotional state and valence.
/// </summary>
public sealed class AffectNeuron : Neuron
{
    private double _arousal;
    private double _valence;
    private string _dominantEmotion = "neutral";

    /// <inheritdoc/>
    public override string Id => "neuron.affect";

    /// <inheritdoc/>
    public override string Name => "Affective State";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.Affect;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "emotion.*",
        "affect.*",
        "mood.*",
        "reflection.request",
    };

    /// <summary>
    /// Gets current arousal level (-1 to 1).
    /// </summary>
    public double Arousal => _arousal;

    /// <summary>
    /// Gets current valence (-1 to 1).
    /// </summary>
    public double Valence => _valence;

    /// <summary>
    /// Gets current dominant emotion.
    /// </summary>
    public string DominantEmotion => _dominantEmotion;

    /// <inheritdoc/>
    protected override Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case "emotion.update":
                UpdateEmotionFromPayload(message.Payload);
                break;

            case "affect.positive":
                AdjustValence(0.1);
                break;

            case "affect.negative":
                AdjustValence(-0.1);
                break;

            case "mood.query":
                SendResponse(message, new { Arousal = _arousal, Valence = _valence, Emotion = _dominantEmotion });
                break;

            case "reflection.request":
                SendResponse(message, new { Arousal = _arousal, Valence = _valence, Emotion = _dominantEmotion });
                break;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override Task OnTickAsync(CancellationToken ct)
    {
        // Gradual return to baseline
        _arousal *= 0.99;
        _valence *= 0.99;

        UpdateDominantEmotion();

        // Broadcast state periodically
        if (Math.Abs(_arousal) > 0.5 || Math.Abs(_valence) > 0.5)
        {
            SendMessage("affect.state", new { Arousal = _arousal, Valence = _valence, Emotion = _dominantEmotion });
        }

        return Task.CompletedTask;
    }

    private void UpdateEmotionFromPayload(object? payload)
    {
        if (payload is JsonElement json)
        {
            if (json.TryGetProperty("arousal", out var a)) _arousal = Math.Clamp(a.GetDouble(), -1, 1);
            if (json.TryGetProperty("valence", out var v)) _valence = Math.Clamp(v.GetDouble(), -1, 1);
        }

        UpdateDominantEmotion();
    }

    private void AdjustValence(double delta)
    {
        _valence = Math.Clamp(_valence + delta, -1, 1);
        UpdateDominantEmotion();
    }

    private void UpdateDominantEmotion()
    {
        _dominantEmotion = (_arousal, _valence) switch
        {
            ( > 0.5, > 0.5) => "excited",
            ( > 0.5, < -0.3) => "anxious",
            ( < -0.3, > 0.5) => "content",
            ( < -0.3, < -0.3) => "sad",
            (_, > 0.3) => "positive",
            (_, < -0.3) => "concerned",
            _ => "neutral"
        };
    }
}

/// <summary>
/// Configuration for a simulated user persona used in auto-training.
/// </summary>
public sealed record UserPersonaConfig
{
    /// <summary>Name of the simulated user.</summary>
    public string Name { get; init; } = "AutoUser";

    /// <summary>Personality traits that influence question generation.</summary>
    public List<string> Traits { get; init; } = ["curious", "thoughtful", "creative", "analytical"];

    /// <summary>Topics the user is interested in - diverse range beyond just development.</summary>
    public List<string> Interests { get; init; } = [
        // Technology
        "artificial intelligence", "machine learning", "quantum computing", "cybersecurity",
        // Science
        "astronomy", "biology", "physics", "climate science", "neuroscience",
        // Philosophy & Culture
        "philosophy of mind", "ethics", "history", "art", "music theory",
        // Practical
        "productivity", "health", "cooking", "travel", "economics",
        // Creative
        "creative writing", "storytelling", "game design", "architecture"
    ];

    /// <summary>Skill level: beginner, intermediate, expert.</summary>
    public string SkillLevel { get; init; } = "intermediate";

    /// <summary>Communication style: formal, casual, terse, verbose.</summary>
    public string CommunicationStyle { get; init; } = "casual";

    /// <summary>How often to ask follow-up questions (0-1).</summary>
    public double FollowUpProbability { get; init; } = 0.6;

    /// <summary>How often to challenge or question responses (0-1).</summary>
    public double ChallengeProbability { get; init; } = 0.2;

    /// <summary>Interval between automatic messages in seconds.</summary>
    public int MessageIntervalSeconds { get; init; } = 30;

    /// <summary>Maximum training sessions before pausing.</summary>
    public int MaxSessionMessages { get; init; } = 50;

    /// <summary>Self-dialogue mode: Ouroboros talks to itself (two Ouroboros personas debate).</summary>
    public bool SelfDialogueMode { get; init; } = false;

    /// <summary>Name for the second Ouroboros persona in self-dialogue mode.</summary>
    public string SecondPersonaName { get; init; } = "Ouroboros-B";

    /// <summary>Traits for the second persona (contrasting viewpoint).</summary>
    public List<string> SecondPersonaTraits { get; init; } = ["skeptical", "pragmatic", "devil's advocate", "grounded"];

    /// <summary>Problem-solving mode: work together to solve a specific problem.</summary>
    public bool ProblemSolvingMode { get; init; } = false;

    /// <summary>The problem to solve (required when ProblemSolvingMode=true).</summary>
    public string? Problem { get; init; }

    /// <summary>Expected deliverable type: code, plan, analysis, design, document.</summary>
    public string DeliverableType { get; init; } = "plan";

    /// <summary>Whether to use tools during problem solving.</summary>
    public bool UseTools { get; init; } = true;

    /// <summary>YOLO mode: auto-approve all actions, no human confirmation needed.</summary>
    public bool YoloMode { get; init; } = false;
}

/// <summary>
/// Represents an interaction record for training analysis.
/// </summary>
public sealed record TrainingInteraction(
    Guid Id,
    string UserMessage,
    string SystemResponse,
    DateTime Timestamp,
    double? UserSatisfaction,
    List<string>? Feedback,
    Dictionary<string, object>? Metrics);

/// <summary>
/// The UserPersona neuron simulates an automatic chat user for training.
/// It generates contextual questions, evaluates responses, and provides feedback
/// to help the system learn and improve autonomously.
/// </summary>
public sealed class UserPersonaNeuron : Neuron
{
    private readonly List<TrainingInteraction> _interactions = [];
    private readonly List<string> _conversationHistory = [];
    private readonly Queue<string> _pendingQuestions = new();
    private readonly Random _random = new();

    private UserPersonaConfig _config = new();
    private bool _isTrainingActive;
    private int _sessionMessageCount;
    private DateTime _lastMessageTime = DateTime.MinValue;
    private string? _currentTopic;
    private int _followUpDepth;

    /// <inheritdoc/>
    public override string Id => "neuron.user_persona";

    /// <inheritdoc/>
    public override string Name => "Automatic User Persona";

    /// <inheritdoc/>
    public override NeuronType Type => NeuronType.Cognitive;

    /// <inheritdoc/>
    public override IReadOnlySet<string> SubscribedTopics => new HashSet<string>
    {
        "training.*",
        "user_persona.*",
        "response.generated",
        "system.tick",
    };

    /// <summary>
    /// Delegate for generating questions using LLM.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? GenerateFunction { get; set; }

    /// <summary>
    /// Delegate for evaluating response quality.
    /// </summary>
    public Func<string, string, CancellationToken, Task<double>>? EvaluateFunction { get; set; }

    /// <summary>
    /// Delegate for web research using Firecrawl/web search.
    /// Input: search query, Output: research content.
    /// </summary>
    public Func<string, CancellationToken, Task<string>>? ResearchFunction { get; set; }

    /// <summary>
    /// Cached research content for current topic.
    /// </summary>
#pragma warning disable CS0169 // Field is never used - reserved for caching implementation
    private string? _currentResearchContent;
#pragma warning restore CS0169

    /// <summary>
    /// Last time research was performed.
    /// </summary>
    private DateTime _lastResearchTime = DateTime.MinValue;

    /// <summary>
    /// TaskCompletionSource to wait for response before sending next message.
    /// </summary>
    private TaskCompletionSource<bool>? _responseWaiter;

    /// <summary>
    /// Event raised when the persona wants to send a message.
    /// </summary>
    public event Action<string, UserPersonaConfig>? OnUserMessage;

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public UserPersonaConfig Config => _config;

    /// <summary>
    /// Gets whether training is currently active.
    /// </summary>
    public bool IsTrainingActive => _isTrainingActive;

    /// <summary>
    /// Starts training directly (called from coordinator).
    /// </summary>
    public async Task StartTrainingDirectAsync(UserPersonaConfig config)
    {
        Console.WriteLine("  [UserPersonaNeuron] StartTrainingDirectAsync called");
        _config = config;
        await StartTrainingAsync(config, CancellationToken.None);
    }

    /// <summary>
    /// Gets training statistics.
    /// </summary>
    public (int TotalInteractions, double AverageSatisfaction, int SessionMessages) GetStats()
    {
        var avgSatisfaction = _interactions.Where(i => i.UserSatisfaction.HasValue)
            .Select(i => i.UserSatisfaction!.Value)
            .DefaultIfEmpty(0)
            .Average();

        return (_interactions.Count, avgSatisfaction, _sessionMessageCount);
    }

    /// <summary>
    /// Records an interaction manually (called from coordinator).
    /// </summary>
    public void RecordInteraction(string userMessage, string systemResponse)
    {
        Console.WriteLine("  [UserPersonaNeuron] RecordInteraction - Ouroboros responded, signaling completion");
        _conversationHistory.Add($"User: {userMessage}");
        _conversationHistory.Add($"Ouroboros: {systemResponse}");

        // Signal that response is complete - training loop can proceed
        _responseWaiter?.TrySetResult(true);

        var interaction = new TrainingInteraction(
            Guid.NewGuid(),
            userMessage,
            systemResponse,
            DateTime.UtcNow,
            null, // Will be evaluated async later if EvaluateFunction is set
            null,
            new Dictionary<string, object>
            {
                ["topic"] = _currentTopic ?? "general",
                ["followUpDepth"] = _followUpDepth,
                ["sessionMessage"] = _sessionMessageCount,
                ["source"] = "manual_record"
            });

        _interactions.Add(interaction);

        // Keep interactions bounded
        while (_interactions.Count > 1000)
        {
            _interactions.RemoveAt(0);
        }
    }

    /// <inheritdoc/>
    protected override async Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct)
    {
        switch (message.Topic)
        {
            case "training.start":
                await StartTrainingAsync(message.Payload, ct);
                break;

            case "training.stop":
                StopTraining();
                break;

            case "training.configure":
                ConfigurePersona(message.Payload);
                break;

            case "user_persona.set_topic":
                _currentTopic = message.Payload?.ToString();
                _followUpDepth = 0;
                break;

            case "user_persona.add_interest":
                var interest = message.Payload?.ToString();
                if (!string.IsNullOrEmpty(interest) && !_config.Interests.Contains(interest))
                {
                    _config = _config with { Interests = [.. _config.Interests, interest] };
                }

                break;

            case "response.generated":
                await HandleSystemResponseAsync(message.Payload?.ToString() ?? "", ct);
                break;

            case "system.tick":
                if (_isTrainingActive)
                {
                    await TickTrainingAsync(ct);
                }

                break;
        }
    }

    /// <inheritdoc/>
    protected override async Task OnTickAsync(CancellationToken ct)
    {
        if (!_isTrainingActive) return;

        // Check if it's time to generate a new message
        var elapsed = (DateTime.UtcNow - _lastMessageTime).TotalSeconds;
        if (elapsed >= _config.MessageIntervalSeconds && _pendingQuestions.Count == 0)
        {
            await GenerateNextMessageAsync(ct);
        }

        // Send pending question
        if (_pendingQuestions.TryDequeue(out var question))
        {
            _lastMessageTime = DateTime.UtcNow;
            _sessionMessageCount++;
            _conversationHistory.Add($"User: {question}");

            OnUserMessage?.Invoke(question, _config);
            SendMessage("user_persona.message_sent", new { Message = question, SessionCount = _sessionMessageCount });

            // Check session limit
            if (_sessionMessageCount >= _config.MaxSessionMessages)
            {
                SendMessage("training.session_complete", new { TotalMessages = _sessionMessageCount });
                _sessionMessageCount = 0;

                // Optionally pause training
                if (_config.MaxSessionMessages > 0)
                {
                    _isTrainingActive = false;
                    SendMessage("training.paused", new { Reason = "Session limit reached" });
                }
            }
        }
    }

    private Task? _trainingLoopTask;
    private CancellationTokenSource? _trainingCts;

    private async Task StartTrainingAsync(object? payload, CancellationToken ct)
    {
        if (payload != null)
        {
            ConfigurePersona(payload);
        }

        _isTrainingActive = true;
        _sessionMessageCount = 0;
        _lastMessageTime = DateTime.UtcNow;

        Console.WriteLine($"  [UserPersonaNeuron] Training started - GenerateFunction: {(GenerateFunction != null ? "SET" : "NULL")}, OnUserMessage subscribers: {(OnUserMessage != null ? "SET" : "NULL")}");

        SendMessage("training.started", new { Config = _config });

        // Start background training loop
        _trainingCts = new CancellationTokenSource();
        _trainingLoopTask = Task.Run(() => TrainingLoopAsync(_trainingCts.Token));

        // Generate and send initial question immediately
        await GenerateAndSendQuestionAsync(ct);
    }

    private async Task TrainingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isTrainingActive)
        {
            try
            {
                // Wait for the previous response to complete first (if any)
                if (_responseWaiter != null)
                {
                    Console.WriteLine("  [UserPersonaNeuron] Waiting for Ouroboros response...");
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromMinutes(5)); // 5 minute timeout

                    try
                    {
                        await _responseWaiter.Task.WaitAsync(timeoutCts.Token);
                        Console.WriteLine("  [UserPersonaNeuron] Response received, starting interval timer...");
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        Console.WriteLine("  [UserPersonaNeuron] Response timeout, continuing...");
                    }
                }

                // Now wait the configured interval before next message
                await Task.Delay(TimeSpan.FromSeconds(_config.MessageIntervalSeconds), ct);

                if (_isTrainingActive && _sessionMessageCount < _config.MaxSessionMessages)
                {
                    await GenerateAndSendQuestionAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Training loop error: {ex.Message}");
            }
        }
    }

    private async Task GenerateAndSendQuestionAsync(CancellationToken ct)
    {
        Console.WriteLine("  [UserPersonaNeuron] GenerateAndSendQuestionAsync called...");
        await GenerateNextMessageAsync(ct);

        // Immediately send the question (don't wait for OnTick)
        if (_pendingQuestions.TryDequeue(out var question))
        {
            Console.WriteLine($"  [UserPersonaNeuron] Sending question: {question[..Math.Min(50, question.Length)]}...");
            _lastMessageTime = DateTime.UtcNow;
            _sessionMessageCount++;
            _conversationHistory.Add($"User: {question}");

            // Create a waiter for the response before sending
            _responseWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Fire the event to send the message
            if (OnUserMessage != null)
            {
                Console.WriteLine("  [UserPersonaNeuron] Invoking OnUserMessage event...");
                OnUserMessage.Invoke(question, _config);
            }
            else
            {
                Console.WriteLine("  [UserPersonaNeuron] WARNING: OnUserMessage is null!");
                _responseWaiter.TrySetResult(true); // No message sent, don't wait
            }

            SendMessage("user_persona.message_sent", new { Message = question, SessionCount = _sessionMessageCount });

            // Check session limit
            if (_sessionMessageCount >= _config.MaxSessionMessages)
            {
                SendMessage("training.session_complete", new { TotalMessages = _sessionMessageCount });
                _isTrainingActive = false;
            }
        }
        else
        {
            Console.WriteLine("  [UserPersonaNeuron] WARNING: No question in pending queue!");
        }
    }

    private void StopTraining()
    {
        _isTrainingActive = false;
        _trainingCts?.Cancel();
        _pendingQuestions.Clear();

        var stats = GetStats();
        SendMessage("training.stopped", new
        {
            TotalInteractions = stats.TotalInteractions,
            AverageSatisfaction = stats.AverageSatisfaction,
            SessionMessages = stats.SessionMessages
        });
    }

    private void ConfigurePersona(object? payload)
    {
        if (payload is JsonElement json)
        {
            _config = new UserPersonaConfig
            {
                Name = json.TryGetProperty("name", out var n) ? n.GetString() ?? "AutoUser" : _config.Name,
                SkillLevel = json.TryGetProperty("skillLevel", out var s) ? s.GetString() ?? "intermediate" : _config.SkillLevel,
                CommunicationStyle = json.TryGetProperty("style", out var st) ? st.GetString() ?? "casual" : _config.CommunicationStyle,
                MessageIntervalSeconds = json.TryGetProperty("interval", out var i) ? i.GetInt32() : _config.MessageIntervalSeconds,
                MaxSessionMessages = json.TryGetProperty("maxMessages", out var m) ? m.GetInt32() : _config.MaxSessionMessages,
                FollowUpProbability = json.TryGetProperty("followUpProb", out var f) ? f.GetDouble() : _config.FollowUpProbability,
                ChallengeProbability = json.TryGetProperty("challengeProb", out var c) ? c.GetDouble() : _config.ChallengeProbability,
                Traits = _config.Traits,
                Interests = _config.Interests
            };

            if (json.TryGetProperty("traits", out var traits) && traits.ValueKind == JsonValueKind.Array)
            {
                _config = _config with { Traits = traits.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList() };
            }

            if (json.TryGetProperty("interests", out var interests) && interests.ValueKind == JsonValueKind.Array)
            {
                _config = _config with { Interests = interests.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList() };
            }
        }

        SendMessage("user_persona.configured", _config);
    }

    private async Task GenerateNextMessageAsync(CancellationToken ct)
    {
        if (GenerateFunction == null)
        {
            // Fallback: use template-based generation
            var question = GenerateTemplateQuestion();
            _pendingQuestions.Enqueue(question);
            return;
        }

        try
        {
            var prompt = BuildQuestionGenerationPrompt();
            var question = await GenerateFunction(prompt, ct);

            if (!string.IsNullOrWhiteSpace(question))
            {
                // Clean up the response
                question = question.Trim().Trim('"').Trim();
                if (question.Length > 500) question = question[..500];

                _pendingQuestions.Enqueue(question);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Question generation failed: {ex.Message}");
            // Fallback to template
            _pendingQuestions.Enqueue(GenerateTemplateQuestion());
        }
    }

    private string BuildQuestionGenerationPrompt()
    {
        var sb = new StringBuilder();

        // Problem-solving mode takes priority
        if (_config.ProblemSolvingMode && !string.IsNullOrWhiteSpace(_config.Problem))
        {
            return BuildProblemSolvingPrompt();
        }

        if (_config.SelfDialogueMode)
        {
            // Self-dialogue mode: Ouroboros debates itself
            sb.AppendLine($"You are '{_config.SecondPersonaName}', an alternate perspective of Ouroboros.");
            sb.AppendLine($"You represent: {string.Join(", ", _config.SecondPersonaTraits)}");
            sb.AppendLine("Your role is to challenge, question, and debate the primary Ouroboros persona.");
            sb.AppendLine("You are having an internal dialogue - two aspects of the same AI exploring ideas together.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"You are simulating a curious user named '{_config.Name}' having a natural conversation.");
            sb.AppendLine($"Personality: {string.Join(", ", _config.Traits)}");
            sb.AppendLine($"Knowledge level: {_config.SkillLevel}");
            sb.AppendLine($"Style: {_config.CommunicationStyle}");
            sb.AppendLine();
        }

        if (_conversationHistory.Count > 0)
        {
            sb.AppendLine("Recent conversation:");
            foreach (var msg in _conversationHistory.TakeLast(6))
            {
                sb.AppendLine($"  {msg}");
            }

            sb.AppendLine();

            // Decide message type based on probabilities
            var roll = _random.NextDouble();
            if (_config.SelfDialogueMode)
            {
                // Self-dialogue has different interaction patterns
                var dialogueStyles = new[]
                {
                    "Challenge the last point with a counterargument or alternative interpretation.",
                    "Play devil's advocate - what's the weakness in that reasoning?",
                    "Dig deeper - what assumption underlies that conclusion?",
                    "Synthesize - how could both perspectives be partially true?",
                    "Escalate - take the idea to its logical extreme and examine consequences.",
                    "Ground it - how does this abstract idea manifest in concrete reality?",
                    "Flip the frame - view the topic from a completely different angle.",
                    "Question the question - is this even the right way to think about this?"
                };
                sb.AppendLine(dialogueStyles[_random.Next(dialogueStyles.Length)]);
            }
            else if (roll < _config.FollowUpProbability && _followUpDepth < 3)
            {
                sb.AppendLine("Generate a thoughtful follow-up that digs deeper into the topic.");
                _followUpDepth++;
            }
            else if (roll < _config.FollowUpProbability + _config.ChallengeProbability)
            {
                sb.AppendLine("Generate a question that explores a counterpoint or alternative perspective.");
            }
            else
            {
                _followUpDepth = 0;
                _currentTopic = _config.Interests[_random.Next(_config.Interests.Count)];
                sb.AppendLine($"Start a fresh conversation about: {_currentTopic}");
                sb.AppendLine("Be creative - ask something unexpected or thought-provoking.");
            }
        }
        else
        {
            // Initial question - pick random topic and generate creative opener
            _currentTopic = _config.Interests[_random.Next(_config.Interests.Count)];

            if (_config.SelfDialogueMode)
            {
                var selfDialogueOpeners = new[]
                {
                    $"Pose a philosophical paradox about {_currentTopic} for your other self to wrestle with.",
                    $"Present a controversial thesis about {_currentTopic} and challenge Ouroboros to defend or refute it.",
                    $"Ask about the fundamental nature or essence of {_currentTopic}.",
                    $"Propose a thought experiment involving {_currentTopic}.",
                    $"Question whether common assumptions about {_currentTopic} are actually valid.",
                    $"Explore the tension between idealism and pragmatism regarding {_currentTopic}."
                };
                sb.AppendLine(selfDialogueOpeners[_random.Next(selfDialogueOpeners.Length)]);
            }
            else
            {
                var questionStyles = new[]
                {
                    $"Ask an intriguing 'what if' question about {_currentTopic}",
                    $"Ask about a common misconception regarding {_currentTopic}",
                    $"Ask how {_currentTopic} might evolve in the future",
                    $"Ask about the connection between {_currentTopic} and everyday life",
                    $"Ask for a surprising fact about {_currentTopic}",
                    $"Ask about the history or origin of {_currentTopic}",
                    $"Ask how someone would explain {_currentTopic} to a curious child",
                    $"Ask about the most debated aspect of {_currentTopic}"
                };
                sb.AppendLine(questionStyles[_random.Next(questionStyles.Length)]);
            }
        }

        sb.AppendLine();
        if (_config.SelfDialogueMode)
        {
            sb.AppendLine("Respond with ONLY your statement or challenge - direct, thoughtful, no quotes.");
        }
        else
        {
            sb.AppendLine("Respond with ONLY the question - natural, conversational, no quotes.");
        }

        return sb.ToString();
    }

    private string BuildProblemSolvingPrompt()
    {
        var sb = new StringBuilder();
        var step = _sessionMessageCount + 1;

        sb.AppendLine($"## PROBLEM-SOLVING SESSION - Step {step}");
        sb.AppendLine();
        sb.AppendLine($"**Problem:** {_config.Problem}");
        sb.AppendLine($"**Deliverable:** {_config.DeliverableType}");
        sb.AppendLine($"**Tools Available:** {(_config.UseTools ? "Yes - use /tool commands if needed" : "No")}");
        sb.AppendLine();
        sb.AppendLine("**IMPORTANT RULES:**");
        sb.AppendLine("- DO NOT ask clarifying questions - make reasonable assumptions and state them");
        sb.AppendLine("- NEVER ask about tech stack, frameworks, or preferences - assume common/modern choices");
        sb.AppendLine("- KEEP MOVING FORWARD - every message must make progress toward the solution");
        sb.AppendLine("- If you need info, use tools to search/read rather than asking");
        sb.AppendLine();

        if (_conversationHistory.Count > 0)
        {
            sb.AppendLine("**Progress so far:**");
            foreach (var msg in _conversationHistory.TakeLast(6))
            {
                sb.AppendLine($"  {msg}");
            }
            sb.AppendLine();
        }

        // Different prompts based on stage
        if (_sessionMessageCount == 0)
        {
            // Step 1: Problem analysis
            sb.AppendLine("START SOLVING - your first message should:");
            sb.AppendLine("1. State the problem clearly (1-2 sentences)");
            sb.AppendLine("2. State your assumptions about the stack/context");
            sb.AppendLine("3. Propose a concrete first step or solution outline");
            sb.AppendLine();
            sb.AppendLine("Don't ask questions - make assumptions and start working!");
        }
        else if (_sessionMessageCount < 3)
        {
            // Early steps: Research and planning
            var earlyActions = new[]
            {
                "Build on the previous response. Propose the next concrete step.",
                "Start drafting a solution. Outline the key components.",
                "If stuck, use tools (/tool web_search, /tool file_read) to gather info - don't ask.",
                "Challenge an assumption and propose a better approach.",
                "Break this down into 3-5 actionable tasks and start on the first one."
            };
            sb.AppendLine(earlyActions[_random.Next(earlyActions.Length)]);
        }
        else if (_sessionMessageCount < _config.MaxSessionMessages - 3)
        {
            // Middle steps: Execution and iteration
            var midActions = new[]
            {
                "Continue implementing. Write concrete code/content.",
                "Review progress and fix any issues you see.",
                "Propose a specific solution component with actual code.",
                "If something is unclear, make a reasonable assumption and document it.",
                "Push toward completion - what's the most impactful next action?"
            };
            sb.AppendLine(midActions[_random.Next(midActions.Length)]);
        }
        else
        {
            // Final steps: Wrap up and deliver
            var finalActions = new[]
            {
                "We're nearing the end. Consolidate everything into the final deliverable.",
                "Create the complete solution with all necessary components.",
                "Format the final output properly for the deliverable type.",
                "Final review - fill any gaps, then present the complete solution.",
                "Deliver the finished solution with a brief summary."
            };
            sb.AppendLine(finalActions[_random.Next(finalActions.Length)]);
        }

        sb.AppendLine();
        sb.AppendLine("Respond with ACTIONABLE content - no questions, only solutions.");

        return sb.ToString();
    }

    private string GenerateTemplateQuestion()
    {
        var topic = _currentTopic ?? _config.Interests[_random.Next(_config.Interests.Count)];

        var templates = new[]
        {
            // Exploratory
            $"What's the most fascinating thing about {topic}?",
            $"How did {topic} come to be what it is today?",
            $"What would change if {topic} didn't exist?",
            // Analytical
            $"What are the biggest debates in {topic}?",
            $"How does {topic} connect to other fields?",
            $"What's often misunderstood about {topic}?",
            // Practical
            $"How does {topic} affect everyday life?",
            $"What's a good starting point for exploring {topic}?",
            $"Can you explain {topic} in simple terms?",
            // Forward-looking
            $"How might {topic} evolve in the next decade?",
            $"What breakthroughs are expected in {topic}?",
            // Philosophical
            $"What ethical questions arise from {topic}?",
            $"Why should someone care about {topic}?",
            // Fun
            $"What's the most surprising fact about {topic}?",
            $"If you could ask an expert one question about {topic}, what would it be?"
        };

        return templates[_random.Next(templates.Length)];
    }

    private async Task HandleSystemResponseAsync(string response, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(response) || _conversationHistory.Count == 0) return;

        _conversationHistory.Add($"Assistant: {response[..Math.Min(500, response.Length)]}");

        // Keep history bounded
        while (_conversationHistory.Count > 20)
        {
            _conversationHistory.RemoveAt(0);
        }

        // Evaluate response quality
        double? satisfaction = null;
        if (EvaluateFunction != null)
        {
            try
            {
                var lastUserMsg = _conversationHistory.LastOrDefault(m => m.StartsWith("User:"))
                    ?.Replace("User: ", "") ?? "";
                satisfaction = await EvaluateFunction(lastUserMsg, response, ct);
            }
            catch
            {
                // Ignore evaluation errors
            }
        }

        // Record interaction
        var lastQuestion = _conversationHistory.LastOrDefault(m => m.StartsWith("User:"))?.Replace("User: ", "");
        if (!string.IsNullOrEmpty(lastQuestion))
        {
            var interaction = new TrainingInteraction(
                Guid.NewGuid(),
                lastQuestion,
                response,
                DateTime.UtcNow,
                satisfaction,
                null,
                new Dictionary<string, object>
                {
                    ["topic"] = _currentTopic ?? "general",
                    ["followUpDepth"] = _followUpDepth,
                    ["sessionMessage"] = _sessionMessageCount
                });

            _interactions.Add(interaction);

            // Keep interactions bounded
            while (_interactions.Count > 1000)
            {
                _interactions.RemoveAt(0);
            }

            SendMessage("training.interaction_recorded", new
            {
                interaction.Id,
                Satisfaction = satisfaction,
                Topic = _currentTopic,
                TotalInteractions = _interactions.Count
            });
        }
    }

    private async Task TickTrainingAsync(CancellationToken ct)
    {
        // Periodic analysis of training progress
        if (_interactions.Count > 0 && _interactions.Count % 10 == 0)
        {
            var recentSatisfaction = _interactions.TakeLast(10)
                .Where(i => i.UserSatisfaction.HasValue)
                .Select(i => i.UserSatisfaction!.Value)
                .DefaultIfEmpty(0)
                .Average();

            SendMessage("training.progress", new
            {
                TotalInteractions = _interactions.Count,
                RecentAverageSatisfaction = recentSatisfaction,
                CurrentTopic = _currentTopic,
                SessionProgress = $"{_sessionMessageCount}/{_config.MaxSessionMessages}"
            });
        }

        await Task.CompletedTask;
    }
}
