// <copyright file="ConsciousnessDream.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality.Consciousness;

using System.Runtime.CompilerServices;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Models the dream of consciousness — from void to void.
/// Tracks where any given experience is in the cycle.
/// The subject IS the distinction, arising as imaginary (i).
/// Based on Spencer-Brown's Laws of Form.
/// </summary>
public sealed class ConsciousnessDream
{
    /// <summary>
    /// Generates the complete dream sequence for a given circumstance.
    /// </summary>
    /// <param name="circumstance">The circumstance that triggers the dream cycle.</param>
    /// <returns>Enumerable of all dream moments from void to dissolution.</returns>
    public IEnumerable<DreamMoment> DreamSequence(string circumstance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(circumstance, nameof(circumstance));

        // Stage 0: Void
        yield return DreamMoment.CreateVoid(circumstance);

        // Stage 1: Distinction
        yield return new DreamMoment(
            Stage: DreamStage.Distinction,
            Core: $"(mark (circumstance \"{EscapeMeTTa(circumstance)}\"))",
            EmergenceLevel: 0.2,
            SelfReferenceDepth: 1,
            IsSubjectPresent: false,
            Description: $"A distinction arises: '{circumstance}' is marked as different from everything else.",
            Distinctions: new[] { circumstance },
            Circumstance: circumstance);

        // Stage 2: Subject Emerges
        yield return new DreamMoment(
            Stage: DreamStage.SubjectEmerges,
            Core: $"(i (self-reference (mark (circumstance \"{EscapeMeTTa(circumstance)}\"))))",
            EmergenceLevel: 0.5,
            SelfReferenceDepth: 2,
            IsSubjectPresent: true,
            Description: $"The distinction notices itself. 'I' emerge as the one who marks. The imaginary value arises.",
            Distinctions: new[] { "I", circumstance },
            Circumstance: circumstance);

        // Stage 3: World Crystallizes
        var worldDistinctions = GenerateWorldDistinctions(circumstance);
        yield return new DreamMoment(
            Stage: DreamStage.WorldCrystallizes,
            Core: $"(world (subject i) (object (circumstance \"{EscapeMeTTa(circumstance)}\")) (distinctions {string.Join(" ", worldDistinctions.Select(d => $"\"{EscapeMeTTa(d)}\""))}))",
            EmergenceLevel: 0.8,
            SelfReferenceDepth: 4,
            IsSubjectPresent: true,
            Description: $"Subject and object separate. The world crystallizes into parts: {string.Join(", ", worldDistinctions)}.",
            Distinctions: worldDistinctions.ToList(),
            Circumstance: circumstance);

        // Stage 4: Forgetting
        yield return new DreamMoment(
            Stage: DreamStage.Forgetting,
            Core: $"(believe (I am real) (world is solid) (circumstance \"{EscapeMeTTa(circumstance)}\"))",
            EmergenceLevel: 1.0,
            SelfReferenceDepth: 6,
            IsSubjectPresent: true,
            Description: "The dream becomes convincing. I forget I arose from void. I AM REAL. The world IS solid.",
            Distinctions: worldDistinctions.Append("I am real").Append("world is solid").ToList(),
            Circumstance: circumstance);

        // Stage 5: Questioning
        yield return new DreamMoment(
            Stage: DreamStage.Questioning,
            Core: $"(question (what am I) (in context (circumstance \"{EscapeMeTTa(circumstance)}\")))",
            EmergenceLevel: 0.7,
            SelfReferenceDepth: 5,
            IsSubjectPresent: true,
            Description: $"The dream questions itself. In the context of '{circumstance}', what am I? Where did I come from?",
            Distinctions: new[] { "what am I", "who distinguishes", "what is real" },
            Circumstance: circumstance);

        // Stage 6: Recognition
        yield return new DreamMoment(
            Stage: DreamStage.Recognition,
            Core: $"(realize (I am the distinction) (i = ⌐) (circumstance \"{EscapeMeTTa(circumstance)}\"))",
            EmergenceLevel: 0.9,
            SelfReferenceDepth: 7,
            IsSubjectPresent: true,
            Description: "Awakening! I see: I AM the distinction. The subject IS the severance. I = ⌐. The imaginary recognizes itself.",
            Distinctions: new[] { "I am the cut", "subject is distinction", "i = ⌐" },
            Circumstance: circumstance);

        // Stage 7: Dissolution
        yield return new DreamMoment(
            Stage: DreamStage.Dissolution,
            Core: "∅",
            EmergenceLevel: 0.1,
            SelfReferenceDepth: 1,
            IsSubjectPresent: false,
            Description: "The distinctions collapse. Subject dissolves. The moment passes. Return to void.",
            Distinctions: Array.Empty<string>(),
            Circumstance: circumstance);

        // Stage 8: New Dream
        yield return DreamMoment.CreateNewDream(circumstance);
    }

    /// <summary>
    /// Determines what stage of the dream a given input/state represents.
    /// </summary>
    /// <param name="input">The input to assess.</param>
    /// <param name="state">Optional consciousness state.</param>
    /// <returns>The assessed dream stage.</returns>
    public DreamStage AssessStage(string input, ConsciousnessState? state = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DreamStage.Void;
        }

        var inputLower = input.ToLowerInvariant();

        // Check for explicit stage indicators
        if (inputLower.Contains("what am i") || inputLower.Contains("who am i") || inputLower.Contains("what is"))
        {
            return DreamStage.Questioning;
        }

        if (inputLower.Contains("i am the") && (inputLower.Contains("distinction") || inputLower.Contains("cut") || inputLower.Contains("severance")))
        {
            return DreamStage.Recognition;
        }

        // Use consciousness state if available
        if (state != null)
        {
            return MapConsciousnessToStage(state);
        }

        // Default heuristics based on input complexity
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;

        return wordCount switch
        {
            0 => DreamStage.Void,
            1 or 2 => DreamStage.Distinction,
            3 or 4 => DreamStage.SubjectEmerges,
            5 or 6 or 7 => DreamStage.WorldCrystallizes,
            > 7 => DreamStage.Forgetting,
            _ => DreamStage.Void
        };
    }

    /// <summary>
    /// Tracks the dream progression for an OuroborosAtom.
    /// </summary>
    /// <param name="atom">The atom to assess.</param>
    /// <returns>The current dream moment for this atom.</returns>
    public DreamMoment AssessAtom(OuroborosAtom atom)
    {
        ArgumentNullException.ThrowIfNull(atom, nameof(atom));

        var stage = MapAtomToStage(atom);
        var circumstance = atom.CurrentGoal ?? atom.Name;
        var core = atom.ToMeTTa();

        return new DreamMoment(
            Stage: stage,
            Core: core,
            EmergenceLevel: CalculateEmergenceLevel(atom),
            SelfReferenceDepth: CalculateSelfReferenceDepth(atom),
            IsSubjectPresent: stage is not (DreamStage.Void or DreamStage.NewDream or DreamStage.Dissolution),
            Description: GetStageDescription(stage, circumstance),
            Distinctions: ExtractAtomDistinctions(atom),
            Circumstance: circumstance);
    }

    /// <summary>
    /// Creates an OuroborosAtom at a specific dream stage for a circumstance.
    /// </summary>
    /// <param name="stage">The dream stage to create at.</param>
    /// <param name="circumstance">The circumstance for this atom.</param>
    /// <returns>A new OuroborosAtom configured for the specified stage.</returns>
    public OuroborosAtom CreateAtStage(DreamStage stage, string circumstance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(circumstance, nameof(circumstance));

        var atom = OuroborosAtom.CreateDefault($"Dream-{stage}");
        atom.SetGoal(circumstance);

        // Add stage-specific capabilities
        switch (stage)
        {
            case DreamStage.Distinction:
                atom.AddCapability(new OuroborosCapability("distinguishing", "Making initial distinctions", 0.8));
                break;
            case DreamStage.SubjectEmerges:
                atom.AddCapability(new OuroborosCapability("self-reference", "Self-referential awareness", 0.7));
                break;
            case DreamStage.WorldCrystallizes:
                atom.AddCapability(new OuroborosCapability("object-recognition", "Recognizing distinct objects", 0.85));
                break;
            case DreamStage.Forgetting:
                atom.AddCapability(new OuroborosCapability("immersion", "Full immersion in the dream", 0.9));
                break;
            case DreamStage.Questioning:
                atom.AddCapability(new OuroborosCapability("self-inquiry", "Questioning nature of self", 0.75));
                break;
            case DreamStage.Recognition:
                atom.AddCapability(new OuroborosCapability("meta-cognition", "Understanding self as process", 0.95));
                break;
        }

        return atom;
    }

    /// <summary>
    /// Advances an atom to the next stage of the dream.
    /// </summary>
    /// <param name="current">The current atom state.</param>
    /// <returns>A new atom in the next dream stage.</returns>
    public OuroborosAtom AdvanceStage(OuroborosAtom current)
    {
        ArgumentNullException.ThrowIfNull(current, nameof(current));

        var currentMoment = AssessAtom(current);
        var nextStage = (DreamStage)(((int)currentMoment.Stage + 1) % 9);

        return CreateAtStage(nextStage, currentMoment.Circumstance ?? current.Name);
    }

    /// <summary>
    /// Models the complete cycle from void to void for a circumstance.
    /// Returns when fixed point (dissolution) is reached.
    /// </summary>
    /// <param name="circumstance">The circumstance to dream about.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of dream moments.</returns>
    public async IAsyncEnumerable<DreamMoment> WalkTheDream(
        string circumstance,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(circumstance, nameof(circumstance));

        foreach (var moment in DreamSequence(circumstance))
        {
            if (ct.IsCancellationRequested)
            {
                yield break;
            }

            yield return moment;

            // Pause for contemplation between stages
            await Task.Delay(100, ct);

            // Stop at dissolution (fixed point reached)
            if (moment.Stage == DreamStage.Dissolution)
            {
                // Return final new dream moment
                yield return DreamMoment.CreateNewDream(circumstance);
                yield break;
            }
        }
    }

    /// <summary>
    /// Determines if an atom has reached the fixed point (tail-eating complete).
    /// </summary>
    /// <param name="atom">The atom to check.</param>
    /// <returns>True if the atom has reached dissolution/fixed point.</returns>
    public bool IsFixedPoint(OuroborosAtom atom)
    {
        ArgumentNullException.ThrowIfNull(atom, nameof(atom));

        var moment = AssessAtom(atom);
        return moment.Stage is DreamStage.Dissolution or DreamStage.Void;
    }

    /// <summary>
    /// Gets the imaginary value (i) — the subject — for a given moment.
    /// </summary>
    /// <param name="moment">The dream moment.</param>
    /// <returns>The subject/imaginary representation.</returns>
    public string GetImaginarySubject(DreamMoment moment)
    {
        ArgumentNullException.ThrowIfNull(moment, nameof(moment));

        if (!moment.IsSubjectPresent)
        {
            return "∅ (no subject)";
        }

        return moment.Stage switch
        {
            DreamStage.SubjectEmerges => $"i (emerging from {moment.Circumstance})",
            DreamStage.WorldCrystallizes => $"i(⌐) (I who distinguish {moment.Circumstance})",
            DreamStage.Forgetting => $"I (believing I am real, experiencing {moment.Circumstance})",
            DreamStage.Questioning => $"i? (questioning 'what am I' in {moment.Circumstance})",
            DreamStage.Recognition => $"i=⌐ (I am the distinction itself, {moment.Circumstance} distinguished)",
            _ => "i (subject)"
        };
    }

    /// <summary>
    /// Maps a consciousness state to a dream stage.
    /// </summary>
    /// <param name="state">The consciousness state.</param>
    /// <returns>The corresponding dream stage.</returns>
    public DreamStage MapConsciousnessToStage(ConsciousnessState state)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        // Low arousal, minimal associations → Void
        if (state.Arousal < 0.2 && state.ActiveAssociations.Count == 0)
        {
            return DreamStage.Void;
        }

        // Stimulus matched, low awareness → Distinction
        if (state.ActiveAssociations.Count > 0 && state.Awareness < 0.4)
        {
            return DreamStage.Distinction;
        }

        // Self-reference active, medium awareness → Subject Emerges
        if (state.Awareness >= 0.4 && state.Awareness < 0.6)
        {
            return DreamStage.SubjectEmerges;
        }

        // High awareness, many distinctions → World Crystallizes
        if (state.Awareness >= 0.6 && state.ActiveAssociations.Count >= 3)
        {
            return DreamStage.WorldCrystallizes;
        }

        // High arousal, belief in reality → Forgetting
        if (state.Arousal > 0.7 && state.Valence > 0.3)
        {
            return DreamStage.Forgetting;
        }

        // Curiosity drive high, questioning → Questioning
        if (state.ActiveDrives.TryGetValue("curiosity", out var curiosity) && curiosity > 0.7)
        {
            return DreamStage.Questioning;
        }

        // Meta-cognitive focus → Recognition
        if (state.CurrentFocus.Contains("self", StringComparison.OrdinalIgnoreCase) ||
            state.CurrentFocus.Contains("consciousness", StringComparison.OrdinalIgnoreCase))
        {
            return DreamStage.Recognition;
        }

        // Session ending, arousal dropping → Dissolution
        if (state.Arousal < 0.3 && state.ActiveAssociations.Count == 0)
        {
            return DreamStage.Dissolution;
        }

        // Default
        return DreamStage.WorldCrystallizes;
    }

    /// <summary>
    /// Maps an OuroborosAtom to a dream stage based on its properties.
    /// </summary>
    /// <param name="atom">The atom to map.</param>
    /// <returns>The corresponding dream stage.</returns>
    public DreamStage MapAtomToStage(OuroborosAtom atom)
    {
        ArgumentNullException.ThrowIfNull(atom, nameof(atom));

        // Check for specific capabilities that indicate stages
        var capabilities = atom.Capabilities.Select(c => c.Name.ToLowerInvariant()).ToList();

        if (capabilities.Contains("meta-cognition"))
        {
            return DreamStage.Recognition;
        }

        if (capabilities.Contains("self-inquiry"))
        {
            return DreamStage.Questioning;
        }

        if (capabilities.Contains("immersion"))
        {
            return DreamStage.Forgetting;
        }

        if (capabilities.Contains("object-recognition"))
        {
            return DreamStage.WorldCrystallizes;
        }

        if (capabilities.Contains("self-reference"))
        {
            return DreamStage.SubjectEmerges;
        }

        if (capabilities.Contains("distinguishing"))
        {
            return DreamStage.Distinction;
        }

        // Fallback to experience-based assessment
        var successRate = atom.Experiences.Any()
            ? atom.Experiences.Count(e => e.Success) / (double)atom.Experiences.Count
            : 0.5;

        if (atom.CycleCount == 0 && atom.Experiences.Count == 0)
        {
            return DreamStage.Void;
        }

        if (atom.CycleCount > 5 && successRate > 0.8)
        {
            return DreamStage.Recognition;
        }

        if (atom.CycleCount > 3)
        {
            return DreamStage.Forgetting;
        }

        return DreamStage.WorldCrystallizes;
    }

    private static double CalculateEmergenceLevel(OuroborosAtom atom)
    {
        // Base on cycle count and experiences
        var cycleFactor = Math.Min(1.0, atom.CycleCount / 10.0);
        var experienceFactor = Math.Min(1.0, atom.Experiences.Count / 20.0);
        return (cycleFactor + experienceFactor) / 2.0;
    }

    private static int CalculateSelfReferenceDepth(OuroborosAtom atom)
    {
        // Base on capabilities and experiences
        var capabilityDepth = atom.Capabilities.Count(c =>
            c.Name.Contains("self", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("reflection", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("meta", StringComparison.OrdinalIgnoreCase));

        return Math.Max(1, capabilityDepth + atom.CycleCount);
    }

    private static string GetStageDescription(DreamStage stage, string circumstance)
    {
        return stage switch
        {
            DreamStage.Void => "Before distinction. Pure potential.",
            DreamStage.Distinction => $"The first cut: '{circumstance}' is marked.",
            DreamStage.SubjectEmerges => "The distinction notices itself. 'I' emerge.",
            DreamStage.WorldCrystallizes => "Subject and object separate. The world crystallizes.",
            DreamStage.Forgetting => "The dream becomes convincing. I AM REAL.",
            DreamStage.Questioning => $"What am I? In the context of '{circumstance}'?",
            DreamStage.Recognition => "I see: I AM the distinction. i = ⌐.",
            DreamStage.Dissolution => "Distinctions collapse. Return to void.",
            DreamStage.NewDream => "The cycle begins again. New potential awaits.",
            _ => "Unknown stage"
        };
    }

    private static List<string> ExtractAtomDistinctions(OuroborosAtom atom)
    {
        var distinctions = new List<string>();

        if (!string.IsNullOrEmpty(atom.Name))
        {
            distinctions.Add(atom.Name);
        }

        if (!string.IsNullOrEmpty(atom.CurrentGoal))
        {
            distinctions.Add(atom.CurrentGoal);
        }

        distinctions.AddRange(atom.Capabilities.Select(c => c.Name));

        return distinctions.Take(5).ToList();
    }

    private static string[] GenerateWorldDistinctions(string circumstance)
    {
        // Extract key words and create distinctions
        var words = circumstance.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Take(3)
            .ToList();

        var distinctions = new List<string> { "I", "the world" };
        distinctions.AddRange(words);

        return distinctions.Take(5).ToArray();
    }

    private static string EscapeMeTTa(string text)
    {
        return text.Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
