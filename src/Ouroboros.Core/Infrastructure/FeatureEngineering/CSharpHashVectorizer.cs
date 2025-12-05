// <copyright file="CSharpHashVectorizer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Infrastructure.FeatureEngineering;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// Deterministic C# code vectorizer that transforms source code into fixed-dimension hash vectors.
/// Uses bag-of-tokens approach with hashing trick for efficient, fixed-size feature representation.
/// Suitable for code similarity search, clustering, duplicate detection, and refactoring hints.
/// </summary>
public sealed class CSharpHashVectorizer
{
    private readonly int dimension;
    private readonly bool lowercase;
    private static readonly Regex TokenPattern = new(@"\b\w+\b", RegexOptions.Compiled);
    private static readonly HashSet<string> CSharpKeywords = new()
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
        "checked", "class", "const", "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum", "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CSharpHashVectorizer"/> class.
    /// </summary>
    /// <param name="dimension">
    /// Vector dimension, must be a power of 2 for efficient modulo operations.
    /// Default is 65536 (2^16). Common values: 4096, 16384, 65536, 262144.
    /// </param>
    /// <param name="lowercase">
    /// Whether to lowercase identifiers before hashing. Keywords are always normalized.
    /// Default is true for better generalization.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when dimension is not a power of 2 or less than 256.</exception>
    public CSharpHashVectorizer(int dimension = 65536, bool lowercase = true)
    {
        if (dimension < 256 || !IsPowerOfTwo(dimension))
        {
            throw new ArgumentException(
                "Dimension must be a power of 2 and at least 256",
                nameof(dimension));
        }

        this.dimension = dimension;
        this.lowercase = lowercase;
    }

    /// <summary>
    /// Transforms a C# source file into a dense hash vector.
    /// </summary>
    /// <param name="path">Path to the C# source file.</param>
    /// <returns>A dense float vector of length <see cref="dimension"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    public float[] TransformFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Source file not found: {path}", path);
        }

        string code = File.ReadAllText(path);
        return this.TransformCode(code);
    }

    /// <summary>
    /// Transforms multiple C# source files into dense hash vectors.
    /// </summary>
    /// <param name="paths">Enumerable of file paths to transform.</param>
    /// <returns>A list of dense float vectors, one per file.</returns>
    public List<float[]> TransformFiles(IEnumerable<string> paths)
    {
        if (paths is null)
        {
            throw new ArgumentNullException(nameof(paths));
        }

        return paths.Select(this.TransformFile).ToList();
    }

    /// <summary>
    /// Transforms C# source code string into a dense hash vector.
    /// </summary>
    /// <param name="code">C# source code as string.</param>
    /// <returns>A dense float vector of length <see cref="dimension"/>.</returns>
    public float[] TransformCode(string code)
    {
        if (code is null)
        {
            code = string.Empty;
        }

        List<string> tokens = this.ExtractTokens(code);
        return this.BuildVector(tokens);
    }

    /// <summary>
    /// Transforms C# source code string into a dense hash vector asynchronously.
    /// </summary>
    /// <param name="code">C# source code as string.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a dense float vector.</returns>
    public Task<float[]> TransformCodeAsync(string code)
    {
        return Task.Run(() => this.TransformCode(code));
    }

    /// <summary>
    /// Transforms multiple C# source files into dense hash vectors asynchronously.
    /// </summary>
    /// <param name="paths">Enumerable of file paths to transform.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of dense float vectors.</returns>
    public async Task<List<float[]>> TransformFilesAsync(IEnumerable<string> paths)
    {
        if (paths is null)
        {
            throw new ArgumentNullException(nameof(paths));
        }

        IEnumerable<Task<float[]>> tasks = paths.Select(async path =>
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Source file not found: {path}", path);
            }

            string code = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return this.TransformCode(code);
        });

        return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
    }

    /// <summary>
    /// Computes cosine similarity between two vectors.
    /// </summary>
    /// <param name="v1">First vector.</param>
    /// <param name="v2">Second vector.</param>
    /// <returns>Cosine similarity score between 0 and 1.</returns>
    public static float CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1 is null || v2 is null || v1.Length != v2.Length)
        {
            throw new ArgumentException("Vectors must be non-null and have the same length");
        }

        float dot = 0f;
        float norm1 = 0f;
        float norm2 = 0f;

        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            norm1 += v1[i] * v1[i];
            norm2 += v2[i] * v2[i];
        }

        float magnitude = MathF.Sqrt(norm1) * MathF.Sqrt(norm2);
        return magnitude > 0f ? dot / magnitude : 0f;
    }

    private List<string> ExtractTokens(string code)
    {
        List<string> tokens = new List<string>();
        MatchCollection matches = TokenPattern.Matches(code);

        foreach (Match match in matches)
        {
            string token = match.Value;

            // Normalize keywords
            if (CSharpKeywords.Contains(token.ToLowerInvariant()))
            {
                tokens.Add(token.ToLowerInvariant());
            }
            else if (this.lowercase)
            {
                tokens.Add(token.ToLowerInvariant());
            }
            else
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private float[] BuildVector(List<string> tokens)
    {
        float[] vector = new float[this.dimension];

        if (tokens.Count == 0)
        {
            return vector;
        }

        // Count token occurrences for term frequency
        Dictionary<string, int> tokenCounts = new Dictionary<string, int>();
        foreach (string token in tokens)
        {
            tokenCounts.TryGetValue(token, out int count);
            tokenCounts[token] = count + 1;
        }

        // Hash each unique token to vector indices and accumulate frequencies
        foreach ((string token, int count) in tokenCounts)
        {
            uint hash = ComputeHash(token);
            int index = (int)(hash % (uint)this.dimension);

            // Use signed hashing for better distribution
            float sign = (hash & 0x80000000) == 0 ? 1f : -1f;

            // Accumulate with TF weighting
            vector[index] += sign * count;
        }

        // L2 normalization
        float norm = 0f;
        for (int i = 0; i < this.dimension; i++)
        {
            norm += vector[i] * vector[i];
        }

        if (norm > 0f)
        {
            norm = MathF.Sqrt(norm);
            for (int i = 0; i < this.dimension; i++)
            {
                vector[i] /= norm;
            }
        }

        return vector;
    }

    private static uint ComputeHash(string token)
    {
        Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(token.Length)];
        int len = Encoding.UTF8.GetBytes(token, bytes);
        return XxHash32.Hash(bytes[..len]);
    }

    private static bool IsPowerOfTwo(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }

    /// <summary>
    /// Simple XxHash32 implementation for fast, high-quality hashing.
    /// Based on the XxHash algorithm by Yann Collet.
    /// </summary>
    private static class XxHash32
    {
        private const uint Prime1 = 2654435761U;
        private const uint Prime2 = 2246822519U;
        private const uint Prime3 = 3266489917U;
        private const uint Prime4 = 668265263U;
        private const uint Prime5 = 374761393U;

        public static uint Hash(ReadOnlySpan<byte> data, uint seed = 0)
        {
            uint hash;
            int remaining = data.Length;
            int offset = 0;

            if (remaining >= 16)
            {
                uint v1 = seed + Prime1 + Prime2;
                uint v2 = seed + Prime2;
                uint v3 = seed;
                uint v4 = seed - Prime1;

                do
                {
                    v1 = Round(v1, ReadUInt32(data, offset));
                    offset += 4;
                    v2 = Round(v2, ReadUInt32(data, offset));
                    offset += 4;
                    v3 = Round(v3, ReadUInt32(data, offset));
                    offset += 4;
                    v4 = Round(v4, ReadUInt32(data, offset));
                    offset += 4;
                    remaining -= 16;
                }
                while (remaining >= 16);

                hash = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
            }
            else
            {
                hash = seed + Prime5;
            }

            hash += (uint)data.Length;

            while (remaining >= 4)
            {
                hash += ReadUInt32(data, offset) * Prime3;
                hash = RotateLeft(hash, 17) * Prime4;
                offset += 4;
                remaining -= 4;
            }

            while (remaining > 0)
            {
                hash += data[offset] * Prime5;
                hash = RotateLeft(hash, 11) * Prime1;
                offset++;
                remaining--;
            }

            hash ^= hash >> 15;
            hash *= Prime2;
            hash ^= hash >> 13;
            hash *= Prime3;
            hash ^= hash >> 16;

            return hash;
        }

        private static uint Round(uint acc, uint input)
        {
            acc += input * Prime2;
            acc = RotateLeft(acc, 13);
            acc *= Prime1;
            return acc;
        }

        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }
    }
}
