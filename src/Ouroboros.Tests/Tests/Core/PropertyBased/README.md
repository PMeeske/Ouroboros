# Property-Based Testing for Monad Laws

This directory contains property-based tests using FsCheck to verify that monad laws hold for arbitrary inputs.

## Overview

Property-based testing generates hundreds or thousands of random test inputs to verify that mathematical properties hold universally, rather than just for a few hand-picked values. This provides much stronger guarantees about correctness.

## Test Files

### OptionPropertyTests.cs
Tests for the `Option<T>` monad:
- **Left Identity**: `return a >>= f ≡ f a`
- **Right Identity**: `m >>= return ≡ m`
- **Associativity**: `(m >>= f) >>= g ≡ m >>= (x -> f x >>= g)`
- **Functor Identity**: `fmap id ≡ id`
- **Functor Composition**: `fmap (g . f) ≡ fmap g . fmap f`
- **Map-Bind Relationship**: `fmap f ≡ (>>= return . f)`

Each test runs 1000 times with randomly generated inputs, including:
- Various integer values (positive, negative, zero, edge cases)
- Both `Some` and `None` values
- Partial functions that may return `None`

### ResultPropertyTests.cs
Tests for the `Result<TValue, TError>` monad:
- **Left Identity** with success and failure cases
- **Right Identity** with arbitrary `Result` values
- **Associativity** including failure propagation
- **Map preservation** of Success/Failure status
- **Functor laws** for Result
- **Map-Bind relationship**

Each test verifies that error handling behaves correctly and that failures propagate as expected.

### KleisliPropertyTests.cs
Tests for Kleisli arrow composition (`KleisliResult<TIn, TOut, TError>`):
- **Associativity**: `(f >=> g) >=> h ≡ f >=> (g >=> h)`
- **Left Identity**: `id >=> f ≡ f`
- **Right Identity**: `f >=> id ≡ f`

Tests verify both success paths and failure cases to ensure proper error propagation in composed async arrows.

### PipelinePropertyTests.cs
Tests for `Pipeline<TIn, TOut>`:
- **Map identity preservation**
- **Map composition law**
- **Bind associativity**
- **Bind left and right identity**
- **Then associativity** for Kleisli composition
- **MapAsync identity**
- **Pure-Map relationship**
- **Map-Then relationship**

Tests verify that pipeline composition maintains functor and monad properties.

## Running the Tests

Run all property-based tests:
```bash
dotnet test --filter "Category=Property"
```

Run specific test class:
```bash
dotnet test --filter "FullyQualifiedName~OptionPropertyTests"
```

## FsCheck Configuration

- Each test runs **1000 iterations** by default (`MaxTest = 1000`)
- Tests use FsCheck's built-in generators for primitive types
- Custom generators can be added in `Generators.cs` if needed

## Why Property-Based Testing?

**Traditional Unit Tests:**
```csharp
[Fact]
public void Option_LeftIdentity_Holds()
{
    var a = 42;  // Fixed input!
    Func<int, Option<string>> f = x => Option<string>.Some(x.ToString());
    var left = Option<int>.Some(a).Bind(f);
    var right = f(a);
    left.Value.Should().Be(right.Value);
}
```

**Property-Based Tests:**
```csharp
[Property(MaxTest = 1000)]
public bool Option_LeftIdentity_HoldsForAllInts(int a)
{
    // FsCheck generates 1000 random values for 'a'
    Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
    var left = Option<int>.Some(a).Bind(f);
    var right = f(a);
    return left.HasValue == right.HasValue &&
           (!left.HasValue || left.Value == right.Value);
}
```

The property test automatically checks the law with values like:
- `-10000`, `-5`, `-1`, `0`, `1`, `5`, `42`, `1000`, `int.MaxValue`, etc.
- Including edge cases that humans might not think to test

## Benefits

1. **Stronger Guarantees**: Proven for thousands of inputs vs. a handful
2. **Edge Case Discovery**: FsCheck finds corner cases automatically
3. **Mathematical Correctness**: Verifies abstract laws, not just examples
4. **Regression Prevention**: Catches bugs that break universal properties
5. **Documentation**: Property tests serve as executable specifications

## Adding New Property Tests

When adding new monadic types or operations:

1. Create a new test class in this directory
2. Add `[Trait("Category", "Property")]` to the class
3. Use `[Property(MaxTest = 1000)]` for test methods
4. Return `bool` from test methods (FsCheck handles the rest)
5. Test all three monad laws: left identity, right identity, associativity
6. Add functor laws if applicable

Example template:
```csharp
[Trait("Category", "Property")]
public class NewMonadPropertyTests
{
    [Property(MaxTest = 1000)]
    public bool NewMonad_LeftIdentity_Holds(int a)
    {
        // return a >>= f ≡ f a
        Func<int, NewMonad<int>> f = /* your function */;
        var left = NewMonad<int>.Return(a).Bind(f);
        var right = f(a);
        return /* equality check */;
    }
    
    // Add right identity and associativity tests...
}
```

## References

- [FsCheck Documentation](https://fscheck.github.io/FsCheck/)
- [Monad Laws](https://wiki.haskell.org/Monad_laws)
- [Functor Laws](https://wiki.haskell.org/Functor)
- [Category Theory for Programmers](https://bartoszmilewski.com/2014/10/28/category-theory-for-programmers-the-preface/)
