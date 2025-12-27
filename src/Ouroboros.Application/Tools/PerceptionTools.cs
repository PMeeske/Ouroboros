// <copyright file="PerceptionTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Ouroboros.Core.Monads;
using Ouroboros.Application.Services;
using Ouroboros.Tools;

/// <summary>
/// Provides perception tools for Ouroboros - screen capture, camera, and active monitoring.
/// Enables proactive observation of user behavior.
/// </summary>
public static class PerceptionTools
{
    /// <summary>
    /// Directory to store captured screenshots and images.
    /// </summary>
    public static string CaptureDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ouroboros", "captures");

    /// <summary>
    /// Event fired when screen content changes significantly.
    /// </summary>
    public static event Action<string>? OnScreenChanged;

    /// <summary>
    /// Event fired when user activity is detected.
    /// </summary>
    public static event Action<string>? OnUserActivity;

    /// <summary>
    /// Shared vision service for AI-powered image understanding.
    /// </summary>
    public static VisionService? VisionService { get; set; }

    /// <summary>
    /// Creates all perception tools.
    /// </summary>
    public static IEnumerable<ITool> CreateAllTools()
    {
        yield return new ScreenCaptureTool();
        yield return new CameraCaptureTool();
        yield return new ActiveWindowTool();
        yield return new MousePositionTool();
        yield return new WatchScreenTool();
        yield return new WatchUserActivityTool();
        yield return new AnalyzeImageTool();
        yield return new ListCapturedImagesTool();

        // Vision AI tools
        yield return new SeeScreenTool();
        yield return new DescribeImageTool();
        yield return new ReadTextFromScreenTool();
        yield return new WhatAmIDoingTool();
        yield return new DetectObjectsTool();
    }

    #region Win32 APIs for screen/window access

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    #endregion

    /// <summary>
    /// Capture the entire screen or a specific region.
    /// </summary>
    public class ScreenCaptureTool : ITool
    {
        public string Name => "capture_screen";
        public string Description => "Capture a screenshot of the screen. Input JSON (optional): {\"region\": {\"x\":0,\"y\":0,\"width\":800,\"height\":600}, \"monitor\": 0}. Returns path to saved image.";
        public string? JsonSchema => """{"type":"object","properties":{"region":{"type":"object","properties":{"x":{"type":"integer"},"y":{"type":"integer"},"width":{"type":"integer"},"height":{"type":"integer"}}},"monitor":{"type":"integer"}}}""";

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
#if NET10_0_OR_GREATER_WINDOWS
            try
            {
                Directory.CreateDirectory(CaptureDirectory);

                // Parse optional region
                Rectangle? region = null;
                int monitor = 0;

                if (!string.IsNullOrWhiteSpace(input))
                {
                    try
                    {
                        var args = JsonSerializer.Deserialize<JsonElement>(input);
                        if (args.TryGetProperty("monitor", out var monEl))
                            monitor = monEl.GetInt32();
                        if (args.TryGetProperty("region", out var regEl))
                        {
                            region = new Rectangle(
                                regEl.GetProperty("x").GetInt32(),
                                regEl.GetProperty("y").GetInt32(),
                                regEl.GetProperty("width").GetInt32(),
                                regEl.GetProperty("height").GetInt32());
                        }
                    }
                    catch { }
                }

                // Get screen bounds
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (monitor >= screens.Length) monitor = 0;
                var screenBounds = screens[monitor].Bounds;

                var captureBounds = region ?? screenBounds;

                using var bitmap = new Bitmap(captureBounds.Width, captureBounds.Height);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(captureBounds.X, captureBounds.Y, 0, 0, captureBounds.Size);

                var filename = $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                var filepath = Path.Combine(CaptureDirectory, filename);
                bitmap.Save(filepath, ImageFormat.Png);

                return Result<string, string>.Success($"📸 Screenshot captured!\n\nSaved to: `{filepath}`\nSize: {captureBounds.Width}x{captureBounds.Height}\nMonitor: {monitor}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Screen capture failed: {ex.Message}");
            }
#else
            return Result<string, string>.Failure("Screen capture is only supported on Windows");
#endif
        }
    }

    /// <summary>
    /// Capture from webcam/camera.
    /// </summary>
    public class CameraCaptureTool : ITool
    {
        public string Name => "capture_camera";
        public string Description => "Capture an image from the webcam/camera. Input (optional): camera index (default 0). Returns path to saved image.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                Directory.CreateDirectory(CaptureDirectory);

                int cameraIndex = 0;
                if (int.TryParse(input?.Trim(), out var idx))
                    cameraIndex = idx;

                var filename = $"camera_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filepath = Path.Combine(CaptureDirectory, filename);

                // Use ffmpeg for camera capture (cross-platform)
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-f dshow -i video=\"Integrated Camera\" -frames:v 1 -y \"{filepath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Try different camera device names
                var cameraNames = new[]
                {
                    "Integrated Camera",
                    "Integrated Webcam",
                    "USB Camera",
                    "HD Webcam",
                    "Webcam"
                };

                bool captured = false;
                string lastError = "";

                foreach (var camName in cameraNames)
                {
                    psi.Arguments = $"-f dshow -i video=\"{camName}\" -frames:v 1 -y \"{filepath}\"";

                    using var process = Process.Start(psi);
                    if (process == null) continue;

                    await process.WaitForExitAsync(ct);
                    lastError = await process.StandardError.ReadToEndAsync(ct);

                    if (File.Exists(filepath) && new FileInfo(filepath).Length > 0)
                    {
                        captured = true;
                        break;
                    }
                }

                if (!captured)
                {
                    // Fallback: try listing available devices
                    var listPsi = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = "-list_devices true -f dshow -i dummy",
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var listProcess = Process.Start(listPsi);
                    if (listProcess != null)
                    {
                        var devices = await listProcess.StandardError.ReadToEndAsync(ct);
                        return Result<string, string>.Failure($"Camera capture failed. Available devices:\n{devices}\n\nLast error: {lastError}");
                    }

                    return Result<string, string>.Failure($"Camera capture failed: {lastError}");
                }

                return Result<string, string>.Success($"📷 Camera image captured!\n\nSaved to: `{filepath}`");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Camera capture failed: {ex.Message}\n\n💡 Make sure ffmpeg is installed and a camera is connected.");
            }
        }
    }

    /// <summary>
    /// Get info about the currently active window.
    /// </summary>
    public class ActiveWindowTool : ITool
    {
        public string Name => "get_active_window";
        public string Description => "Get information about the currently active window - title, process name, etc.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            try
            {
                var hwnd = GetForegroundWindow();
                var length = GetWindowTextLength(hwnd);
                var sb = new StringBuilder(length + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                var windowTitle = sb.ToString();

                GetWindowThreadProcessId(hwnd, out uint processId);
                var process = Process.GetProcessById((int)processId);

                var result = new StringBuilder();
                result.AppendLine("🪟 **Active Window**\n");
                result.AppendLine($"**Title:** {windowTitle}");
                result.AppendLine($"**Process:** {process.ProcessName}");
                result.AppendLine($"**PID:** {processId}");
                result.AppendLine($"**Path:** {GetProcessPath(process)}");
                result.AppendLine($"**Memory:** {process.WorkingSet64 / 1024 / 1024} MB");

                return Result<string, string>.Success(result.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get active window: {ex.Message}");
            }
        }

        private static string GetProcessPath(Process process)
        {
            try { return process.MainModule?.FileName ?? "Unknown"; }
            catch { return "Access denied"; }
        }
    }

    /// <summary>
    /// Get current mouse position.
    /// </summary>
    public class MousePositionTool : ITool
    {
        public string Name => "get_mouse_position";
        public string Description => "Get the current mouse cursor position on screen.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            try
            {
                GetCursorPos(out POINT point);
                return Result<string, string>.Success($"🖱️ Mouse position: ({point.X}, {point.Y})");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get mouse position: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Watch screen for changes (polling-based).
    /// </summary>
    public class WatchScreenTool : ITool
    {
        public string Name => "watch_screen";
        public string Description => "Start watching the screen for changes. Input JSON: {\"duration_seconds\": 60, \"interval_ms\": 1000, \"sensitivity\": 0.1}. Reports when significant screen changes occur.";
        public string? JsonSchema => """{"type":"object","properties":{"duration_seconds":{"type":"integer"},"interval_ms":{"type":"integer"},"sensitivity":{"type":"number"}}}""";

        private static CancellationTokenSource? _watchCts;
        private static bool _isWatching = false;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
#if NET10_0_OR_GREATER_WINDOWS
            try
            {
                // Parse parameters
                int durationSeconds = 60;
                int intervalMs = 1000;
                double sensitivity = 0.1; // 10% change threshold

                if (!string.IsNullOrWhiteSpace(input))
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    if (args.TryGetProperty("duration_seconds", out var durEl))
                        durationSeconds = durEl.GetInt32();
                    if (args.TryGetProperty("interval_ms", out var intEl))
                        intervalMs = intEl.GetInt32();
                    if (args.TryGetProperty("sensitivity", out var sensEl))
                        sensitivity = sensEl.GetDouble();
                }

                if (_isWatching)
                {
                    _watchCts?.Cancel();
                    _isWatching = false;
                    return Result<string, string>.Success("⏹️ Stopped screen watching.");
                }

                _watchCts = new CancellationTokenSource();
                _isWatching = true;

                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _watchCts.Token);
                var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;

                // Start watching in background
                _ = Task.Run(async () =>
                {
                    Bitmap? previousFrame = null;
                    var startTime = DateTime.Now;
                    int changeCount = 0;

                    try
                    {
                        while (!linkedCts.Token.IsCancellationRequested &&
                               (DateTime.Now - startTime).TotalSeconds < durationSeconds)
                        {
                            using var currentFrame = new Bitmap(screen.Width, screen.Height);
                            using var g = Graphics.FromImage(currentFrame);
                            g.CopyFromScreen(0, 0, 0, 0, screen.Size);

                            if (previousFrame != null)
                            {
                                var changePercent = CompareImages(previousFrame, currentFrame);
                                if (changePercent > sensitivity)
                                {
                                    changeCount++;
                                    var filename = $"change_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                                    var path = Path.Combine(CaptureDirectory, filename);
                                    Directory.CreateDirectory(CaptureDirectory);
                                    currentFrame.Save(path, ImageFormat.Png);

                                    OnScreenChanged?.Invoke($"Screen changed {changePercent:P1} - saved to {filename}");
                                }
                            }

                            previousFrame?.Dispose();
                            previousFrame = (Bitmap)currentFrame.Clone();

                            await Task.Delay(intervalMs, linkedCts.Token);
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        previousFrame?.Dispose();
                        _isWatching = false;
                    }
                }, linkedCts.Token);

                return Result<string, string>.Success($"👁️ Now watching screen for {durationSeconds}s (interval: {intervalMs}ms, sensitivity: {sensitivity:P0}).\n\nTo stop: call this tool again.\nChanges saved to: `{CaptureDirectory}`");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Screen watch failed: {ex.Message}");
            }
#else
            await Task.CompletedTask; // Suppress async warning
            return Result<string, string>.Failure("Screen watching is only supported on Windows");
#endif
        }

        private static double CompareImages(Bitmap img1, Bitmap img2)
        {
            // Simple pixel sampling comparison
            int sampleSize = 100;
            int differences = 0;
            var random = new Random(42);

            for (int i = 0; i < sampleSize; i++)
            {
                int x = random.Next(img1.Width);
                int y = random.Next(img1.Height);

                var p1 = img1.GetPixel(x, y);
                var p2 = img2.GetPixel(x, y);

                int diff = Math.Abs(p1.R - p2.R) + Math.Abs(p1.G - p2.G) + Math.Abs(p1.B - p2.B);
                if (diff > 30) differences++;
            }

            return (double)differences / sampleSize;
        }
    }

    /// <summary>
    /// Watch user activity (keyboard, mouse, window changes).
    /// </summary>
    public class WatchUserActivityTool : ITool
    {
        public string Name => "watch_user_activity";
        public string Description => "Monitor user activity - active window changes, idle time, etc. Input JSON: {\"duration_seconds\": 60, \"report_interval_seconds\": 10}";
        public string? JsonSchema => """{"type":"object","properties":{"duration_seconds":{"type":"integer"},"report_interval_seconds":{"type":"integer"}}}""";

        private static CancellationTokenSource? _activityCts;
        private static bool _isWatchingActivity = false;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                int durationSeconds = 60;
                int reportInterval = 10;

                if (!string.IsNullOrWhiteSpace(input))
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    if (args.TryGetProperty("duration_seconds", out var durEl))
                        durationSeconds = durEl.GetInt32();
                    if (args.TryGetProperty("report_interval_seconds", out var repEl))
                        reportInterval = repEl.GetInt32();
                }

                if (_isWatchingActivity)
                {
                    _activityCts?.Cancel();
                    _isWatchingActivity = false;
                    return Result<string, string>.Success("⏹️ Stopped activity monitoring.");
                }

                _activityCts = new CancellationTokenSource();
                _isWatchingActivity = true;
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _activityCts.Token);

                var activityLog = new List<(DateTime Time, string Activity)>();
                string lastWindow = "";

                _ = Task.Run(async () =>
                {
                    var startTime = DateTime.Now;
                    POINT lastMousePos = default;

                    try
                    {
                        while (!linkedCts.Token.IsCancellationRequested &&
                               (DateTime.Now - startTime).TotalSeconds < durationSeconds)
                        {
                            // Check active window
                            var hwnd = GetForegroundWindow();
                            var sb = new StringBuilder(256);
                            GetWindowText(hwnd, sb, sb.Capacity);
                            var currentWindow = sb.ToString();

                            if (currentWindow != lastWindow && !string.IsNullOrEmpty(currentWindow))
                            {
                                var activity = $"Window: {currentWindow}";
                                activityLog.Add((DateTime.Now, activity));
                                OnUserActivity?.Invoke(activity);
                                lastWindow = currentWindow;
                            }

                            // Check mouse movement
                            GetCursorPos(out POINT mousePos);
                            if (Math.Abs(mousePos.X - lastMousePos.X) > 50 ||
                                Math.Abs(mousePos.Y - lastMousePos.Y) > 50)
                            {
                                activityLog.Add((DateTime.Now, $"Mouse moved to ({mousePos.X}, {mousePos.Y})"));
                                lastMousePos = mousePos;
                            }

                            await Task.Delay(500, linkedCts.Token);
                        }

                        // Save activity log
                        if (activityLog.Count > 0)
                        {
                            Directory.CreateDirectory(CaptureDirectory);
                            var logPath = Path.Combine(CaptureDirectory, $"activity_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                            var logContent = string.Join("\n", activityLog.Select(a => $"[{a.Time:HH:mm:ss}] {a.Activity}"));
                            await File.WriteAllTextAsync(logPath, logContent);
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        _isWatchingActivity = false;
                    }
                }, linkedCts.Token);

                return Result<string, string>.Success($"👀 Now monitoring user activity for {durationSeconds}s.\n\nTracking: window changes, mouse movement\nLogs saved to: `{CaptureDirectory}`\n\nTo stop: call this tool again.");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Activity monitoring failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Analyze an image using vision model.
    /// </summary>
    public class AnalyzeImageTool : ITool
    {
        public string Name => "analyze_image";
        public string Description => "Analyze an image using vision AI. Input: path to image file. Describes what's visible in the image.";
        public string? JsonSchema => null;

        /// <summary>
        /// Vision model endpoint for image analysis.
        /// </summary>
        public static Func<string, CancellationToken, Task<string>>? VisionAnalyzer { get; set; }

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var imagePath = input.Trim().Trim('"');
                if (!Path.IsPathRooted(imagePath))
                {
                    imagePath = Path.Combine(CaptureDirectory, imagePath);
                }

                if (!File.Exists(imagePath))
                {
                    return Result<string, string>.Failure($"Image not found: {imagePath}");
                }

                if (VisionAnalyzer != null)
                {
                    var analysis = await VisionAnalyzer(imagePath, ct);
                    return Result<string, string>.Success($"🔍 **Image Analysis**\n\n{analysis}");
                }

                // Fallback: basic image info
                using var img = Image.FromFile(imagePath);
                var info = new StringBuilder();
                info.AppendLine("📊 **Image Information**\n");
                info.AppendLine($"**File:** {Path.GetFileName(imagePath)}");
                info.AppendLine($"**Size:** {img.Width}x{img.Height}");
                info.AppendLine($"**Format:** {img.RawFormat}");
                info.AppendLine($"**File size:** {new FileInfo(imagePath).Length / 1024} KB");
                info.AppendLine("\n_Note: Vision analysis requires a vision model to be configured._");

                return Result<string, string>.Success(info.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Image analysis failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// List captured images and screenshots.
    /// </summary>
    public class ListCapturedImagesTool : ITool
    {
        public string Name => "list_captured_images";
        public string Description => "List all captured screenshots and camera images. Shows recent captures with timestamps.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            try
            {
                if (!Directory.Exists(CaptureDirectory))
                {
                    return Result<string, string>.Success("No captures yet. Use `capture_screen` or `capture_camera` first.");
                }

                var files = Directory.GetFiles(CaptureDirectory)
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".log"))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(20)
                    .ToList();

                if (files.Count == 0)
                {
                    return Result<string, string>.Success("No captures found.");
                }

                var sb = new StringBuilder();
                sb.AppendLine("📁 **Recent Captures**\n");

                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    var icon = file.EndsWith(".log") ? "📝" : "🖼️";
                    sb.AppendLine($"{icon} `{info.Name}` - {info.LastWriteTime:yyyy-MM-dd HH:mm} ({info.Length / 1024} KB)");
                }

                sb.AppendLine($"\n📂 Location: `{CaptureDirectory}`");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to list captures: {ex.Message}");
            }
        }
    }

    // ==================== VISION AI TOOLS ====================

    /// <summary>
    /// Look at the screen and understand what's visible using AI vision.
    /// </summary>
    public class SeeScreenTool : ITool
    {
        public string Name => "see_screen";
        public string Description => "Look at my screen using AI vision and describe what I see. I can understand the content, applications, and context. Input (optional): specific question about what's on screen.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available. Make sure a vision model (llava) is running.");
            }

            try
            {
                var prompt = string.IsNullOrWhiteSpace(input)
                    ? null
                    : input.Trim();

                var result = await VisionService.CaptureAndAnalyzeScreenAsync(prompt, ct: ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Vision analysis failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine("👁️ **What I See on Screen:**\n");
                sb.AppendLine(result.Description);
                sb.AppendLine($"\n_Analyzed at {result.Timestamp:HH:mm:ss} using {result.Model}_");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Vision failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Describe an image file using AI vision.
    /// </summary>
    public class DescribeImageTool : ITool
    {
        public string Name => "describe_image";
        public string Description => "Use AI vision to describe an image file in detail. Input: path to image file, optionally with a question (e.g., 'screenshot.png what color is the button?').";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            var parts = input.Trim().Split(' ', 2);
            var imagePath = parts[0].Trim('"');
            var prompt = parts.Length > 1 ? parts[1] : null;

            // Try to resolve relative path
            if (!Path.IsPathRooted(imagePath))
            {
                var capturesPath = Path.Combine(CaptureDirectory, imagePath);
                if (File.Exists(capturesPath))
                {
                    imagePath = capturesPath;
                }
                else
                {
                    imagePath = Path.Combine(Environment.CurrentDirectory, imagePath);
                }
            }

            try
            {
                var result = await VisionService.AnalyzeImageAsync(imagePath, prompt, ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Image analysis failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🖼️ **Image Analysis: {Path.GetFileName(imagePath)}**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Image analysis failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Read text (OCR) from the screen using AI vision.
    /// </summary>
    public class ReadTextFromScreenTool : ITool
    {
        public string Name => "read_screen_text";
        public string Description => "Read and transcribe all visible text from the screen using AI vision (OCR). Useful for understanding dialogs, error messages, or document content.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            try
            {
                var prompt = "Read and transcribe ALL visible text on this screen. Include text from windows, dialogs, buttons, menus, documents, code editors, and any other visible text. Format it clearly.";

                var result = await VisionService.CaptureAndAnalyzeScreenAsync(prompt, ct: ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "OCR failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine("📖 **Text Visible on Screen:**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"OCR failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Understand what the user is currently doing.
    /// </summary>
    public class WhatAmIDoingTool : ITool
    {
        public string Name => "what_am_i_doing";
        public string Description => "Use AI vision to understand and describe what I (the user) am currently doing on my computer. Provides context about current task and activity.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            try
            {
                var result = await VisionService.DescribeUserActivityAsync(ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Activity analysis failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine("🔍 **Current User Activity Analysis:**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Activity analysis failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Detect specific objects or elements on screen.
    /// </summary>
    public class DetectObjectsTool : ITool
    {
        public string Name => "detect_on_screen";
        public string Description => "Detect specific objects, elements, or conditions on screen. Input: what to look for (e.g., 'error dialog', 'red button', 'loading spinner', 'specific text').";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (VisionService == null)
            {
                return Result<string, string>.Failure("Vision service not available.");
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                return Result<string, string>.Failure("Please specify what to detect (e.g., 'error dialog', 'submit button').");
            }

            try
            {
                var prompt = $"Look at this screen and answer: Can you see '{input.Trim()}'? If yes, describe where it is and its current state. If no, describe what you see instead.";

                var result = await VisionService.CaptureAndAnalyzeScreenAsync(prompt, ct: ct);

                if (!result.Success)
                {
                    return Result<string, string>.Failure(result.ErrorMessage ?? "Detection failed");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🎯 **Detection: '{input.Trim()}'**\n");
                sb.AppendLine(result.Description);

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Detection failed: {ex.Message}");
            }
        }
    }
}
