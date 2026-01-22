// <copyright file="VectorConvolution.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ouroboros.Core.Vectors;

/// <summary>
/// Vector convolution operations for creating higher-dimensional thought representations.
/// Combines multiple thought vectors through convolution to capture complex relational patterns.
/// </summary>
/// <remarks>
/// <para>In the context of thought processing:</para>
/// <list type="bullet">
///   <item><description>1D convolution: Slide a kernel across a thought vector to extract local patterns</description></item>
///   <item><description>Cross-convolution: Combine two thoughts to create a relational representation</description></item>
///   <item><description>Holographic convolution: Create distributed representations that preserve composition</description></item>
///   <item><description>Circular convolution: Enable invertible binding for memory retrieval</description></item>
/// </list>
/// </remarks>
public static class VectorConvolution
{
    /// <summary>
    /// Convolves two thought vectors to create a higher-dimensional combined representation.
    /// Uses circular convolution which preserves information and is invertible.
    /// </summary>
    /// <param name="thought1">First thought embedding.</param>
    /// <param name="thought2">Second thought embedding.</param>
    /// <returns>Combined thought vector of same dimension.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float[] CircularConvolve(ReadOnlySpan<float> thought1, ReadOnlySpan<float> thought2)
    {
        if (thought1.Length != thought2.Length)
            throw new ArgumentException("Thought vectors must have same dimension");

        int n = thought1.Length;
        var result = new float[n];

        // Circular convolution: result[k] = sum(thought1[j] * thought2[(k-j) mod n])
        for (int k = 0; k < n; k++)
        {
            float sum = 0;
            for (int j = 0; j < n; j++)
            {
                int index = (k - j + n) % n;
                sum += thought1[j] * thought2[index];
            }
            result[k] = sum;
        }

        return result;
    }

    /// <summary>
    /// Performs FFT-accelerated circular convolution for large vectors.
    /// Uses the convolution theorem: conv(a,b) = IFFT(FFT(a) * FFT(b)).
    /// </summary>
    /// <param name="thought1">First thought embedding.</param>
    /// <param name="thought2">Second thought embedding.</param>
    /// <returns>Combined thought vector.</returns>
    public static float[] FastCircularConvolve(ReadOnlySpan<float> thought1, ReadOnlySpan<float> thought2)
    {
        if (thought1.Length != thought2.Length)
            throw new ArgumentException("Thought vectors must have same dimension");

        int n = thought1.Length;

        // Convert to complex for FFT
        var complex1 = new Complex[n];
        var complex2 = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            complex1[i] = new Complex(thought1[i], 0);
            complex2[i] = new Complex(thought2[i], 0);
        }

        // FFT both
        FFT(complex1);
        FFT(complex2);

        // Element-wise multiply in frequency domain
        for (int i = 0; i < n; i++)
        {
            complex1[i] *= complex2[i];
        }

        // Inverse FFT
        IFFT(complex1);

        // Extract real parts
        var result = new float[n];
        for (int i = 0; i < n; i++)
        {
            result[i] = (float)complex1[i].Real;
        }

        return result;
    }

    /// <summary>
    /// Correlation (inverse of convolution) - retrieves thought1 given the combined vector and thought2.
    /// Used for memory retrieval: if C = conv(A, B), then A ≈ corr(C, B).
    /// </summary>
    /// <param name="combined">The combined thought vector.</param>
    /// <param name="key">The key thought used in the original binding.</param>
    /// <returns>Approximation of the original thought.</returns>
    public static float[] CircularCorrelate(ReadOnlySpan<float> combined, ReadOnlySpan<float> key)
    {
        if (combined.Length != key.Length)
            throw new ArgumentException("Vectors must have same dimension");

        int n = combined.Length;

        // Correlation is convolution with reversed key
        var reversedKey = new float[n];
        reversedKey[0] = key[0];
        for (int i = 1; i < n; i++)
        {
            reversedKey[i] = key[n - i];
        }

        return CircularConvolve(combined, reversedKey);
    }

    /// <summary>
    /// Expands a thought to higher dimensions through convolution with a random projection matrix.
    /// Creates richer representations by capturing more nuanced features.
    /// </summary>
    /// <param name="thought">Original thought embedding.</param>
    /// <param name="targetDimension">Target higher dimension.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>Higher-dimensional thought vector.</returns>
    public static float[] ExpandDimension(ReadOnlySpan<float> thought, int targetDimension, int seed = 42)
    {
        if (targetDimension <= thought.Length)
            throw new ArgumentException("Target dimension must be larger than source");

        var random = new Random(seed);
        int sourceDim = thought.Length;
        var result = new float[targetDimension];

        // Create expansion through multiple convolution kernels
        int numKernels = targetDimension / sourceDim + 1;

        for (int k = 0; k < numKernels && k * sourceDim < targetDimension; k++)
        {
            // Generate random kernel for this expansion
            var kernel = new float[sourceDim];
            for (int i = 0; i < sourceDim; i++)
            {
                kernel[i] = (float)(random.NextDouble() * 2 - 1) / MathF.Sqrt(sourceDim);
            }

            // Convolve thought with kernel
            var convolved = CircularConvolve(thought, kernel);

            // Copy to result at offset
            int offset = k * sourceDim;
            int count = Math.Min(sourceDim, targetDimension - offset);
            convolved.AsSpan(0, count).CopyTo(result.AsSpan(offset, count));
        }

        // Normalize result
        Normalize(result);
        return result;
    }

    /// <summary>
    /// Creates a "meta-thought" by convolving multiple thoughts together.
    /// The result captures the combined semantic essence of all input thoughts.
    /// </summary>
    /// <param name="thoughts">Collection of thought embeddings.</param>
    /// <returns>Meta-thought embedding capturing all thoughts.</returns>
    public static float[] CreateMetaThought(IEnumerable<float[]> thoughts)
    {
        var thoughtList = thoughts.ToList();
        if (thoughtList.Count == 0)
            throw new ArgumentException("At least one thought required");

        if (thoughtList.Count == 1)
            return thoughtList[0].ToArray();

        // Pairwise convolution, then combine
        var result = thoughtList[0].ToArray();
        for (int i = 1; i < thoughtList.Count; i++)
        {
            result = CircularConvolve(result, thoughtList[i]);
            Normalize(result);
        }

        return result;
    }

    /// <summary>
    /// Computes the "thought gradient" - the directional change between two thoughts.
    /// Useful for understanding thought evolution and transitions.
    /// </summary>
    /// <param name="thought1">Starting thought.</param>
    /// <param name="thought2">Ending thought.</param>
    /// <returns>Gradient vector representing the thought transition.</returns>
    public static float[] ThoughtGradient(ReadOnlySpan<float> thought1, ReadOnlySpan<float> thought2)
    {
        if (thought1.Length != thought2.Length)
            throw new ArgumentException("Thought vectors must have same dimension");

        int n = thought1.Length;
        var gradient = new float[n];

        for (int i = 0; i < n; i++)
        {
            gradient[i] = thought2[i] - thought1[i];
        }

        return gradient;
    }

    /// <summary>
    /// Applies a thought transition gradient to create a new thought.
    /// thought_new = thought_base + alpha * gradient.
    /// </summary>
    /// <param name="baseThought">Starting thought.</param>
    /// <param name="gradient">Direction of change.</param>
    /// <param name="alpha">Step size (0-1 for interpolation, >1 for extrapolation).</param>
    /// <returns>New thought after applying gradient.</returns>
    public static float[] ApplyGradient(ReadOnlySpan<float> baseThought, ReadOnlySpan<float> gradient, float alpha = 1.0f)
    {
        if (baseThought.Length != gradient.Length)
            throw new ArgumentException("Vectors must have same dimension");

        int n = baseThought.Length;
        var result = new float[n];

        for (int i = 0; i < n; i++)
        {
            result[i] = baseThought[i] + alpha * gradient[i];
        }

        Normalize(result);
        return result;
    }

    /// <summary>
    /// Performs multi-scale convolution to capture patterns at different granularities.
    /// Returns a concatenated higher-dimensional vector.
    /// </summary>
    /// <param name="thought">Input thought vector.</param>
    /// <param name="scales">Kernel sizes for each scale (e.g., 3, 5, 7).</param>
    /// <returns>Multi-scale feature vector (dimension = original * number of scales).</returns>
    public static float[] MultiScaleConvolve(ReadOnlySpan<float> thought, params int[] scales)
    {
        if (scales.Length == 0)
            scales = new[] { 3, 5, 7 }; // Default scales

        int n = thought.Length;
        var results = new List<float[]>();

        foreach (int kernelSize in scales)
        {
            // Create averaging kernel of this size
            var kernel = new float[n];
            int halfSize = kernelSize / 2;
            float value = 1.0f / kernelSize;

            for (int i = 0; i < kernelSize && i < n; i++)
            {
                kernel[(n - halfSize + i) % n] = value;
            }

            var convolved = CircularConvolve(thought, kernel);
            results.Add(convolved);
        }

        // Concatenate all scales
        var combined = new float[n * scales.Length];
        for (int s = 0; s < scales.Length; s++)
        {
            results[s].CopyTo(combined, s * n);
        }

        return combined;
    }

    /// <summary>
    /// Holographic reduced representation (HRR) binding.
    /// Creates a distributed representation that can store multiple associations.
    /// </summary>
    /// <param name="role">The role/relationship vector.</param>
    /// <param name="filler">The filler/value vector.</param>
    /// <returns>Bound representation.</returns>
    public static float[] HolographicBind(ReadOnlySpan<float> role, ReadOnlySpan<float> filler)
    {
        return FastCircularConvolve(role, filler);
    }

    /// <summary>
    /// Holographic unbinding - retrieves filler given bound representation and role.
    /// </summary>
    /// <param name="bound">The bound representation.</param>
    /// <param name="role">The role used in binding.</param>
    /// <returns>Approximation of the original filler.</returns>
    public static float[] HolographicUnbind(ReadOnlySpan<float> bound, ReadOnlySpan<float> role)
    {
        return CircularCorrelate(bound, role);
    }

    /// <summary>
    /// Creates a "thought resonance" by iteratively convolving a thought with itself.
    /// Amplifies dominant patterns in the thought representation.
    /// </summary>
    /// <param name="thought">Input thought.</param>
    /// <param name="iterations">Number of self-convolution iterations.</param>
    /// <returns>Resonated thought with amplified patterns.</returns>
    public static float[] ThoughtResonance(ReadOnlySpan<float> thought, int iterations = 3)
    {
        var result = thought.ToArray();

        for (int i = 0; i < iterations; i++)
        {
            result = CircularConvolve(result, result);
            Normalize(result);
        }

        return result;
    }

    /// <summary>
    /// Similarity between two thought vectors using cosine similarity.
    /// </summary>
    public static float CosineSimilarity(ReadOnlySpan<float> v1, ReadOnlySpan<float> v2)
    {
        if (v1.Length != v2.Length)
            throw new ArgumentException("Vectors must have same dimension");

        float dot = 0, norm1 = 0, norm2 = 0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            norm1 += v1[i] * v1[i];
            norm2 += v2[i] * v2[i];
        }

        if (norm1 == 0 || norm2 == 0) return 0;
        return dot / (MathF.Sqrt(norm1) * MathF.Sqrt(norm2));
    }

    /// <summary>
    /// Normalizes a vector in-place to unit length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Normalize(Span<float> vector)
    {
        float norm = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            norm += vector[i] * vector[i];
        }

        if (norm > 0)
        {
            norm = MathF.Sqrt(norm);
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }
    }

    #region FFT Implementation

    private static void FFT(Complex[] data)
    {
        int n = data.Length;
        if (n <= 1) return;

        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
            int k = n / 2;
            while (k <= j)
            {
                j -= k;
                k /= 2;
            }
            j += k;
        }

        // Cooley-Tukey FFT
        for (int len = 2; len <= n; len *= 2)
        {
            double angle = -2.0 * Math.PI / len;
            var wLen = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int k = 0; k < len / 2; k++)
                {
                    var t = w * data[i + k + len / 2];
                    var u = data[i + k];
                    data[i + k] = u + t;
                    data[i + k + len / 2] = u - t;
                    w *= wLen;
                }
            }
        }
    }

    private static void IFFT(Complex[] data)
    {
        // Conjugate
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Complex.Conjugate(data[i]);
        }

        FFT(data);

        // Conjugate and scale
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = Complex.Conjugate(data[i]) / data.Length;
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for thought vector operations.
/// </summary>
public static class ThoughtVectorExtensions
{
    /// <summary>
    /// Convolves this thought with another to create a combined representation.
    /// </summary>
    public static float[] ConvolveWith(this float[] thought, float[] other)
        => VectorConvolution.CircularConvolve(thought, other);

    /// <summary>
    /// Expands this thought to a higher dimension.
    /// </summary>
    public static float[] ExpandTo(this float[] thought, int targetDimension, int seed = 42)
        => VectorConvolution.ExpandDimension(thought, targetDimension, seed);

    /// <summary>
    /// Creates a meta-thought combining this with other thoughts.
    /// </summary>
    public static float[] CombineWith(this float[] thought, params float[][] others)
        => VectorConvolution.CreateMetaThought(new[] { thought }.Concat(others));

    /// <summary>
    /// Computes the gradient from this thought to another.
    /// </summary>
    public static float[] GradientTo(this float[] thought, float[] target)
        => VectorConvolution.ThoughtGradient(thought, target);

    /// <summary>
    /// Amplifies patterns through self-resonance.
    /// </summary>
    public static float[] Resonate(this float[] thought, int iterations = 3)
        => VectorConvolution.ThoughtResonance(thought, iterations);

    /// <summary>
    /// Binds this role vector with a filler using holographic representation.
    /// </summary>
    public static float[] Bind(this float[] role, float[] filler)
        => VectorConvolution.HolographicBind(role, filler);

    /// <summary>
    /// Unbinds using this as the role to retrieve the filler.
    /// </summary>
    public static float[] Unbind(this float[] role, float[] bound)
        => VectorConvolution.HolographicUnbind(bound, role);
}
