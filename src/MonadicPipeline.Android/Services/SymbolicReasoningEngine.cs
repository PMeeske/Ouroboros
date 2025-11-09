using System.Text;

namespace MonadicPipeline.Android.Services;

/// <summary>
/// Symbolic reasoning engine for logical inference and knowledge representation
/// </summary>
public class SymbolicReasoningEngine
{
    private readonly Dictionary<string, Fact> _knowledgeBase;
    private readonly List<Rule> _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicReasoningEngine"/> class.
    /// </summary>
    public SymbolicReasoningEngine()
    {
        _knowledgeBase = new Dictionary<string, Fact>();
        _rules = new List<Rule>();
        InitializeDefaultRules();
    }

    /// <summary>
    /// Add a fact to the knowledge base
    /// </summary>
    public void AddFact(string subject, string predicate, string obj)
    {
        var key = $"{subject}:{predicate}";
        _knowledgeBase[key] = new Fact(subject, predicate, obj);
    }

    /// <summary>
    /// Add a rule to the reasoning engine
    /// </summary>
    public void AddRule(Rule rule)
    {
        _rules.Add(rule);
    }

    /// <summary>
    /// Query the knowledge base
    /// </summary>
    public List<Fact> Query(string? subject = null, string? predicate = null)
    {
        return _knowledgeBase.Values
            .Where(f => (subject == null || f.Subject == subject) &&
                       (predicate == null || f.Predicate == predicate))
            .ToList();
    }

    /// <summary>
    /// Perform forward chaining inference
    /// </summary>
    public List<Fact> Infer()
    {
        var newFacts = new List<Fact>();
        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var rule in _rules)
            {
                var inferredFacts = rule.Apply(_knowledgeBase);
                
                foreach (var fact in inferredFacts)
                {
                    var key = $"{fact.Subject}:{fact.Predicate}";
                    
                    if (!_knowledgeBase.ContainsKey(key))
                    {
                        _knowledgeBase[key] = fact;
                        newFacts.Add(fact);
                        changed = true;
                    }
                }
            }
        }

        return newFacts;
    }

    /// <summary>
    /// Get all facts in the knowledge base
    /// </summary>
    public List<Fact> GetAllFacts()
    {
        return _knowledgeBase.Values.ToList();
    }

    /// <summary>
    /// Clear the knowledge base
    /// </summary>
    public void Clear()
    {
        _knowledgeBase.Clear();
    }

    /// <summary>
    /// Export knowledge base as text
    /// </summary>
    public string ExportKnowledgeBase()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Knowledge Base:");
        sb.AppendLine("═══════════════════════════════════════════");
        
        foreach (var fact in _knowledgeBase.Values.OrderBy(f => f.Subject))
        {
            sb.AppendLine($"{fact.Subject} {fact.Predicate} {fact.Object}");
        }
        
        sb.AppendLine();
        sb.AppendLine($"Total facts: {_knowledgeBase.Count}");
        sb.AppendLine($"Total rules: {_rules.Count}");
        
        return sb.ToString();
    }

    /// <summary>
    /// Perform logical unification
    /// </summary>
    public bool Unify(Pattern pattern, Fact fact, Dictionary<string, string> bindings)
    {
        if (pattern.Subject != null && pattern.Subject.StartsWith("?"))
        {
            var variable = pattern.Subject;
            if (bindings.ContainsKey(variable) && bindings[variable] != fact.Subject)
            {
                return false;
            }
            bindings[variable] = fact.Subject;
        }
        else if (pattern.Subject != null && pattern.Subject != fact.Subject)
        {
            return false;
        }

        if (pattern.Predicate != null && pattern.Predicate.StartsWith("?"))
        {
            var variable = pattern.Predicate;
            if (bindings.ContainsKey(variable) && bindings[variable] != fact.Predicate)
            {
                return false;
            }
            bindings[variable] = fact.Predicate;
        }
        else if (pattern.Predicate != null && pattern.Predicate != fact.Predicate)
        {
            return false;
        }

        if (pattern.Object != null && pattern.Object.StartsWith("?"))
        {
            var variable = pattern.Object;
            if (bindings.ContainsKey(variable) && bindings[variable] != fact.Object)
            {
                return false;
            }
            bindings[variable] = fact.Object;
        }
        else if (pattern.Object != null && pattern.Object != fact.Object)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluate a logical query with variables
    /// </summary>
    public List<Dictionary<string, string>> EvaluateQuery(List<Pattern> patterns)
    {
        var results = new List<Dictionary<string, string>>();
        var facts = _knowledgeBase.Values.ToList();

        EvaluateQueryRecursive(patterns, 0, new Dictionary<string, string>(), facts, results);

        return results;
    }

    private void EvaluateQueryRecursive(
        List<Pattern> patterns,
        int index,
        Dictionary<string, string> currentBindings,
        List<Fact> facts,
        List<Dictionary<string, string>> results)
    {
        if (index >= patterns.Count)
        {
            results.Add(new Dictionary<string, string>(currentBindings));
            return;
        }

        var pattern = patterns[index];

        foreach (var fact in facts)
        {
            var bindings = new Dictionary<string, string>(currentBindings);
            
            if (Unify(pattern, fact, bindings))
            {
                EvaluateQueryRecursive(patterns, index + 1, bindings, facts, results);
            }
        }
    }

    private void InitializeDefaultRules()
    {
        // Transitivity rule: if X is-a Y and Y is-a Z, then X is-a Z
        AddRule(new Rule
        {
            Name = "Transitivity",
            Conditions = new List<Pattern>
            {
                new Pattern("?x", "is-a", "?y"),
                new Pattern("?y", "is-a", "?z")
            },
            Conclusion = facts =>
            {
                var results = new List<Fact>();
                var xValue = facts.GetValueOrDefault("?x");
                var zValue = facts.GetValueOrDefault("?z");
                
                if (xValue != null && zValue != null)
                {
                    results.Add(new Fact(xValue, "is-a", zValue));
                }
                
                return results;
            }
        });

        // Property inheritance: if X is-a Y and Y has-property P, then X has-property P
        AddRule(new Rule
        {
            Name = "PropertyInheritance",
            Conditions = new List<Pattern>
            {
                new Pattern("?x", "is-a", "?y"),
                new Pattern("?y", "has-property", "?p")
            },
            Conclusion = facts =>
            {
                var results = new List<Fact>();
                var xValue = facts.GetValueOrDefault("?x");
                var pValue = facts.GetValueOrDefault("?p");
                
                if (xValue != null && pValue != null)
                {
                    results.Add(new Fact(xValue, "has-property", pValue));
                }
                
                return results;
            }
        });
    }
}

/// <summary>
/// Represents a fact in the knowledge base
/// </summary>
public class Fact
{
    /// <summary>
    /// Gets the subject of the fact
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Gets the predicate (relation) of the fact
    /// </summary>
    public string Predicate { get; }

    /// <summary>
    /// Gets the object of the fact
    /// </summary>
    public string Object { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Fact"/> class.
    /// </summary>
    public Fact(string subject, string predicate, string obj)
    {
        Subject = subject;
        Predicate = predicate;
        Object = obj;
    }

    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString() => $"{Subject} {Predicate} {Object}";
}

/// <summary>
/// Represents a pattern for matching facts
/// </summary>
public class Pattern
{
    /// <summary>
    /// Gets the subject pattern (can be a variable starting with ?)
    /// </summary>
    public string? Subject { get; }

    /// <summary>
    /// Gets the predicate pattern (can be a variable starting with ?)
    /// </summary>
    public string? Predicate { get; }

    /// <summary>
    /// Gets the object pattern (can be a variable starting with ?)
    /// </summary>
    public string? Object { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pattern"/> class.
    /// </summary>
    public Pattern(string? subject, string? predicate, string? obj)
    {
        Subject = subject;
        Predicate = predicate;
        Object = obj;
    }
}

/// <summary>
/// Represents a reasoning rule
/// </summary>
public class Rule
{
    /// <summary>
    /// Gets or sets the rule name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the conditions (patterns to match)
    /// </summary>
    public List<Pattern> Conditions { get; set; } = new();

    /// <summary>
    /// Gets or sets the conclusion function
    /// </summary>
    public Func<Dictionary<string, string>, List<Fact>> Conclusion { get; set; } = _ => new();

    /// <summary>
    /// Apply the rule to a knowledge base
    /// </summary>
    public List<Fact> Apply(Dictionary<string, Fact> knowledgeBase)
    {
        var results = new List<Fact>();
        var facts = knowledgeBase.Values.ToList();

        // Try to match all conditions
        var bindings = FindBindings(facts, 0, new Dictionary<string, string>());

        foreach (var binding in bindings)
        {
            var newFacts = Conclusion(binding);
            results.AddRange(newFacts);
        }

        return results;
    }

    private List<Dictionary<string, string>> FindBindings(
        List<Fact> facts,
        int conditionIndex,
        Dictionary<string, string> currentBindings)
    {
        if (conditionIndex >= Conditions.Count)
        {
            return new List<Dictionary<string, string>> { currentBindings };
        }

        var results = new List<Dictionary<string, string>>();
        var pattern = Conditions[conditionIndex];

        foreach (var fact in facts)
        {
            var bindings = new Dictionary<string, string>(currentBindings);

            if (TryMatch(pattern, fact, bindings))
            {
                var nextBindings = FindBindings(facts, conditionIndex + 1, bindings);
                results.AddRange(nextBindings);
            }
        }

        return results;
    }

    private bool TryMatch(Pattern pattern, Fact fact, Dictionary<string, string> bindings)
    {
        if (!MatchTerm(pattern.Subject, fact.Subject, bindings)) return false;
        if (!MatchTerm(pattern.Predicate, fact.Predicate, bindings)) return false;
        if (!MatchTerm(pattern.Object, fact.Object, bindings)) return false;

        return true;
    }

    private bool MatchTerm(string? pattern, string value, Dictionary<string, string> bindings)
    {
        if (pattern == null) return true;

        if (pattern.StartsWith("?"))
        {
            if (bindings.ContainsKey(pattern))
            {
                return bindings[pattern] == value;
            }
            else
            {
                bindings[pattern] = value;
                return true;
            }
        }

        return pattern == value;
    }
}
