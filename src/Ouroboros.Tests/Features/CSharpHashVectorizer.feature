Feature: CSharp Hash Vectorizer
    As a developer
    I want to vectorize C# code using hash-based feature extraction
    So that I can perform semantic search and similarity analysis on code

    Background:
        Given a fresh hash vectorizer context

    Scenario Outline: Create vectorizer with valid dimensions
        When I create a vectorizer with dimension <dimension>
        Then the vectorizer should not be null

        Examples:
            | dimension |
            | 256       |
            | 4096      |
            | 65536     |

    Scenario Outline: Invalid dimensions throw exception
        When I attempt to create a vectorizer with dimension <dimension>
        Then it should throw ArgumentException with parameter "dimension"

        Examples:
            | dimension |
            | 100       |
            | 1000      |
            | 65535     |

    Scenario: Transform code returns vector of correct dimension
        Given a vectorizer with dimension 4096
        And C# code "public class MyClass { public int GetValue() => 42; }"
        When I transform the code
        Then the vector should have length 4096

    Scenario: Transform code returns normalized vector
        Given a vectorizer with dimension 4096
        And C# code "public class Test { }"
        When I transform the code
        Then the vector L2 norm should be approximately 1.0

    Scenario: Identical code produces identical vectors
        Given a vectorizer with dimension 4096 and lowercase true
        And C# code "public class Calculator { public int Add(int a, int b) => a + b; }"
        When I transform the code twice
        Then both vectors should be identical

    Scenario: Similar code produces similar vectors
        Given a vectorizer with dimension 4096
        And C# code "public class Calculator { public int Add(int a, int b) => a + b; }"
        And C# code "public class Calculator { public int Sum(int x, int y) => x + y; }"
        When I transform both codes
        Then the cosine similarity should be greater than 0.6

    Scenario: Different code produces different vectors
        Given a vectorizer with dimension 4096
        And C# code "public class Calculator { public int Add(int a, int b) => a + b; }"
        And C# code "public class Logger { public void Log(string message) => Console.WriteLine(message); }"
        When I transform both codes
        Then the cosine similarity should be less than 0.5

    Scenario: Lowercase option affects vectorization
        Given a vectorizer with dimension 4096 and lowercase true
        And C# code "Public Class Test { }"
        And a vectorizer with dimension 4096 and lowercase false
        When I transform with both vectorizers
        Then the vectors should differ

    Scenario: Empty code produces zero vector
        Given a vectorizer with dimension 4096
        And C# code ""
        When I transform the code
        Then all vector elements should be 0.0

    Scenario: Whitespace is normalized
        Given a vectorizer with dimension 4096
        And C# code "public class Test{public void Method(){}}"
        And C# code "public class Test { public void Method() { } }"
        When I transform both codes
        Then the vectors should be similar with similarity > 0.95

    Scenario: Comments affect vectorization
        Given a vectorizer with dimension 4096
        And C# code "public class Test { }"
        And C# code "// This is a test\npublic class Test { }"
        When I transform both codes
        Then the vectors should differ

    Scenario: Method signatures are captured
        Given a vectorizer with dimension 4096
        And C# code "public void Process(string input, int count) { }"
        And C# code "public void Process(string text, int number) { }"
        When I transform both codes
        Then the cosine similarity should be greater than 0.7

    Scenario: Class structure is captured
        Given a vectorizer with dimension 4096
        And C# code "public class Parent { public class Child { } }"
        And C# code "public class Parent { public class Other { } }"
        When I transform both codes
        Then the cosine similarity should be greater than 0.7

    Scenario: Namespace affects vectorization
        Given a vectorizer with dimension 4096
        And C# code "namespace MyApp { public class Test { } }"
        And C# code "namespace OtherApp { public class Test { } }"
        When I transform both codes
        Then the vectors should differ

    Scenario: Using directives are captured
        Given a vectorizer with dimension 4096
        And C# code "using System; using System.Linq; public class Test { }"
        And C# code "using System.Collections.Generic; public class Test { }"
        When I transform both codes
        Then the vectors should differ
