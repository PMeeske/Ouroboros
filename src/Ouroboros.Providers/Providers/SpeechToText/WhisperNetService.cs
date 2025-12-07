// <copyright file="WhisperNetService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Whisper.net;
using Whisper.net.Ggml;

namespace LangChainPipeline.Providers.SpeechToText;

/// <summary>
/// Native .NET Whisper speech-to-text service using Whisper.net.
/// Provides high-performance local speech recognition without external processes.
/// </summary>
public sealed class WhisperNetService : ISpeechToTextService, IDisposable
{
    private readonly GgmlType modelType;
    private readonly string modelDirectory;
    private readonly bool lazyLoad;
    private WhisperProcessor? processor;
    private WhisperFactory? factory;
    private readonly SemaphoreSlim initLock = new(1, 1);
    private bool isInitialized;
    private bool isDisposed;

    /// <summary>
    /// Supported audio formats for Whisper.net (requires 16kHz mono WAV).
    /// </summary>
    private static readonly string[] SupportedAudioFormats = [".wav"];

    /// <inheritdoc/>
    public string ProviderName => "Whisper.net (Native)";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => SupportedAudioFormats;

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 500 * 1024 * 1024; // 500 MB

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperNetService"/> class.
    /// </summary>
    /// <param name="modelType">Whisper model type (Tiny, Base, Small, Medium, Large).</param>
    /// <param name="modelDirectory">Directory to store/load models. Defaults to user's .whisper folder.</param>
    /// <param name="lazyLoad">If true, defer model loading until first use.</param>
    public WhisperNetService(
        GgmlType modelType = GgmlType.Base,
        string? modelDirectory = null,
        bool lazyLoad = true)
    {
        this.modelType = modelType;
        this.modelDirectory = modelDirectory ?? GetDefaultModelDirectory();
        this.lazyLoad = lazyLoad;

        if (!lazyLoad)
        {
            _ = EnsureInitializedAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Creates a WhisperNetService with a specific model size string.
    /// </summary>
    /// <param name="modelSize">Model size: "tiny", "base", "small", "medium", "large".</param>
    /// <param name="modelDirectory">Directory to store/load models.</param>
    /// <returns>Configured WhisperNetService instance.</returns>
    public static WhisperNetService FromModelSize(string modelSize, string? modelDirectory = null)
    {
        GgmlType type = modelSize.ToLowerInvariant() switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            "large" or "large-v1" => GgmlType.LargeV1,
            "large-v2" => GgmlType.LargeV2,
            "large-v3" => GgmlType.LargeV3,
            _ => GgmlType.Base
        };

        return new WhisperNetService(type, modelDirectory);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<TranscriptionResult, string>.Failure($"File not found: {filePath}");
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        // Convert to WAV if needed
        string wavPath = filePath;
        bool needsCleanup = false;

        if (extension != ".wav")
        {
            var convertResult = await ConvertToWavAsync(filePath, ct);
            if (!convertResult.IsSuccess)
            {
                return Result<TranscriptionResult, string>.Failure(convertResult.Error!);
            }
            wavPath = convertResult.Value!;
            needsCleanup = true;
        }

        try
        {
            var initResult = await EnsureInitializedAsync(ct);
            if (!initResult.IsSuccess)
            {
                return Result<TranscriptionResult, string>.Failure(initResult.Error!);
            }

            // Read and process audio
            var samples = await ReadAudioSamplesAsync(wavPath, ct);
            if (samples == null || samples.Length == 0)
            {
                return Result<TranscriptionResult, string>.Failure("Failed to read audio samples");
            }

            // Transcribe
            var segments = new List<TranscriptionSegment>();
            var fullText = new System.Text.StringBuilder();

            await foreach (var segment in processor!.ProcessAsync(samples, ct))
            {
                string text = segment.Text?.Trim() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    fullText.Append(text).Append(' ');
                    segments.Add(new TranscriptionSegment(
                        text,
                        segment.Start.TotalSeconds,
                        segment.End.TotalSeconds));
                }
            }

            string transcribedText = fullText.ToString().Trim();
            double? durationSeconds = segments.Count > 0 ? segments[^1].End : null;

            return Result<TranscriptionResult, string>.Success(new TranscriptionResult(
                Text: transcribedText,
                Language: options?.Language ?? "en",
                Duration: durationSeconds,
                Segments: segments));
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
        finally
        {
            if (needsCleanup && File.Exists(wavPath))
            {
                try { File.Delete(wavPath); } catch { }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        try
        {
            await using FileStream fileStream = File.Create(tempPath);
            await audioStream.CopyToAsync(fileStream, ct);
            await fileStream.FlushAsync(ct);
            fileStream.Close();

            return await TranscribeFileAsync(tempPath, options, ct);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        await using MemoryStream stream = new(audioData);
        return await TranscribeStreamAsync(stream, fileName, options, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        // Whisper.net handles translation via language detection
        options ??= new TranscriptionOptions();
        var translationOptions = options with { Language = "en" };
        return await TranscribeFileAsync(filePath, translationOptions, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await EnsureInitializedAsync(ct);
            return result.IsSuccess;
        }
        catch
        {
            return false;
        }
    }

    private async Task<Result<bool, string>> EnsureInitializedAsync(CancellationToken ct)
    {
        if (isInitialized && processor != null)
        {
            return Result<bool, string>.Success(true);
        }

        await initLock.WaitAsync(ct);
        try
        {
            if (isInitialized && processor != null)
            {
                return Result<bool, string>.Success(true);
            }

            // Ensure model directory exists
            if (!Directory.Exists(modelDirectory))
            {
                Directory.CreateDirectory(modelDirectory);
            }

            string modelFileName = GetModelFileName(modelType);
            string modelPath = Path.Combine(modelDirectory, modelFileName);

            // Download model if not present
            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"  [whisper.net] Downloading {modelType} model...");
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    var downloader = new WhisperGgmlDownloader(httpClient);
                    await using var modelStream = await downloader.GetGgmlModelAsync(modelType);
                    await using var fileStream = File.Create(modelPath);
                    await modelStream.CopyToAsync(fileStream, ct);
                    Console.WriteLine($"  [whisper.net] Model downloaded to {modelPath}");
                }
                catch (Exception ex)
                {
                    return Result<bool, string>.Failure($"Failed to download model: {ex.Message}");
                }
            }

            // Create factory and processor
            factory = WhisperFactory.FromPath(modelPath);
            processor = factory.CreateBuilder()
                .WithLanguage("auto")
                .WithThreads(Environment.ProcessorCount)
                .Build();

            isInitialized = true;
            Console.WriteLine($"  [whisper.net] Initialized with {modelType} model");

            return Result<bool, string>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Failed to initialize Whisper.net: {ex.Message}");
        }
        finally
        {
            initLock.Release();
        }
    }

    private static async Task<float[]?> ReadAudioSamplesAsync(string wavPath, CancellationToken ct)
    {
        try
        {
            // Read WAV file and convert to float samples
            await using var fileStream = File.OpenRead(wavPath);
            using var reader = new BinaryReader(fileStream);

            // Read WAV header
            string riff = new(reader.ReadChars(4));
            if (riff != "RIFF")
            {
                // Try to convert with ffmpeg first
                string convertedPath = Path.Combine(Path.GetTempPath(), $"whisper_converted_{Guid.NewGuid()}.wav");
                var convertResult = await ConvertToWavAsync(wavPath, ct, convertedPath);
                if (convertResult.IsSuccess)
                {
                    var samples = await ReadAudioSamplesAsync(convertedPath, ct);
                    try { File.Delete(convertedPath); } catch { }
                    return samples;
                }
                return null;
            }

            reader.ReadInt32(); // File size
            string wave = new(reader.ReadChars(4));
            if (wave != "WAVE") return null;

            // Find fmt chunk
            int channels = 1;
            int sampleRate = 16000;
            int bitsPerSample = 16;

            while (fileStream.Position < fileStream.Length)
            {
                string chunkId = new(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                {
                    reader.ReadInt16(); // Audio format
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // Byte rate
                    reader.ReadInt16(); // Block align
                    bitsPerSample = reader.ReadInt16();
                    if (chunkSize > 16) reader.ReadBytes(chunkSize - 16);
                }
                else if (chunkId == "data")
                {
                    // Read audio data
                    int bytesPerSample = bitsPerSample / 8;
                    int numSamples = chunkSize / bytesPerSample / channels;
                    float[] samples = new float[numSamples];

                    for (int i = 0; i < numSamples && fileStream.Position < fileStream.Length; i++)
                    {
                        float sample = 0;
                        for (int c = 0; c < channels; c++)
                        {
                            float channelSample = bitsPerSample switch
                            {
                                8 => (reader.ReadByte() - 128) / 128f,
                                16 => reader.ReadInt16() / 32768f,
                                24 => (reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadSByte() << 16)) / 8388608f,
                                32 => reader.ReadInt32() / 2147483648f,
                                _ => 0f
                            };
                            sample += channelSample;
                        }
                        samples[i] = sample / channels; // Average channels to mono
                    }

                    // Resample to 16kHz if needed
                    if (sampleRate != 16000)
                    {
                        samples = Resample(samples, sampleRate, 16000);
                    }

                    return samples;
                }
                else
                {
                    // Skip unknown chunk
                    if (chunkSize > 0 && fileStream.Position + chunkSize <= fileStream.Length)
                    {
                        reader.ReadBytes(chunkSize);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static float[] Resample(float[] samples, int fromRate, int toRate)
    {
        double ratio = (double)toRate / fromRate;
        int newLength = (int)(samples.Length * ratio);
        float[] resampled = new float[newLength];

        for (int i = 0; i < newLength; i++)
        {
            double srcIndex = i / ratio;
            int srcIndexInt = (int)srcIndex;
            double frac = srcIndex - srcIndexInt;

            if (srcIndexInt + 1 < samples.Length)
            {
                resampled[i] = (float)(samples[srcIndexInt] * (1 - frac) + samples[srcIndexInt + 1] * frac);
            }
            else if (srcIndexInt < samples.Length)
            {
                resampled[i] = samples[srcIndexInt];
            }
        }

        return resampled;
    }

    private static async Task<Result<string, string>> ConvertToWavAsync(string inputPath, CancellationToken ct, string? outputPath = null)
    {
        outputPath ??= Path.Combine(Path.GetTempPath(), $"whisper_converted_{Guid.NewGuid()}.wav");

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{inputPath}\" -ar 16000 -ac 1 -f wav -y \"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start ffmpeg");
            }

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                return Result<string, string>.Failure("FFmpeg conversion failed");
            }

            return Result<string, string>.Success(outputPath);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Conversion failed: {ex.Message}");
        }
    }

    private static string GetDefaultModelDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".whisper", "models");
    }

    private static string GetModelFileName(GgmlType modelType)
    {
        return modelType switch
        {
            GgmlType.Tiny => "ggml-tiny.bin",
            GgmlType.TinyEn => "ggml-tiny.en.bin",
            GgmlType.Base => "ggml-base.bin",
            GgmlType.BaseEn => "ggml-base.en.bin",
            GgmlType.Small => "ggml-small.bin",
            GgmlType.SmallEn => "ggml-small.en.bin",
            GgmlType.Medium => "ggml-medium.bin",
            GgmlType.MediumEn => "ggml-medium.en.bin",
            GgmlType.LargeV1 => "ggml-large-v1.bin",
            GgmlType.LargeV2 => "ggml-large-v2.bin",
            GgmlType.LargeV3 => "ggml-large-v3.bin",
            _ => "ggml-base.bin"
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        processor?.Dispose();
        factory?.Dispose();
        initLock.Dispose();
    }
}
