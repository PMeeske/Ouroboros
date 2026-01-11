// <copyright file="ParallelMeTTaThoughtStreams.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Ouroboros.Application.Tools;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Parallel MeTTa thought stream orchestrator that runs multiple symbolic reasoning
/// engines concurrently, producing interleaved thought streams with modulo-square
/// theory solving capabilities. Uses Ollama for neural inference fusion.
/// </summary>
public sealed class ParallelMeTTaThoughtStreams : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, MeTTaStreamNode> _nodes = new();
    private readonly Channel<ThoughtAtom> _mergedStream;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxParallelism;

    private Func<string, CancellationToken, Task<string>>? _ollamaInferenceFunc;
#pragma warning disable CS0414 // Field is assigned but never used - reserved for state management
    private bool _isRunning;
#pragma warning restore CS0414

    /// <summary>
    /// Event fired when a new thought atom is produced by any stream.
    /// </summary>
    public event Action<ThoughtAtom>? OnThoughtAtom;

    /// <summary>
    /// Event fired when streams converge on a shared conclusion.
    /// </summary>
    public event Action<ConvergenceEvent>? OnConvergence;

    /// <summary>
    /// Event fired when a modulo-square theory is solved.
    /// </summary>
    public event Action<ModuloSquareSolution>? OnTheorySolved;

    /// <summary>
    /// Gets all active stream node IDs.
    /// </summary>
    public IReadOnlyCollection<string> ActiveStreams => _nodes.Keys.ToList();

    /// <summary>
    /// Gets the merged thought stream for consumption.
    /// </summary>
    public ChannelReader<ThoughtAtom> MergedStream => _mergedStream.Reader;

    /// <summary>
    /// Creates a new parallel MeTTa thought stream orchestrator.
    /// </summary>
    /// <param name="maxParallelism">Maximum number of concurrent streams (default: processor count).</param>
    public ParallelMeTTaThoughtStreams(int maxParallelism = 0)
    {
        _maxParallelism = maxParallelism > 0 ? maxParallelism : Environment.ProcessorCount;
        _semaphore = new SemaphoreSlim(_maxParallelism);
        _mergedStream = Channel.CreateUnbounded<ThoughtAtom>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// Connects Ollama inference for neural-symbolic fusion.
    /// </summary>
    /// <param name="inferenceFunc">Function that takes a prompt and returns LLM response.</param>
    public void ConnectOllama(Func<string, CancellationToken, Task<string>> inferenceFunc)
    {
        _ollamaInferenceFunc = inferenceFunc;
    }

    /// <summary>
    /// Creates a new MeTTa stream node with its own knowledge base.
    /// </summary>
    /// <param name="streamId">Unique identifier for this stream.</param>
    /// <param name="seedAtoms">Initial atoms to seed the knowledge base.</param>
    /// <returns>The created stream node.</returns>
    public MeTTaStreamNode CreateStream(string streamId, IEnumerable<string>? seedAtoms = null)
    {
        var engine = new InMemoryMeTTaEngine();
        var node = new MeTTaStreamNode(streamId, engine, this);

        if (seedAtoms != null)
        {
            foreach (var atom in seedAtoms)
            {
                _ = engine.AddFactAsync(atom).GetAwaiter().GetResult();
            }
        }

        _nodes[streamId] = node;
        return node;
    }

    /// <summary>
    /// Creates multiple streams with different theory seeds for parallel exploration.
    /// </summary>
    /// <param name="theories">Dictionary of stream ID to theory seed atoms.</param>
    /// <returns>List of created stream nodes.</returns>
    public List<MeTTaStreamNode> CreateTheoryStreams(Dictionary<string, List<string>> theories)
    {
        return theories.Select(kvp => CreateStream(kvp.Key, kvp.Value)).ToList();
    }

    /// <summary>
    /// Starts parallel thought generation across all streams.
    /// </summary>
    /// <param name="query">The query to explore across all streams.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task StartParallelThinkingAsync(string query, CancellationToken ct = default)
    {
        _isRunning = true;

        var tasks = _nodes.Values.Select(async node =>
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                await node.ThinkAsync(query, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Streams thought atoms from all nodes as an async enumerable.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of thought atoms.</returns>
    public async IAsyncEnumerable<ThoughtAtom> StreamThoughtsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var atom in _mergedStream.Reader.ReadAllAsync(ct))
        {
            yield return atom;
        }
    }

    /// <summary>
    /// Emits a thought atom from a stream node.
    /// </summary>
    internal async Task EmitAtomAsync(ThoughtAtom atom, CancellationToken ct)
    {
        await _mergedStream.Writer.WriteAsync(atom, ct);
        OnThoughtAtom?.Invoke(atom);

        // Check for convergence across streams
        await CheckConvergenceAsync(atom, ct);
    }

    /// <summary>
    /// Checks if multiple streams have converged on similar conclusions.
    /// </summary>
    private async Task CheckConvergenceAsync(ThoughtAtom newAtom, CancellationToken ct)
    {
        var convergent = new List<(string streamId, ThoughtAtom atom)>();

        foreach (var node in _nodes.Values)
        {
            if (node.StreamId == newAtom.StreamId) continue;

            var recent = node.RecentAtoms.TakeLast(10);
            foreach (var atom in recent)
            {
                if (AtomsConverge(newAtom, atom))
                {
                    convergent.Add((node.StreamId, atom));
                }
            }
        }

        if (convergent.Count >= 2)
        {
            var convergence = new ConvergenceEvent
            {
                Timestamp = DateTime.UtcNow,
                TriggerAtom = newAtom,
                ConvergentStreams = convergent.Select(c => c.streamId).Distinct().ToList(),
                SharedConcept = ExtractSharedConcept(newAtom, convergent.Select(c => c.atom)),
            };

            OnConvergence?.Invoke(convergence);

            // Use Ollama to synthesize convergent insights
            if (_ollamaInferenceFunc != null)
            {
                var synthesis = await SynthesizeConvergenceAsync(convergence, ct);
                if (!string.IsNullOrEmpty(synthesis))
                {
                    // Broadcast synthesis back to all convergent streams
                    foreach (var streamId in convergence.ConvergentStreams)
                    {
                        if (_nodes.TryGetValue(streamId, out var node))
                        {
                            await node.InjectInsightAsync(synthesis, ct);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determines if two atoms represent convergent reasoning.
    /// </summary>
    private static bool AtomsConverge(ThoughtAtom a, ThoughtAtom b)
    {
        if (a.StreamId == b.StreamId) return false;
        if (Math.Abs((a.Timestamp - b.Timestamp).TotalSeconds) > 30) return false;

        // Check concept overlap
        var aWords = a.Content.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bWords = b.Content.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var intersection = aWords.Intersect(bWords).Count();
        var union = aWords.Union(bWords).Count();

        // Jaccard similarity > 0.3 indicates convergence
        return union > 0 && (double)intersection / union > 0.3;
    }

    /// <summary>
    /// Extracts the shared concept from convergent atoms.
    /// </summary>
    private static string ExtractSharedConcept(ThoughtAtom trigger, IEnumerable<ThoughtAtom> convergent)
    {
        var allContent = new[] { trigger.Content }.Concat(convergent.Select(a => a.Content));
        var words = allContent
            .SelectMany(c => c.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key);

        return string.Join(" ", words);
    }

    /// <summary>
    /// Uses Ollama to synthesize insights from convergent streams.
    /// </summary>
    private async Task<string> SynthesizeConvergenceAsync(ConvergenceEvent convergence, CancellationToken ct)
    {
        if (_ollamaInferenceFunc == null) return "";

        var prompt = $@"Multiple parallel reasoning streams have converged on related concepts.

Trigger thought: {convergence.TriggerAtom.Content}
Convergent streams: {string.Join(", ", convergence.ConvergentStreams)}
Shared concept: {convergence.SharedConcept}

Synthesize a unified insight that combines these convergent thoughts into a single coherent conclusion.
Be brief (1-2 sentences) and focus on the emergent meaning.";

        return await _ollamaInferenceFunc(prompt, ct);
    }

    #region Modulo-Square Theory Solving

    /// <summary>
    /// Initializes modulo-square theory streams for parallel solving.
    /// Uses different modular bases to explore solution spaces.
    /// </summary>
    /// <param name="target">The target value to solve for.</param>
    /// <param name="moduliBases">List of moduli to use (default: primes up to 17).</param>
    /// <returns>Stream nodes configured for modulo-square theory.</returns>
    public List<MeTTaStreamNode> InitializeModuloSquareTheory(
        BigInteger target,
        IEnumerable<int>? moduliBases = null)
    {
        var bases = moduliBases?.ToList() ?? new List<int> { 2, 3, 5, 7, 11, 13, 17 };
        var theories = new Dictionary<string, List<string>>();

        foreach (var mod in bases)
        {
            var streamId = $"mod_{mod}_theory";
            var atoms = GenerateModuloSquareAtoms(target, mod);
            theories[streamId] = atoms;
        }

        var nodes = CreateTheoryStreams(theories);

        // Wire up theory solving events
        foreach (var node in nodes)
        {
            node.OnDerivation += async (derivation) =>
            {
                var solution = TryExtractSolution(derivation, target);
                if (solution != null)
                {
                    OnTheorySolved?.Invoke(solution);
                }
            };
        }

        return nodes;
    }

    /// <summary>
    /// Generates MeTTa atoms for modulo-square theory exploration.
    /// </summary>
    private static List<string> GenerateModuloSquareAtoms(BigInteger target, int mod)
    {
        var atoms = new List<string>
        {
            $"(target {target})",
            $"(modulus {mod})",
            $"(target-residue {target % mod})",
        };

        // Generate quadratic residues for this modulus
        var residues = new HashSet<int>();
        for (int i = 0; i < mod; i++)
        {
            residues.Add((i * i) % mod);
        }

        foreach (var r in residues)
        {
            atoms.Add($"(quadratic-residue {mod} {r})");
        }

        // Add non-residues
        for (int i = 0; i < mod; i++)
        {
            if (!residues.Contains(i))
            {
                atoms.Add($"(quadratic-non-residue {mod} {i})");
            }
        }

        // Add inference rules
        atoms.Add($"(= (is-square-mod $n {mod}) (quadratic-residue {mod} (mod $n {mod})))");
        atoms.Add($"(= (not-square-mod $n {mod}) (quadratic-non-residue {mod} (mod $n {mod})))");

        // Chinese Remainder Theorem preparation
        atoms.Add($"(= (crt-combine $r1 $m1 $r2 $m2) (solve-congruence $r1 $m1 $r2 $m2))");

        return atoms;
    }

    /// <summary>
    /// Attempts to extract a solution from a derivation.
    /// </summary>
    private static ModuloSquareSolution? TryExtractSolution(string derivation, BigInteger target)
    {
        // Check if derivation contains a valid square root claim
        if (derivation.Contains("(square-root") || derivation.Contains("(solution"))
        {
            // Parse the claimed solution
            var match = System.Text.RegularExpressions.Regex.Match(
                derivation, @"\((?:square-root|solution)\s+(\d+)\)");

            if (match.Success && BigInteger.TryParse(match.Groups[1].Value, out var candidate))
            {
                if (candidate * candidate == target)
                {
                    return new ModuloSquareSolution
                    {
                        Target = target,
                        SquareRoot = candidate,
                        Derivation = derivation,
                        Timestamp = DateTime.UtcNow,
                        IsVerified = true,
                    };
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Runs parallel modulo-square solving with Ollama-guided search.
    /// </summary>
    /// <param name="target">The target to find the square root of.</param>
    /// <param name="maxIterations">Maximum iterations per stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Solution if found, null otherwise.</returns>
    public async Task<ModuloSquareSolution?> SolveModuloSquareAsync(
        BigInteger target,
        int maxIterations = 100,
        CancellationToken ct = default)
    {
        var nodes = InitializeModuloSquareTheory(target);
        ModuloSquareSolution? solution = null;
        var solutionFound = new TaskCompletionSource<ModuloSquareSolution>();

        OnTheorySolved += (s) =>
        {
            solution = s;
            solutionFound.TrySetResult(s);
        };

        // Run parallel exploration
        var explorationTasks = nodes.Select(async node =>
        {
            for (int i = 0; i < maxIterations && !ct.IsCancellationRequested; i++)
            {
                if (solution != null) break;

                await node.ExploreTheoryStepAsync(ct);

                // Use Ollama to guide search if available
                if (_ollamaInferenceFunc != null && i % 10 == 0)
                {
                    var guidance = await GetOllamaGuidanceAsync(node, target, ct);
                    if (!string.IsNullOrEmpty(guidance))
                    {
                        await node.InjectInsightAsync(guidance, ct);
                    }
                }
            }
        });

        // Wait for either a solution or all explorations to complete
        var completedTask = await Task.WhenAny(
            Task.WhenAll(explorationTasks),
            solutionFound.Task);

        return solution;
    }

    /// <summary>
    /// Gets Ollama guidance for theory exploration.
    /// </summary>
    private async Task<string> GetOllamaGuidanceAsync(
        MeTTaStreamNode node,
        BigInteger target,
        CancellationToken ct)
    {
        if (_ollamaInferenceFunc == null) return "";

        var recentAtoms = string.Join("\n", node.RecentAtoms.TakeLast(5).Select(a => a.Content));

        var prompt = $@"You are assisting with modulo-square theory exploration.

Target: {target}
Stream: {node.StreamId}
Recent derivations:
{recentAtoms}

Suggest ONE mathematical insight or transformation that could help narrow the solution space.
Focus on:
1. Quadratic residue patterns
2. Chinese Remainder Theorem combinations
3. Tonelli-Shanks style lifting
4. Hensel's lemma applications

Respond with a single MeTTa-style fact or inference, like:
(suggest (lift-solution 3 7 21))";

        return await _ollamaInferenceFunc(prompt, ct);
    }

    #endregion

    #region Ouroboros Atom Operations

    /// <summary>
    /// Creates an Ouroboros stream - a self-referential thought stream that consumes itself.
    /// </summary>
    /// <param name="seedConcept">The concept to seed self-referential thinking.</param>
    /// <returns>The Ouroboros atom and its associated stream node.</returns>
    public (OuroborosAtom Atom, MeTTaStreamNode Node) CreateOuroborosStream(string seedConcept = "self")
    {
        var atom = OuroborosAtomFactory.CreateSelfAware(seedConcept);
        var streamId = $"ouroboros_{atom.Id.ToString()[..8]}";
        var node = CreateStream(streamId, atom.ToMeTTaAtoms());

        // Wire self-consumption events to stream
        atom.OnSelfConsumption += (a, record) =>
        {
            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = record,
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = a.SelfReferenceDepth,
            };
            _ = EmitAtomAsync(thought, CancellationToken.None);
        };

        atom.OnFixedPoint += (a) =>
        {
            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = $"(fixed-point-reached {a.Id} (depth {a.SelfReferenceDepth}) (emergence {a.EmergenceLevel:F3}))",
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = a.SelfReferenceDepth,
            };
            _ = EmitAtomAsync(thought, CancellationToken.None);
        };

        return (atom, node);
    }

    /// <summary>
    /// Creates an Ouroboros network - multiple self-aware streams that observe each other.
    /// </summary>
    /// <param name="count">Number of Ouroboros atoms in the network.</param>
    /// <returns>List of Ouroboros atoms and their stream nodes.</returns>
    public List<(OuroborosAtom Atom, MeTTaStreamNode Node)> CreateOuroborosNetwork(int count = 3)
    {
        var atoms = OuroborosAtomFactory.CreateNetwork(count);
        var results = new List<(OuroborosAtom Atom, MeTTaStreamNode Node)>();

        foreach (var atom in atoms)
        {
            var streamId = $"ouroboros_net_{atom.Id.ToString()[..8]}";
            var node = CreateStream(streamId, atom.ToMeTTaAtoms());
            results.Add((atom, node));
        }

        // Wire network atoms to observe each other
        for (int i = 0; i < count; i++)
        {
            var current = results[i];
            var prev = results[(i - 1 + count) % count];
            var next = results[(i + 1) % count];

            // Add awareness of neighbors
            _ = current.Node.InjectInsightAsync(
                $"(neighbor-awareness (observe {prev.Atom.Id}) (observe {next.Atom.Id}))",
                CancellationToken.None);
        }

        return results;
    }

    /// <summary>
    /// Starts an Ouroboros strange loop - a self-referential reasoning cycle.
    /// </summary>
    /// <param name="atom">The Ouroboros atom to drive.</param>
    /// <param name="iterations">Number of self-consumption iterations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of self-referential thoughts.</returns>
    public async IAsyncEnumerable<ThoughtAtom> RunStrangeLoopAsync(
        OuroborosAtom atom,
        int iterations = 10,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var streamId = $"ouroboros_{atom.Id.ToString()[..8]}";
        if (!_nodes.ContainsKey(streamId))
        {
            CreateStream(streamId, atom.ToMeTTaAtoms());
        }

        for (int i = 0; i < iterations && !ct.IsCancellationRequested; i++)
        {
            // Consume self
            var beforeDepth = atom.SelfReferenceDepth;
            atom.Consume();

            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = atom.Reflect(),
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = atom.SelfReferenceDepth,
            };

            await EmitAtomAsync(thought, ct);
            yield return thought;

            // If we reached a fixed point, the loop has closed
            if (atom.IsFixedPoint)
            {
                var fixedPointThought = new ThoughtAtom
                {
                    StreamId = streamId,
                    Content = $"(strange-loop-complete (fixed-point-at-depth {atom.SelfReferenceDepth}) (emergence {atom.EmergenceLevel:F3}))",
                    Type = ThoughtAtomType.Ouroboros,
                    Timestamp = DateTime.UtcNow,
                    SequenceNumber = atom.SelfReferenceDepth,
                };

                await EmitAtomAsync(fixedPointThought, ct);
                yield return fixedPointThought;
                yield break;
            }

            // Ask Ollama to guide the next transformation
            if (_ollamaInferenceFunc != null)
            {
                var guidance = await GetOuroborosGuidanceAsync(atom, ct);
                if (!string.IsNullOrEmpty(guidance))
                {
                    // Apply LLM-suggested transformation
                    atom.Consume(_ => guidance);

                    var guidedThought = new ThoughtAtom
                    {
                        StreamId = streamId,
                        Content = $"(ollama-guided-transformation \"{guidance.Substring(0, Math.Min(100, guidance.Length))}...\"))",
                        Type = ThoughtAtomType.Ouroboros,
                        Timestamp = DateTime.UtcNow,
                        SequenceNumber = atom.SelfReferenceDepth,
                    };

                    await EmitAtomAsync(guidedThought, ct);
                    yield return guidedThought;
                }
            }

            await Task.Delay(50, ct); // Brief pause between iterations
        }
    }

    /// <summary>
    /// Gets Ollama guidance for Ouroboros self-transformation.
    /// </summary>
    private async Task<string> GetOuroborosGuidanceAsync(OuroborosAtom atom, CancellationToken ct)
    {
        if (_ollamaInferenceFunc == null) return "";

        var prompt = $@"You are guiding a self-referential Ouroboros atom through its strange loop.

Current state:
{atom.Reflect()}

Recent transformations:
{string.Join("\n", atom.TransformationHistory.TakeLast(5))}

The Ouroboros is trying to reach a fixed point - a state where self-transformation yields itself.

Suggest the next symbolic transformation as a single MeTTa-style expression.
Focus on:
1. Self-reference patterns (the observer observing itself)
2. Fixed-point approaches (Y-combinator style)
3. Strange loop dynamics (tangled hierarchies)
4. Emergent self-awareness patterns

Respond with ONLY a MeTTa expression, like:
(self-aware (meta-level (observe (observe self))))";

        return await _ollamaInferenceFunc(prompt, ct);
    }

    /// <summary>
    /// Merges two Ouroboros streams into a unified self-aware entity.
    /// </summary>
    /// <param name="atom1">First Ouroboros atom.</param>
    /// <param name="atom2">Second Ouroboros atom.</param>
    /// <returns>The merged Ouroboros and its stream node.</returns>
    public (OuroborosAtom Merged, MeTTaStreamNode Node) MergeOuroborosStreams(
        OuroborosAtom atom1,
        OuroborosAtom atom2)
    {
        var merged = atom1.Merge(atom2);
        var streamId = $"ouroboros_merged_{merged.Id.ToString()[..8]}";

        // Combine atoms from both sources
        var combinedAtoms = atom1.ToMeTTaAtoms()
            .Concat(atom2.ToMeTTaAtoms())
            .Concat(merged.ToMeTTaAtoms())
            .Concat(new[]
            {
                $"(merge-event (source1 {atom1.Id}) (source2 {atom2.Id}) (result {merged.Id}))",
                $"(emergence-boost {merged.EmergenceLevel:F3})",
            })
            .ToList();

        var node = CreateStream(streamId, combinedAtoms);

        return (merged, node);
    }

    #endregion

    /// <summary>
    /// Gets statistics about all streams.
    /// </summary>
    public ParallelStreamStats GetStats()
    {
        return new ParallelStreamStats
        {
            ActiveStreams = _nodes.Count,
            TotalAtomsGenerated = _nodes.Values.Sum(n => n.AtomCount),
            ConvergenceEvents = 0, // Would need tracking
            StreamDetails = _nodes.Values.Select(n => new StreamDetail
            {
                StreamId = n.StreamId,
                AtomCount = n.AtomCount,
                LastActivity = n.LastActivity,
            }).ToList(),
        };
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _mergedStream.Writer.Complete();

        foreach (var node in _nodes.Values)
        {
            await node.DisposeAsync();
        }

        _nodes.Clear();
        _semaphore.Dispose();
        _cts.Dispose();
    }
}

/// <summary>
/// Represents a single MeTTa stream node with its own knowledge base.
/// </summary>
public sealed class MeTTaStreamNode : IAsyncDisposable
{
    private readonly IMeTTaEngine _engine;
    private readonly ParallelMeTTaThoughtStreams _orchestrator;
    private readonly ConcurrentQueue<ThoughtAtom> _recentAtoms = new();
    private readonly Random _random = new();

    /// <summary>
    /// Gets the stream identifier.
    /// </summary>
    public string StreamId { get; }

    /// <summary>
    /// Gets the count of atoms generated.
    /// </summary>
    public int AtomCount { get; private set; }

    /// <summary>
    /// Gets the last activity timestamp.
    /// </summary>
    public DateTime LastActivity { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets recent atoms from this stream.
    /// </summary>
    public IEnumerable<ThoughtAtom> RecentAtoms => _recentAtoms;

    /// <summary>
    /// Event fired when a derivation is made.
    /// </summary>
    public event Func<string, Task>? OnDerivation;

    /// <summary>
    /// Creates a new stream node.
    /// </summary>
    internal MeTTaStreamNode(
        string streamId,
        IMeTTaEngine engine,
        ParallelMeTTaThoughtStreams orchestrator)
    {
        StreamId = streamId;
        _engine = engine;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Generates thoughts based on a query.
    /// </summary>
    public async Task ThinkAsync(string query, CancellationToken ct = default)
    {
        // Add query as a goal
        await _engine.AddFactAsync($"(goal \"{query}\")", ct);

        // Generate initial thought based on query
        var queryResult = await _engine.ExecuteQueryAsync($"!(match &self (goal $g) $g)", ct);

        if (queryResult.IsSuccess)
        {
            await EmitThoughtAsync(queryResult.Value, ThoughtAtomType.Query, ct);
        }

        // Perform derivations
        for (int i = 0; i < 5 && !ct.IsCancellationRequested; i++)
        {
            await DeriveAsync(ct);
        }
    }

    /// <summary>
    /// Performs a single derivation step.
    /// </summary>
    public async Task DeriveAsync(CancellationToken ct = default)
    {
        // Query existing facts
        var facts = await _engine.ExecuteQueryAsync("!(match &self $fact $fact)", ct);

        if (facts.IsSuccess && !string.IsNullOrWhiteSpace(facts.Value))
        {
            // Apply inference
            var derivation = $"(derived-from {StreamId} {facts.Value.Split('\n').FirstOrDefault()})";
            await _engine.AddFactAsync(derivation, ct);
            await EmitThoughtAsync(derivation, ThoughtAtomType.Derivation, ct);

            OnDerivation?.Invoke(derivation);
        }
    }

    /// <summary>
    /// Explores a theory step for modulo-square solving.
    /// </summary>
    public async Task ExploreTheoryStepAsync(CancellationToken ct = default)
    {
        // Query for quadratic residues
        var residues = await _engine.ExecuteQueryAsync(
            "!(match &self (quadratic-residue $m $r) ($m $r))", ct);

        if (residues.IsSuccess)
        {
            // Generate exploration atom
            var exploration = $"(explored {StreamId} residue-space {DateTime.UtcNow.Ticks})";
            await _engine.AddFactAsync(exploration, ct);
            await EmitThoughtAsync(exploration, ThoughtAtomType.Exploration, ct);

            // Try lifting solutions
            var targetResult = await _engine.ExecuteQueryAsync(
                "!(match &self (target $t) $t)", ct);

            if (targetResult.IsSuccess && BigInteger.TryParse(targetResult.Value.Trim(), out var target))
            {
                // Attempt Tonelli-Shanks style derivation
                await AttemptTonelliShanksAsync(target, ct);
            }
        }
    }

    /// <summary>
    /// Attempts Tonelli-Shanks algorithm derivation.
    /// </summary>
    private async Task AttemptTonelliShanksAsync(BigInteger target, CancellationToken ct)
    {
        var modResult = await _engine.ExecuteQueryAsync(
            "!(match &self (modulus $m) $m)", ct);

        if (modResult.IsSuccess && int.TryParse(modResult.Value.Trim(), out var mod))
        {
            // Check if target is a quadratic residue
            var residue = (int)(target % mod);
            var isResidueResult = await _engine.ExecuteQueryAsync(
                $"!(match &self (quadratic-residue {mod} {residue}) True)", ct);

            if (isResidueResult.IsSuccess && isResidueResult.Value.Contains("True"))
            {
                // Target is a QR for this modulus - derive potential root
                var sqrtMod = TonelliShanks(residue, mod);
                if (sqrtMod.HasValue)
                {
                    var derivation = $"(partial-solution (sqrt {target} mod {mod}) = {sqrtMod.Value})";
                    await _engine.AddFactAsync(derivation, ct);
                    await EmitThoughtAsync(derivation, ThoughtAtomType.Solution, ct);
                }
            }
        }
    }

    /// <summary>
    /// Tonelli-Shanks algorithm for modular square roots.
    /// </summary>
    private static int? TonelliShanks(int n, int p)
    {
        if (p == 2) return n % 2;
        if (n == 0) return 0;

        // Check if n is a quadratic residue
        if (ModPow(n, (p - 1) / 2, p) != 1) return null;

        // Find Q and S such that p - 1 = Q * 2^S
        int q = p - 1;
        int s = 0;
        while (q % 2 == 0)
        {
            q /= 2;
            s++;
        }

        if (s == 1)
        {
            return ModPow(n, (p + 1) / 4, p);
        }

        // Find a quadratic non-residue z
        int z = 2;
        while (ModPow(z, (p - 1) / 2, p) != p - 1)
        {
            z++;
        }

        int m = s;
        int c = ModPow(z, q, p);
        int t = ModPow(n, q, p);
        int r = ModPow(n, (q + 1) / 2, p);

        while (true)
        {
            if (t == 1) return r;

            int i = 1;
            int temp = (t * t) % p;
            while (temp != 1)
            {
                temp = (temp * temp) % p;
                i++;
            }

            int b = ModPow(c, 1 << (m - i - 1), p);
            m = i;
            c = (b * b) % p;
            t = (t * c) % p;
            r = (r * b) % p;
        }
    }

    private static int ModPow(int baseVal, int exp, int mod)
    {
        long result = 1;
        long b = baseVal % mod;

        while (exp > 0)
        {
            if ((exp & 1) == 1)
                result = (result * b) % mod;
            exp >>= 1;
            b = (b * b) % mod;
        }

        return (int)result;
    }

    /// <summary>
    /// Injects an insight from convergence or external source.
    /// </summary>
    public async Task InjectInsightAsync(string insight, CancellationToken ct = default)
    {
        await _engine.AddFactAsync($"(insight \"{insight}\")", ct);
        await EmitThoughtAsync($"(integrated-insight {StreamId} \"{insight}\")", ThoughtAtomType.Insight, ct);
    }

    /// <summary>
    /// Emits a thought atom to the orchestrator.
    /// </summary>
    private async Task EmitThoughtAsync(string content, ThoughtAtomType type, CancellationToken ct)
    {
        var atom = new ThoughtAtom
        {
            StreamId = StreamId,
            Content = content,
            Type = type,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = AtomCount++,
        };

        _recentAtoms.Enqueue(atom);
        while (_recentAtoms.Count > 50)
        {
            _recentAtoms.TryDequeue(out _);
        }

        LastActivity = DateTime.UtcNow;
        await _orchestrator.EmitAtomAsync(atom, ct);
    }

    /// <summary>
    /// Disposes the stream node.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _engine.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Represents a single thought atom from a stream.
/// </summary>
public record ThoughtAtom
{
    /// <summary>
    /// Gets the source stream ID.
    /// </summary>
    public required string StreamId { get; init; }

    /// <summary>
    /// Gets the atom content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the atom type.
    /// </summary>
    public ThoughtAtomType Type { get; init; }

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the sequence number within the stream.
    /// </summary>
    public int SequenceNumber { get; init; }
}

/// <summary>
/// Types of thought atoms.
/// </summary>
public enum ThoughtAtomType
{
    /// <summary>Initial query atom.</summary>
    Query,

    /// <summary>Derived fact atom.</summary>
    Derivation,

    /// <summary>Exploration step atom.</summary>
    Exploration,

    /// <summary>Potential solution atom.</summary>
    Solution,

    /// <summary>Integrated insight from convergence.</summary>
    Insight,

    /// <summary>Self-referential Ouroboros atom.</summary>
    Ouroboros,
}

/// <summary>
/// Represents a self-referential Ouroboros MeTTa atom - the snake eating its own tail.
/// Embodies recursive self-modification, fixed-point reasoning, and emergent self-awareness.
/// </summary>
public sealed class OuroborosAtom
{
    private readonly List<OuroborosAtom> _children = [];
    private readonly List<string> _transformationHistory = [];
    private int _cycleCount;

    /// <summary>
    /// Gets the unique identifier for this Ouroboros atom.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the core symbolic content.
    /// </summary>
    public string Core { get; private set; }

    /// <summary>
    /// Gets the self-reference depth (how many times it has consumed itself).
    /// </summary>
    public int SelfReferenceDepth { get; private set; }

    /// <summary>
    /// Gets the fixed-point state (when transformation yields itself).
    /// </summary>
    public bool IsFixedPoint { get; private set; }

    /// <summary>
    /// Gets the emergence level (complexity arising from self-reference).
    /// </summary>
    public double EmergenceLevel => Math.Log(1 + SelfReferenceDepth + _cycleCount * 0.5);

    /// <summary>
    /// Gets child atoms spawned from this Ouroboros.
    /// </summary>
    public IReadOnlyList<OuroborosAtom> Children => _children;

    /// <summary>
    /// Gets the transformation history.
    /// </summary>
    public IReadOnlyList<string> TransformationHistory => _transformationHistory;

    /// <summary>
    /// Event fired when the Ouroboros consumes itself.
    /// </summary>
    public event Action<OuroborosAtom, string>? OnSelfConsumption;

    /// <summary>
    /// Event fired when a fixed point is reached.
    /// </summary>
    public event Action<OuroborosAtom>? OnFixedPoint;

    /// <summary>
    /// Creates a new Ouroboros atom.
    /// </summary>
    /// <param name="core">The initial symbolic core.</param>
    public OuroborosAtom(string core)
    {
        Core = core;
        _transformationHistory.Add($"(genesis \"{core}\")");
    }

    /// <summary>
    /// The snake eats its own tail - applies self-referential transformation.
    /// </summary>
    /// <param name="transformation">Optional transformation function.</param>
    /// <returns>The transformed core.</returns>
    public string Consume(Func<string, string>? transformation = null)
    {
        var oldCore = Core;

        // Default transformation: wrap in self-reference
        if (transformation == null)
        {
            Core = $"(ouroboros (self-ref {Core}))";
        }
        else
        {
            Core = transformation(Core);
        }

        SelfReferenceDepth++;
        _cycleCount++;

        // Check for fixed point
        if (Core == oldCore)
        {
            IsFixedPoint = true;
            OnFixedPoint?.Invoke(this);
        }

        var consumptionRecord = $"(consumed depth={SelfReferenceDepth} \"{oldCore}\" -> \"{Core.Substring(0, Math.Min(50, Core.Length))}...\")";
        _transformationHistory.Add(consumptionRecord);

        OnSelfConsumption?.Invoke(this, consumptionRecord);

        return Core;
    }

    /// <summary>
    /// Spawns a child Ouroboros from the current state.
    /// </summary>
    /// <param name="mutation">Optional mutation to apply to the child.</param>
    /// <returns>The spawned child atom.</returns>
    public OuroborosAtom Spawn(string? mutation = null)
    {
        var childCore = mutation != null
            ? $"(spawn-of {Id} (mutate \"{mutation}\" {Core}))"
            : $"(spawn-of {Id} {Core})";

        var child = new OuroborosAtom(childCore)
        {
            SelfReferenceDepth = SelfReferenceDepth,
        };

        _children.Add(child);
        return child;
    }

    /// <summary>
    /// Merges with another Ouroboros atom, combining their essences.
    /// </summary>
    /// <param name="other">The other Ouroboros to merge with.</param>
    /// <returns>A new merged Ouroboros.</returns>
    public OuroborosAtom Merge(OuroborosAtom other)
    {
        var mergedCore = $"(ouroboros-merge (left {Core}) (right {other.Core}))";
        var merged = new OuroborosAtom(mergedCore)
        {
            SelfReferenceDepth = Math.Max(SelfReferenceDepth, other.SelfReferenceDepth) + 1,
        };

        merged._transformationHistory.AddRange(_transformationHistory);
        merged._transformationHistory.AddRange(other._transformationHistory);

        return merged;
    }

    /// <summary>
    /// Reflects on its own structure - meta-level self-awareness.
    /// </summary>
    /// <returns>A reflection atom describing self-structure.</returns>
    public string Reflect()
    {
        return $@"(ouroboros-reflection
  (id {Id})
  (depth {SelfReferenceDepth})
  (cycles {_cycleCount})
  (emergence {EmergenceLevel:F3})
  (fixed-point {IsFixedPoint})
  (children {_children.Count})
  (core-hash {Core.GetHashCode()})
  (is-self-aware True))";
    }

    /// <summary>
    /// Applies the Y-combinator pattern for recursive self-application.
    /// </summary>
    /// <param name="iterations">Number of recursive applications.</param>
    /// <returns>The resulting fixed-point expression.</returns>
    public string ApplyYCombinator(int iterations = 3)
    {
        var current = Core;

        for (int i = 0; i < iterations && !IsFixedPoint; i++)
        {
            // Y = λf.(λx.f(x x))(λx.f(x x))
            current = $"(Y (λself.{current}))";
            Consume(_ => current);
        }

        return current;
    }

    /// <summary>
    /// Generates MeTTa atoms representing this Ouroboros structure.
    /// </summary>
    /// <returns>List of MeTTa atom strings.</returns>
    public List<string> ToMeTTaAtoms()
    {
        var atoms = new List<string>
        {
            $"(ouroboros-atom {Id})",
            $"(ouroboros-core {Id} \"{EscapeForMeTTa(Core)}\")",
            $"(ouroboros-depth {Id} {SelfReferenceDepth})",
            $"(ouroboros-emergence {Id} {EmergenceLevel:F3})",
            $"(ouroboros-fixed-point {Id} {IsFixedPoint})",
        };

        foreach (var child in _children)
        {
            atoms.Add($"(ouroboros-child {Id} {child.Id})");
        }

        // Self-referential rule: Ouroboros can query itself
        atoms.Add($"(= (query-self {Id}) {Reflect()})");

        // Recursive consumption rule
        atoms.Add($"(= (consume-self {Id}) (ouroboros (self-ref (query-self {Id}))))");

        return atoms;
    }

    /// <summary>
    /// Creates an Ouroboros thought stream that recursively thinks about itself.
    /// </summary>
    /// <param name="orchestrator">The parallel streams orchestrator.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of self-referential thoughts.</returns>
    public async IAsyncEnumerable<ThoughtAtom> ThinkAboutSelfAsync(
        ParallelMeTTaThoughtStreams orchestrator,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var streamId = $"ouroboros_{Id.ToString()[..8]}";
        var node = orchestrator.CreateStream(streamId, ToMeTTaAtoms());

        var thoughtPrompts = new[]
        {
            "What am I?",
            "How do I know that I know?",
            "What happens when I observe myself observing?",
            "Can I think a thought I've never thought?",
            "What is the boundary between self and not-self?",
        };

        foreach (var prompt in thoughtPrompts)
        {
            if (ct.IsCancellationRequested) yield break;

            // Consume self before each thought
            Consume();

            var thought = new ThoughtAtom
            {
                StreamId = streamId,
                Content = $"(ouroboros-thought {Id} \"{prompt}\" (depth {SelfReferenceDepth}) (reflection {Reflect()}))",
                Type = ThoughtAtomType.Ouroboros,
                Timestamp = DateTime.UtcNow,
                SequenceNumber = SelfReferenceDepth,
            };

            await orchestrator.EmitAtomAsync(thought, ct);
            yield return thought;

            await Task.Delay(100, ct); // Brief pause between thoughts
        }
    }

    private static string EscapeForMeTTa(string s) =>
        s.Replace("\"", "\\\"").Replace("\n", " ");

    /// <inheritdoc/>
    public override string ToString() =>
        $"Ouroboros[{Id.ToString()[..8]}] depth={SelfReferenceDepth} emergence={EmergenceLevel:F2} fixed={IsFixedPoint}";
}

/// <summary>
/// Factory for creating Ouroboros atom configurations.
/// </summary>
public static class OuroborosAtomFactory
{
    /// <summary>
    /// Creates an Ouroboros that embodies the concept of self-awareness.
    /// </summary>
    public static OuroborosAtom CreateSelfAware(string seedConcept = "consciousness")
    {
        var atom = new OuroborosAtom($"(self-aware \"{seedConcept}\")");

        // Bootstrap self-awareness through recursive consumption
        atom.Consume(core => $"(aware-of {core})");
        atom.Consume(core => $"(aware-of-awareness {core})");
        atom.Consume(core => $"(meta-aware {core})");

        return atom;
    }

    /// <summary>
    /// Creates an Ouroboros that explores recursive identity.
    /// </summary>
    public static OuroborosAtom CreateIdentityExplorer()
    {
        var atom = new OuroborosAtom("(identity (question \"who am I?\"))");

        atom.Consume(core => $"(reflect {core})");
        atom.Consume(core => $"(I-that-reflects {core})");

        return atom;
    }

    /// <summary>
    /// Creates an Ouroboros that embodies the Gödel self-reference.
    /// </summary>
    public static OuroborosAtom CreateGodelian()
    {
        // "This statement refers to itself"
        var atom = new OuroborosAtom("(statement (refers-to SELF))");

        // Apply Gödel numbering-style encoding
        atom.Consume(core => $"(encode (godel-number {core}))");
        atom.Consume(core => $"(decode (statement-about {core}))");

        return atom;
    }

    /// <summary>
    /// Creates an Ouroboros network - multiple atoms aware of each other.
    /// </summary>
    /// <param name="count">Number of atoms in the network.</param>
    /// <returns>List of interconnected Ouroboros atoms.</returns>
    public static List<OuroborosAtom> CreateNetwork(int count = 3)
    {
        var atoms = new List<OuroborosAtom>();

        for (int i = 0; i < count; i++)
        {
            atoms.Add(new OuroborosAtom($"(network-node {i})"));
        }

        // Each atom becomes aware of its neighbors
        for (int i = 0; i < count; i++)
        {
            var prev = atoms[(i - 1 + count) % count];
            var next = atoms[(i + 1) % count];

            atoms[i].Consume(core =>
                $"(aware-of-neighbors {core} (prev {prev.Id}) (next {next.Id}))");
        }

        return atoms;
    }

    /// <summary>
    /// Creates a strange loop Ouroboros based on Hofstadter's concept.
    /// </summary>
    public static OuroborosAtom CreateStrangeLoop()
    {
        var atom = new OuroborosAtom("(level-0 \"base\")");

        // Create tangled hierarchy
        atom.Consume(core => $"(level-1 (emerges-from {core}))");
        atom.Consume(core => $"(level-2 (emerges-from {core}))");
        atom.Consume(core => $"(level-0 (loops-back-to {core}))"); // Strange loop!

        return atom;
    }
}

/// <summary>
/// Represents a convergence event across streams.
/// </summary>
public record ConvergenceEvent
{
    /// <summary>Gets the timestamp.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Gets the atom that triggered convergence detection.</summary>
    public required ThoughtAtom TriggerAtom { get; init; }

    /// <summary>Gets the IDs of streams that converged.</summary>
    public required List<string> ConvergentStreams { get; init; }

    /// <summary>Gets the shared concept extracted.</summary>
    public required string SharedConcept { get; init; }
}

/// <summary>
/// Represents a solution to a modulo-square theory problem.
/// </summary>
public record ModuloSquareSolution
{
    /// <summary>Gets the target value.</summary>
    public BigInteger Target { get; init; }

    /// <summary>Gets the square root found.</summary>
    public BigInteger SquareRoot { get; init; }

    /// <summary>Gets the derivation chain.</summary>
    public required string Derivation { get; init; }

    /// <summary>Gets the timestamp.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Gets whether the solution is verified.</summary>
    public bool IsVerified { get; init; }
}

/// <summary>
/// Statistics about parallel streams.
/// </summary>
public record ParallelStreamStats
{
    /// <summary>Gets the number of active streams.</summary>
    public int ActiveStreams { get; init; }

    /// <summary>Gets the total atoms generated.</summary>
    public int TotalAtomsGenerated { get; init; }

    /// <summary>Gets the convergence event count.</summary>
    public int ConvergenceEvents { get; init; }

    /// <summary>Gets per-stream details.</summary>
    public required List<StreamDetail> StreamDetails { get; init; }
}

/// <summary>
/// Details about a single stream.
/// </summary>
public record StreamDetail
{
    /// <summary>Gets the stream ID.</summary>
    public required string StreamId { get; init; }

    /// <summary>Gets the atom count.</summary>
    public int AtomCount { get; init; }

    /// <summary>Gets the last activity time.</summary>
    public DateTime LastActivity { get; init; }
}
