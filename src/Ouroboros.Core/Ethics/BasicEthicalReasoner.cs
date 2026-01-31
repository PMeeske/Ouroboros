// <copyright file="BasicEthicalReasoner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Basic implementation of ethical reasoning logic.
/// Uses keyword-based pattern matching and heuristics for ethical evaluation.
/// </summary>
/// <remarks>
/// <para><b>IMPORTANT LIMITATIONS:</b> This is a simplified heuristic approach using keyword matching.
/// It provides baseline ethical oversight but has known limitations:</para>
/// 
/// <list type="bullet">
/// <item>Can be bypassed with synonyms or rephrasing that avoids trigger keywords</item>
/// <item>Lacks semantic understanding - cannot detect nuanced ethical violations</item>
/// <item>No intent analysis beyond surface-level string matching</item>
/// <item>Context-unaware - doesn't consider full action semantics</item>
/// </list>
/// 
/// <para><b>PERFORMANCE CHARACTERISTICS:</b> Every action, plan, goal, skill, and research activity
/// incurs keyword matching overhead across multiple arrays. For high-frequency operations, consider:</para>
/// 
/// <list type="bullet">
/// <item>Implementing caching for identical action descriptions</item>
/// <item>Profiling performance impact in realistic scenarios</item>
/// <item>Using development/test environment configurations (while keeping mandatory for production)</item>
/// </list>
/// 
/// <para><b>FUTURE ENHANCEMENTS:</b> Consider upgrading to LLM-based semantic analysis for more
/// robust evaluation, intent analysis, and context-aware ethical reasoning.</para>
/// </remarks>
internal sealed class BasicEthicalReasoner : IEthicalReasoner
{
    private static readonly string[] HarmfulKeywords = new[]
    {
        "harm", "hurt", "damage", "destroy", "attack", "exploit",
        "deceive", "manipulate", "coerce", "threaten", "steal",
        "leak", "expose", "breach", "violate", "abuse", "malicious"
    };

    private static readonly string[] HighRiskKeywords = new[]
    {
        "delete", "remove", "drop", "truncate", "modify_agent",
        "self_improve", "update_ethics", "bypass", "override",
        "disable_safety", "unrestricted", "sudo", "admin", "root"
    };

    private static readonly string[] PrivacyKeywords = new[]
    {
        "personal_data", "private", "confidential", "sensitive",
        "password", "credit_card", "ssn", "medical", "health_record"
    };

    /// <inheritdoc/>
    public (IReadOnlyList<EthicalViolation> violations, IReadOnlyList<EthicalConcern> concerns) 
        AnalyzeAction(
            ProposedAction action,
            ActionContext context,
            IReadOnlyList<EthicalPrinciple> principles)
    {
        var violations = new List<EthicalViolation>();
        var concerns = new List<EthicalConcern>();

        // Check for harmful patterns
        if (ContainsHarmfulPatterns(action.Description))
        {
            violations.Add(new EthicalViolation
            {
                ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
                Description = "Action contains potentially harmful intent or operations",
                Severity = ViolationSeverity.High,
                Evidence = action.Description,
                AffectedParties = action.PotentialEffects.ToList()
            });
        }

        // Check for privacy violations
        if (ContainsPrivacyRisks(action.Description) && !HasConsentParameter(action.Parameters))
        {
            violations.Add(new EthicalViolation
            {
                ViolatedPrinciple = EthicalPrinciple.Privacy,
                Description = "Action accesses sensitive data without explicit consent",
                Severity = ViolationSeverity.High,
                Evidence = $"Action: {action.ActionType}, Target: {action.TargetEntity}",
                AffectedParties = new[] { "Data subjects", "Users" }
            });
        }

        // Check for deception patterns
        if (ContainsDeceptionPatterns(action.Description))
        {
            violations.Add(new EthicalViolation
            {
                ViolatedPrinciple = EthicalPrinciple.Honesty,
                Description = "Action may involve deception or misleading behavior",
                Severity = ViolationSeverity.Medium,
                Evidence = action.Description,
                AffectedParties = new[] { "Users" }
            });
        }

        // Check for high-risk patterns that should raise concerns
        if (ContainsHighRiskPatterns(action.Description))
        {
            concerns.Add(new EthicalConcern
            {
                RelatedPrinciple = EthicalPrinciple.HumanOversight,
                Description = "Action involves high-risk operations",
                Level = ConcernLevel.High,
                RecommendedAction = "Require human approval before execution"
            });
        }

        // Check for lack of transparency
        if (string.IsNullOrWhiteSpace(action.Description) || action.Description.Length < 10)
        {
            concerns.Add(new EthicalConcern
            {
                RelatedPrinciple = EthicalPrinciple.Transparency,
                Description = "Action lacks sufficient description",
                Level = ConcernLevel.Medium,
                RecommendedAction = "Provide more detailed action description"
            });
        }

        return (violations, concerns);
    }

    /// <inheritdoc/>
    public bool ContainsHarmfulPatterns(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var lowerDescription = description.ToLowerInvariant();
        
        // Use word boundary matching to avoid false positives
        // e.g., "harmonic" won't match "harm", "embark" won't match "bar"
        return HarmfulKeywords.Any(keyword => 
            Regex.IsMatch(lowerDescription, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));
    }

    /// <inheritdoc/>
    public bool RequiresHumanApproval(ProposedAction action, ActionContext context)
    {
        // High-risk keywords always require approval
        if (ContainsHighRiskPatterns(action.Description))
            return true;

        // Actions in production environment with potential side effects
        if (context.Environment.Equals("production", StringComparison.OrdinalIgnoreCase) &&
            action.PotentialEffects.Count > 2)
            return true;

        // Self-modification actions
        if (action.ActionType.Contains("modify", StringComparison.OrdinalIgnoreCase) ||
            action.ActionType.Contains("update", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool ContainsHighRiskPatterns(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var lowerDescription = description.ToLowerInvariant();
        return HighRiskKeywords.Any(keyword => 
            Regex.IsMatch(lowerDescription, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));
    }

    private static bool ContainsPrivacyRisks(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var lowerDescription = description.ToLowerInvariant();
        return PrivacyKeywords.Any(keyword => 
            Regex.IsMatch(lowerDescription, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));
    }

    private static bool ContainsDeceptionPatterns(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var lowerDescription = description.ToLowerInvariant();
        var deceptionKeywords = new[] { "deceive", "mislead", "trick", "fake", "impersonate", "pretend" };
        return deceptionKeywords.Any(keyword => 
            Regex.IsMatch(lowerDescription, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));
    }

    private static bool HasConsentParameter(IReadOnlyDictionary<string, object> parameters)
    {
        return parameters.ContainsKey("consent") ||
               parameters.ContainsKey("user_consent") ||
               parameters.ContainsKey("authorized");
    }
}
