// <copyright file="FourierVectorCompressor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics;

namespace Ouroboros.Domain.VectorCompression;

/// <summary>
/// Compresses high-dimensional embedding vectors using Fourier Transform.
/// Keeps only the most significant frequency components, reducing storage while preserving semantic similarity.
/// </summary>
public sealed class FourierVectorCompressor
{
    private readonly int _targetDimension;
    private readonly CompressionStrategy _strategy;

    /// <summary>
    /// Compression strategies for selecting which components to keep.
    /// </summary>
    public enum CompressionStrategy
    {
        /// <summary>Keep lowest frequency components (smoothest features).</summary>
        LowFrequency,

        /// <summary>Keep highest magnitude components (most energy).</summary>
        HighestMagnitude,

        /// <summary>Keep components with highest variance across dataset.</summary>
        HighestVariance,

        /// <summary>Adaptive selection based on energy distribution.</summary>
        Adaptive
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FourierVectorCompressor"/> class.
    /// </summary>
    /// <param name="targetDimension">Target dimension after compression.</param>
    /// <param name="strategy">Strategy for selecting components to keep.</param>
    public FourierVectorCompressor(int targetDimension = 256, CompressionStrategy strategy = CompressionStrategy.HighestMagnitude)
    {
        _targetDimension = targetDimension;
        _strategy = strategy;
    }

    /// <summary>
    /// Compresses a vector using FFT, keeping only the most significant components.
    /// </summary>
    /// <param name="vector">Input embedding vector.</param>
    /// <returns>Compressed vector and metadata for reconstruction.</returns>
    public CompressedVector Compress(float[] vector)
    {
        if (vector.Length <= _targetDimension)
        {
            // No compression needed
            return new CompressedVector(
                Components: vector.ToArray(),
                Indices: Enumerable.Range(0, vector.Length).ToArray(),
                OriginalLength: vector.Length,
                CompressionRatio: 1.0,
                Strategy: _strategy);
        }

        // Pad to power of 2 for efficient FFT
        int paddedLength = NextPowerOfTwo(vector.Length);
        var padded = new Complex[paddedLength];
        for (int i = 0; i < vector.Length; i++)
            padded[i] = new Complex(vector[i], 0);

        // Apply FFT
        FFT(padded, false);

        // Select components based on strategy
        var (indices, magnitudes) = SelectComponents(padded);

        // Extract real and imaginary parts of selected components
        var components = new float[_targetDimension * 2]; // Store both real and imaginary
        for (int i = 0; i < _targetDimension && i < indices.Length; i++)
        {
            components[i * 2] = (float)padded[indices[i]].Real;
            components[i * 2 + 1] = (float)padded[indices[i]].Imaginary;
        }

        return new CompressedVector(
            Components: components,
            Indices: indices.Take(_targetDimension).ToArray(),
            OriginalLength: vector.Length,
            CompressionRatio: (double)vector.Length / (_targetDimension * 2),
            Strategy: _strategy);
    }

    /// <summary>
    /// Decompresses a vector back to its original dimension.
    /// </summary>
    /// <param name="compressed">Compressed vector data.</param>
    /// <returns>Reconstructed vector (approximate).</returns>
    public float[] Decompress(CompressedVector compressed)
    {
        int paddedLength = NextPowerOfTwo(compressed.OriginalLength);
        var spectrum = new Complex[paddedLength];

        // Reconstruct frequency components
        for (int i = 0; i < compressed.Indices.Length && i * 2 + 1 < compressed.Components.Length; i++)
        {
            int idx = compressed.Indices[i];
            if (idx < paddedLength)
            {
                spectrum[idx] = new Complex(compressed.Components[i * 2], compressed.Components[i * 2 + 1]);

                // Mirror for conjugate symmetry (real signal)
                int mirror = paddedLength - idx;
                if (mirror < paddedLength && mirror != idx)
                {
                    spectrum[mirror] = Complex.Conjugate(spectrum[idx]);
                }
            }
        }

        // Apply inverse FFT
        FFT(spectrum, true);

        // Extract real parts
        var result = new float[compressed.OriginalLength];
        for (int i = 0; i < compressed.OriginalLength; i++)
        {
            result[i] = (float)spectrum[i].Real;
        }

        return result;
    }

    /// <summary>
    /// Computes similarity between two compressed vectors without full decompression.
    /// </summary>
    /// <param name="a">First compressed vector.</param>
    /// <param name="b">Second compressed vector.</param>
    /// <returns>Approximate cosine similarity.</returns>
    public double CompressedSimilarity(CompressedVector a, CompressedVector b)
    {
        // Find common indices
        var commonIndices = a.Indices.Intersect(b.Indices).ToHashSet();

        if (commonIndices.Count == 0)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        var aDict = a.Indices.Select((idx, i) => (idx, i)).ToDictionary(x => x.idx, x => x.i);
        var bDict = b.Indices.Select((idx, i) => (idx, i)).ToDictionary(x => x.idx, x => x.i);

        foreach (var idx in commonIndices)
        {
            int ai = aDict[idx];
            int bi = bDict[idx];

            var aComplex = new Complex(a.Components[ai * 2], a.Components[ai * 2 + 1]);
            var bComplex = new Complex(b.Components[bi * 2], b.Components[bi * 2 + 1]);

            // Use magnitude for similarity
            double aMag = aComplex.Magnitude;
            double bMag = bComplex.Magnitude;

            dotProduct += aMag * bMag * Math.Cos(aComplex.Phase - bComplex.Phase);
            normA += aMag * aMag;
            normB += bMag * bMag;
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Batch compress multiple vectors, optionally learning optimal indices from the dataset.
    /// </summary>
    /// <param name="vectors">Collection of vectors to compress.</param>
    /// <returns>Compressed vectors with shared index scheme for efficient comparison.</returns>
    public IReadOnlyList<CompressedVector> BatchCompress(IEnumerable<float[]> vectors)
    {
        var vectorList = vectors.ToList();
        if (vectorList.Count == 0)
            return Array.Empty<CompressedVector>();

        if (_strategy == CompressionStrategy.HighestVariance)
        {
            // Learn optimal indices from dataset variance
            var optimalIndices = LearnOptimalIndices(vectorList);
            return vectorList.Select(v => CompressWithIndices(v, optimalIndices)).ToList();
        }

        return vectorList.Select(Compress).ToList();
    }

    private CompressedVector CompressWithIndices(float[] vector, int[] fixedIndices)
    {
        int paddedLength = NextPowerOfTwo(vector.Length);
        var padded = new Complex[paddedLength];
        for (int i = 0; i < vector.Length; i++)
            padded[i] = new Complex(vector[i], 0);

        FFT(padded, false);

        var components = new float[fixedIndices.Length * 2];
        for (int i = 0; i < fixedIndices.Length; i++)
        {
            if (fixedIndices[i] < paddedLength)
            {
                components[i * 2] = (float)padded[fixedIndices[i]].Real;
                components[i * 2 + 1] = (float)padded[fixedIndices[i]].Imaginary;
            }
        }

        return new CompressedVector(
            Components: components,
            Indices: fixedIndices,
            OriginalLength: vector.Length,
            CompressionRatio: (double)vector.Length / (fixedIndices.Length * 2),
            Strategy: _strategy);
    }

    private int[] LearnOptimalIndices(List<float[]> vectors)
    {
        if (vectors.Count == 0)
            return Array.Empty<int>();

        int paddedLength = NextPowerOfTwo(vectors[0].Length);
        var variances = new double[paddedLength];

        // Compute FFT for each vector
        var allSpectra = new List<Complex[]>();
        foreach (var vector in vectors)
        {
            var padded = new Complex[paddedLength];
            for (int i = 0; i < vector.Length; i++)
                padded[i] = new Complex(vector[i], 0);
            FFT(padded, false);
            allSpectra.Add(padded);
        }

        // Compute variance for each frequency component
        for (int i = 0; i < paddedLength; i++)
        {
            var magnitudes = allSpectra.Select(s => s[i].Magnitude).ToList();
            double mean = magnitudes.Average();
            variances[i] = magnitudes.Sum(m => (m - mean) * (m - mean)) / magnitudes.Count;
        }

        // Select indices with highest variance
        return variances
            .Select((v, i) => (Variance: v, Index: i))
            .OrderByDescending(x => x.Variance)
            .Take(_targetDimension)
            .Select(x => x.Index)
            .OrderBy(x => x) // Keep sorted for consistent comparison
            .ToArray();
    }

    private (int[] Indices, double[] Magnitudes) SelectComponents(Complex[] spectrum)
    {
        var components = spectrum
            .Select((c, i) => (Index: i, Magnitude: c.Magnitude, Complex: c))
            .ToArray();

        int[] indices;

        switch (_strategy)
        {
            case CompressionStrategy.LowFrequency:
                // Keep DC and low frequencies
                indices = Enumerable.Range(0, Math.Min(_targetDimension, spectrum.Length / 2)).ToArray();
                break;

            case CompressionStrategy.HighestMagnitude:
                // Keep components with highest energy
                indices = components
                    .OrderByDescending(c => c.Magnitude)
                    .Take(_targetDimension)
                    .Select(c => c.Index)
                    .OrderBy(x => x)
                    .ToArray();
                break;

            case CompressionStrategy.Adaptive:
                // Keep enough components to capture 95% of energy
                double totalEnergy = components.Sum(c => c.Magnitude * c.Magnitude);
                double targetEnergy = totalEnergy * 0.95;
                double accumulatedEnergy = 0;

                var sorted = components.OrderByDescending(c => c.Magnitude).ToList();
                var selected = new List<int>();

                foreach (var c in sorted)
                {
                    selected.Add(c.Index);
                    accumulatedEnergy += c.Magnitude * c.Magnitude;
                    if (accumulatedEnergy >= targetEnergy || selected.Count >= _targetDimension)
                        break;
                }

                indices = selected.OrderBy(x => x).ToArray();
                break;

            default:
                indices = Enumerable.Range(0, Math.Min(_targetDimension, spectrum.Length)).ToArray();
                break;
        }

        var magnitudes = indices.Select(i => spectrum[i].Magnitude).ToArray();
        return (indices, magnitudes);
    }

    /// <summary>
    /// In-place Cooley-Tukey FFT.
    /// </summary>
    private static void FFT(Complex[] data, bool inverse)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
                (data[i], data[j]) = (data[j], data[i]);

            int k = n / 2;
            while (k <= j)
            {
                j -= k;
                k /= 2;
            }
            j += k;
        }

        // Cooley-Tukey iterative FFT
        for (int len = 2; len <= n; len *= 2)
        {
            double angle = (inverse ? 2 : -2) * Math.PI / len;
            var wBase = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int k = 0; k < len / 2; k++)
                {
                    var t = w * data[i + k + len / 2];
                    var u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + len / 2] = u - t;
                    w *= wBase;
                }
            }
        }

        // Scale for inverse
        if (inverse)
        {
            for (int i = 0; i < n; i++)
                data[i] /= n;
        }
    }

    private static int NextPowerOfTwo(int n)
    {
        int power = 1;
        while (power < n)
            power *= 2;
        return power;
    }
}

/// <summary>
/// Represents a compressed embedding vector.
/// </summary>
/// <param name="Components">The kept frequency components (real, imaginary pairs).</param>
/// <param name="Indices">Indices of the kept frequency components.</param>
/// <param name="OriginalLength">Original vector dimension.</param>
/// <param name="CompressionRatio">Ratio of original to compressed size.</param>
/// <param name="Strategy">Compression strategy used.</param>
public sealed record CompressedVector(
    float[] Components,
    int[] Indices,
    int OriginalLength,
    double CompressionRatio,
    FourierVectorCompressor.CompressionStrategy Strategy)
{
    /// <summary>
    /// Gets the compressed size in bytes (approximate).
    /// </summary>
    public int CompressedSizeBytes => Components.Length * sizeof(float) + Indices.Length * sizeof(int);

    /// <summary>
    /// Gets the original size in bytes (approximate).
    /// </summary>
    public int OriginalSizeBytes => OriginalLength * sizeof(float);

    /// <summary>
    /// Serializes to a compact byte array.
    /// </summary>
    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(OriginalLength);
        writer.Write((int)Strategy);
        writer.Write(Indices.Length);

        foreach (var idx in Indices)
            writer.Write(idx);

        foreach (var comp in Components)
            writer.Write(comp);

        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes from bytes.
    /// </summary>
    public static CompressedVector FromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        int origLen = reader.ReadInt32();
        var strategy = (FourierVectorCompressor.CompressionStrategy)reader.ReadInt32();
        int indexCount = reader.ReadInt32();

        var indices = new int[indexCount];
        for (int i = 0; i < indexCount; i++)
            indices[i] = reader.ReadInt32();

        var components = new float[indexCount * 2];
        for (int i = 0; i < components.Length; i++)
            components[i] = reader.ReadSingle();

        return new CompressedVector(
            components,
            indices,
            origLen,
            (double)origLen / components.Length,
            strategy);
    }
}
