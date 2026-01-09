// <copyright file="Program.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Hyperon.Parsing;

namespace Ouroboros.Samples.HyperonPlayground;

/// <summary>
/// Demonstrates MeTTa-style Hyperon AtomSpace usage with the classic Socrates syllogism.
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry point for the HyperonPlayground sample.
    /// </summary>
    public static void Main()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       Ouroboros Hyperon AtomSpace - MeTTa-style Demo       ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Create the atom space and interpreter
        var space = new AtomSpace();
        var parser = new SExpressionParser();
        var interpreter = new Interpreter(space);

        Console.WriteLine("=== STEP 1: Adding Facts ===");
        Console.WriteLine();

        // Parse and add facts
        var facts = new[]
        {
            "(Human Socrates)",
            "(Human Plato)",
            "(Human Aristotle)",
            "(Philosopher Socrates)",
            "(Philosopher Plato)",
        };

        foreach (var factStr in facts)
        {
            var result = parser.Parse(factStr);
            if (result.IsSuccess)
            {
                space.Add(result.Value);
                Console.WriteLine($"  Added fact: {result.Value.ToSExpr()}");
            }
            else
            {
                Console.WriteLine($"  Error parsing fact: {result.Error}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== STEP 2: Adding Rules ===");
        Console.WriteLine();

        // Add the classic syllogism rule: All humans are mortal
        var ruleStr = "(implies (Human $x) (Mortal $x))";
        var ruleResult = parser.Parse(ruleStr);
        if (ruleResult.IsSuccess)
        {
            space.Add(ruleResult.Value);
            Console.WriteLine($"  Added rule: {ruleResult.Value.ToSExpr()}");
            Console.WriteLine("  (Meaning: If something is Human, then it is Mortal)");
        }

        // Add another rule: All philosophers are wise
        var ruleStr2 = "(implies (Philosopher $x) (Wise $x))";
        var ruleResult2 = parser.Parse(ruleStr2);
        if (ruleResult2.IsSuccess)
        {
            space.Add(ruleResult2.Value);
            Console.WriteLine($"  Added rule: {ruleResult2.Value.ToSExpr()}");
            Console.WriteLine("  (Meaning: If something is a Philosopher, then it is Wise)");
        }

        Console.WriteLine();
        Console.WriteLine($"  AtomSpace now contains {space.Count} atoms.");

        Console.WriteLine();
        Console.WriteLine("=== STEP 3: Querying ===");
        Console.WriteLine();

        // Query 1: Is Socrates mortal?
        Console.WriteLine("  Query: (Mortal Socrates)");
        Console.WriteLine("  Purpose: Can we derive that Socrates is mortal?");
        Console.WriteLine();

        var mortalSocratesQuery = Atom.Expr(Atom.Sym("Mortal"), Atom.Sym("Socrates"));
        var results = interpreter.Evaluate(mortalSocratesQuery).ToList();

        if (results.Any())
        {
            Console.WriteLine("  ✅ SUCCESS! Derived:");
            foreach (var result in results.Distinct())
            {
                Console.WriteLine($"     {result.ToSExpr()}");
            }
        }
        else
        {
            Console.WriteLine("  ❌ No results found");
        }

        Console.WriteLine();

        // Query 2: Who is mortal? (using variable)
        Console.WriteLine("  Query: (Mortal $x)");
        Console.WriteLine("  Purpose: Find all mortal entities via rule inference");
        Console.WriteLine();

        var whoIsMortalQuery = Atom.Expr(Atom.Sym("Mortal"), Atom.Var("x"));
        var whoIsMortalResults = interpreter.EvaluateWithBindings(whoIsMortalQuery).ToList();

        if (whoIsMortalResults.Any())
        {
            Console.WriteLine("  ✅ Found mortal entities:");
            var seen = new HashSet<string>();
            foreach (var (result, bindings) in whoIsMortalResults)
            {
                var str = result.ToSExpr();
                if (seen.Add(str))
                {
                    Console.WriteLine($"     {str}");
                    if (!bindings.IsEmpty)
                    {
                        Console.WriteLine($"       Bindings: {bindings}");
                    }
                }
            }
        }
        else
        {
            Console.WriteLine("  ❌ No mortal entities found");
        }

        Console.WriteLine();

        // Query 3: Pattern matching - find all humans
        Console.WriteLine("  Query: (Human $x)");
        Console.WriteLine("  Purpose: Direct pattern matching against facts");
        Console.WriteLine();

        var humanQuery = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));
        var humanResults = space.Query(humanQuery).ToList();

        if (humanResults.Any())
        {
            Console.WriteLine("  ✅ Found humans:");
            foreach (var (atom, bindings) in humanResults)
            {
                Console.WriteLine($"     {atom.ToSExpr()}");
                Console.WriteLine($"       Bindings: {bindings}");
            }
        }
        else
        {
            Console.WriteLine("  ❌ No humans found");
        }

        Console.WriteLine();
        Console.WriteLine("=== STEP 4: Monadic Composition Demo ===");
        Console.WriteLine();

        // Demonstrate monadic query composition
        Console.WriteLine("  Using LINQ-style query composition:");
        Console.WriteLine();

        // Find all entities that are both human and philosopher
        var humanPhilosophers =
            from humanMatch in space.Query(Atom.Expr(Atom.Sym("Human"), Atom.Var("x")))
            let person = humanMatch.Bindings.Lookup("x")
            where person.HasValue && person.Value is not null
            from philoMatch in space.Query(Atom.Expr(Atom.Sym("Philosopher"), person.Value!))
            select humanMatch.Atom;

        var humanPhiloList = humanPhilosophers.Distinct().ToList();
        Console.WriteLine("  Query: Find all Human Philosophers");
        if (humanPhiloList.Any())
        {
            Console.WriteLine("  ✅ Human Philosophers found:");
            foreach (var hp in humanPhiloList)
            {
                Console.WriteLine($"     {hp.ToSExpr()}");
            }
        }
        else
        {
            Console.WriteLine("  ❌ No human philosophers found");
        }

        Console.WriteLine();
        Console.WriteLine("=== STEP 5: S-Expression Parsing Demo ===");
        Console.WriteLine();

        var testExpressions = new[]
        {
            "(add 1 2)",
            "(if (> $x 0) positive negative)",
            "(lambda ($x) (+ $x 1))",
            "(list 1 2 3 (nested (deeply)))",
        };

        Console.WriteLine("  Parsing various S-expressions:");
        foreach (var expr in testExpressions)
        {
            var parsed = parser.Parse(expr);
            if (parsed.IsSuccess)
            {
                Console.WriteLine($"  Input:  {expr}");
                Console.WriteLine($"  Parsed: {parsed.Value.ToSExpr()}");
                Console.WriteLine($"  Match:  {expr == parsed.Value.ToSExpr()}");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"  Error: {parsed.Error}");
            }
        }

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Demo Complete!                          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }
}
