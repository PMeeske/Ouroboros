// <copyright file="VectorCompressionService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.VectorCompression;

/// <summary>
/// Unified service for vector compression using spectral methods.
/// Supports FFT and DCT-based compression with configurable quality/size tradeoffs.
/// </summary>
public sealed class VectorCompressionService
{
    private readonly FourierVectorCompressor _fftCompressor;
    private readonly DCTVectorCompressor _dctCompressor;
    private readonly CompressionMethod _defaultMethod;

    /// <summary>
    /// Available compression methods.
    /// </summary>
    public enum CompressionMethod
    {
        /// <summary>Discrete Cosine Transform - best for real-valued embeddings.</summary>
        DCT,

        /// <summary>Fast Fourier Transform - good for periodic patterns.</summary>
        FFT,

        /// <summary>Quantized DCT - maximum compression with some quality loss.</summary>
        QuantizedDCT,

        /// <summary>Adaptive - auto-select based on vector characteristics.</summary>
        Adaptive
    }

    /// <summary>
    /// Compression statistics for monitoring.
    /// </summary>
    public record CompressionStats(
        int VectorsCompressed,
        long OriginalBytes,
        long CompressedBytes,
        double AverageCompressionRatio,
        double AverageEnergyRetained);

    private int _vectorsCompressed;
    private long _originalBytes;
    private long _compressedBytes;
    private double _totalEnergyRetained;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorCompressionService"/> class.
    /// </summary>
    /// <param name="targetDimension">Target dimension for compressed vectors.</param>
    /// <param name="energyThreshold">Energy retention threshold (0.9-0.99).</param>
    /// <param name="defaultMethod">Default compression method.</param>
    public VectorCompressionService(
        int targetDimension = 128,
        double energyThreshold = 0.95,
        CompressionMethod defaultMethod = CompressionMethod.DCT)
    {
        _fftCompressor = new FourierVectorCompressor(
            targetDimension,
            FourierVectorCompressor.CompressionStrategy.HighestMagnitude);

        _dctCompressor = new DCTVectorCompressor(
            targetDimension,
            energyThreshold);

        _defaultMethod = defaultMethod;
    }

    /// <summary>
    /// Compresses a vector using the specified or default method.
    /// </summary>
    /// <param name="vector">Input embedding vector.</param>
    /// <param name="method">Compression method (null = default).</param>
    /// <returns>Compressed vector data.</returns>
    public byte[] Compress(float[] vector, CompressionMethod? method = null)
    {
        var m = method ?? _defaultMethod;

        if (m == CompressionMethod.Adaptive)
        {
            m = SelectOptimalMethod(vector);
        }

        byte[] result;
        double energyRetained = 1.0;

        switch (m)
        {
            case CompressionMethod.DCT:
                var dct = _dctCompressor.Compress(vector);
                result = WrapWithHeader(CompressionMethod.DCT, dct.ToBytes());
                energyRetained = dct.EnergyRetained;
                break;

            case CompressionMethod.QuantizedDCT:
                var dctQ = _dctCompressor.Compress(vector);
                var quantized = _dctCompressor.Quantize(dctQ, 8);
                result = WrapWithHeader(CompressionMethod.QuantizedDCT, quantized.ToBytes());
                energyRetained = dctQ.EnergyRetained;
                break;

            case CompressionMethod.FFT:
                var fft = _fftCompressor.Compress(vector);
                result = WrapWithHeader(CompressionMethod.FFT, fft.ToBytes());
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }

        // Update stats
        Interlocked.Increment(ref _vectorsCompressed);
        Interlocked.Add(ref _originalBytes, vector.Length * sizeof(float));
        Interlocked.Add(ref _compressedBytes, result.Length);
        Interlocked.Exchange(ref _totalEnergyRetained, _totalEnergyRetained + energyRetained);

        return result;
    }

    /// <summary>
    /// Decompresses vector data back to float array.
    /// </summary>
    /// <param name="data">Compressed vector data.</param>
    /// <returns>Decompressed vector.</returns>
    public float[] Decompress(byte[] data)
    {
        var (method, payload) = UnwrapHeader(data);

        return method switch
        {
            CompressionMethod.DCT => _dctCompressor.Decompress(DCTCompressedVector.FromBytes(payload)),
            CompressionMethod.QuantizedDCT => _dctCompressor.DecompressQuantized(QuantizedDCTVector.FromBytes(payload)),
            CompressionMethod.FFT => _fftCompressor.Decompress(CompressedVector.FromBytes(payload)),
            _ => throw new InvalidOperationException($"Unknown compression method: {method}")
        };
    }

    /// <summary>
    /// Computes similarity between two compressed vectors without full decompression.
    /// </summary>
    public double CompressedSimilarity(byte[] a, byte[] b)
    {
        var (methodA, payloadA) = UnwrapHeader(a);
        var (methodB, payloadB) = UnwrapHeader(b);

        if (methodA != methodB)
        {
            // Fall back to full decompression if methods differ
            var vecA = Decompress(a);
            var vecB = Decompress(b);
            return CosineSimilarity(vecA, vecB);
        }

        return methodA switch
        {
            CompressionMethod.DCT => _dctCompressor.CompressedSimilarity(
                DCTCompressedVector.FromBytes(payloadA),
                DCTCompressedVector.FromBytes(payloadB)),
            CompressionMethod.FFT => _fftCompressor.CompressedSimilarity(
                CompressedVector.FromBytes(payloadA),
                CompressedVector.FromBytes(payloadB)),
            _ => CosineSimilarity(Decompress(a), Decompress(b))
        };
    }

    /// <summary>
    /// Gets compression statistics.
    /// </summary>
    public CompressionStats GetStats()
    {
        int count = _vectorsCompressed;
        return new CompressionStats(
            VectorsCompressed: count,
            OriginalBytes: _originalBytes,
            CompressedBytes: _compressedBytes,
            AverageCompressionRatio: count > 0 ? (double)_originalBytes / _compressedBytes : 1.0,
            AverageEnergyRetained: count > 0 ? _totalEnergyRetained / count : 1.0);
    }

    /// <summary>
    /// Batch compress multiple vectors efficiently.
    /// </summary>
    public IReadOnlyList<byte[]> BatchCompress(IEnumerable<float[]> vectors, CompressionMethod? method = null)
    {
        return vectors.AsParallel().Select(v => Compress(v, method)).ToList();
    }

    /// <summary>
    /// Analyzes a vector and returns compression statistics preview.
    /// </summary>
    public CompressionPreview Preview(float[] vector, CompressionMethod method)
    {
        var dct = _dctCompressor.Compress(vector);
        var fft = _fftCompressor.Compress(vector);

        return new CompressionPreview(
            OriginalDimension: vector.Length,
            OriginalSizeBytes: vector.Length * sizeof(float),
            DCTCompressedSize: dct.ToBytes().Length,
            DCTEnergyRetained: dct.EnergyRetained,
            FFTCompressedSize: fft.ToBytes().Length,
            FFTCompressionRatio: fft.CompressionRatio,
            QuantizedDCTSize: _dctCompressor.Quantize(dct, 8).ToBytes().Length);
    }

    private CompressionMethod SelectOptimalMethod(float[] vector)
    {
        // Use DCT for typical embedding vectors - it's more efficient for real-valued data
        // FFT is better when there are periodic patterns

        // Quick heuristic: check if vector has periodic structure
        double periodicScore = ComputePeriodicityScore(vector);

        return periodicScore > 0.7 ? CompressionMethod.FFT : CompressionMethod.DCT;
    }

    private static double ComputePeriodicityScore(float[] vector)
    {
        if (vector.Length < 16)
            return 0;

        // Simple autocorrelation check for periodicity
        int testLag = vector.Length / 4;
        double mean = vector.Average();
        double variance = vector.Sum(v => (v - mean) * (v - mean)) / vector.Length;

        if (variance < float.Epsilon)
            return 0;

        double autocorr = 0;
        for (int i = 0; i < vector.Length - testLag; i++)
        {
            autocorr += (vector[i] - mean) * (vector[i + testLag] - mean);
        }

        autocorr /= (vector.Length - testLag) * variance;

        return Math.Abs(autocorr);
    }

    private static byte[] WrapWithHeader(CompressionMethod method, byte[] payload)
    {
        // Header: 4 bytes magic + 1 byte method + 4 bytes payload length
        var result = new byte[9 + payload.Length];
        result[0] = (byte)'O';
        result[1] = (byte)'V';
        result[2] = (byte)'C';
        result[3] = (byte)'1'; // Version 1
        result[4] = (byte)method;
        BitConverter.GetBytes(payload.Length).CopyTo(result, 5);
        payload.CopyTo(result, 9);
        return result;
    }

    private static (CompressionMethod Method, byte[] Payload) UnwrapHeader(byte[] data)
    {
        if (data.Length < 9 || data[0] != 'O' || data[1] != 'V' || data[2] != 'C')
        {
            throw new InvalidOperationException("Invalid compressed vector format");
        }

        var method = (CompressionMethod)data[4];
        int payloadLen = BitConverter.ToInt32(data, 5);
        var payload = new byte[payloadLen];
        Array.Copy(data, 9, payload, 0, payloadLen);

        return (method, payload);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < len; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}

/// <summary>
/// Preview of compression options for a vector.
/// </summary>
public sealed record CompressionPreview(
    int OriginalDimension,
    int OriginalSizeBytes,
    int DCTCompressedSize,
    double DCTEnergyRetained,
    int FFTCompressedSize,
    double FFTCompressionRatio,
    int QuantizedDCTSize)
{
    /// <summary>Best compression ratio achievable.</summary>
    public double BestCompressionRatio => (double)OriginalSizeBytes / Math.Min(DCTCompressedSize, Math.Min(FFTCompressedSize, QuantizedDCTSize));

    /// <summary>Recommended method based on size/quality tradeoff.</summary>
    public VectorCompressionService.CompressionMethod RecommendedMethod
    {
        get
        {
            if (QuantizedDCTSize < DCTCompressedSize / 2 && DCTEnergyRetained > 0.9)
                return VectorCompressionService.CompressionMethod.QuantizedDCT;

            return DCTCompressedSize <= FFTCompressedSize
                ? VectorCompressionService.CompressionMethod.DCT
                : VectorCompressionService.CompressionMethod.FFT;
        }
    }
}
