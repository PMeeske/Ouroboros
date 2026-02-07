// <copyright file="TapoRtspClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// RTSP client for Tapo cameras (C200, C210, C220, etc.).
/// Provides video frame capture using FFmpeg as the underlying RTSP decoder.
/// </summary>
public sealed class TapoRtspClient : IDisposable
{
    private readonly ILogger<TapoRtspClient>? _logger;
    private readonly string _cameraIp;
    private readonly string _username;
    private readonly string _password;
    private readonly CameraStreamQuality _quality;
    private Process? _ffmpegProcess;
    private bool _disposed;
    private long _frameCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="TapoRtspClient"/> class.
    /// </summary>
    /// <param name="cameraIp">IP address of the Tapo camera.</param>
    /// <param name="username">Tapo account username/email.</param>
    /// <param name="password">Tapo account password.</param>
    /// <param name="quality">Stream quality (affects which RTSP stream to use).</param>
    /// <param name="logger">Optional logger.</param>
    public TapoRtspClient(
        string cameraIp,
        string username,
        string password,
        CameraStreamQuality quality = CameraStreamQuality.HD,
        ILogger<TapoRtspClient>? logger = null)
    {
        _cameraIp = cameraIp ?? throw new ArgumentNullException(nameof(cameraIp));
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _quality = quality;
        _logger = logger;
    }

    /// <summary>
    /// Gets the RTSP URL for the camera stream.
    /// </summary>
    public string RtspUrl => BuildRtspUrl();

    /// <summary>
    /// Gets the camera IP address.
    /// </summary>
    public string CameraIp => _cameraIp;

    /// <summary>
    /// Gets the current frame count.
    /// </summary>
    public long FrameCount => _frameCount;

    /// <summary>
    /// Builds the RTSP URL based on quality settings.
    /// </summary>
    private string BuildRtspUrl()
    {
        // Tapo cameras use stream1 for high quality, stream2 for low quality
        var stream = _quality switch
        {
            CameraStreamQuality.Low => "stream2",
            CameraStreamQuality.Standard => "stream2",
            _ => "stream1" // HD, FullHD, QHD all use stream1
        };

        // URL encode special characters in password
        var encodedPassword = Uri.EscapeDataString(_password);
        var encodedUsername = Uri.EscapeDataString(_username);

        return $"rtsp://{encodedUsername}:{encodedPassword}@{_cameraIp}:554/{stream}";
    }

    /// <summary>
    /// Captures a single frame from the camera as JPEG.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the captured frame or error.</returns>
    public async Task<Result<TapoCameraFrame>> CaptureFrameAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<TapoCameraFrame>.Failure("Client is disposed");

        try
        {
            var rtspUrl = BuildRtspUrl();
            _logger?.LogDebug("Capturing frame from {CameraIp}", _cameraIp);

            // Use FFmpeg to capture a single frame as JPEG
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-rtsp_transport tcp -i \"{rtspUrl}\" -frames:v 1 -f image2pipe -vcodec mjpeg -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            using var memoryStream = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(memoryStream, ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                _logger?.LogError("FFmpeg error: {Error}", error);
                return Result<TapoCameraFrame>.Failure($"FFmpeg failed: {error}");
            }

            var frameData = memoryStream.ToArray();
            if (frameData.Length == 0)
            {
                return Result<TapoCameraFrame>.Failure("No frame data captured");
            }

            _frameCount++;

            // Determine resolution based on quality
            var (width, height) = _quality switch
            {
                CameraStreamQuality.Low => (640, 360),
                CameraStreamQuality.Standard => (640, 480),
                CameraStreamQuality.HD => (1280, 720),
                CameraStreamQuality.FullHD => (1920, 1080),
                CameraStreamQuality.QHD => (2560, 1440),
                _ => (1920, 1080)
            };

            var frame = new TapoCameraFrame(
                Data: frameData,
                Width: width,
                Height: height,
                FrameNumber: _frameCount,
                Timestamp: DateTime.UtcNow,
                CameraName: $"Tapo-{_cameraIp}");

            _logger?.LogDebug("Captured frame {FrameNumber}, {Size} bytes", _frameCount, frameData.Length);
            return Result<TapoCameraFrame>.Success(frame);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to capture frame from {CameraIp}", _cameraIp);
            return Result<TapoCameraFrame>.Failure($"Frame capture failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts continuous frame streaming.
    /// </summary>
    /// <param name="frameRate">Target frame rate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of captured frames.</returns>
    public async IAsyncEnumerable<Result<TapoCameraFrame>> StreamFramesAsync(
        int frameRate = 15,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_disposed)
        {
            yield return Result<TapoCameraFrame>.Failure("Client is disposed");
            yield break;
        }

        var rtspUrl = BuildRtspUrl();
        _logger?.LogInformation("Starting RTSP stream from {CameraIp} at {FrameRate} fps", _cameraIp, frameRate);

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-rtsp_transport tcp -i \"{rtspUrl}\" -r {frameRate} -f image2pipe -vcodec mjpeg -",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _ffmpegProcess = new Process { StartInfo = startInfo };

        try
        {
            _ffmpegProcess.Start();

            var buffer = new byte[1024 * 1024]; // 1MB buffer
            var stream = _ffmpegProcess.StandardOutput.BaseStream;

            while (!ct.IsCancellationRequested && !_ffmpegProcess.HasExited)
            {
                using var frameStream = new MemoryStream();

                // Read JPEG frame (starts with FFD8, ends with FFD9)
                var frameData = await ReadJpegFrameAsync(stream, ct);
                if (frameData == null || frameData.Length == 0)
                {
                    continue;
                }

                _frameCount++;

                var (width, height) = _quality switch
                {
                    CameraStreamQuality.Low => (640, 360),
                    CameraStreamQuality.Standard => (640, 480),
                    CameraStreamQuality.HD => (1280, 720),
                    CameraStreamQuality.FullHD => (1920, 1080),
                    CameraStreamQuality.QHD => (2560, 1440),
                    _ => (1920, 1080)
                };

                var frame = new TapoCameraFrame(
                    Data: frameData,
                    Width: width,
                    Height: height,
                    FrameNumber: _frameCount,
                    Timestamp: DateTime.UtcNow,
                    CameraName: $"Tapo-{_cameraIp}");

                yield return Result<TapoCameraFrame>.Success(frame);
            }
        }
        finally
        {
            StopStreaming();
        }
    }

    /// <summary>
    /// Reads a complete JPEG frame from the stream.
    /// </summary>
    private async Task<byte[]?> ReadJpegFrameAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new List<byte>();
        var headerFound = false;
        int prev = -1;

        while (!ct.IsCancellationRequested)
        {
            var b = stream.ReadByte();
            if (b == -1) break;

            if (!headerFound)
            {
                // Look for JPEG start marker (FF D8)
                if (prev == 0xFF && b == 0xD8)
                {
                    headerFound = true;
                    buffer.Add(0xFF);
                    buffer.Add((byte)b);
                }
                prev = b;
                continue;
            }

            buffer.Add((byte)b);

            // Check for JPEG end marker (FF D9)
            if (prev == 0xFF && b == 0xD9)
            {
                return buffer.ToArray();
            }

            prev = b;

            // Safety limit to prevent memory issues
            if (buffer.Count > 10 * 1024 * 1024)
            {
                _logger?.LogWarning("Frame too large, discarding");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Stops the active stream.
    /// </summary>
    public void StopStreaming()
    {
        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            try
            {
                _ffmpegProcess.Kill(entireProcessTree: true);
                _ffmpegProcess.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping FFmpeg process");
            }
            finally
            {
                _ffmpegProcess = null;
            }
        }
    }

    /// <summary>
    /// Tests connectivity to the camera.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    public async Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var rtspUrl = BuildRtspUrl();
            _logger?.LogDebug("Testing connection to {CameraIp}", _cameraIp);

            var startInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-rtsp_transport tcp -i \"{rtspUrl}\" -show_format -v quiet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                return Result<string>.Failure("Connection test timed out");
            }

            if (process.ExitCode == 0)
            {
                _logger?.LogInformation("Successfully connected to camera at {CameraIp}", _cameraIp);
                return Result<string>.Success($"Connected to Tapo camera at {_cameraIp}");
            }
            else
            {
                var error = await errorTask;
                return Result<string>.Failure($"Connection failed: {error}");
            }
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Connection test failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopStreaming();
        _logger?.LogDebug("TapoRtspClient disposed");
    }
}
