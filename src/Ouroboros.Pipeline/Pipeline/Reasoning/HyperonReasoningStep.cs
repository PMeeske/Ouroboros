// <copyright file="HyperonReasoningStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1101 // Prefix local calls with this

namespace Ouroboros.Pipeline.Reasoning;

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// A pipeline step that performs Hyperon-based symbolic reasoning.
/// </summary>
/// <remarks>
/// This step integrates native Hyperon AtomSpace reasoning into the
/// Ouroboros pipeline system, enabling:
/// - Symbolic pattern matching on pipeline state
/// - Inference using loaded knowledge bases
/// - Meta-cognitive reasoning about the pipeline itself
/// - Neuro-symbolic fusion with LLM outputs
/// </remarks>
public class HyperonReasoningStep : IDisposable
{
    private readonly HyperonMeTTaEngine _engine;
    private readonly HyperonFlowIntegration _flow;
    private readonly string _stepName;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonReasoningStep"/> class.
    /// </summary>
    /// <param name="stepName">Name of this reasoning step.</param>
    public HyperonReasoningStep(string stepName)
    {
        _stepName = stepName;
        _engine = new HyperonMeTTaEngine();
        _flow = new HyperonFlowIntegration(_engine);
    }

    /// <summary>
    /// Initializes a new instance with a shared engine.
    /// </summary>
    /// <param name="stepName">Name of this reasoning step.</param>
    /// <param name="engine">Shared Hyperon engine.</param>
    public HyperonReasoningStep(string stepName, HyperonMeTTaEngine engine)
    {
        _stepName = stepName;
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _flow = new HyperonFlowIntegration(_engine);
    }

    /// <summary>
    /// Gets the step name.
    /// </summary>
    public string Name => _stepName;

    /// <summary>
    /// Gets the underlying engine.
    /// </summary>
    public HyperonMeTTaEngine Engine => _engine;

    /// <summary>
    /// Gets the flow integration.
    /// </summary>
    public HyperonFlowIntegration Flow => _flow;

    /// <summary>
    /// Creates a Kleisli arrow for this reasoning step.
    /// </summary>
    /// <typeparam name="TContext">The pipeline context type.</typeparam>
    /// <param name="reasoningLogic">The reasoning logic to apply.</param>
    /// <returns>A step function.</returns>
    public Func<TContext, Task<TContext>> CreateArrow<TContext>(
        Func<HyperonMeTTaEngine, TContext, Task<TContext>> reasoningLogic)
        where TContext : class
    {
        return async context =>
        {
            // Record step entry
            _engine.AddAtom(Atom.Expr(
                Atom.Sym("StepEntry"),
                Atom.Sym(_stepName),
                Atom.Sym(DateTime.UtcNow.Ticks.ToString())));

            try
            {
                var result = await reasoningLogic(_engine, context);

                // Record step success
                _engine.AddAtom(Atom.Expr(
                    Atom.Sym("StepSuccess"),
                    Atom.Sym(_stepName),
                    Atom.Sym(DateTime.UtcNow.Ticks.ToString())));

                return result;
            }
            catch (Exception ex)
            {
                // Record step failure
                _engine.AddAtom(Atom.Expr(
                    Atom.Sym("StepFailure"),
                    Atom.Sym(_stepName),
                    Atom.Sym(ex.Message)));
                throw;
            }
        };
    }

    /// <summary>
    /// Loads context into the AtomSpace for reasoning.
    /// </summary>
    /// <param name="contextName">Name of the context.</param>
    /// <param name="facts">Facts to load.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadContextAsync(
        string contextName,
        IEnumerable<string> facts,
        CancellationToken ct = default)
    {
        // Create context container
        _engine.AddAtom(Atom.Expr(
            Atom.Sym("Context"),
            Atom.Sym(contextName)));

        foreach (var fact in facts)
        {
            await _engine.AddFactAsync(fact, ct);
        }
    }

    /// <summary>
    /// Performs inference and returns conclusions.
    /// </summary>
    /// <param name="query">The query pattern.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Inference results.</returns>
    public async Task<IReadOnlyList<string>> InferAsync(
        string query,
        CancellationToken ct = default)
    {
        Result<string, string> result = await _engine.ExecuteQueryAsync(query, ct);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Value) || result.Value.Contains("Empty"))
            return Array.Empty<string>();

        // Parse results into list
        var conclusions = new List<string>();

        // Split by common delimiters
        var parts = result.Value.Split(new[] { '\n', ';', '[', ']', '(', ')' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part) && part.Length > 1)
            {
                conclusions.Add(part);
            }
        }

        return conclusions;
    }

    /// <summary>
    /// Applies a reasoning rule to the context.
    /// </summary>
    /// <param name="rule">The MeTTa rule.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        await _engine.ApplyRuleAsync(rule, ct);
    }

    /// <summary>
    /// Creates a reasoning flow for multi-step inference.
    /// </summary>
    /// <param name="flowName">Name of the flow.</param>
    /// <param name="description">Flow description.</param>
    /// <returns>A chainable HyperonFlow.</returns>
    public HyperonFlow CreateReasoningFlow(string flowName, string description)
    {
        return _flow.CreateFlow($"{_stepName}-{flowName}", description);
    }

    /// <summary>
    /// Executes a reasoning flow.
    /// </summary>
    /// <param name="flowName">Name of the flow.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExecuteFlowAsync(string flowName, CancellationToken ct = default)
    {
        await _flow.ExecuteFlowAsync($"{_stepName}-{flowName}", ct);
    }

    /// <summary>
    /// Gets the reasoning trace for debugging.
    /// </summary>
    /// <returns>List of step events.</returns>
    public async Task<IReadOnlyList<ReasoningTraceEntry>> GetTraceAsync(CancellationToken ct = default)
    {
        List<ReasoningTraceEntry> entries = new();

        // Query step entries
        Result<string, string> entryResult = await _engine.ExecuteQueryAsync(
            $"(match &self (StepEntry {_stepName} $time) $time)",
            ct);

        if (entryResult.IsSuccess && !string.IsNullOrWhiteSpace(entryResult.Value) && !entryResult.Value.Contains("Empty"))
        {
            entries.Add(new ReasoningTraceEntry
            {
                Event = "Entry",
                StepName = _stepName,
                Details = entryResult.Value,
            });
        }

        // Query step successes
        Result<string, string> successResult = await _engine.ExecuteQueryAsync(
            $"(match &self (StepSuccess {_stepName} $time) $time)",
            ct);

        if (successResult.IsSuccess && !string.IsNullOrWhiteSpace(successResult.Value) && !successResult.Value.Contains("Empty"))
        {
            entries.Add(new ReasoningTraceEntry
            {
                Event = "Success",
                StepName = _stepName,
                Details = successResult.Value,
            });
        }

        // Query step failures
        Result<string, string> failureResult = await _engine.ExecuteQueryAsync(
            $"(match &self (StepFailure {_stepName} $msg) $msg)",
            ct);

        if (failureResult.IsSuccess && !string.IsNullOrWhiteSpace(failureResult.Value) && !failureResult.Value.Contains("Empty"))
        {
            entries.Add(new ReasoningTraceEntry
            {
                Event = "Failure",
                StepName = _stepName,
                Details = failureResult.Value,
            });
        }

        return entries;
    }

    /// <summary>
    /// Exports the step's knowledge to MeTTa.
    /// </summary>
    /// <returns>MeTTa source.</returns>
    public string ExportKnowledge()
        => _engine.ExportToMeTTa();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flow.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}

/// <summary>
/// A trace entry from reasoning.
/// </summary>
public class ReasoningTraceEntry
{
    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public required string Event { get; set; }

    /// <summary>
    /// Gets or sets the step name.
    /// </summary>
    public required string StepName { get; set; }

    /// <summary>
    /// Gets or sets the details.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Extension methods for integrating Hyperon reasoning into pipelines.
/// </summary>
public static class HyperonReasoningExtensions
{
    /// <summary>
    /// Adds Hyperon reasoning to a pipeline context.
    /// </summary>
    /// <typeparam name="T">The context type.</typeparam>
    /// <param name="context">The pipeline context.</param>
    /// <param name="stepName">Name of the reasoning step.</param>
    /// <param name="reasoningAction">The reasoning to perform.</param>
    /// <returns>The modified context.</returns>
    public static async Task<T> WithHyperonReasoningAsync<T>(
        this T context,
        string stepName,
        Func<HyperonMeTTaEngine, T, Task<T>> reasoningAction)
        where T : class
    {
        using var step = new HyperonReasoningStep(stepName);
        var arrow = step.CreateArrow(reasoningAction);
        return await arrow(context);
    }

    /// <summary>
    /// Creates a reasoning step that enriches context with symbolic inference.
    /// </summary>
    /// <typeparam name="T">The context type.</typeparam>
    /// <param name="stepName">Name of the step.</param>
    /// <param name="knowledgeBase">MeTTa knowledge base to load.</param>
    /// <param name="queries">Queries to execute.</param>
    /// <param name="contextEnricher">Function to enrich context with results.</param>
    /// <returns>A step function.</returns>
    public static Func<T, Task<T>> CreateInferenceStep<T>(
        string stepName,
        string knowledgeBase,
        IEnumerable<string> queries,
        Func<T, IReadOnlyList<string>, T> contextEnricher)
        where T : class
    {
        return async context =>
        {
            using var step = new HyperonReasoningStep(stepName);

            // Load knowledge base
            await step.Engine.LoadMeTTaSourceAsync(knowledgeBase);

            // Execute queries and collect results
            var allResults = new List<string>();
            foreach (var query in queries)
            {
                var results = await step.InferAsync(query);
                allResults.AddRange(results);
            }

            // Enrich context
            return contextEnricher(context, allResults);
        };
    }

    /// <summary>
    /// Creates a pattern-matching step.
    /// </summary>
    /// <typeparam name="T">The context type.</typeparam>
    /// <param name="stepName">Name of the step.</param>
    /// <param name="pattern">Pattern to match.</param>
    /// <param name="onMatch">Action on match.</param>
    /// <param name="onNoMatch">Action on no match.</param>
    /// <returns>A step function.</returns>
    public static Func<T, Task<T>> CreatePatternStep<T>(
        string stepName,
        string pattern,
        Func<T, Substitution, T> onMatch,
        Func<T, T>? onNoMatch = null)
        where T : class
    {
        return async context =>
        {
            using HyperonReasoningStep step = new(stepName);

            Result<string, string> result = await step.Engine.ExecuteQueryAsync(
                $"(match &self {pattern} $result)");

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value) && !result.Value.Contains("Empty"))
            {
                // Create empty substitution - in real usage this would be parsed from result
                Substitution substitution = Substitution.Empty;
                return onMatch(context, substitution);
            }

            return onNoMatch?.Invoke(context) ?? context;
        };
    }
}
