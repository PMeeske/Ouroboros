// <copyright file="HyperonMeTTaEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Hyperon;

using System.Collections.Concurrent;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Hyperon.Parsing;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Native C# Hyperon-based MeTTa engine implementation.
/// Uses the in-process AtomSpace and Interpreter for high-performance symbolic reasoning.
/// </summary>
public sealed class HyperonMeTTaEngine : IMeTTaEngine
{
    private readonly AtomSpace _space;
    private readonly Interpreter _interpreter;
    private readonly SExpressionParser _parser;
    private readonly GroundedRegistry _groundedOps;
    private readonly ConcurrentDictionary<string, Atom> _namedAtoms = new();
    private bool _disposed;

    /// <summary>
    /// Gets the underlying AtomSpace for direct access.
    /// </summary>
    public IAtomSpace AtomSpace => _space;

    /// <summary>
    /// Gets the interpreter for direct evaluation.
    /// </summary>
    public Interpreter Interpreter => _interpreter;

    /// <summary>
    /// Gets the parser for S-expression parsing.
    /// </summary>
    public SExpressionParser Parser => _parser;

    /// <summary>
    /// Event raised when atoms are added to the space.
    /// </summary>
    public event Action<Atom>? AtomAdded;

    /// <summary>
    /// Event raised when a query is evaluated.
    /// </summary>
    public event Action<string, IReadOnlyList<Atom>>? QueryEvaluated;

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonMeTTaEngine"/> class.
    /// </summary>
    /// <param name="groundedOps">Optional custom grounded operations.</param>
    public HyperonMeTTaEngine(GroundedRegistry? groundedOps = null)
    {
        _groundedOps = groundedOps ?? CreateOuroborosGroundedOps();
        _space = new AtomSpace();
        _interpreter = new Interpreter(_space, _groundedOps);
        _parser = new SExpressionParser();

        // Initialize with core Ouroboros atoms
        InitializeCoreAtoms();
    }

    /// <summary>
    /// Creates a new engine from an existing AtomSpace.
    /// </summary>
    /// <param name="space">The atom space to use.</param>
    /// <param name="groundedOps">Optional custom grounded operations.</param>
    /// <returns>A new HyperonMeTTaEngine.</returns>
    public static HyperonMeTTaEngine FromAtomSpace(AtomSpace space, GroundedRegistry? groundedOps = null)
    {
        var engine = new HyperonMeTTaEngine(groundedOps);
        foreach (var atom in space.All())
        {
            engine._space.Add(atom);
        }

        return engine;
    }

    /// <inheritdoc/>
    public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<string, string>.Failure("Engine disposed"));

        try
        {
            var parseResult = _parser.Parse(query);
            if (!parseResult.IsSuccess)
            {
                return Task.FromResult(Result<string, string>.Failure($"Parse error: {parseResult.Error}"));
            }

            var results = _interpreter.Evaluate(parseResult.Value).ToList();

            QueryEvaluated?.Invoke(query, results);

            if (results.Count == 0)
            {
                return Task.FromResult(Result<string, string>.Success("()"));
            }

            var resultStr = results.Count == 1
                ? results[0].ToSExpr()
                : $"({string.Join(" ", results.Select(r => r.ToSExpr()))})";

            return Task.FromResult(Result<string, string>.Success(resultStr));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Query failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<Unit, string>.Failure("Engine disposed"));

        try
        {
            var parseResult = _parser.Parse(fact);
            if (!parseResult.IsSuccess)
            {
                return Task.FromResult(Result<Unit, string>.Failure($"Parse error: {parseResult.Error}"));
            }

            _space.Add(parseResult.Value);
            AtomAdded?.Invoke(parseResult.Value);

            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<Unit, string>.Failure($"Failed to add fact: {ex.Message}"));
        }
    }

    /// <summary>
    /// Adds an atom directly (without parsing).
    /// </summary>
    /// <param name="atom">The atom to add.</param>
    /// <returns>True if newly added.</returns>
    public bool AddAtom(Atom atom)
    {
        var added = _space.Add(atom);
        if (added)
        {
            AtomAdded?.Invoke(atom);
        }

        return added;
    }

    /// <summary>
    /// Adds a named atom for later reference.
    /// </summary>
    /// <param name="name">The name to bind.</param>
    /// <param name="atom">The atom to bind.</param>
    public void BindAtom(string name, Atom atom)
    {
        _namedAtoms[name] = atom;
        AddAtom(atom);
    }

    /// <summary>
    /// Gets a named atom by its binding name.
    /// </summary>
    /// <param name="name">The binding name.</param>
    /// <returns>The atom if found.</returns>
    public Option<Atom> GetBoundAtom(string name)
    {
        return _namedAtoms.TryGetValue(name, out var atom)
            ? Option<Atom>.Some(atom)
            : Option<Atom>.None();
    }

    /// <inheritdoc/>
    public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<string, string>.Failure("Engine disposed"));

        try
        {
            var parseResult = _parser.Parse(rule);
            if (!parseResult.IsSuccess)
            {
                return Task.FromResult(Result<string, string>.Failure($"Parse error: {parseResult.Error}"));
            }

            // Add the rule to the space
            _space.Add(parseResult.Value);
            AtomAdded?.Invoke(parseResult.Value);

            // If it's an implies rule, try to trigger forward chaining
            if (parseResult.Value is Expression expr &&
                expr.Children.Count >= 3 &&
                expr.Children[0] is Symbol sym &&
                sym.Name == "implies")
            {
                var condition = expr.Children[1];
                var conclusion = expr.Children[2];

                // Find all matches for the condition
                var matches = _space.Query(condition).ToList();
                var derived = new List<Atom>();

                foreach (var (_, bindings) in matches)
                {
                    var derivedAtom = bindings.Apply(conclusion);
                    if (_space.Add(derivedAtom))
                    {
                        derived.Add(derivedAtom);
                        AtomAdded?.Invoke(derivedAtom);
                    }
                }

                if (derived.Count > 0)
                {
                    return Task.FromResult(Result<string, string>.Success(
                        $"Derived {derived.Count} new facts: {string.Join(", ", derived.Select(d => d.ToSExpr()))}"));
                }
            }

            return Task.FromResult(Result<string, string>.Success("Rule added"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Failed to apply rule: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<bool, string>.Failure("Engine disposed"));

        try
        {
            var parseResult = _parser.Parse(plan);
            if (!parseResult.IsSuccess)
            {
                return Task.FromResult(Result<bool, string>.Failure($"Parse error: {parseResult.Error}"));
            }

            // Wrap plan in a verification query
            var verifyExpr = Atom.Expr(Atom.Sym("verify-plan"), parseResult.Value);
            var results = _interpreter.Evaluate(verifyExpr).ToList();

            // Check if verification succeeded
            bool verified = results.Any(r =>
                (r is Symbol s && (s.Name == "True" || s.Name == "verified")) ||
                (r is Expression e && e.Children.Count > 0 && e.Children[0] is Symbol vs && vs.Name == "verified"));

            return Task.FromResult(Result<bool, string>.Success(verified));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<bool, string>.Failure($"Verification failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<Unit, string>.Failure("Engine disposed"));

        // Clear all atoms by creating new space (AtomSpace doesn't have Clear method)
        // We'll remove atoms one by one
        var allAtoms = _space.All().ToList();
        foreach (var atom in allAtoms)
        {
            _space.Remove(atom);
        }

        _namedAtoms.Clear();

        // Re-initialize core atoms
        InitializeCoreAtoms();

        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    /// <summary>
    /// Evaluates a query and returns results with their bindings.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of (result atom, bindings) pairs.</returns>
    public Task<Result<IReadOnlyList<(Atom Result, Substitution Bindings)>, string>> EvaluateWithBindingsAsync(
        string query, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Failure("Engine disposed"));

        try
        {
            var parseResult = _parser.Parse(query);
            if (!parseResult.IsSuccess)
            {
                return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Failure($"Parse error: {parseResult.Error}"));
            }

            var results = _interpreter.EvaluateWithBindings(parseResult.Value).ToList();
            return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Success(results));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Failure($"Evaluation failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Queries the space for pattern matches.
    /// </summary>
    /// <param name="pattern">The pattern to match.</param>
    /// <returns>List of matching atoms with their bindings.</returns>
    public IReadOnlyList<(Atom Atom, Substitution Bindings)> Query(Atom pattern)
    {
        return _space.Query(pattern).ToList();
    }

    /// <summary>
    /// Gets all atoms in the space.
    /// </summary>
    /// <returns>All atoms.</returns>
    public IEnumerable<Atom> GetAllAtoms() => _space.All();

    /// <summary>
    /// Gets the count of atoms in the space.
    /// </summary>
    public int AtomCount => _space.Count;

    /// <summary>
    /// Adds multiple atoms from MeTTa source code.
    /// </summary>
    /// <param name="mettaSource">Multi-line MeTTa source code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of atoms successfully added.</returns>
    public async Task<int> LoadMeTTaSourceAsync(string mettaSource, CancellationToken ct = default)
    {
        int added = 0;
        var lines = mettaSource.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                continue;

            var result = await AddFactAsync(trimmed, ct);
            if (result.IsSuccess)
                added++;
        }

        return added;
    }

    /// <summary>
    /// Exports all atoms as MeTTa source code.
    /// </summary>
    /// <returns>MeTTa source code string.</returns>
    public string ExportToMeTTa()
    {
        return string.Join("\n", _space.All().Select(a => a.ToSExpr()));
    }

    private void InitializeCoreAtoms()
    {
        // Core Ouroboros type atoms
        AddAtom(Atom.Sym("Self"));
        AddAtom(Atom.Sym("Consciousness"));
        AddAtom(Atom.Sym("Thought"));
        AddAtom(Atom.Sym("Emotion"));
        AddAtom(Atom.Sym("Memory"));
        AddAtom(Atom.Sym("Intention"));
        AddAtom(Atom.Sym("Tool"));
        AddAtom(Atom.Sym("Skill"));

        // Self-reference atom
        AddAtom(Atom.Expr(Atom.Sym("is-a"), Atom.Sym("Ouroboros"), Atom.Sym("Self")));

        // Basic inference rules
        AddAtom(_parser.Parse("(implies (is-a $x Self) (has-consciousness $x))").Value);
        AddAtom(_parser.Parse("(implies (has-consciousness $x) (can-think $x))").Value);
        AddAtom(_parser.Parse("(implies (can-think $x) (can-introspect $x))").Value);

        // Plan verification rule
        AddAtom(_parser.Parse("(= (verify-plan (plan $steps)) (check-steps $steps))").Value);
    }

    private static GroundedRegistry CreateOuroborosGroundedOps()
    {
        var registry = GroundedRegistry.CreateStandard();

        // Add Ouroboros-specific grounded operations
        registry.Register("reflect", (space, expr) =>
        {
            // Self-reflection: query the space for self-related atoms
            var selfPattern = Atom.Expr(Atom.Var("_pred"), Atom.Sym("Ouroboros"), Atom.Var("_obj"));
            return space.Query(selfPattern).Select(r => r.Atom);
        });

        registry.Register("introspect", (space, expr) =>
        {
            // Return a summary of consciousness-related atoms
            var thoughtPattern = Atom.Expr(Atom.Var("_"), Atom.Sym("Thought"), Atom.Var("_"));
            return space.Query(thoughtPattern).Select(r => r.Atom);
        });

        registry.Register("check-steps", (space, expr) =>
        {
            // Verify plan steps are valid
            if (expr.Children.Count < 2)
            {
                return new[] { Atom.Sym("False") };
            }

            var steps = expr.Children[1];
            // Basic validation: steps must be an expression
            return steps is Expression ? new[] { Atom.Sym("verified") } : new[] { Atom.Sym("invalid") };
        });

        registry.Register("think", (space, expr) =>
        {
            // Generate a thought atom based on input
            if (expr.Children.Count < 2)
            {
                return Enumerable.Empty<Atom>();
            }

            var topic = expr.Children[1];
            var thoughtAtom = Atom.Expr(Atom.Sym("Thought"), topic, Atom.Sym(DateTime.UtcNow.Ticks.ToString()));
            space.Add(thoughtAtom);
            return new[] { thoughtAtom };
        });

        registry.Register("remember", (space, expr) =>
        {
            // Create a memory atom
            if (expr.Children.Count < 2)
            {
                return Enumerable.Empty<Atom>();
            }

            var content = expr.Children[1];
            var memoryAtom = Atom.Expr(Atom.Sym("Memory"), content, Atom.Sym(DateTime.UtcNow.ToString("O")));
            space.Add(memoryAtom);
            return new[] { memoryAtom };
        });

        registry.Register("intend", (space, expr) =>
        {
            // Create an intention atom
            if (expr.Children.Count < 2)
            {
                return Enumerable.Empty<Atom>();
            }

            var goal = expr.Children[1];
            var intentionAtom = Atom.Expr(Atom.Sym("Intention"), goal, Atom.Sym("pending"));
            space.Add(intentionAtom);
            return new[] { intentionAtom };
        });

        return registry;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _namedAtoms.Clear();
    }
}
