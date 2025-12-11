#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Valence Monitor Implementation
// Phase 3: Affective Dynamics - Synthetic affective states
// Uses Fourier transform for stress pattern detection
// ==========================================================

using System.Collections.Concurrent;
using System.Numerics;

namespace LangChainPipeline.Agent.MetaAI.Affect;

/// <summary>
/// Implementation of valence monitoring with spectral stress detection.
/// </summary>
public sealed class ValenceMonitor : IValenceMonitor
{
    private readonly ConcurrentDictionary<SignalType, ConcurrentQueue<ValenceSignal>> _signals = new();
    private readonly ConcurrentBag<AffectiveState> _stateHistory = new();
    private readonly AffectConfig _config;
    private readonly object _lock = new();

    // Current state values
    private double _valence = 0.0;
    private double _stress = 0.0;
    private double _confidence = 0.5;
    private double _curiosity = 0.3;
    private double _arousal = 0.5;

    public ValenceMonitor(AffectConfig? config = null)
    {
        _config = config ?? new AffectConfig();

        // Initialize signal queues for each type
        foreach (SignalType type in Enum.GetValues<SignalType>())
        {
            _signals[type] = new ConcurrentQueue<ValenceSignal>();
        }
    }

    public AffectiveState GetCurrentState()
    {
        var state = new AffectiveState(
            Guid.NewGuid(),
            _valence,
            _stress,
            _confidence,
            _curiosity,
            _arousal,
            DateTime.UtcNow,
            new Dictionary<string, object>
            {
                ["signal_count"] = _signals.Values.Sum(q => q.Count)
            });

        _stateHistory.Add(state);
        return state;
    }

    public void RecordSignal(string source, double value, SignalType type)
    {
        ArgumentNullException.ThrowIfNull(source);

        var signal = new ValenceSignal(
            source,
            Math.Clamp(value, -1.0, 1.0),
            type,
            DateTime.UtcNow,
            null);

        if (_signals.TryGetValue(type, out var queue))
        {
            queue.Enqueue(signal);

            // Maintain max history size
            while (queue.Count > _config.SignalHistorySize)
            {
                queue.TryDequeue(out _);
            }
        }

        // Update internal state based on signal type
        UpdateInternalState(type, value);
    }

    public async Task<StressDetectionResult> DetectStressAsync(CancellationToken ct = default)
    {
        // Get stress signal history
        double[] stressSignals = GetSignalHistory(SignalType.Stress);

        if (stressSignals.Length < 4)
        {
            // Not enough data for spectral analysis
            return new StressDetectionResult(
                _stress,
                0.0,
                0.0,
                false,
                new List<double>(),
                "Insufficient data for spectral analysis",
                DateTime.UtcNow);
        }

        // Perform Fourier Transform for spectral analysis
        var (frequency, amplitude, spectralPeaks) = await Task.Run(() =>
            PerformSpectralAnalysis(stressSignals), ct);

        // Detect anomalies based on spectral peaks
        bool isAnomalous = spectralPeaks.Any(p => p > _config.StressThreshold);
        double detectedStress = spectralPeaks.Count > 0 ? spectralPeaks.Max() : _stress;

        // Build analysis description
        string analysis = isAnomalous
            ? $"Anomalous stress pattern detected: {spectralPeaks.Count} peaks above threshold. " +
              $"Dominant frequency: {frequency:F2} Hz, Amplitude: {amplitude:F3}"
            : $"Normal stress levels. Frequency: {frequency:F2} Hz, Amplitude: {amplitude:F3}";

        return new StressDetectionResult(
            detectedStress,
            frequency,
            amplitude,
            isAnomalous,
            spectralPeaks,
            analysis,
            DateTime.UtcNow);
    }

    public void UpdateConfidence(string taskId, bool success, double weight = 1.0)
    {
        ArgumentNullException.ThrowIfNull(taskId);
        weight = Math.Clamp(weight, 0.0, 1.0);

        lock (_lock)
        {
            double change = success ? weight * 0.1 : -weight * 0.15;
            _confidence = Math.Clamp(_confidence + change, 0.0, 1.0);

            // Apply decay to prevent overconfidence
            _confidence *= (1.0 - _config.ConfidenceDecayRate);
        }

        // Record as signal
        RecordSignal(taskId, success ? _confidence : -_confidence, SignalType.Confidence);
    }

    public void UpdateCuriosity(double noveltyScore, string context)
    {
        ArgumentNullException.ThrowIfNull(context);
        noveltyScore = Math.Clamp(noveltyScore, 0.0, 1.0);

        lock (_lock)
        {
            // High novelty increases curiosity
            double boost = noveltyScore * _config.CuriosityBoostFactor;
            _curiosity = Math.Clamp(_curiosity + boost - 0.05, 0.0, 1.0); // Slight decay
        }

        RecordSignal(context, noveltyScore, SignalType.Curiosity);
    }

    public List<ValenceSignal> GetRecentSignals(SignalType type, int count = 100)
    {
        if (_signals.TryGetValue(type, out var queue))
        {
            return queue.TakeLast(count).ToList();
        }
        return new List<ValenceSignal>();
    }

    public double[] GetSignalHistory(SignalType type)
    {
        if (_signals.TryGetValue(type, out var queue))
        {
            return queue.Select(s => s.Value).ToArray();
        }
        return Array.Empty<double>();
    }

    public double GetRunningAverage(SignalType type, int windowSize = 10)
    {
        var signals = GetRecentSignals(type, windowSize);
        return signals.Count > 0 ? signals.Average(s => s.Value) : 0.0;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _valence = 0.0;
            _stress = 0.0;
            _confidence = 0.5;
            _curiosity = 0.3;
            _arousal = 0.5;
        }

        foreach (var queue in _signals.Values)
        {
            while (queue.TryDequeue(out _)) { }
        }
    }

    public List<AffectiveState> GetStateHistory(int count = 50)
    {
        return _stateHistory
            .OrderByDescending(s => s.Timestamp)
            .Take(count)
            .ToList();
    }

    private void UpdateInternalState(SignalType type, double value)
    {
        lock (_lock)
        {
            switch (type)
            {
                case SignalType.Stress:
                    // Exponential moving average for stress
                    _stress = (_stress * 0.8) + (Math.Abs(value) * 0.2);
                    // High stress reduces arousal
                    _arousal = Math.Clamp(_arousal - (value * 0.05), 0.0, 1.0);
                    break;

                case SignalType.Confidence:
                    // Already handled in UpdateConfidence
                    break;

                case SignalType.Curiosity:
                    // Already handled in UpdateCuriosity
                    // High curiosity increases arousal
                    _arousal = Math.Clamp(_arousal + (value * 0.05), 0.0, 1.0);
                    break;

                case SignalType.Valence:
                    // Direct valence update with smoothing
                    _valence = (_valence * 0.7) + (value * 0.3);
                    break;

                case SignalType.Arousal:
                    _arousal = (_arousal * 0.8) + (value * 0.2);
                    break;
            }

            // Update composite valence based on all factors
            UpdateCompositeValence();
        }
    }

    private void UpdateCompositeValence()
    {
        // Composite valence is influenced by all affective components
        // Positive: high confidence, high curiosity, low stress
        // Negative: low confidence, high stress
        double positiveFactors = (_confidence * 0.4) + (_curiosity * 0.3);
        double negativeFactors = _stress * 0.5;

        _valence = Math.Clamp(positiveFactors - negativeFactors, -1.0, 1.0);
    }

    /// <summary>
    /// Performs Discrete Fourier Transform (DFT) for spectral analysis.
    /// Uses Cooley-Tukey FFT algorithm for efficiency.
    /// </summary>
    private (double frequency, double amplitude, List<double> spectralPeaks) PerformSpectralAnalysis(double[] signals)
    {
        int n = signals.Length;

        // Ensure power of 2 for FFT (pad if necessary)
        int fftSize = NextPowerOfTwo(n);
        var paddedSignals = new double[fftSize];
        Array.Copy(signals, paddedSignals, n);

        // Apply Hanning window to reduce spectral leakage
        ApplyHanningWindow(paddedSignals, n);

        // Perform FFT
        var spectrum = PerformFFT(paddedSignals);

        // Calculate magnitudes (power spectrum)
        var magnitudes = new double[fftSize / 2];
        for (int i = 0; i < fftSize / 2; i++)
        {
            magnitudes[i] = spectrum[i].Magnitude / fftSize;
        }

        // Find peaks (local maxima above threshold)
        var peaks = new List<double>();
        double threshold = _config.StressThreshold * 0.5; // Lower threshold for peak detection

        for (int i = 1; i < magnitudes.Length - 1; i++)
        {
            if (magnitudes[i] > magnitudes[i - 1] &&
                magnitudes[i] > magnitudes[i + 1] &&
                magnitudes[i] > threshold)
            {
                peaks.Add(magnitudes[i]);
            }
        }

        // Find dominant frequency
        int dominantBin = 0;
        double maxMagnitude = 0;
        for (int i = 1; i < magnitudes.Length; i++) // Skip DC component
        {
            if (magnitudes[i] > maxMagnitude)
            {
                maxMagnitude = magnitudes[i];
                dominantBin = i;
            }
        }

        // Convert bin to frequency (assuming 1 sample per second)
        double dominantFrequency = (double)dominantBin / fftSize;

        return (dominantFrequency, maxMagnitude, peaks);
    }

    private static void ApplyHanningWindow(double[] data, int length)
    {
        for (int i = 0; i < length; i++)
        {
            double window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (length - 1)));
            data[i] *= window;
        }
    }

    /// <summary>
    /// Cooley-Tukey FFT implementation.
    /// </summary>
    private static Complex[] PerformFFT(double[] input)
    {
        int n = input.Length;
        var data = new Complex[n];

        for (int i = 0; i < n; i++)
        {
            data[i] = new Complex(input[i], 0);
        }

        // Bit-reverse permutation
        int bits = (int)Math.Log2(n);
        for (int i = 0; i < n; i++)
        {
            int j = ReverseBits(i, bits);
            if (j > i)
            {
                (data[i], data[j]) = (data[j], data[i]);
            }
        }

        // Cooley-Tukey iterative FFT
        for (int size = 2; size <= n; size *= 2)
        {
            double angle = -2 * Math.PI / size;
            var wn = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int start = 0; start < n; start += size)
            {
                var w = Complex.One;
                for (int k = 0; k < size / 2; k++)
                {
                    var even = data[start + k];
                    var odd = data[start + k + size / 2] * w;

                    data[start + k] = even + odd;
                    data[start + k + size / 2] = even - odd;

                    w *= wn;
                }
            }
        }

        return data;
    }

    private static int ReverseBits(int value, int bits)
    {
        int result = 0;
        for (int i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }

    private static int NextPowerOfTwo(int n)
    {
        int power = 1;
        while (power < n)
        {
            power *= 2;
        }
        return power;
    }
}
