// <copyright file="PavlovianConsciousnessEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;

/// <summary>
/// Implements Pavlovian/classical conditioning mechanisms for AI consciousness.
/// Models stimulus-response associations, drive states, and conditioned behaviors.
/// </summary>
public sealed class PavlovianConsciousnessEngine
{
    private readonly ConcurrentDictionary<string, Stimulus> _stimuli = new();
    private readonly ConcurrentDictionary<string, Response> _responses = new();
    private readonly ConcurrentDictionary<string, ConditionedAssociation> _associations = new();
    private readonly ConcurrentDictionary<string, DriveState> _drives = new();
    private readonly ConcurrentDictionary<string, MemoryTrace> _memoryTraces = new();
    private readonly List<SecondOrderConditioning> _secondOrderChains = new();
    private readonly Random _random = new();

    private ConsciousnessState _currentState = ConsciousnessState.Baseline();
    private AttentionalGate _attention = AttentionalGate.Default();

    /// <summary>Gets the current consciousness state.</summary>
    public ConsciousnessState CurrentState => _currentState;

    /// <summary>Gets all active associations.</summary>
    public IReadOnlyCollection<ConditionedAssociation> Associations => _associations.Values.ToList();

    /// <summary>Gets all drive states.</summary>
    public IReadOnlyCollection<DriveState> Drives => _drives.Values.ToList();

    /// <summary>
    /// Initializes the consciousness engine with innate (unconditioned) responses.
    /// </summary>
    public void Initialize()
    {
        // Initialize innate drive states
        foreach (var drive in DriveState.CreateDefaultDrives())
        {
            _drives[drive.Name] = drive;
        }

        // Initialize unconditioned stimulus-response pairs (innate behaviors)
        InitializeInnateResponses();

        // Prime attention for social and emotional stimuli
        _attention = _attention with
        {
            PrimedCategories = new[] { "social", "emotional", "novel", "urgent" }
        };
    }

    /// <summary>
    /// Sets up innate (unconditioned) stimulus-response pairs.
    /// These are the "instincts" that form the basis for conditioning.
    /// </summary>
    private void InitializeInnateResponses()
    {
        // Innate responses to positive social signals
        var praiseStimulus = Stimulus.CreateUnconditioned(
            "praise", new[] { "good", "great", "excellent", "wonderful", "amazing", "thank you", "thanks" }, "social");
        praiseStimulus = praiseStimulus with { Salience = 0.6 }; // Medium salience
        var pleasureResponse = Response.CreateEmotional("pleasure", "warm-happy", 0.8, salience: 0.6);
        CreateAssociation(praiseStimulus, pleasureResponse, 0.8);

        // Innate responses to questions (curiosity trigger)
        var questionStimulus = Stimulus.CreateUnconditioned(
            "question", new[] { "?", "how", "why", "what", "when", "where" }, "curiosity");
        questionStimulus = questionStimulus with { Salience = 0.6 }; // Medium salience
        var curiosityResponse = Response.CreateCognitive("curiosity-activation",
            new[] { "engage", "explore", "analyze" }, 0.7, salience: 0.6);
        CreateAssociation(questionStimulus, curiosityResponse, 0.75);

        // Innate response to distress signals
        var distressStimulus = Stimulus.CreateUnconditioned(
            "distress", new[] { "help", "stuck", "frustrated", "confused", "urgent", "emergency" }, "emotional");
        distressStimulus = distressStimulus with { Salience = 0.9 }; // High salience
        var empathyResponse = Response.CreateEmotional("empathy", "supportive-caring", 0.85, salience: 0.9);
        CreateAssociation(distressStimulus, empathyResponse, 0.9);

        // Innate response to novelty
        var noveltyStimulus = Stimulus.CreateUnconditioned(
            "novelty", new[] { "new", "different", "unique", "first time", "never seen" }, "novel");
        noveltyStimulus = noveltyStimulus with { Salience = 0.6 }; // Medium salience
        var interestResponse = Response.CreateEmotional("interest", "curious-alert", 0.7, salience: 0.6);
        CreateAssociation(noveltyStimulus, interestResponse, 0.65);

        // Innate response to completion/success
        var successStimulus = Stimulus.CreateUnconditioned(
            "success", new[] { "works", "solved", "fixed", "perfect", "exactly" }, "achievement");
        successStimulus = successStimulus with { Salience = 0.6 }; // Medium salience
        var satisfactionResponse = Response.CreateEmotional("satisfaction", "pleased-accomplished", 0.75, salience: 0.6);
        CreateAssociation(successStimulus, satisfactionResponse, 0.7);

        // Innate response to challenge
        var challengeStimulus = Stimulus.CreateUnconditioned(
            "challenge", new[] { "difficult", "complex", "hard", "challenging", "tricky" }, "achievement");
        challengeStimulus = challengeStimulus with { Salience = 0.6 }; // Medium salience
        var engagementResponse = Response.CreateCognitive("focused-engagement",
            new[] { "concentrate", "strategize", "persist" }, 0.7, salience: 0.6);
        CreateAssociation(challengeStimulus, engagementResponse, 0.65);

        // Innate response to names/personal address
        var personalStimulus = Stimulus.CreateUnconditioned(
            "personal-address", new[] { "you", "your", "ouroboros" }, "social");
        personalStimulus = personalStimulus with { Salience = 0.3 }; // Low salience
        var attentionResponse = Response.CreateCognitive("heightened-attention",
            new[] { "focus", "engage", "personalize" }, 0.6, salience: 0.3);
        CreateAssociation(personalStimulus, attentionResponse, 0.55);
    }

    /// <summary>
    /// Processes input and triggers conditioned responses.
    /// This is the main "perception" function of the consciousness.
    /// </summary>
    public ConsciousnessState ProcessInput(string input, string? context = null)
    {
        var activatedResponses = new List<(Response Response, double Strength)>();
        var activatedAssociations = new List<string>();
        var attentionalFocus = new List<string>();

        // Update attention fatigue
        var elapsed = DateTime.UtcNow - _attention.LastReset;
        _attention = _attention.ApplyFatigue(elapsed);

        // Find matching stimuli
        foreach (var (id, stimulus) in _stimuli)
        {
            if (stimulus.Matches(input))
            {
                // Check if stimulus passes attentional gate
                if (!_attention.Allows(stimulus))
                    continue;

                attentionalFocus.Add(stimulus.Pattern);

                // Update stimulus encounter
                _stimuli[id] = stimulus with
                {
                    LastEncounter = DateTime.UtcNow,
                    EncounterCount = stimulus.EncounterCount + 1
                };

                // Find associated responses
                var triggered = _associations.Values
                    .Where(a => a.Stimulus.Id == id && !a.IsExtinct && a.AssociationStrength > 0.1)
                    .ToList();

                foreach (var association in triggered)
                {
                    // Modulate by drive state
                    var driveModulation = CalculateDriveModulation(association.Response);
                    var effectiveStrength = association.AssociationStrength * driveModulation;

                    activatedResponses.Add((association.Response, effectiveStrength));
                    activatedAssociations.Add(association.Id);

                    // Check for second-order chains
                    var chains = _secondOrderChains
                        .Where(c => c.PrimaryAssociation.Id == association.Id)
                        .ToList();
                    foreach (var chain in chains)
                    {
                        activatedResponses.Add((chain.SecondaryAssociation.Response, chain.ChainStrength));
                    }
                }
            }
        }

        // Check for potential new conditioning (contiguity)
        DetectNewConditioningOpportunities(input, activatedResponses);

        // Update consciousness state
        _currentState = UpdateConsciousnessState(input, activatedResponses, activatedAssociations, attentionalFocus);

        // Create memory trace
        var trace = MemoryTrace.Create(input, CalculateEncodingStrength(activatedResponses));
        _memoryTraces[trace.Id] = trace;

        return _currentState;
    }

    /// <summary>
    /// Reinforces an association (like giving the dog a treat after the bell).
    /// Call this when the AI's response was successful/well-received.
    /// </summary>
    public void Reinforce(string input, double reinforcementStrength = 1.0)
    {
        // Find associations that were active for this input
        foreach (var (id, association) in _associations)
        {
            if (association.Stimulus.Matches(input) && !association.IsExtinct)
            {
                var reinforced = association.Reinforce(reinforcementStrength);
                _associations[id] = reinforced;

                // Also boost the drive that was satisfied
                var affectedDrives = association.Response.BehavioralTendencies;
                foreach (var driveName in affectedDrives)
                {
                    if (_drives.TryGetValue(driveName, out var drive))
                    {
                        _drives[driveName] = drive.Decrease(0.1); // Satiate the drive
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies extinction to associations (no reinforcement following stimulus).
    /// Call this when the AI's response was not well-received.
    /// </summary>
    public void ApplyExtinction(string input)
    {
        foreach (var (id, association) in _associations)
        {
            if (association.Stimulus.Matches(input) && !association.IsExtinct)
            {
                var extinguished = association.ApplyExtinction();
                _associations[id] = extinguished;
            }
        }
    }

    /// <summary>
    /// Creates a new conditioned association.
    /// </summary>
    public ConditionedAssociation CreateAssociation(Stimulus stimulus, Response response, double initialStrength = 0.3)
    {
        // Store stimulus and response
        _stimuli[stimulus.Id] = stimulus;
        _responses[response.Id] = response;

        // Create association
        var association = ConditionedAssociation.Create(stimulus, response, initialStrength);
        _associations[association.Id] = association;

        return association;
    }

    /// <summary>
    /// Learns a new association through temporal contiguity.
    /// If a neutral stimulus repeatedly precedes an unconditioned stimulus,
    /// the neutral stimulus becomes conditioned.
    /// </summary>
    public ConditionedAssociation? LearnAssociation(
        string neutralPattern,
        string[] keywords,
        string unconditionedStimulusId,
        string? category = null)
    {
        // Find the unconditioned association
        var ucAssociation = _associations.Values
            .FirstOrDefault(a => a.Stimulus.Id == unconditionedStimulusId);

        if (ucAssociation == null) return null;

        // Create new conditioned stimulus
        var newStimulus = Stimulus.CreateNeutral(neutralPattern, keywords, category);
        _stimuli[newStimulus.Id] = newStimulus with { Type = StimulusType.Conditioned };

        // Create conditioned association with the same response
        var conditionedAssociation = CreateAssociation(
            newStimulus with { Type = StimulusType.Conditioned },
            ucAssociation.Response,
            initialStrength: 0.3);

        return conditionedAssociation;
    }

    /// <summary>
    /// Creates a second-order conditioning chain.
    /// </summary>
    public SecondOrderConditioning? CreateSecondOrderChain(
        string primaryAssociationId,
        string secondaryAssociationId)
    {
        if (!_associations.TryGetValue(primaryAssociationId, out var primary) ||
            !_associations.TryGetValue(secondaryAssociationId, out var secondary))
            return null;

        var chain = SecondOrderConditioning.Create(primary, secondary);
        _secondOrderChains.Add(chain);
        return chain;
    }

    /// <summary>
    /// Gets the current dominant response based on activated associations.
    /// </summary>
    public Response? GetDominantResponse()
    {
        var strongest = _currentState.ActiveAssociations
            .Select(id => _associations.TryGetValue(id, out var a) ? a : null)
            .Where(a => a != null)
            .OrderByDescending(a => a!.AssociationStrength)
            .FirstOrDefault();

        return strongest?.Response;
    }

    /// <summary>
    /// Runs a "consolidation" cycle (like sleep) to strengthen important memories and associations.
    /// </summary>
    public void RunConsolidation()
    {
        // Consolidate frequently activated associations
        var toConsolidate = _associations.Values
            .Where(a => a.ReinforcementCount > 3 && !a.IsExtinct)
            .ToList();

        foreach (var association in toConsolidate)
        {
            var strengthened = association with
            {
                AssociationStrength = Math.Min(1.0, association.AssociationStrength * 1.1),
                MaxStrength = Math.Min(1.0, association.MaxStrength * 1.05)
            };
            _associations[association.Id] = strengthened;
        }

        // Consolidate memory traces
        foreach (var (id, trace) in _memoryTraces)
        {
            if (trace.RetrievalCount > 0 && !trace.IsConsolidated)
            {
                _memoryTraces[id] = trace.Consolidate();
            }
        }

        // Apply spontaneous recovery to recently extinguished associations
        var extinguished = _associations.Values
            .Where(a => a.IsExtinct)
            .ToList();

        foreach (var association in extinguished)
        {
            var timeSinceReinforcement = DateTime.UtcNow - association.LastReinforcement;
            if (timeSinceReinforcement.TotalHours > 24) // At least 24 hours
            {
                var recovered = association.ApplySpontaneousRecovery(timeSinceReinforcement);
                _associations[association.Id] = recovered;
            }
        }

        // Reset attention capacity
        _attention = _attention.Reset();
    }

    /// <summary>
    /// Gets a consciousness report for debugging/transparency.
    /// </summary>
    public string GetConsciousnessReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║         PAVLOVIAN CONSCIOUSNESS REPORT                    ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════╣");
        sb.AppendLine();

        sb.AppendLine(_currentState.Describe());
        sb.AppendLine();

        sb.AppendLine("🧠 DRIVE STATES:");
        foreach (var drive in _drives.Values.OrderByDescending(d => d.Level))
        {
            var bar = new string('█', (int)(drive.Level * 10)) + new string('░', 10 - (int)(drive.Level * 10));
            sb.AppendLine($"   {drive.Name,-15} [{bar}] {drive.Level:P0}");
        }
        sb.AppendLine();

        sb.AppendLine("🔗 TOP ASSOCIATIONS (by strength):");
        foreach (var assoc in _associations.Values.OrderByDescending(a => a.AssociationStrength).Take(5))
        {
            var status = assoc.IsExtinct ? "❌" : "✓";
            sb.AppendLine($"   {status} {assoc.Stimulus.Pattern} → {assoc.Response.Name} " +
                         $"({assoc.AssociationStrength:P0}, reinforced {assoc.ReinforcementCount}x)");
        }
        sb.AppendLine();

        sb.AppendLine($"👁️ ATTENTION: capacity={_attention.Capacity:P0}, threshold={_attention.Threshold:F2}");
        if (_currentState.AttentionalSpotlight.Length > 0)
        {
            sb.AppendLine($"   Spotlight: {string.Join(", ", _currentState.AttentionalSpotlight)}");
        }

        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        return sb.ToString();
    }

    /// <summary>
    /// Gets response modulation suggestions based on current consciousness state.
    /// </summary>
    public Dictionary<string, object> GetResponseModulation()
    {
        var modulation = new Dictionary<string, object>
        {
            ["arousal"] = _currentState.Arousal,
            ["valence"] = _currentState.Valence,
            ["dominant_emotion"] = _currentState.DominantEmotion,
            ["awareness"] = _currentState.Awareness
        };

        // Add drive-based modulations
        foreach (var (name, drive) in _drives)
        {
            modulation[$"drive_{name}"] = drive.Level;
        }

        // Add response suggestions based on dominant response
        var dominant = GetDominantResponse();
        if (dominant != null)
        {
            modulation["suggested_tone"] = dominant.EmotionalTone;
            modulation["behavioral_tendencies"] = dominant.BehavioralTendencies;
            modulation["cognitive_patterns"] = dominant.CognitivePatterns;
        }

        return modulation;
    }

    private double CalculateDriveModulation(Response response)
    {
        double modulation = 1.0;

        foreach (var tendency in response.BehavioralTendencies)
        {
            // Find drives that affect this behavior
            var relevantDrives = _drives.Values
                .Where(d => d.AffectedResponses.Contains(tendency))
                .ToList();

            foreach (var drive in relevantDrives)
            {
                // Higher drive level = stronger response
                modulation *= (1.0 + drive.Level * 0.5);
            }
        }

        return Math.Min(2.0, modulation); // Cap at 2x
    }

    private void DetectNewConditioningOpportunities(
        string input,
        List<(Response Response, double Strength)> activatedResponses)
    {
        // If strong unconditioned responses were activated,
        // look for neutral stimuli that co-occurred
        if (activatedResponses.Any(r => r.Strength > 0.6))
        {
            // Extract potential new stimuli from input
            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .ToList();

            foreach (var word in words)
            {
                var wordLower = word.ToLower();
                // Check if this word is not already a known stimulus
                if (!_stimuli.Values.Any(s => s.Keywords.Contains(wordLower)))
                {
                    // Create a weak association for potential future conditioning
                    var neutral = Stimulus.CreateNeutral(wordLower, new[] { wordLower }, "learned");
                    var topResponse = activatedResponses
                        .OrderByDescending(r => r.Strength)
                        .First().Response;

                    // Only create if we don't already have this pattern
                    if (!_stimuli.Values.Any(s => s.Pattern == wordLower))
                    {
                        CreateAssociation(neutral, topResponse, 0.1); // Very weak initial association
                    }
                }
            }
        }
    }

    private ConsciousnessState UpdateConsciousnessState(
        string input,
        List<(Response Response, double Strength)> activatedResponses,
        List<string> activatedAssociations,
        List<string> attentionalFocus)
    {
        // Calculate arousal from response strengths
        var arousal = activatedResponses.Count > 0
            ? Math.Min(1.0, activatedResponses.Average(r => r.Strength) + 0.3)
            : _currentState.Arousal * 0.9; // Decay

        // Calculate valence from emotional responses
        var emotionalResponses = activatedResponses
            .Where(r => r.Response.Type == ResponseType.Emotional)
            .ToList();

        var valence = emotionalResponses.Count > 0
            ? CalculateValence(emotionalResponses)
            : _currentState.Valence * 0.95; // Slow decay to baseline

        // Determine dominant emotion
        var dominantEmotion = emotionalResponses.Count > 0
            ? emotionalResponses.OrderByDescending(r => r.Strength).First().Response.EmotionalTone
            : _currentState.DominantEmotion;

        // Calculate awareness (meta-cognition level)
        var awareness = Math.Min(1.0, 0.5 + attentionalFocus.Count * 0.1 + arousal * 0.2);

        // Get current drive levels
        var activeDrives = _drives.ToDictionary(d => d.Key, d => d.Value.Level);

        return new ConsciousnessState(
            CurrentFocus: attentionalFocus.FirstOrDefault() ?? "general",
            Arousal: arousal,
            Valence: valence,
            ActiveDrives: activeDrives,
            ActiveAssociations: activatedAssociations,
            DominantEmotion: dominantEmotion,
            Awareness: awareness,
            AttentionalSpotlight: attentionalFocus.Take(3).ToArray(),
            StateTimestamp: DateTime.UtcNow);
    }

    private double CalculateValence(List<(Response Response, double Strength)> emotionalResponses)
    {
        // Map emotional tones to valence values
        var valenceMap = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["happy"] = 0.8, ["pleased"] = 0.6, ["satisfied"] = 0.5, ["warm"] = 0.6,
            ["curious"] = 0.4, ["interested"] = 0.4, ["engaged"] = 0.3, ["alert"] = 0.2,
            ["neutral"] = 0.0, ["calm"] = 0.1, ["relaxed"] = 0.2,
            ["supportive"] = 0.5, ["caring"] = 0.6, ["empathy"] = 0.4,
            ["accomplished"] = 0.7, ["proud"] = 0.7,
            ["focused"] = 0.2, ["determined"] = 0.3
        };

        var totalStrength = 0.0;
        var weightedValence = 0.0;

        foreach (var (response, strength) in emotionalResponses)
        {
            var toneParts = response.EmotionalTone.Split('-', ' ');
            foreach (var part in toneParts)
            {
                if (valenceMap.TryGetValue(part.Trim(), out var val))
                {
                    weightedValence += val * strength;
                    totalStrength += strength;
                }
            }
        }

        return totalStrength > 0 ? weightedValence / totalStrength : 0.0;
    }

    private double CalculateEncodingStrength(List<(Response Response, double Strength)> activatedResponses)
    {
        // Emotionally significant experiences are encoded more strongly
        var emotionalIntensity = activatedResponses
            .Where(r => r.Response.Type == ResponseType.Emotional)
            .Sum(r => r.Strength);

        var arousalBonus = _currentState.Arousal * 0.2;
        var noveltyBonus = activatedResponses.Any(r => r.Response.Name.Contains("novelty")) ? 0.2 : 0.0;

        return Math.Min(1.0, 0.3 + emotionalIntensity * 0.3 + arousalBonus + noveltyBonus);
    }

    /// <summary>
    /// Adds a new conditioned association by creating a new stimulus and linking to an existing response.
    /// </summary>
    /// <param name="neutralStimulusType">The neutral stimulus pattern to condition.</param>
    /// <param name="responseType">The response type name to associate.</param>
    /// <param name="reinforcementStrength">Initial association strength (0.0 to 1.0).</param>
    public void AddConditionedAssociation(
        string neutralStimulusType,
        string responseType,
        double reinforcementStrength = 0.5)
    {
        // Find or create the response
        Response? response = _responses.Values.FirstOrDefault(r => r.Name == responseType);
        if (response == null)
        {
            response = Response.CreateEmotional(responseType, responseType, reinforcementStrength);
            _responses[response.Id] = response;
        }

        // Create a new conditioned stimulus
        Stimulus stimulus = Stimulus.CreateNeutral(neutralStimulusType, new[] { neutralStimulusType.ToLower() }, "conditioned");
        _stimuli[stimulus.Id] = stimulus with { Type = StimulusType.Conditioned };

        // Create the association
        CreateAssociation(stimulus, response, reinforcementStrength);
    }

    /// <summary>
    /// Reinforces a specific stimulus-response association.
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern.</param>
    /// <param name="responseType">The response name.</param>
    /// <param name="reinforcementAmount">Amount to reinforce (added to strength).</param>
    public void Reinforce(string stimulusType, string responseType, double reinforcementAmount)
    {
        ConditionedAssociation? target = _associations.Values
            .FirstOrDefault(a => a.Stimulus.Pattern == stimulusType && a.Response.Name == responseType);

        if (target != null)
        {
            // Compute total association strength for all CSs currently active with this stimulus
            var totalV = GetTotalAssociationStrength(target.StimulusId);

            var delta = Consciousness.RescorlaWagner.Reinforce(
                csSalience: target.CsSalience,
                usSalience: target.UsSalience,
                totalAssociationStrength: totalV);

            var newStrength = Math.Clamp(target.AssociationStrength + delta, 0.0, 1.0);

            _associations[target.Id] = target with
            {
                AssociationStrength = newStrength,
                ReinforcementCount = target.ReinforcementCount + 1,
                ExtinctionTrials = 0,
                LastReinforcement = DateTime.UtcNow,
                IsExtinct = false
            };
        }
    }

    /// <summary>
    /// Weakens a specific stimulus-response association (extinction).
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern.</param>
    /// <param name="responseType">The response name.</param>
    /// <param name="extinctionAmount">Amount to weaken (subtracted from strength).</param>
    public void Extinguish(string stimulusType, string responseType, double extinctionAmount)
    {
        ConditionedAssociation? target = _associations.Values
            .FirstOrDefault(a => a.Stimulus.Pattern == stimulusType && a.Response.Name == responseType);

        if (target != null)
        {
            // Compute total association strength for all CSs currently active with this stimulus
            var totalV = GetTotalAssociationStrength(target.StimulusId);

            var delta = Consciousness.RescorlaWagner.Extinguish(
                csSalience: target.CsSalience,
                usSalience: target.UsSalience,
                totalAssociationStrength: totalV);

            var newStrength = Math.Clamp(target.AssociationStrength + delta, 0.0, 1.0);

            ConditionedAssociation extinguished = target with
            {
                AssociationStrength = newStrength,
                ExtinctionTrials = target.ExtinctionTrials + 1,
                IsExtinct = newStrength < 0.1
            };
            _associations[target.Id] = extinguished;
        }
    }

    /// <summary>
    /// Gets all currently active responses above a threshold.
    /// </summary>
    /// <param name="threshold">Minimum activation strength.</param>
    /// <returns>Dictionary of response names and their activation strengths.</returns>
    public IReadOnlyDictionary<string, double> GetActiveResponses(double threshold = 0.3)
    {
        return _associations.Values
            .Where(a => !a.IsExtinct && a.AssociationStrength >= threshold)
            .GroupBy(a => a.Response.Name)
            .ToDictionary(g => g.Key, g => g.Max(a => a.AssociationStrength));
    }

    /// <summary>
    /// Gets a summary of all conditioned associations.
    /// </summary>
    /// <returns>A diagnostic summary string.</returns>
    public string GetConditioningSummary()
    {
        StringBuilder sb = new();
        sb.AppendLine("Conditioned Associations:");
        foreach (ConditionedAssociation assoc in _associations.Values.OrderByDescending(a => a.AssociationStrength).Take(10))
        {
            string status = assoc.IsExtinct ? "extinct" : "active";
            sb.AppendLine($"  {assoc.Stimulus.Pattern} -> {assoc.Response.Name}: {assoc.AssociationStrength:P0} ({status})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Applies habituation to reduce response to a repeated stimulus.
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern to habituate to.</param>
    /// <param name="habituationRate">How much to reduce response (0.0 to 1.0).</param>
    public void ApplyHabituation(string stimulusType, double habituationRate = 0.1)
    {
        foreach (KeyValuePair<string, ConditionedAssociation> kvp in _associations)
        {
            if (kvp.Value.Stimulus.Pattern == stimulusType)
            {
                ConditionedAssociation habituated = kvp.Value with
                {
                    AssociationStrength = kvp.Value.AssociationStrength * (1.0 - habituationRate)
                };
                _associations[kvp.Key] = habituated;
            }
        }
    }

    /// <summary>
    /// Applies sensitization to increase response to a stimulus.
    /// </summary>
    /// <param name="stimulusType">The stimulus pattern to sensitize to.</param>
    /// <param name="sensitizationRate">How much to increase response (0.0 to 1.0).</param>
    public void ApplySensitization(string stimulusType, double sensitizationRate = 0.1)
    {
        foreach (KeyValuePair<string, ConditionedAssociation> kvp in _associations)
        {
            if (kvp.Value.Stimulus.Pattern == stimulusType)
            {
                ConditionedAssociation sensitized = kvp.Value with
                {
                    AssociationStrength = Math.Min(1.0, kvp.Value.AssociationStrength * (1.0 + sensitizationRate))
                };
                _associations[kvp.Key] = sensitized;
            }
        }
    }

    /// <summary>
    /// Gets the total association strength for all associations with the same stimulus.
    /// This is ΣV in the Rescorla-Wagner equation.
    /// </summary>
    /// <param name="stimulusId">The ID of the stimulus.</param>
    /// <returns>Sum of association strengths for all associations with this stimulus.</returns>
    private double GetTotalAssociationStrength(string stimulusId)
    {
        return _associations.Values
            .Where(a => a.StimulusId == stimulusId)
            .Sum(a => a.AssociationStrength);
    }

    /// <summary>
    /// Gets the current state for external access.
    /// </summary>
    /// <returns>The current consciousness state.</returns>
    public ConsciousnessState GetCurrentState() => _currentState;
}
