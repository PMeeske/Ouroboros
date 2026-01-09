// <copyright file="DCTVectorCompressor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.VectorCompression;

/// <summary>
/// Compresses embedding vectors using Discrete Cosine Transform (DCT).
/// DCT is particularly effective for real-valued signals like embeddings,
/// concentrating energy in fewer coefficients than FFT.
/// </summary>
public sealed class DCTVectorCompressor
{
    private readonly int _keepCoefficients;
    private readonly double _energyThreshold;
    private readonly bool _useAdaptive;

    /// <summary>
    /// Initializes a new instance of the <see cref="DCTVectorCompressor"/> class.
    /// </summary>
    /// <param name="keepCoefficients">Number of DCT coefficients to keep. 0 = adaptive.</param>
    /// <param name="energyThreshold">For adaptive mode, keep coefficients capturing this % of energy.</param>
    public DCTVectorCompressor(int keepCoefficients = 128, double energyThreshold = 0.95)
    {
        _keepCoefficients = keepCoefficients;
        _energyThreshold = energyThreshold;
        _useAdaptive = keepCoefficients <= 0;
    }

    /// <summary>
    /// Compresses a vector using DCT-II (the "standard" DCT).
    /// </summary>
    /// <param name="vector">Input embedding vector.</param>
    /// <returns>Compressed DCT representation.</returns>
    public DCTCompressedVector Compress(float[] vector)
    {
        int n = vector.Length;

        // Apply DCT-II
        var dct = new double[n];
        double sqrt2n = Math.Sqrt(2.0 / n);
        double sqrtN = Math.Sqrt(1.0 / n);

        for (int k = 0; k < n; k++)
        {
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                sum += vector[i] * Math.Cos(Math.PI * k * (2 * i + 1) / (2.0 * n));
            }

            dct[k] = sum * (k == 0 ? sqrtN : sqrt2n);
        }

        // Determine how many coefficients to keep
        int keep;
        if (_useAdaptive)
        {
            keep = DetermineOptimalCoefficients(dct, _energyThreshold);
        }
        else
        {
            keep = Math.Min(_keepCoefficients, n);
        }

        // Keep only significant coefficients (DCT energy is concentrated in low frequencies)
        var coefficients = new float[keep];
        for (int i = 0; i < keep; i++)
        {
            coefficients[i] = (float)dct[i];
        }

        // Compute actual energy retention
        double totalEnergy = dct.Sum(c => c * c);
        double retainedEnergy = coefficients.Sum(c => (double)c * c);

        return new DCTCompressedVector(
            Coefficients: coefficients,
            OriginalLength: n,
            EnergyRetained: totalEnergy > 0 ? retainedEnergy / totalEnergy : 1.0,
            CompressionRatio: (double)n / keep);
    }

    /// <summary>
    /// Decompresses a DCT-compressed vector using inverse DCT (DCT-III).
    /// </summary>
    /// <param name="compressed">Compressed DCT data.</param>
    /// <returns>Reconstructed vector.</returns>
    public float[] Decompress(DCTCompressedVector compressed)
    {
        int n = compressed.OriginalLength;
        int keep = compressed.Coefficients.Length;

        // Pad coefficients with zeros
        var dct = new double[n];
        for (int i = 0; i < keep; i++)
        {
            dct[i] = compressed.Coefficients[i];
        }

        // Apply inverse DCT (DCT-III)
        var result = new float[n];
        double sqrt2n = Math.Sqrt(2.0 / n);
        double sqrtN = Math.Sqrt(1.0 / n);

        for (int i = 0; i < n; i++)
        {
            double sum = dct[0] * sqrtN;
            for (int k = 1; k < n; k++)
            {
                sum += dct[k] * sqrt2n * Math.Cos(Math.PI * k * (2 * i + 1) / (2.0 * n));
            }

            result[i] = (float)sum;
        }

        return result;
    }

    /// <summary>
    /// Computes approximate cosine similarity between two DCT-compressed vectors.
    /// Uses Parseval's theorem - energy in frequency domain equals energy in time domain.
    /// </summary>
    public double CompressedSimilarity(DCTCompressedVector a, DCTCompressedVector b)
    {
        int minLen = Math.Min(a.Coefficients.Length, b.Coefficients.Length);

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < minLen; i++)
        {
            dotProduct += a.Coefficients[i] * b.Coefficients[i];
            normA += a.Coefficients[i] * a.Coefficients[i];
            normB += b.Coefficients[i] * b.Coefficients[i];
        }

        // Add remaining coefficients to norms
        for (int i = minLen; i < a.Coefficients.Length; i++)
            normA += a.Coefficients[i] * a.Coefficients[i];

        for (int i = minLen; i < b.Coefficients.Length; i++)
            normB += b.Coefficients[i] * b.Coefficients[i];

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Batch compress with optimized matrix operations.
    /// </summary>
    public IReadOnlyList<DCTCompressedVector> BatchCompress(IEnumerable<float[]> vectors)
    {
        return vectors.Select(Compress).ToList();
    }

    /// <summary>
    /// Quantizes DCT coefficients for even more compression.
    /// </summary>
    /// <param name="compressed">DCT-compressed vector.</param>
    /// <param name="bits">Bits per coefficient (8 = byte, 16 = short).</param>
    /// <returns>Quantized compressed vector.</returns>
    public QuantizedDCTVector Quantize(DCTCompressedVector compressed, int bits = 8)
    {
        var coeffs = compressed.Coefficients;

        // Find min/max for scaling
        float min = coeffs.Min();
        float max = coeffs.Max();
        float range = max - min;

        if (range < float.Epsilon)
        {
            // All values are the same
            return new QuantizedDCTVector(
                QuantizedCoefficients: new byte[coeffs.Length],
                Min: min,
                Max: max,
                OriginalLength: compressed.OriginalLength,
                BitsPerCoefficient: bits);
        }

        int maxVal = (1 << bits) - 1;
        var quantized = new byte[coeffs.Length * (bits > 8 ? 2 : 1)];

        for (int i = 0; i < coeffs.Length; i++)
        {
            double normalized = (coeffs[i] - min) / range;
            int qVal = (int)Math.Round(normalized * maxVal);
            qVal = Math.Clamp(qVal, 0, maxVal);

            if (bits <= 8)
            {
                quantized[i] = (byte)qVal;
            }
            else
            {
                quantized[i * 2] = (byte)(qVal & 0xFF);
                quantized[i * 2 + 1] = (byte)((qVal >> 8) & 0xFF);
            }
        }

        return new QuantizedDCTVector(
            QuantizedCoefficients: quantized,
            Min: min,
            Max: max,
            OriginalLength: compressed.OriginalLength,
            BitsPerCoefficient: bits);
    }

    /// <summary>
    /// Dequantizes and decompresses a quantized DCT vector.
    /// </summary>
    public float[] DecompressQuantized(QuantizedDCTVector quantized)
    {
        int coeffCount = quantized.BitsPerCoefficient <= 8
            ? quantized.QuantizedCoefficients.Length
            : quantized.QuantizedCoefficients.Length / 2;

        var coeffs = new float[coeffCount];
        float range = quantized.Max - quantized.Min;
        int maxVal = (1 << quantized.BitsPerCoefficient) - 1;

        for (int i = 0; i < coeffCount; i++)
        {
            int qVal;
            if (quantized.BitsPerCoefficient <= 8)
            {
                qVal = quantized.QuantizedCoefficients[i];
            }
            else
            {
                qVal = quantized.QuantizedCoefficients[i * 2] |
                       (quantized.QuantizedCoefficients[i * 2 + 1] << 8);
            }

            coeffs[i] = quantized.Min + (qVal / (float)maxVal) * range;
        }

        var dct = new DCTCompressedVector(coeffs, quantized.OriginalLength, 1.0, 1.0);
        return Decompress(dct);
    }

    private static int DetermineOptimalCoefficients(double[] dct, double threshold)
    {
        double totalEnergy = dct.Sum(c => c * c);
        if (totalEnergy < double.Epsilon)
            return 1;

        double targetEnergy = totalEnergy * threshold;
        double accumulated = 0;

        for (int i = 0; i < dct.Length; i++)
        {
            accumulated += dct[i] * dct[i];
            if (accumulated >= targetEnergy)
                return i + 1;
        }

        return dct.Length;
    }
}

/// <summary>
/// DCT-compressed vector representation.
/// </summary>
/// <param name="Coefficients">DCT coefficients (low-frequency first).</param>
/// <param name="OriginalLength">Original vector dimension.</param>
/// <param name="EnergyRetained">Fraction of energy retained (0-1).</param>
/// <param name="CompressionRatio">Compression ratio achieved.</param>
public sealed record DCTCompressedVector(
    float[] Coefficients,
    int OriginalLength,
    double EnergyRetained,
    double CompressionRatio)
{
    /// <summary>Gets approximate compressed size in bytes.</summary>
    public int CompressedSizeBytes => Coefficients.Length * sizeof(float) + sizeof(int);

    /// <summary>Gets original size in bytes.</summary>
    public int OriginalSizeBytes => OriginalLength * sizeof(float);

    /// <summary>Serializes to bytes.</summary>
    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(OriginalLength);
        writer.Write(Coefficients.Length);

        foreach (var c in Coefficients)
            writer.Write(c);

        return ms.ToArray();
    }

    /// <summary>Deserializes from bytes.</summary>
    public static DCTCompressedVector FromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        int origLen = reader.ReadInt32();
        int coeffLen = reader.ReadInt32();

        var coeffs = new float[coeffLen];
        for (int i = 0; i < coeffLen; i++)
            coeffs[i] = reader.ReadSingle();

        return new DCTCompressedVector(coeffs, origLen, 1.0, (double)origLen / coeffLen);
    }
}

/// <summary>
/// Quantized DCT vector for maximum compression.
/// </summary>
public sealed record QuantizedDCTVector(
    byte[] QuantizedCoefficients,
    float Min,
    float Max,
    int OriginalLength,
    int BitsPerCoefficient)
{
    /// <summary>Gets compressed size in bytes.</summary>
    public int CompressedSizeBytes => QuantizedCoefficients.Length + sizeof(float) * 2 + sizeof(int) * 2;

    /// <summary>Serializes to bytes.</summary>
    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(OriginalLength);
        writer.Write(BitsPerCoefficient);
        writer.Write(Min);
        writer.Write(Max);
        writer.Write(QuantizedCoefficients.Length);
        writer.Write(QuantizedCoefficients);

        return ms.ToArray();
    }

    /// <summary>Deserializes from bytes.</summary>
    public static QuantizedDCTVector FromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        int origLen = reader.ReadInt32();
        int bits = reader.ReadInt32();
        float min = reader.ReadSingle();
        float max = reader.ReadSingle();
        int qLen = reader.ReadInt32();
        var quantized = reader.ReadBytes(qLen);

        return new QuantizedDCTVector(quantized, min, max, origLen, bits);
    }
}
