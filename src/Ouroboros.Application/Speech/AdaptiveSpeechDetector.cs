// <copyright file="AdaptiveSpeechDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Speech;

/// <summary>
/// Adaptive Voice Activity Detection (VAD) system that learns from ambient noise
/// and adjusts sensitivity dynamically for optimal speech detection.
/// </summary>
public sealed class AdaptiveSpeechDetector : IDisposable
{
    // === Configuration ===
    private readonly SpeechDetectionConfig _config;

    // === Adaptive State ===
    private double _ambientNoiseFloor = 0.02;      // Base noise level (RMS)
    private double _currentThreshold = 0.04;       // Dynamic detection threshold
    private double _peakSpeechLevel = 0.3;         // Typical peak speech level
    private readonly CircularBuffer<double> _noiseHistory;
    private readonly CircularBuffer<double> _speechHistory;
    private readonly CircularBuffer<SpeechSegment> _recentSegments;

    // === Detection State ===
    private DateTime _lastSpeechTime = DateTime.MinValue;
    private DateTime _silenceStartTime = DateTime.UtcNow;
    private int _consecutiveSpeechFrames = 0;
    private int _consecutiveSilenceFrames = 0;
    private SpeechState _currentState = SpeechState.Silence;

    // === Self-Voice Exclusion ===
    private volatile bool _isSelfSpeaking = false;
    private DateTime _selfSpeechEndTime = DateTime.MinValue;
    private TimeSpan _selfSpeechCooldown = TimeSpan.FromMilliseconds(500);
    private readonly CircularBuffer<AudioFingerprint> _selfVoiceFingerprints;
    private const int MaxSelfVoiceFingerprints = 50;
    private double _selfVoiceEnergyBaseline = 0.0;
    private double _selfVoiceZcrBaseline = 0.0;
    private int _selfVoiceSamplesCollected = 0;

    // === Statistics ===
    private long _totalFramesAnalyzed = 0;
    private long _speechFramesDetected = 0;
    private readonly ConcurrentDictionary<string, int> _environmentProfile;

    /// <summary>
    /// Current speech detection state.
    /// </summary>
    public enum SpeechState
    {
        /// <summary>No speech detected - ambient silence.</summary>
        Silence,

        /// <summary>Possible speech onset - waiting for confirmation.</summary>
        SpeechOnset,

        /// <summary>Active speech detected.</summary>
        Speaking,

        /// <summary>Speech trailing off - possible end.</summary>
        SpeechOffset,

        /// <summary>Brief pause in speech (not end of utterance).</summary>
        Pause
    }

    /// <summary>
    /// Result of analyzing an audio segment.
    /// </summary>
    /// <param name="HasSpeech">Whether speech was detected.</param>
    /// <param name="Confidence">Confidence level 0.0-1.0.</param>
    /// <param name="EnergyLevel">RMS energy of the segment.</param>
    /// <param name="State">Current detection state.</param>
    /// <param name="IsUtteranceComplete">Whether this marks end of an utterance.</param>
    /// <param name="SuggestedAction">What the caller should do.</param>
    public record SpeechAnalysisResult(
        bool HasSpeech,
        double Confidence,
        double EnergyLevel,
        SpeechState State,
        bool IsUtteranceComplete,
        SuggestedAction SuggestedAction);

    /// <summary>
    /// Suggested action for the caller after analysis.
    /// </summary>
    public enum SuggestedAction
    {
        /// <summary>Continue recording and listening.</summary>
        ContinueListening,

        /// <summary>Process the accumulated audio for transcription.</summary>
        ProcessAudio,

        /// <summary>Discard this audio segment (noise/silence).</summary>
        DiscardSegment,

        /// <summary>Wait for more audio before deciding.</summary>
        WaitForMore
    }

    /// <summary>
    /// Configuration for speech detection behavior.
    /// </summary>
    public record SpeechDetectionConfig(
        double InitialThreshold = 0.04,
        double MinThreshold = 0.015,
        double MaxThreshold = 0.15,
        int SpeechOnsetFrames = 2,
        int SpeechOffsetFrames = 8,
        double AdaptationRate = 0.02,
        int HistorySize = 100,
        double SpeechToNoiseRatio = 2.5,
        bool EnableZeroCrossingRate = true,
        bool EnableSpectralAnalysis = false,
        int SampleRate = 16000);

    /// <summary>
    /// Represents a detected speech segment.
    /// </summary>
    public record SpeechSegment(
        DateTime StartTime,
        DateTime EndTime,
        double PeakEnergy,
        double AverageEnergy,
        int FrameCount);

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveSpeechDetector"/> class.
    /// </summary>
    /// <param name="config">Optional configuration override.</param>
    public AdaptiveSpeechDetector(SpeechDetectionConfig? config = null)
    {
        _config = config ?? new SpeechDetectionConfig();
        _currentThreshold = _config.InitialThreshold;
        _noiseHistory = new CircularBuffer<double>(_config.HistorySize);
        _speechHistory = new CircularBuffer<double>(_config.HistorySize);
        _recentSegments = new CircularBuffer<SpeechSegment>(20);
        _environmentProfile = new ConcurrentDictionary<string, int>();
        _selfVoiceFingerprints = new CircularBuffer<AudioFingerprint>(MaxSelfVoiceFingerprints);
    }

    /// <summary>
    /// Analyzes raw PCM audio data (16-bit, mono) for speech activity.
    /// </summary>
    /// <param name="audioData">Raw PCM audio data.</param>
    /// <returns>Analysis result with speech detection info.</returns>
    public SpeechAnalysisResult AnalyzeAudio(byte[] audioData)
    {
        if (audioData == null || audioData.Length < 64)
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }

        // Self-voice exclusion: skip analysis during TTS playback + cooldown
        if (IsSelfVoiceActive())
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }

        _totalFramesAnalyzed++;

        // Calculate audio features
        var features = ExtractAudioFeatures(audioData);

        // Check if this matches self-voice fingerprint (echo/feedback detection)
        if (MatchesSelfVoiceFingerprint(features))
        {
            return new SpeechAnalysisResult(
                false, 0.0, features.RmsEnergy, _currentState, false, SuggestedAction.DiscardSegment);
        }

        // Update adaptive thresholds
        UpdateAdaptiveThresholds(features);

        // Determine if this is speech
        bool isSpeech = DetectSpeech(features);
        double confidence = CalculateConfidence(features, isSpeech);

        // Update state machine
        var previousState = _currentState;
        UpdateStateMachine(isSpeech, features);

        // Determine suggested action
        var action = DetermineAction(isSpeech, features);
        bool utteranceComplete = _currentState == SpeechState.Silence &&
                                  previousState is SpeechState.SpeechOffset or SpeechState.Speaking;

        if (utteranceComplete && _consecutiveSpeechFrames > 0)
        {
            RecordSpeechSegment(features);
        }

        return new SpeechAnalysisResult(
            isSpeech,
            confidence,
            features.RmsEnergy,
            _currentState,
            utteranceComplete,
            action);
    }

    /// <summary>
    /// Analyzes a WAV file for speech activity.
    /// </summary>
    /// <param name="wavFilePath">Path to the WAV file.</param>
    /// <returns>Analysis result.</returns>
    public SpeechAnalysisResult AnalyzeWavFile(string wavFilePath)
    {
        if (!File.Exists(wavFilePath))
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }

        try
        {
            byte[] fileData = File.ReadAllBytes(wavFilePath);

            // Skip WAV header (typically 44 bytes) to get raw PCM data
            int headerSize = FindWavDataOffset(fileData);
            if (headerSize >= fileData.Length)
            {
                return new SpeechAnalysisResult(
                    false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
            }

            byte[] pcmData = new byte[fileData.Length - headerSize];
            Array.Copy(fileData, headerSize, pcmData, 0, pcmData.Length);

            return AnalyzeAudio(pcmData);
        }
        catch
        {
            return new SpeechAnalysisResult(
                false, 0.0, 0.0, _currentState, false, SuggestedAction.DiscardSegment);
        }
    }

    // ========================================================================
    // SELF-VOICE EXCLUSION API
    // ========================================================================

    /// <summary>
    /// Notifies the detector that TTS/self-speech has started.
    /// Audio will be ignored until NotifySelfSpeechEnded is called.
    /// </summary>
    public void NotifySelfSpeechStarted()
    {
        _isSelfSpeaking = true;
        ResetState(); // Clear any pending speech detection
    }

    /// <summary>
    /// Notifies the detector that TTS/self-speech has ended.
    /// A cooldown period will be applied before resuming detection.
    /// </summary>
    /// <param name="cooldownMs">Optional cooldown in milliseconds (default 500ms).</param>
    public void NotifySelfSpeechEnded(int cooldownMs = 500)
    {
        _isSelfSpeaking = false;
        _selfSpeechCooldown = TimeSpan.FromMilliseconds(cooldownMs);
        _selfSpeechEndTime = DateTime.UtcNow;
        ResetState();
    }

    /// <summary>
    /// Registers audio that the system is about to play (TTS output).
    /// Used to create a fingerprint for echo/feedback detection.
    /// </summary>
    /// <param name="ttsAudioData">Raw PCM audio data of TTS output.</param>
    public void RegisterSelfVoiceAudio(byte[] ttsAudioData)
    {
        if (ttsAudioData == null || ttsAudioData.Length < 64) return;

        var features = ExtractAudioFeatures(ttsAudioData);
        var fingerprint = new AudioFingerprint(
            features.RmsEnergy,
            features.ZeroCrossingRate,
            features.SpectralCentroid,
            features.LowFreqEnergy / (features.HighFreqEnergy + 0.001),
            DateTime.UtcNow);

        _selfVoiceFingerprints.Add(fingerprint);

        // Update running baseline of self-voice characteristics
        _selfVoiceSamplesCollected++;
        double alpha = Math.Min(0.3, 1.0 / _selfVoiceSamplesCollected);
        _selfVoiceEnergyBaseline = _selfVoiceEnergyBaseline * (1 - alpha) + features.RmsEnergy * alpha;
        _selfVoiceZcrBaseline = _selfVoiceZcrBaseline * (1 - alpha) + features.ZeroCrossingRate * alpha;
    }

    /// <summary>
    /// Sets the cooldown period after self-speech ends.
    /// </summary>
    /// <param name="cooldown">Cooldown duration.</param>
    public void SetSelfSpeechCooldown(TimeSpan cooldown)
    {
        _selfSpeechCooldown = cooldown;
    }

    /// <summary>
    /// Checks if self-voice exclusion is currently active.
    /// </summary>
    public bool IsSelfVoiceActive()
    {
        if (_isSelfSpeaking) return true;

        // Check cooldown period
        if (_selfSpeechEndTime != DateTime.MinValue)
        {
            var elapsed = DateTime.UtcNow - _selfSpeechEndTime;
            if (elapsed < _selfSpeechCooldown)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears all self-voice fingerprints (e.g., when voice profile changes).
    /// </summary>
    public void ClearSelfVoiceProfile()
    {
        _selfVoiceSamplesCollected = 0;
        _selfVoiceEnergyBaseline = 0.0;
        _selfVoiceZcrBaseline = 0.0;
    }

    /// <summary>
    /// Fingerprint for audio segment comparison.
    /// </summary>
    private record AudioFingerprint(
        double Energy,
        double ZeroCrossingRate,
        double SpectralCentroid,
        double FrequencyRatio,
        DateTime Timestamp);

    /// <summary>
    /// Checks if audio features match the learned self-voice profile.
    /// Used to detect echo/feedback from speakers being picked up by mic.
    /// </summary>
    private bool MatchesSelfVoiceFingerprint(AudioFeatures features)
    {
        // Need sufficient samples to have a valid profile
        if (_selfVoiceSamplesCollected < 5) return false;

        // Check if within recent self-speech window (echo detection)
        var recentWindow = TimeSpan.FromSeconds(2);
        var fingerprints = _selfVoiceFingerprints.ToArray()
            .Where(fp => DateTime.UtcNow - fp.Timestamp < recentWindow)
            .ToArray();

        if (fingerprints.Length == 0) return false;

        // Compare current audio to self-voice baseline
        double energyDiff = Math.Abs(features.RmsEnergy - _selfVoiceEnergyBaseline) / (_selfVoiceEnergyBaseline + 0.001);
        double zcrDiff = Math.Abs(features.ZeroCrossingRate - _selfVoiceZcrBaseline) / (_selfVoiceZcrBaseline + 0.001);

        // If very similar to self-voice profile, likely echo
        bool energySimilar = energyDiff < 0.3;
        bool zcrSimilar = zcrDiff < 0.4;

        // Also check against recent fingerprints for direct echo matching
        foreach (var fp in fingerprints)
        {
            double fpEnergyDiff = Math.Abs(features.RmsEnergy - fp.Energy) / (fp.Energy + 0.001);
            double fpZcrDiff = Math.Abs(features.ZeroCrossingRate - fp.ZeroCrossingRate) / (fp.ZeroCrossingRate + 0.001);
            double fpSpectralDiff = Math.Abs(features.SpectralCentroid - fp.SpectralCentroid) / (fp.SpectralCentroid + 0.001);

            // Strong match to a recent self-voice segment
            if (fpEnergyDiff < 0.25 && fpZcrDiff < 0.3 && fpSpectralDiff < 0.35)
            {
                return true;
            }
        }

        // Baseline profile match (for consistent TTS voice)
        return energySimilar && zcrSimilar && _selfVoiceSamplesCollected >= 10;
    }

    // ========================================================================

    /// <summary>
    /// Calibrates the detector to the current ambient noise environment.
    /// Should be called during quiet periods to establish baseline.
    /// </summary>
    /// <param name="audioData">Audio sample of ambient noise.</param>
    public void CalibrateToAmbientNoise(byte[] audioData)
    {
        if (audioData == null || audioData.Length < 64) return;

        var features = ExtractAudioFeatures(audioData);

        // Update noise floor with heavy smoothing for calibration
        _ambientNoiseFloor = _ambientNoiseFloor * 0.3 + features.RmsEnergy * 0.7;

        // Set threshold above noise floor
        _currentThreshold = Math.Max(
            _config.MinThreshold,
            _ambientNoiseFloor * _config.SpeechToNoiseRatio);

        _environmentProfile.AddOrUpdate("calibration_count", 1, (_, v) => v + 1);

        Console.WriteLine($"  [🎚️] Calibrated: noise floor={_ambientNoiseFloor:F4}, threshold={_currentThreshold:F4}");
    }

    /// <summary>
    /// Resets the detector state, keeping learned thresholds.
    /// </summary>
    public void ResetState()
    {
        _currentState = SpeechState.Silence;
        _consecutiveSpeechFrames = 0;
        _consecutiveSilenceFrames = 0;
        _silenceStartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets current detection statistics.
    /// </summary>
    public DetectionStatistics GetStatistics() => new(
        TotalFrames: _totalFramesAnalyzed,
        SpeechFrames: _speechFramesDetected,
        SpeechRatio: _totalFramesAnalyzed > 0 ? (double)_speechFramesDetected / _totalFramesAnalyzed : 0,
        CurrentNoiseFloor: _ambientNoiseFloor,
        CurrentThreshold: _currentThreshold,
        PeakSpeechLevel: _peakSpeechLevel,
        CurrentState: _currentState,
        RecentSegments: _recentSegments.ToArray());

    /// <summary>
    /// Detection statistics record.
    /// </summary>
    public record DetectionStatistics(
        long TotalFrames,
        long SpeechFrames,
        double SpeechRatio,
        double CurrentNoiseFloor,
        double CurrentThreshold,
        double PeakSpeechLevel,
        SpeechState CurrentState,
        SpeechSegment[] RecentSegments);

    // === Private Methods ===

    private record AudioFeatures(
        double RmsEnergy,
        double PeakAmplitude,
        double ZeroCrossingRate,
        double LowFreqEnergy,
        double HighFreqEnergy,
        double SpectralCentroid,
        bool HasClipping);

    private AudioFeatures ExtractAudioFeatures(byte[] audioData)
    {
        // Convert bytes to 16-bit samples
        int sampleCount = audioData.Length / 2;
        short[] samples = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = BitConverter.ToInt16(audioData, i * 2);
        }

        // Calculate RMS energy (normalized)
        double sumSquares = 0;
        double peak = 0;
        int zeroCrossings = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            double normalized = samples[i] / 32768.0;
            sumSquares += normalized * normalized;
            peak = Math.Max(peak, Math.Abs(normalized));

            if (i > 0 && ((samples[i] >= 0) != (samples[i - 1] >= 0)))
            {
                zeroCrossings++;
            }
        }

        double rmsEnergy = Math.Sqrt(sumSquares / sampleCount);
        double zcr = (double)zeroCrossings / sampleCount;

        // Simple frequency band analysis (split signal into low/high components)
        double lowFreqEnergy = 0;
        double highFreqEnergy = 0;

        // Moving average to approximate low frequencies
        int windowSize = Math.Min(32, sampleCount / 4);
        for (int i = windowSize; i < sampleCount; i++)
        {
            double avg = 0;
            for (int j = 0; j < windowSize; j++)
            {
                avg += samples[i - j] / 32768.0;
            }
            avg /= windowSize;

            double original = samples[i] / 32768.0;
            double highFreq = original - avg;

            lowFreqEnergy += avg * avg;
            highFreqEnergy += highFreq * highFreq;
        }

        if (sampleCount > windowSize)
        {
            lowFreqEnergy = Math.Sqrt(lowFreqEnergy / (sampleCount - windowSize));
            highFreqEnergy = Math.Sqrt(highFreqEnergy / (sampleCount - windowSize));
        }

        // Spectral centroid approximation (based on ZCR and energy distribution)
        double spectralCentroid = (zcr * 4000) + (highFreqEnergy / (lowFreqEnergy + 0.001) * 500);

        return new AudioFeatures(
            rmsEnergy,
            peak,
            zcr,
            lowFreqEnergy,
            highFreqEnergy,
            spectralCentroid,
            peak > 0.99);
    }

    private void UpdateAdaptiveThresholds(AudioFeatures features)
    {
        // Slowly adapt to sustained low-energy periods (background noise)
        if (features.RmsEnergy < _currentThreshold * 0.5 && _currentState == SpeechState.Silence)
        {
            _noiseHistory.Add(features.RmsEnergy);

            // Update noise floor with weighted moving average
            if (_noiseHistory.Count >= 10)
            {
                double avgNoise = _noiseHistory.Average();
                _ambientNoiseFloor = _ambientNoiseFloor * (1 - _config.AdaptationRate) +
                                     avgNoise * _config.AdaptationRate;

                // Update threshold to maintain good separation
                double newThreshold = _ambientNoiseFloor * _config.SpeechToNoiseRatio;
                _currentThreshold = Math.Clamp(newThreshold, _config.MinThreshold, _config.MaxThreshold);
            }
        }

        // Track speech energy for peak detection
        if (_currentState == SpeechState.Speaking)
        {
            _speechHistory.Add(features.RmsEnergy);
            if (_speechHistory.Count >= 5)
            {
                double avgSpeech = _speechHistory.Max();
                _peakSpeechLevel = _peakSpeechLevel * 0.95 + avgSpeech * 0.05;
            }
        }
    }

    private bool DetectSpeech(AudioFeatures features)
    {
        // Primary: energy above threshold
        bool energyDetection = features.RmsEnergy > _currentThreshold;

        // Secondary: signal-to-noise ratio check
        double snr = features.RmsEnergy / (_ambientNoiseFloor + 0.001);
        bool snrDetection = snr > _config.SpeechToNoiseRatio * 0.7;

        // Zero crossing rate helps distinguish speech from noise
        // Speech typically has ZCR between 0.01-0.1, pure noise is often higher
        bool zcrInRange = true;
        if (_config.EnableZeroCrossingRate)
        {
            zcrInRange = features.ZeroCrossingRate >= 0.005 && features.ZeroCrossingRate <= 0.15;
        }

        // Spectral characteristics (speech has specific frequency distribution)
        bool spectralMatch = features.SpectralCentroid > 200 && features.SpectralCentroid < 4000;

        // Combined detection with voting
        int votes = 0;
        if (energyDetection) votes += 2;  // Energy is most important
        if (snrDetection) votes += 2;
        if (zcrInRange) votes += 1;
        if (spectralMatch) votes += 1;

        return votes >= 4; // Need at least 4 out of 6 votes
    }

    private double CalculateConfidence(AudioFeatures features, bool isSpeech)
    {
        if (!isSpeech)
        {
            return 0.0;
        }

        double snr = features.RmsEnergy / (_ambientNoiseFloor + 0.001);

        // Confidence based on how far above threshold and typical speech characteristics
        double energyConfidence = Math.Min(1.0, (features.RmsEnergy - _currentThreshold) / _currentThreshold);
        double snrConfidence = Math.Min(1.0, snr / 10.0);

        // ZCR confidence (optimal range for speech is ~0.02-0.08)
        double zcrOptimal = features.ZeroCrossingRate >= 0.02 && features.ZeroCrossingRate <= 0.08 ? 1.0 :
                           features.ZeroCrossingRate >= 0.01 && features.ZeroCrossingRate <= 0.12 ? 0.7 : 0.3;

        return (energyConfidence * 0.4 + snrConfidence * 0.4 + zcrOptimal * 0.2);
    }

    private void UpdateStateMachine(bool isSpeech, AudioFeatures features)
    {
        if (isSpeech)
        {
            _consecutiveSpeechFrames++;
            _consecutiveSilenceFrames = 0;
            _speechFramesDetected++;
        }
        else
        {
            _consecutiveSilenceFrames++;
            // Don't reset speech frames immediately - allows for natural pauses
        }

        _currentState = (_currentState, isSpeech, _consecutiveSpeechFrames, _consecutiveSilenceFrames) switch
        {
            // From Silence
            (SpeechState.Silence, true, >= 1, _) => SpeechState.SpeechOnset,
            (SpeechState.Silence, false, _, _) => SpeechState.Silence,

            // From SpeechOnset
            (SpeechState.SpeechOnset, true, var sf, _) when sf >= _config.SpeechOnsetFrames => SpeechState.Speaking,
            (SpeechState.SpeechOnset, false, _, var silf) when silf >= 2 => SpeechState.Silence,
            (SpeechState.SpeechOnset, _, _, _) => SpeechState.SpeechOnset,

            // From Speaking
            (SpeechState.Speaking, true, _, _) => SpeechState.Speaking,
            (SpeechState.Speaking, false, _, var silf) when silf >= 3 => SpeechState.Pause,
            (SpeechState.Speaking, false, _, _) => SpeechState.Speaking,

            // From Pause (brief pause in speech)
            (SpeechState.Pause, true, _, _) => SpeechState.Speaking,
            (SpeechState.Pause, false, _, var silf) when silf >= _config.SpeechOffsetFrames => SpeechState.SpeechOffset,
            (SpeechState.Pause, false, _, _) => SpeechState.Pause,

            // From SpeechOffset
            (SpeechState.SpeechOffset, true, _, _) => SpeechState.Speaking,
            (SpeechState.SpeechOffset, false, _, var silf) when silf >= _config.SpeechOffsetFrames + 2 => SpeechState.Silence,
            (SpeechState.SpeechOffset, false, _, _) => SpeechState.SpeechOffset,

            // Default
            _ => _currentState
        };

        if (_currentState == SpeechState.Silence)
        {
            _consecutiveSpeechFrames = 0;
            _silenceStartTime = DateTime.UtcNow;
        }
        else if (_currentState is SpeechState.Speaking or SpeechState.SpeechOnset)
        {
            _lastSpeechTime = DateTime.UtcNow;
        }
    }

    private SuggestedAction DetermineAction(bool isSpeech, AudioFeatures features)
    {
        return _currentState switch
        {
            // Silence - either wait or discard
            SpeechState.Silence => _consecutiveSilenceFrames > 5
                ? SuggestedAction.DiscardSegment
                : SuggestedAction.ContinueListening,

            // Onset - wait for confirmation
            SpeechState.SpeechOnset => SuggestedAction.WaitForMore,

            // Speaking - keep recording
            SpeechState.Speaking => SuggestedAction.ContinueListening,

            // Pause - might be mid-sentence
            SpeechState.Pause => SuggestedAction.WaitForMore,

            // Offset - utterance ending, process it
            SpeechState.SpeechOffset => SuggestedAction.ProcessAudio,

            _ => SuggestedAction.ContinueListening
        };
    }

    private void RecordSpeechSegment(AudioFeatures features)
    {
        var segment = new SpeechSegment(
            _lastSpeechTime.AddSeconds(-_consecutiveSpeechFrames * 0.1),
            DateTime.UtcNow,
            features.PeakAmplitude,
            features.RmsEnergy,
            _consecutiveSpeechFrames);

        _recentSegments.Add(segment);
    }

    private static int FindWavDataOffset(byte[] fileData)
    {
        // Look for "data" chunk in WAV header
        for (int i = 0; i < Math.Min(fileData.Length - 4, 200); i++)
        {
            if (fileData[i] == 'd' && fileData[i + 1] == 'a' &&
                fileData[i + 2] == 't' && fileData[i + 3] == 'a')
            {
                return i + 8; // Skip "data" + 4 bytes for size
            }
        }
        return 44; // Default WAV header size
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // No unmanaged resources
    }

    // === Circular Buffer Implementation ===
    private sealed class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _head;
        private int _count;

        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
        }

        public int Count => _count;

        public void Add(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
        }

        public double Average()
        {
            if (_count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < _count; i++)
            {
                sum += Convert.ToDouble(_buffer[i]);
            }
            return sum / _count;
        }

        public double Max()
        {
            if (_count == 0) return 0;
            double max = double.MinValue;
            for (int i = 0; i < _count; i++)
            {
                max = Math.Max(max, Convert.ToDouble(_buffer[i]));
            }
            return max;
        }

        public T[] ToArray()
        {
            T[] result = new T[_count];
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - _count + i + _buffer.Length) % _buffer.Length;
                result[i] = _buffer[idx];
            }
            return result;
        }
    }
}
