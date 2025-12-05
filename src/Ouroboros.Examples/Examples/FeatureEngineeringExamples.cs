// <copyright file="FeatureEngineeringExamples.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Examples;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LangChainPipeline.Infrastructure.FeatureEngineering;

/// <summary>
/// Examples demonstrating C# code vectorization and stream deduplication.
/// </summary>
public static class FeatureEngineeringExamples
{
    /// <summary>
    /// Example 1: Basic code vectorization and similarity comparison.
    /// </summary>
    public static void BasicCodeVectorizationExample()
    {
        Console.WriteLine("=== Example 1: Basic Code Vectorization ===\n");

        // Create a vectorizer with 4096-dimensional vectors
        CSharpHashVectorizer vectorizer = new CSharpHashVectorizer(dimension: 4096, lowercase: true);

        // Sample C# code
        string code1 = @"
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
                public int Subtract(int a, int b) => a - b;
            }";

        string code2 = @"
            public class Calculator
            {
                public int Add(int x, int y) => x + y;
                public int Subtract(int x, int y) => x - y;
            }";

        string code3 = @"
            public interface ILogger
            {
                void Log(string message);
                void LogError(string message);
            }";

        // Transform code into vectors
        float[] vector1 = vectorizer.TransformCode(code1);
        float[] vector2 = vectorizer.TransformCode(code2);
        float[] vector3 = vectorizer.TransformCode(code3);

        // Compute similarities
        float similarity12 = CSharpHashVectorizer.CosineSimilarity(vector1, vector2);
        float similarity13 = CSharpHashVectorizer.CosineSimilarity(vector1, vector3);

        Console.WriteLine($"Similarity between Calculator (a,b) and Calculator (x,y): {similarity12:F4}");
        Console.WriteLine($"Similarity between Calculator and ILogger: {similarity13:F4}");
        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Batch vectorization of multiple files.
    /// </summary>
    public static void BatchVectorizationExample()
    {
        Console.WriteLine("=== Example 2: Batch Vectorization ===\n");

        CSharpHashVectorizer vectorizer = new CSharpHashVectorizer(dimension: 4096);

        // Simulate multiple code snippets
        Dictionary<string, string> codeSnippets = new Dictionary<string, string>
        {
            ["Service"] = "public class UserService { public User GetUser(int id) => null; }",
            ["Repository"] = "public class UserRepository { public User Find(int id) => null; }",
            ["Controller"] = "public class UserController { public IActionResult Get(int id) => Ok(); }",
        };

        // Vectorize all snippets
        Dictionary<string, float[]> vectors = codeSnippets.ToDictionary(
            kvp => kvp.Key,
            kvp => vectorizer.TransformCode(kvp.Value));

        // Compute similarity matrix
        Console.WriteLine("Similarity Matrix:");
        foreach (string key1 in vectors.Keys)
        {
            foreach (string key2 in vectors.Keys)
            {
                if (string.CompareOrdinal(key1, key2) <= 0)
                {
                    continue;
                }

                float similarity = CSharpHashVectorizer.CosineSimilarity(vectors[key1], vectors[key2]);
                Console.WriteLine($"  {key1} <-> {key2}: {similarity:F4}");
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 3: Real-time stream deduplication.
    /// </summary>
    public static void StreamDeduplicationExample()
    {
        Console.WriteLine("=== Example 3: Stream Deduplication ===\n");

        CSharpHashVectorizer vectorizer = new CSharpHashVectorizer(dimension: 4096);
        StreamDeduplicator deduplicator = new StreamDeduplicator(
            similarityThreshold: 0.95f,
            maxCacheSize: 100);

        // Simulate a stream of code changes with duplicates
        string[] codeStream = new[]
        {
            "public class User { public int Id { get; set; } }",
            "public class User { public int Id { get; set; } }", // Duplicate
            "public class User { public int Id { get; set; } public string Name { get; set; } }",
            "public class User { public int Id { get; set; } }", // Duplicate
            "public class Product { public int Id { get; set; } }",
            "public class Product { public int Id { get; set; } }", // Duplicate
        };

        Console.WriteLine($"Original stream: {codeStream.Length} items");

        // Vectorize and deduplicate
        IEnumerable<float[]> vectors = codeStream.Select(code => vectorizer.TransformCode(code));
        List<float[]> uniqueVectors = deduplicator.FilterBatch(vectors);

        Console.WriteLine($"After deduplication: {uniqueVectors.Count} unique items");
        Console.WriteLine($"Removed {codeStream.Length - uniqueVectors.Count} duplicates");
        Console.WriteLine();
    }

    /// <summary>
    /// Example 4: Async stream deduplication with IAsyncEnumerable.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task AsyncStreamDeduplicationExample()
    {
        Console.WriteLine("=== Example 4: Async Stream Deduplication ===\n");

        CSharpHashVectorizer vectorizer = new CSharpHashVectorizer(dimension: 4096);
        StreamDeduplicator deduplicator = new StreamDeduplicator(
            similarityThreshold: 0.95f,
            maxCacheSize: 1000);

        // Simulate an async stream of log entries
        async IAsyncEnumerable<string> GetLogStreamAsync()
        {
            string[] logs = new[]
            {
                "Error: Connection timeout",
                "Error: Connection timeout", // Duplicate
                "Info: User logged in",
                "Error: Connection timeout", // Duplicate
                "Warning: High memory usage",
                "Info: User logged in", // Duplicate
                "Error: Database connection failed",
            };

            foreach (string? log in logs)
            {
                await Task.Delay(10); // Simulate streaming delay
                yield return log;
            }
        }

        // Process stream with deduplication
        int originalCount = 0;
        int uniqueCount = 0;

        await foreach (string log in GetLogStreamAsync())
        {
            originalCount++;
            float[] vector = vectorizer.TransformCode(log);

            if (!deduplicator.IsDuplicate(vector))
            {
                uniqueCount++;
                Console.WriteLine($"  Unique: {log}");
            }
        }

        Console.WriteLine($"\nProcessed {originalCount} log entries");
        Console.WriteLine($"Found {uniqueCount} unique entries");
        Console.WriteLine($"Filtered out {originalCount - uniqueCount} duplicates");
        Console.WriteLine();
    }

    /// <summary>
    /// Example 5: Using extension methods for fluent API.
    /// </summary>
    public static void FluentApiExample()
    {
        Console.WriteLine("=== Example 5: Fluent API ===\n");

        CSharpHashVectorizer vectorizer = new CSharpHashVectorizer(dimension: 4096);

        string[] codeSnippets = new[]
        {
            "public class A { }",
            "public class A { }", // Duplicate
            "public class B { }",
            "public class C { }",
            "public class C { }", // Duplicate
        };

        // Use extension method for concise deduplication
        List<float[]> vectors = codeSnippets
            .Select(code => vectorizer.TransformCode(code))
            .Deduplicate(similarityThreshold: 0.95f, maxCacheSize: 100);

        Console.WriteLine($"Original: {codeSnippets.Length} snippets");
        Console.WriteLine($"After deduplication: {vectors.Count} unique vectors");
        Console.WriteLine();
    }

    /// <summary>
    /// Example 6: Code similarity search (find similar code).
    /// </summary>
    public static void CodeSimilaritySearchExample()
    {
        Console.WriteLine("=== Example 6: Code Similarity Search ===\n");

        CSharpHashVectorizer vectorizer = new CSharpHashVectorizer(dimension: 4096);

        // Build a "database" of code snippets
        Dictionary<string, string> codeDatabase = new Dictionary<string, string>
        {
            ["AddMethod"] = "public int Add(int a, int b) => a + b;",
            ["SubtractMethod"] = "public int Subtract(int a, int b) => a - b;",
            ["MultiplyMethod"] = "public int Multiply(int a, int b) => a * b;",
            ["SumMethod"] = "public int Sum(int x, int y) => x + y;", // Similar to Add
            ["LogMethod"] = "public void Log(string msg) => Console.WriteLine(msg);",
        };

        Dictionary<string, float[]> databaseVectors = codeDatabase.ToDictionary(
            kvp => kvp.Key,
            kvp => vectorizer.TransformCode(kvp.Value));

        // Query: Find similar code to a new snippet
        string query = "public int Plus(int num1, int num2) => num1 + num2;";
        float[] queryVector = vectorizer.TransformCode(query);

        Console.WriteLine($"Query: {query}");
        Console.WriteLine("\nTop similar matches:");

        var similarities = databaseVectors
            .Select(kvp => new
            {
                Name = kvp.Key,
                Similarity = CSharpHashVectorizer.CosineSimilarity(queryVector, kvp.Value),
            })
            .OrderByDescending(x => x.Similarity)
            .Take(3);

        foreach (var match in similarities)
        {
            Console.WriteLine($"  {match.Name}: {match.Similarity:F4}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Example 7: Duplicate detection in codebase.
    /// </summary>
    public static void DuplicateDetectionExample()
    {
        Console.WriteLine("=== Example 7: Duplicate Detection ===\n");

        CSharpHashVectorizer vectorizer = new CSharpHashVectorizer(dimension: 4096);

        Dictionary<string, string> codeFiles = new Dictionary<string, string>
        {
            ["File1.cs"] = "public class Calculator { public int Add(int a, int b) => a + b; }",
            ["File2.cs"] = "public class MathHelper { public int Add(int x, int y) => x + y; }",
            ["File3.cs"] = "public interface ILogger { void Log(string msg); }",
            ["File4.cs"] = "public class Calculator { public int Add(int a, int b) => a + b; }", // Exact duplicate
        };

        // Detect duplicates with high threshold
        Dictionary<string, float[]> vectors = codeFiles.ToDictionary(
            kvp => kvp.Key,
            kvp => vectorizer.TransformCode(kvp.Value));

        Console.WriteLine("Potential duplicates (similarity > 0.95):");
        foreach (string file1 in vectors.Keys)
        {
            foreach (string file2 in vectors.Keys)
            {
                if (string.CompareOrdinal(file1, file2) <= 0)
                {
                    continue;
                }

                float similarity = CSharpHashVectorizer.CosineSimilarity(vectors[file1], vectors[file2]);
                if (similarity > 0.95f)
                {
                    Console.WriteLine($"  {file1} <-> {file2}: {similarity:F4}");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Run all examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Feature Engineering Examples");
        Console.WriteLine("========================================\n");

        BasicCodeVectorizationExample();
        BatchVectorizationExample();
        StreamDeduplicationExample();
        await AsyncStreamDeduplicationExample();
        FluentApiExample();
        CodeSimilaritySearchExample();
        DuplicateDetectionExample();

        Console.WriteLine("========================================");
        Console.WriteLine("All examples completed!");
        Console.WriteLine("========================================");
    }
}
