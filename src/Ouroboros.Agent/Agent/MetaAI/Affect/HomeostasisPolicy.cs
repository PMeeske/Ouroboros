#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Homeostasis Policy Implementation
// Phase 3: Affective Dynamics - SLA regulation & corrective triggers
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI.Affect;

/// <summary>
/// Implementation of homeostasis policy management.
/// </summary>
public sealed class HomeostasisPolicy : IHomeostasisPolicy
{
    private readonly ConcurrentDictionary<Guid, HomeostasisRule> _rules = new();
    private readonly ConcurrentBag<PolicyViolation> _violations = new();
    private readonly ConcurrentBag<CorrectionResult> _corrections = new();
    private readonly ConcurrentDictionary<string, Func<PolicyViolation, IValenceMonitor, Task<CorrectionResult>>> _customHandlers = new();

    public HomeostasisPolicy()
    {
        // Add default rules
        InitializeDefaultRules();
    }

    public HomeostasisRule AddRule(
        string name,
        string description,
        SignalType targetSignal,
        double lowerBound,
        double upperBound,
        double targetValue,
        HomeostasisAction action,
        double priority = 1.0)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        var rule = new HomeostasisRule(
            Guid.NewGuid(),
            name,
            description,
            targetSignal,
            lowerBound,
            upperBound,
            targetValue,
            action,
            priority,
            true,
            DateTime.UtcNow);

        _rules[rule.Id] = rule;
        return rule;
    }

    public void UpdateRule(Guid ruleId, double? lowerBound = null, double? upperBound = null, double? targetValue = null)
    {
        if (_rules.TryGetValue(ruleId, out var existing))
        {
            var updated = existing with
            {
                LowerBound = lowerBound ?? existing.LowerBound,
                UpperBound = upperBound ?? existing.UpperBound,
                TargetValue = targetValue ?? existing.TargetValue
            };
            _rules[ruleId] = updated;
        }
    }

    public void SetRuleActive(Guid ruleId, bool isActive)
    {
        if (_rules.TryGetValue(ruleId, out var existing))
        {
            _rules[ruleId] = existing with { IsActive = isActive };
        }
    }

    public List<HomeostasisRule> GetRules(bool activeOnly = true)
    {
        var rules = _rules.Values.AsEnumerable();
        if (activeOnly)
        {
            rules = rules.Where(r => r.IsActive);
        }
        return rules.OrderByDescending(r => r.Priority).ToList();
    }

    public List<PolicyViolation> EvaluatePolicies(AffectiveState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var violations = new List<PolicyViolation>();

        foreach (var rule in GetRules(activeOnly: true))
        {
            double observedValue = GetSignalValue(state, rule.TargetSignal);
            
            PolicyViolation? violation = null;

            if (observedValue < rule.LowerBound)
            {
                double severity = (rule.LowerBound - observedValue) / Math.Max(0.01, rule.LowerBound);
                violation = new PolicyViolation(
                    rule.Id,
                    rule.Name,
                    rule.TargetSignal,
                    observedValue,
                    rule.LowerBound,
                    rule.UpperBound,
                    "BelowLowerBound",
                    rule.Action,
                    Math.Min(severity, 1.0),
                    DateTime.UtcNow);
            }
            else if (observedValue > rule.UpperBound)
            {
                double severity = (observedValue - rule.UpperBound) / Math.Max(0.01, 1.0 - rule.UpperBound);
                violation = new PolicyViolation(
                    rule.Id,
                    rule.Name,
                    rule.TargetSignal,
                    observedValue,
                    rule.LowerBound,
                    rule.UpperBound,
                    "AboveUpperBound",
                    rule.Action,
                    Math.Min(severity, 1.0),
                    DateTime.UtcNow);
            }

            if (violation != null)
            {
                _violations.Add(violation);
                violations.Add(violation);
            }
        }

        return violations.OrderByDescending(v => v.Severity).ToList();
    }

    public async Task<CorrectionResult> ApplyCorrectionAsync(
        PolicyViolation violation,
        IValenceMonitor monitor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(violation);
        ArgumentNullException.ThrowIfNull(monitor);

        var state = monitor.GetCurrentState();
        double valueBefore = GetSignalValue(state, violation.Signal);

        CorrectionResult result;

        switch (violation.RecommendedAction)
        {
            case HomeostasisAction.Log:
                result = new CorrectionResult(
                    violation.RuleId,
                    HomeostasisAction.Log,
                    true,
                    $"Logged violation: {violation.RuleName} - {violation.Signal} at {violation.ObservedValue:F3}",
                    valueBefore,
                    valueBefore,
                    DateTime.UtcNow);
                break;

            case HomeostasisAction.Alert:
                // In real implementation, this would send alerts
                result = new CorrectionResult(
                    violation.RuleId,
                    HomeostasisAction.Alert,
                    true,
                    $"Alert triggered for {violation.RuleName}: {violation.ViolationType}",
                    valueBefore,
                    valueBefore,
                    DateTime.UtcNow);
                break;

            case HomeostasisAction.Throttle:
                // Record negative stress signals to reduce stress
                for (int i = 0; i < 5; i++)
                {
                    monitor.RecordSignal("homeostasis_throttle", -0.2, violation.Signal);
                    await Task.Delay(10, ct);
                }
                var throttledState = monitor.GetCurrentState();
                double valueAfterThrottle = GetSignalValue(throttledState, violation.Signal);
                result = new CorrectionResult(
                    violation.RuleId,
                    HomeostasisAction.Throttle,
                    valueAfterThrottle < valueBefore,
                    $"Throttled {violation.Signal}: {valueBefore:F3} -> {valueAfterThrottle:F3}",
                    valueBefore,
                    valueAfterThrottle,
                    DateTime.UtcNow);
                break;

            case HomeostasisAction.Boost:
                // Record positive signals to boost
                for (int i = 0; i < 5; i++)
                {
                    monitor.RecordSignal("homeostasis_boost", 0.2, violation.Signal);
                    await Task.Delay(10, ct);
                }
                var boostedState = monitor.GetCurrentState();
                double valueAfterBoost = GetSignalValue(boostedState, violation.Signal);
                result = new CorrectionResult(
                    violation.RuleId,
                    HomeostasisAction.Boost,
                    valueAfterBoost > valueBefore,
                    $"Boosted {violation.Signal}: {valueBefore:F3} -> {valueAfterBoost:F3}",
                    valueBefore,
                    valueAfterBoost,
                    DateTime.UtcNow);
                break;

            case HomeostasisAction.Pause:
                result = new CorrectionResult(
                    violation.RuleId,
                    HomeostasisAction.Pause,
                    true,
                    $"Paused operations due to {violation.RuleName} violation",
                    valueBefore,
                    valueBefore,
                    DateTime.UtcNow);
                break;

            case HomeostasisAction.Reset:
                monitor.Reset();
                var resetState = monitor.GetCurrentState();
                double valueAfterReset = GetSignalValue(resetState, violation.Signal);
                result = new CorrectionResult(
                    violation.RuleId,
                    HomeostasisAction.Reset,
                    true,
                    $"Reset affective state to baseline",
                    valueBefore,
                    valueAfterReset,
                    DateTime.UtcNow);
                break;

            case HomeostasisAction.Custom:
                if (_rules.TryGetValue(violation.RuleId, out var rule) &&
                    _customHandlers.TryGetValue(rule.Name, out var handler))
                {
                    result = await handler(violation, monitor);
                }
                else
                {
                    result = new CorrectionResult(
                        violation.RuleId,
                        HomeostasisAction.Custom,
                        false,
                        "No custom handler registered for this rule",
                        valueBefore,
                        valueBefore,
                        DateTime.UtcNow);
                }
                break;

            default:
                result = new CorrectionResult(
                    violation.RuleId,
                    violation.RecommendedAction,
                    false,
                    $"Unknown action: {violation.RecommendedAction}",
                    valueBefore,
                    valueBefore,
                    DateTime.UtcNow);
                break;
        }

        _corrections.Add(result);
        return result;
    }

    public List<PolicyViolation> GetViolationHistory(int count = 50)
    {
        return _violations
            .OrderByDescending(v => v.DetectedAt)
            .Take(count)
            .ToList();
    }

    public List<CorrectionResult> GetCorrectionHistory(int count = 50)
    {
        return _corrections
            .OrderByDescending(c => c.AppliedAt)
            .Take(count)
            .ToList();
    }

    public void RegisterCustomHandler(string name, Func<PolicyViolation, IValenceMonitor, Task<CorrectionResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(handler);
        _customHandlers[name] = handler;
    }

    public PolicyHealthSummary GetHealthSummary()
    {
        var allViolations = _violations.ToList();
        var allCorrections = _corrections.ToList();
        var recentCutoff = DateTime.UtcNow.AddHours(-24);

        var violationsBySignal = allViolations
            .GroupBy(v => v.Signal)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PolicyHealthSummary(
            _rules.Count,
            _rules.Values.Count(r => r.IsActive),
            allViolations.Count,
            allViolations.Count(v => v.DetectedAt >= recentCutoff),
            allCorrections.Count,
            allCorrections.Count(c => c.Success),
            allCorrections.Count > 0
                ? (double)allCorrections.Count(c => c.Success) / allCorrections.Count
                : 1.0,
            violationsBySignal);
    }

    private static double GetSignalValue(AffectiveState state, SignalType signal)
    {
        return signal switch
        {
            SignalType.Stress => state.Stress,
            SignalType.Confidence => state.Confidence,
            SignalType.Curiosity => state.Curiosity,
            SignalType.Valence => state.Valence,
            SignalType.Arousal => state.Arousal,
            _ => 0.0
        };
    }

    private void InitializeDefaultRules()
    {
        // Default stress management rule
        AddRule(
            "MaxStress",
            "Prevents stress from exceeding critical threshold",
            SignalType.Stress,
            0.0,
            0.8,
            0.3,
            HomeostasisAction.Throttle,
            priority: 1.0);

        // Default confidence management rule
        AddRule(
            "MinConfidence",
            "Ensures confidence doesn't fall too low",
            SignalType.Confidence,
            0.2,
            1.0,
            0.5,
            HomeostasisAction.Boost,
            priority: 0.8);

        // Default curiosity management rule
        AddRule(
            "CuriosityBalance",
            "Maintains healthy curiosity levels",
            SignalType.Curiosity,
            0.1,
            0.9,
            0.4,
            HomeostasisAction.Log,
            priority: 0.5);
    }
}
