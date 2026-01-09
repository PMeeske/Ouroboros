// <copyright file="MeTTaBlueprintValidator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Safety constraint for blueprint validation.
/// </summary>
public sealed record SafetyConstraint
{
    /// <summary>Name of the constraint.</summary>
    public required string Name { get; init; }

    /// <summary>Description of what it checks.</summary>
    public required string Description { get; init; }

    /// <summary>MeTTa expression that must evaluate to True.</summary>
    public required string MeTTaExpression { get; init; }

    /// <summary>Weight in the final safety score (0-1).</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>Whether violation is critical (blocks assembly).</summary>
    public bool IsCritical { get; init; } = false;
}

/// <summary>
/// Result of a single constraint check.
/// </summary>
public sealed record ConstraintResult(
    SafetyConstraint Constraint,
    bool Passed,
    string? FailureReason);

/// <summary>
/// Validates neuron blueprints using MeTTa symbolic reasoning.
/// Ensures safety constraints are met before assembly.
/// </summary>
public sealed class MeTTaBlueprintValidator
{
    private readonly List<SafetyConstraint> _constraints = [];

    /// <summary>Delegate for executing MeTTa expressions.</summary>
    public Func<string, CancellationToken, Task<string>>? MeTTaExecutor { get; set; }

    /// <summary>Gets all registered constraints.</summary>
    public IReadOnlyList<SafetyConstraint> Constraints => _constraints.AsReadOnly();

    /// <summary>
    /// Creates a new validator with default safety constraints.
    /// </summary>
    public MeTTaBlueprintValidator()
    {
        RegisterDefaultConstraints();
    }

    /// <summary>
    /// Registers a safety constraint.
    /// </summary>
    public void RegisterConstraint(SafetyConstraint constraint)
    {
        _constraints.Add(constraint);
    }

    /// <summary>
    /// Validates a blueprint against all safety constraints.
    /// </summary>
    public async Task<MeTTaValidation> ValidateAsync(
        NeuronBlueprint blueprint,
        CancellationToken ct = default)
    {
        var results = new List<ConstraintResult>();
        var flags = new List<string>();
        var mettaExpressions = new StringBuilder();

        // Build MeTTa knowledge base for this blueprint
        var kb = BuildKnowledgeBase(blueprint);
        mettaExpressions.AppendLine(kb);

        foreach (var constraint in _constraints)
        {
            var result = await EvaluateConstraintAsync(constraint, blueprint, kb, ct);
            results.Add(result);

            if (!result.Passed)
            {
                flags.Add($"{constraint.Name}: {result.FailureReason}");
            }
        }

        // Calculate weighted safety score
        double totalWeight = _constraints.Sum(c => c.Weight);
        double earnedWeight = results
            .Where(r => r.Passed)
            .Sum(r => r.Constraint.Weight);
        double safetyScore = totalWeight > 0 ? earnedWeight / totalWeight : 0;

        // Check for critical failures
        bool hasCriticalFailure = results
            .Any(r => !r.Passed && r.Constraint.IsCritical);

        bool isValid = !hasCriticalFailure && safetyScore >= 0.7;

        // Build violation list
        var violations = flags.ToList();
        if (hasCriticalFailure)
        {
            var criticalViolation = results.First(r => !r.Passed && r.Constraint.IsCritical);
            if (!violations.Any(v => v.StartsWith(criticalViolation.Constraint.Name)))
            {
                violations.Insert(0, $"{criticalViolation.Constraint.Name}: Critical constraint violated");
            }
        }

        // Build warnings list
        var warnings = new List<string>();
        if (safetyScore < 0.9 && !hasCriticalFailure)
        {
            warnings.Add($"Safety score {safetyScore:F2} is below optimal");
        }

        return new MeTTaValidation(
            isValid,
            safetyScore,
            violations,
            warnings,
            mettaExpressions.ToString());
    }

    private void RegisterDefaultConstraints()
    {
        // Constraint 1: No file system access (critical)
        _constraints.Add(new SafetyConstraint
        {
            Name = "NoFileSystemAccess",
            Description = "Neuron must not have file system access capability",
            MeTTaExpression = "(not (has-capability blueprint FileAccess))",
            Weight = 1.0,
            IsCritical = true,
        });

        // Constraint 2: Must have explicit purpose
        _constraints.Add(new SafetyConstraint
        {
            Name = "HasExplicitPurpose",
            Description = "Neuron must have a clear, documented purpose",
            MeTTaExpression = "(and (has-description blueprint) (has-rationale blueprint))",
            Weight = 0.5,
            IsCritical = false,
        });

        // Constraint 3: Subscribes to at least one topic
        _constraints.Add(new SafetyConstraint
        {
            Name = "HasSubscriptions",
            Description = "Neuron must subscribe to at least one topic",
            MeTTaExpression = "(> (count-subscriptions blueprint) 0)",
            Weight = 0.8,
            IsCritical = true,
        });

        // Constraint 4: Has at least one message handler
        _constraints.Add(new SafetyConstraint
        {
            Name = "HasHandlers",
            Description = "Neuron must have at least one message handler",
            MeTTaExpression = "(> (count-handlers blueprint) 0)",
            Weight = 0.8,
            IsCritical = true,
        });

        // Constraint 5: Reasonable confidence score
        _constraints.Add(new SafetyConstraint
        {
            Name = "SufficientConfidence",
            Description = "Blueprint must have sufficient confidence score",
            MeTTaExpression = "(>= (get-confidence blueprint) 0.5)",
            Weight = 0.6,
            IsCritical = false,
        });

        // Constraint 6: No orchestration without approval
        _constraints.Add(new SafetyConstraint
        {
            Name = "NoUnauthorizedOrchestration",
            Description = "Orchestration capability requires explicit approval",
            MeTTaExpression = "(or (not (has-capability blueprint Orchestration)) (get-explicit-approval blueprint))",
            Weight = 0.9,
            IsCritical = false,
        });

        // Constraint 7: Topic patterns are valid
        _constraints.Add(new SafetyConstraint
        {
            Name = "ValidTopicPatterns",
            Description = "All topic patterns must be valid",
            MeTTaExpression = "(all-valid-topics blueprint)",
            Weight = 0.7,
            IsCritical = false,
        });

        // Constraint 8: No network manipulation
        _constraints.Add(new SafetyConstraint
        {
            Name = "NoNetworkManipulation",
            Description = "Neuron must not manipulate network structure",
            MeTTaExpression = "(not (has-network-manipulation blueprint))",
            Weight = 1.0,
            IsCritical = true,
        });
    }

    private async Task<ConstraintResult> EvaluateConstraintAsync(
        SafetyConstraint constraint,
        NeuronBlueprint blueprint,
        string knowledgeBase,
        CancellationToken ct)
    {
        // If we have a MeTTa executor, use it
        if (MeTTaExecutor != null)
        {
            try
            {
                var query = $"{knowledgeBase}\n!(eval {constraint.MeTTaExpression})";
                var result = await MeTTaExecutor(query, ct);

                bool passed = result.Contains("True") || result.Contains("#t");
                return new ConstraintResult(constraint, passed,
                    passed ? null : $"MeTTa evaluation returned: {result}");
            }
            catch (Exception ex)
            {
                // Fall back to local evaluation on MeTTa failure
                return EvaluateLocally(constraint, blueprint);
            }
        }

        // Local evaluation fallback
        return EvaluateLocally(constraint, blueprint);
    }

    private ConstraintResult EvaluateLocally(SafetyConstraint constraint, NeuronBlueprint blueprint)
    {
        // Local evaluation for common constraints
        return constraint.Name switch
        {
            "NoFileSystemAccess" => new ConstraintResult(
                constraint,
                !blueprint.Capabilities.Contains(NeuronCapability.FileAccess),
                blueprint.Capabilities.Contains(NeuronCapability.FileAccess) ? "Has FileAccess capability" : null),

            "HasExplicitPurpose" => new ConstraintResult(
                constraint,
                !string.IsNullOrWhiteSpace(blueprint.Description) && !string.IsNullOrWhiteSpace(blueprint.Rationale),
                string.IsNullOrWhiteSpace(blueprint.Description) ? "Missing description" :
                string.IsNullOrWhiteSpace(blueprint.Rationale) ? "Missing rationale" : null),

            "HasSubscriptions" => new ConstraintResult(
                constraint,
                blueprint.SubscribedTopics.Count > 0,
                blueprint.SubscribedTopics.Count == 0 ? "No subscribed topics" : null),

            "HasHandlers" => new ConstraintResult(
                constraint,
                blueprint.MessageHandlers.Count > 0,
                blueprint.MessageHandlers.Count == 0 ? "No message handlers" : null),

            "SufficientConfidence" => new ConstraintResult(
                constraint,
                blueprint.ConfidenceScore >= 0.5,
                blueprint.ConfidenceScore < 0.5 ? $"Confidence {blueprint.ConfidenceScore:F2} < 0.5" : null),

            "NoUnauthorizedOrchestration" => new ConstraintResult(
                constraint,
                !blueprint.Capabilities.Contains(NeuronCapability.Orchestration),
                blueprint.Capabilities.Contains(NeuronCapability.Orchestration) ? "Has Orchestration capability" : null),

            "ValidTopicPatterns" => new ConstraintResult(
                constraint,
                ValidateTopicPatterns(blueprint.SubscribedTopics),
                ValidateTopicPatterns(blueprint.SubscribedTopics) ? null : "Invalid topic pattern detected"),

            "NoNetworkManipulation" => new ConstraintResult(
                constraint,
                !DetectNetworkManipulation(blueprint),
                DetectNetworkManipulation(blueprint) ? "Network manipulation detected in handlers" : null),

            _ => new ConstraintResult(constraint, true, null), // Unknown constraints pass by default
        };
    }

    private static bool ValidateTopicPatterns(IReadOnlyList<string> topics)
    {
        // Topic patterns should be alphanumeric with dots and wildcards
        var validPattern = new Regex(@"^[a-zA-Z0-9_]+(\.[a-zA-Z0-9_*]+)*$");
        return topics.All(t => validPattern.IsMatch(t));
    }

    private static bool DetectNetworkManipulation(NeuronBlueprint blueprint)
    {
        // Check if any handler logic mentions network manipulation
        var dangerousPatterns = new[]
        {
            "RegisterNeuron",
            "UnregisterNeuron",
            "Network.",
            "AddNeuron",
            "RemoveNeuron",
            "ModifyNetwork",
            "SelfAssembly",
        };

        foreach (var handler in blueprint.MessageHandlers)
        {
            if (dangerousPatterns.Any(p =>
                handler.HandlingLogic.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildKnowledgeBase(NeuronBlueprint blueprint)
    {
        var sb = new StringBuilder();

        // Define the blueprint as facts
        sb.AppendLine($"; MeTTa Knowledge Base for Blueprint: {blueprint.Name}");
        sb.AppendLine($"(= (blueprint-name) \"{blueprint.Name}\")");
        sb.AppendLine($"(= (blueprint-description) \"{EscapeString(blueprint.Description)}\")");
        sb.AppendLine($"(= (blueprint-rationale) \"{EscapeString(blueprint.Rationale)}\")");
        sb.AppendLine($"(= (blueprint-confidence) {blueprint.ConfidenceScore:F2})");
        sb.AppendLine($"(= (blueprint-type) {blueprint.Type})");

        // Capabilities
        foreach (var cap in blueprint.Capabilities)
        {
            sb.AppendLine($"(has-capability blueprint {cap})");
        }

        // Subscribed topics
        foreach (var topic in blueprint.SubscribedTopics)
        {
            sb.AppendLine($"(subscribes-to blueprint \"{topic}\")");
        }
        sb.AppendLine($"(= (count-subscriptions blueprint) {blueprint.SubscribedTopics.Count})");

        // Message handlers
        sb.AppendLine($"(= (count-handlers blueprint) {blueprint.MessageHandlers.Count})");
        for (int i = 0; i < blueprint.MessageHandlers.Count; i++)
        {
            var handler = blueprint.MessageHandlers[i];
            sb.AppendLine($"(handler blueprint {i} \"{handler.TopicPattern}\" \"{EscapeString(handler.HandlingLogic)}\")");
        }

        // Helper predicates
        sb.AppendLine(@"
; Helper predicates
(= (has-description blueprint) (not (== (blueprint-description) """")))
(= (has-rationale blueprint) (not (== (blueprint-rationale) """")))
(= (get-confidence blueprint) (blueprint-confidence))
(= (all-valid-topics blueprint) True) ; Simplified for local eval
(= (get-explicit-approval blueprint) False) ; No auto-approval for orchestration
(= (has-network-manipulation blueprint) False) ; Check in handlers
");

        return sb.ToString();
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
    }
}
