# Ouroboros Hyperon - MeTTa-Style AtomSpace for C#

This module brings MeTTa-style symbolic AI concepts to the Ouroboros ecosystem, providing a type-safe, performant, and composable atom space with unification-based pattern matching.

## Overview

The Hyperon module implements:

- **Atoms**: Immutable symbolic building blocks (Symbols, Variables, Expressions)
- **AtomSpace**: Thread-safe storage with indexed queries
- **Unification**: Structural pattern matching with variable binding
- **Grounded Operations**: Bridge between abstract atoms and executable code
- **Interpreter**: Forward-chaining inference engine
- **S-Expression Parser**: MeTTa-compatible syntax parsing

## Quick Start

```csharp
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Hyperon.Parsing;

// Create the space and tools
var space = new AtomSpace();
var parser = new SExpressionParser();
var interpreter = new Interpreter(space);

// Add facts
space.Add(parser.Parse("(Human Socrates)").Value);
space.Add(parser.Parse("(Human Plato)").Value);

// Add a rule: All humans are mortal
space.Add(parser.Parse("(implies (Human $x) (Mortal $x))").Value);

// Query: Is Socrates mortal?
var query = parser.Parse("(Mortal Socrates)").Value;
var isMortal = interpreter.Succeeds(query);
Console.WriteLine($"Is Socrates mortal? {isMortal}"); // true!

// Query with variables: Who is mortal?
var whoQuery = parser.Parse("(Mortal $x)").Value;
foreach (var (result, bindings) in interpreter.EvaluateWithBindings(whoQuery))
{
    Console.WriteLine($"Found: {result.ToSExpr()}, Bindings: {bindings}");
}
```

## Core Concepts

### Atoms

Atoms are the fundamental units of knowledge representation:

```csharp
// Symbols: Named constants
var human = Atom.Sym("Human");
var socrates = Atom.Sym("Socrates");

// Variables: Placeholders for pattern matching (prefixed with $)
var x = Atom.Var("x");

// Expressions: S-expressions (nested lists)
var fact = Atom.Expr(human, socrates);  // (Human Socrates)
var rule = Atom.Expr(
    Atom.Sym("implies"),
    Atom.Expr(Atom.Sym("Human"), Atom.Var("x")),
    Atom.Expr(Atom.Sym("Mortal"), Atom.Var("x"))
);  // (implies (Human $x) (Mortal $x))
```

All atoms are **immutable records** with value-based equality:

```csharp
var a = Atom.Sym("Human");
var b = Atom.Sym("Human");
Console.WriteLine(a == b);  // true
Console.WriteLine(a.Equals(b));  // true
```

### AtomSpace

The AtomSpace is a thread-safe storage for atoms with pattern matching queries:

```csharp
var space = new AtomSpace();

// Add atoms
space.Add(Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates")));
space.AddRange(new[] { fact1, fact2, fact3 });

// Check membership
bool exists = space.Contains(fact);

// Pattern query with unification
var pattern = Atom.Expr(Atom.Sym("Human"), Atom.Var("person"));
foreach (var (atom, bindings) in space.Query(pattern))
{
    var person = bindings.Lookup("person");
    Console.WriteLine($"Found: {person.Value.ToSExpr()}");
}
```

### Unification

Unification finds a substitution that makes two atoms equal:

```csharp
var pattern = Atom.Expr(Atom.Sym("Human"), Atom.Var("x"));
var target = Atom.Expr(Atom.Sym("Human"), Atom.Sym("Socrates"));

var subst = Unifier.Unify(pattern, target);
// subst contains: {$x -> Socrates}

var bound = subst.Apply(pattern);
// bound == (Human Socrates)
```

Key features:
- Variable binding: `$x` unifies with any atom
- Structural matching: Expressions match recursively
- Occurs check: Prevents infinite recursion (e.g., `$x = f($x)` fails)

### Substitutions

Substitutions track variable bindings and can be composed:

```csharp
var subst = Substitution.Of("x", Atom.Sym("Socrates"));

// Lookup bindings
var value = subst.Lookup("x");  // Some(Socrates)
var missing = subst.Lookup("y"); // None

// Apply to atoms
var template = Atom.Expr(Atom.Sym("Mortal"), Atom.Var("x"));
var result = subst.Apply(template);  // (Mortal Socrates)

// Compose substitutions
var subst2 = Substitution.Of("y", Atom.Sym("Plato"));
var combined = subst.Compose(subst2);  // {$x -> Socrates, $y -> Plato}
```

### Grounded Operations

Grounded operations connect abstract atoms to executable code:

```csharp
// Built-in operations
registry.Register("custom-op", (space, args) =>
{
    // args.Children[0] is the operation name
    // args.Children[1..] are the arguments
    var input = args.Children[1];
    // Return results as IEnumerable<Atom>
    yield return Atom.Sym("computed-result");
});

// Standard operations:
// - implies: Forward-chaining inference
// - equal: Equality check
// - not: Negation as failure
// - and: Logical conjunction
// - or: Logical disjunction
// - match: Pattern matching
// - assert: Add atom to space
// - retract: Remove atom from space
// - quote: Return atom without evaluation
```

### Interpreter

The interpreter evaluates queries against the atom space:

```csharp
var interpreter = new Interpreter(space);

// Simple evaluation
var results = interpreter.Evaluate(query);

// Get bindings
var resultsWithBindings = interpreter.EvaluateWithBindings(query);

// Check success
bool succeeds = interpreter.Succeeds(query);

// Get first result
var first = interpreter.EvaluateFirst(query);  // Option<Atom>
```

### Parsing

Parse MeTTa-style S-expressions:

```csharp
var parser = new SExpressionParser();

// Parse single expression
var result = parser.Parse("(implies (Human $x) (Mortal $x))");
if (result.IsSuccess)
{
    var atom = result.Value;
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}

// Parse multiple expressions
var atoms = parser.ParseMultiple("(Human Socrates) (Human Plato)");

// TryParse pattern
if (parser.TryParse(input, out var atom))
{
    // Use atom
}

// Syntax features:
// - Symbols: Human, Mortal, implies
// - Variables: $x, $person, $y
// - Expressions: (a b c), (nested (expressions))
// - Comments: ; line comments
// - Whitespace: flexible spacing and newlines
```

## Mapping to MeTTa Concepts

| MeTTa Concept | C# Implementation |
|---------------|-------------------|
| Atom | `abstract record Atom` |
| Symbol | `sealed record Symbol(string Name)` |
| Variable | `sealed record Variable(string Name)` |
| Expression | `sealed record Expression(ImmutableList<Atom> Children)` |
| AtomSpace | `class AtomSpace : IAtomSpace` |
| Pattern Matching | `Unifier.Unify(pattern, target)` |
| Bindings | `Substitution` class |
| grounded operation | `delegate GroundedOperation` |
| !import | Manual space population |
| ! (reduce) | `Interpreter.Evaluate()` |

## Monadic Composition

The Hyperon module integrates with Ouroboros's monadic architecture:

```csharp
// LINQ-style query composition
var humanPhilosophers =
    from humanMatch in space.Query(Atom.Expr(Atom.Sym("Human"), Atom.Var("x")))
    let person = humanMatch.Bindings.Lookup("x")
    where person.HasValue
    from philoMatch in space.Query(Atom.Expr(Atom.Sym("Philosopher"), person.Value))
    select humanMatch.Atom;

// Result<T> integration
var parseResult = parser.Parse("(Human Socrates)");
var evaluated = parseResult.Bind(atom => 
{
    var results = interpreter.Evaluate(atom).ToList();
    return results.Any() 
        ? Result<IList<Atom>>.Success(results)
        : Result<IList<Atom>>.Failure("No results");
});

// Option<T> for nullable results
var firstResult = interpreter.EvaluateFirst(query);
firstResult.Match(
    some: atom => Console.WriteLine($"Found: {atom}"),
    none: () => Console.WriteLine("Not found"));
```

## Thread Safety

The `AtomSpace` is fully thread-safe:

```csharp
// Safe concurrent access
Parallel.ForEach(atoms, atom => space.Add(atom));

// Safe concurrent queries
var results = atoms
    .AsParallel()
    .SelectMany(pattern => space.Query(pattern))
    .ToList();
```

## Performance Considerations

1. **Indexing**: Expressions are indexed by their head symbol for faster lookups
2. **Immutability**: All atoms are immutable, enabling safe sharing
3. **Lazy Evaluation**: Queries return `IEnumerable<T>` for lazy processing
4. **Structural Equality**: Record-based equality is optimized by the compiler

For large knowledge bases (100k+ atoms), consider:
- Pre-filtering candidates before unification
- Using batch operations (`AddRange`)
- Profiling hot query patterns

## Roadmap & Caveats

### Current Limitations

1. **Forward Chaining Only**: No backward chaining inference
2. **Single-Step Inference**: Chained rules require intermediate materialization
3. **No Probabilistic Weights**: All atoms have equal standing
4. **Memory-Only**: No persistence layer
5. **No Type System**: All atoms are untyped

### Future Enhancements

- [ ] Backward chaining inference
- [ ] Probabilistic weights/attention values
- [ ] Graph-based indexing (GIndex)
- [ ] Persistence to vector stores
- [ ] Type constraints on variables
- [ ] Parallel inference strategies
- [ ] Integration with neural embeddings

## Examples

### Classic Syllogism

```csharp
// All men are mortal. Socrates is a man. Therefore, Socrates is mortal.
space.Add(parser.Parse("(Man Socrates)").Value);
space.Add(parser.Parse("(implies (Man $x) (Mortal $x))").Value);

var isMortal = interpreter.Succeeds(parser.Parse("(Mortal Socrates)").Value);
// true
```

### Family Relationships

```csharp
// Facts
space.Add(parser.Parse("(parent Alice Bob)").Value);
space.Add(parser.Parse("(parent Bob Charlie)").Value);

// Grandparent rule
space.Add(parser.Parse("(implies (and (parent $x $y) (parent $y $z)) (grandparent $x $z))").Value);

// Note: "and" needs to be evaluated as grounded operation
// For this to work fully, you'd need a more sophisticated inference engine
```

### Type Classification

```csharp
space.Add(parser.Parse("(isa Dog Mammal)").Value);
space.Add(parser.Parse("(isa Mammal Animal)").Value);
space.Add(parser.Parse("(isa Fido Dog)").Value);

// Transitivity rule
space.Add(parser.Parse("(implies (and (isa $x $y) (isa $y $z)) (isa $x $z))").Value);
```

## API Reference

### Atom Types

- `Atom.Sym(string name)` - Create a symbol
- `Atom.Var(string name)` - Create a variable
- `Atom.Expr(params Atom[] children)` - Create an expression
- `atom.ToSExpr()` - Get S-expression string
- `atom.ContainsVariables()` - Check for variables

### AtomSpace

- `Add(Atom)` - Add an atom
- `AddRange(IEnumerable<Atom>)` - Add multiple atoms
- `Remove(Atom)` - Remove an atom
- `Contains(Atom)` - Check membership
- `Query(Atom pattern)` - Pattern matching query
- `All()` - Get all atoms
- `Count` - Number of atoms

### Unifier

- `Unify(pattern, target)` - Find unifying substitution
- `UnifyAll(pattern, atoms)` - Find all unifications
- `CanUnify(a, b)` - Check if unifiable

### Interpreter

- `Evaluate(query)` - Evaluate and return results
- `EvaluateWithBindings(query)` - Evaluate with substitutions
- `Succeeds(query)` - Check if query has results
- `EvaluateFirst(query)` - Get first result as Option

### Parser

- `Parse(string)` - Parse single expression
- `ParseMultiple(string)` - Parse multiple expressions
- `TryParse(string, out Atom?)` - Try-pattern parsing
