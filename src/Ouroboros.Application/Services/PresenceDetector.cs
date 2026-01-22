// <copyright file="PresenceDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Detects user presence via camera (face/motion detection) and WiFi (device proximity).
/// Enables proactive interaction when the user is nearby.
/// </summary>
public class PresenceDetector : IDisposable
{
    private readonly PresenceConfig _config;
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitoringTask;
    private bool _isActive;
    private DateTime _lastPresenceDetected = DateTime.MinValue;
    private DateTime _lastAbsenceDetected = DateTime.MinValue;
    private int _consecutivePresenceFrames;
    private int _consecutiveAbsenceFrames;
    private PresenceState _currentState = PresenceState.Unknown;
    private readonly HashSet<string> _knownDevices = [];
    private int _lastWifiDeviceCount;

    /// <summary>
    /// Event fired when user presence is detected.
    /// </summary>
    public event Action<PresenceEvent>? OnPresenceDetected;

    /// <summary>
    /// Event fired when user absence is detected.
    /// </summary>
    public event Action<PresenceEvent>? OnAbsenceDetected;

    /// <summary>
    /// Event fired when presence state changes.
    /// </summary>
    public event Action<PresenceState, PresenceState>? OnStateChanged;

    /// <summary>
    /// Gets the current presence state.
    /// </summary>
    public PresenceState CurrentState => _currentState;

    /// <summary>
    /// Gets whether monitoring is active.
    /// </summary>
    public bool IsMonitoring => _isActive;

    /// <summary>
    /// Gets the last time presence was detected.
    /// </summary>
    public DateTime LastPresenceTime => _lastPresenceDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="PresenceDetector"/> class.
    /// </summary>
    public PresenceDetector(PresenceConfig? config = null)
    {
        _config = config ?? new PresenceConfig();
    }

    /// <summary>
    /// Starts presence monitoring.
    /// </summary>
    public void Start()
    {
        if (_isActive) return;
        _isActive = true;

        _monitoringTask = Task.Run(MonitoringLoopAsync);
    }

    /// <summary>
    /// Stops presence monitoring.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isActive) return;
        _isActive = false;
        _cts.Cancel();

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }

    /// <summary>
    /// Performs an immediate presence check.
    /// </summary>
    public async Task<PresenceCheckResult> CheckPresenceAsync(CancellationToken ct = default)
    {
        var result = new PresenceCheckResult { Timestamp = DateTime.UtcNow };

        // Check WiFi/network for nearby devices
        var wifiResult = await CheckWifiPresenceAsync(ct);
        result.WifiDevicesNearby = wifiResult.DeviceCount;
        result.WifiPresenceConfidence = wifiResult.Confidence;

        // Check for camera/motion (if available)
        var motionResult = await CheckCameraPresenceAsync(ct);
        result.MotionDetected = motionResult.MotionDetected;
        result.CameraConfidence = motionResult.Confidence;

        // Check for keyboard/mouse activity
        var activityResult = CheckInputActivity();
        result.RecentInputActivity = activityResult.HasRecentActivity;
        result.InputActivityConfidence = activityResult.Confidence;

        // Calculate overall presence probability
        var confidences = new List<double>();
        if (_config.UseWifi) confidences.Add(result.WifiPresenceConfidence);
        if (_config.UseCamera) confidences.Add(result.CameraConfidence);
        if (_config.UseInputActivity) confidences.Add(result.InputActivityConfidence);

        result.OverallConfidence = confidences.Count > 0
            ? confidences.Average()
            : 0.5;

        result.IsPresent = result.OverallConfidence >= _config.PresenceThreshold;

        return result;
    }

    /// <summary>
    /// Adds a known device MAC/IP to track for presence.
    /// </summary>
    public void AddKnownDevice(string deviceIdentifier)
    {
        _knownDevices.Add(deviceIdentifier.ToUpperInvariant());
    }

    private async Task MonitoringLoopAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.CheckIntervalSeconds), _cts.Token);

                var check = await CheckPresenceAsync(_cts.Token);
                ProcessPresenceCheck(check);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PresenceDetector] Error: {ex.Message}");
            }
        }
    }

    private void ProcessPresenceCheck(PresenceCheckResult check)
    {
        var previousState = _currentState;

        if (check.IsPresent)
        {
            _consecutivePresenceFrames++;
            _consecutiveAbsenceFrames = 0;

            // Require multiple consecutive frames to confirm presence
            if (_consecutivePresenceFrames >= _config.PresenceConfirmationFrames)
            {
                if (_currentState != PresenceState.Present)
                {
                    _currentState = PresenceState.Present;
                    _lastPresenceDetected = DateTime.UtcNow;

                    var evt = new PresenceEvent
                    {
                        State = PresenceState.Present,
                        Timestamp = DateTime.UtcNow,
                        Confidence = check.OverallConfidence,
                        Source = DetermineSource(check),
                        TimeSinceLastState = previousState != PresenceState.Unknown
                            ? DateTime.UtcNow - _lastAbsenceDetected
                            : null,
                    };

                    OnPresenceDetected?.Invoke(evt);
                    OnStateChanged?.Invoke(previousState, _currentState);
                }
            }
        }
        else
        {
            _consecutiveAbsenceFrames++;
            _consecutivePresenceFrames = 0;

            // Require more frames to confirm absence (avoid false negatives)
            if (_consecutiveAbsenceFrames >= _config.AbsenceConfirmationFrames)
            {
                if (_currentState != PresenceState.Absent)
                {
                    _currentState = PresenceState.Absent;
                    _lastAbsenceDetected = DateTime.UtcNow;

                    var evt = new PresenceEvent
                    {
                        State = PresenceState.Absent,
                        Timestamp = DateTime.UtcNow,
                        Confidence = 1.0 - check.OverallConfidence,
                        Source = "timeout",
                        TimeSinceLastState = previousState != PresenceState.Unknown
                            ? DateTime.UtcNow - _lastPresenceDetected
                            : null,
                    };

                    OnAbsenceDetected?.Invoke(evt);
                    OnStateChanged?.Invoke(previousState, _currentState);
                }
            }
        }
    }

    private string DetermineSource(PresenceCheckResult check)
    {
        var sources = new List<string>();

        if (check.WifiPresenceConfidence > 0.5) sources.Add("wifi");
        if (check.CameraConfidence > 0.5) sources.Add("camera");
        if (check.InputActivityConfidence > 0.5) sources.Add("input");

        return string.Join("+", sources);
    }

    private async Task<(int DeviceCount, double Confidence)> CheckWifiPresenceAsync(CancellationToken ct)
    {
        try
        {
            // Method 1: Check ARP table for devices on local network
            var arpDevices = await GetArpDevicesAsync(ct);
            var deviceCount = arpDevices.Count;

            // Check if known devices are present
            var knownDevicesFound = arpDevices.Count(d => _knownDevices.Contains(d.ToUpperInvariant()));

            // Method 2: Check network interface activity
            var interfaceActivity = GetNetworkInterfaceActivity();

            // Calculate confidence based on:
            // - Change in device count (more devices = likely user nearby)
            // - Known devices present
            // - Network activity
            double confidence = 0.3; // Base confidence

            if (deviceCount > _lastWifiDeviceCount)
            {
                confidence += 0.2; // More devices appeared
            }

            if (knownDevicesFound > 0)
            {
                confidence += 0.3; // Known device detected
            }

            if (interfaceActivity > 1000) // bytes/sec
            {
                confidence += 0.2; // Active network usage
            }

            _lastWifiDeviceCount = deviceCount;

            return (deviceCount, Math.Min(confidence, 1.0));
        }
        catch
        {
            return (0, 0.3);
        }
    }

    private async Task<List<string>> GetArpDevicesAsync(CancellationToken ct)
    {
        var devices = new List<string>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "arp",
                Arguments = "-a",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                // Parse ARP output for IP addresses and MACs
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        // Add both IP and MAC
                        devices.Add(parts[0]);
                        if (parts.Length > 1) devices.Add(parts[1]);
                    }
                }
            }
        }
        catch
        {
            // Silently fail - WiFi detection is best-effort
        }

        return devices;
    }

    private long GetNetworkInterfaceActivity()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                            && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            long totalBytes = 0;
            foreach (var ni in interfaces)
            {
                var stats = ni.GetIPv4Statistics();
                totalBytes += stats.BytesReceived + stats.BytesSent;
            }

            return totalBytes;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<(bool MotionDetected, double Confidence)> CheckCameraPresenceAsync(CancellationToken ct)
    {
        // Camera-based presence detection using frame differencing
        // This is a lightweight approach that doesn't require ML models

        if (!_config.UseCamera)
        {
            return (false, 0.0);
        }

        try
        {
            // Check if any camera capture exists recently (from PerceptionTools)
            var captureDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ouroboros", "captures");

            if (Directory.Exists(captureDir))
            {
                var recentCaptures = Directory.GetFiles(captureDir, "camera_*.jpg")
                    .Select(f => new FileInfo(f))
                    .Where(f => (DateTime.Now - f.LastWriteTime).TotalMinutes < 5)
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(2)
                    .ToList();

                if (recentCaptures.Count >= 2)
                {
                    // Compare file sizes as a simple motion proxy
                    // (significant change = motion detected)
                    var sizeDiff = Math.Abs(recentCaptures[0].Length - recentCaptures[1].Length);
                    var motionThreshold = recentCaptures[0].Length * 0.1; // 10% change

                    return (sizeDiff > motionThreshold, sizeDiff > motionThreshold ? 0.7 : 0.3);
                }
            }

            await Task.CompletedTask;
            return (false, 0.3);
        }
        catch
        {
            return (false, 0.0);
        }
    }

    private (bool HasRecentActivity, double Confidence) CheckInputActivity()
    {
#if NET10_0_OR_GREATER_WINDOWS
        try
        {
            var idleTime = GetIdleTime();
            var idleSeconds = idleTime.TotalSeconds;

            // Recent activity if idle time is less than threshold
            var hasActivity = idleSeconds < _config.InputIdleThresholdSeconds;
            var confidence = hasActivity
                ? Math.Max(0.9 - (idleSeconds / _config.InputIdleThresholdSeconds * 0.5), 0.5)
                : 0.2;

            return (hasActivity, confidence);
        }
        catch
        {
            return (false, 0.3);
        }
#else
        return (false, 0.3);
#endif
    }

#if NET10_0_OR_GREATER_WINDOWS
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    private static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };

        if (GetLastInputInfo(ref info))
        {
            var idleTicks = Environment.TickCount - (int)info.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }

        return TimeSpan.Zero;
    }
#endif

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Presence state enumeration.
/// </summary>
public enum PresenceState
{
    /// <summary>Unknown presence state.</summary>
    Unknown,

    /// <summary>User is present.</summary>
    Present,

    /// <summary>User is absent.</summary>
    Absent,
}

/// <summary>
/// Configuration for presence detection.
/// </summary>
public record PresenceConfig
{
    /// <summary>Interval between presence checks in seconds.</summary>
    public int CheckIntervalSeconds { get; init; } = 5;

    /// <summary>Confidence threshold to consider user present (0-1).</summary>
    public double PresenceThreshold { get; init; } = 0.6;

    /// <summary>Number of consecutive frames needed to confirm presence.</summary>
    public int PresenceConfirmationFrames { get; init; } = 2;

    /// <summary>Number of consecutive frames needed to confirm absence.</summary>
    public int AbsenceConfirmationFrames { get; init; } = 6;

    /// <summary>Whether to use WiFi/network for detection.</summary>
    public bool UseWifi { get; init; } = true;

    /// <summary>Whether to use camera for detection.</summary>
    public bool UseCamera { get; init; } = false; // Disabled by default for privacy

    /// <summary>Whether to use keyboard/mouse activity for detection.</summary>
    public bool UseInputActivity { get; init; } = true;

    /// <summary>Seconds of input idle before considering inactive.</summary>
    public int InputIdleThresholdSeconds { get; init; } = 300; // 5 minutes
}

/// <summary>
/// Presence event data.
/// </summary>
public record PresenceEvent
{
    /// <summary>The presence state.</summary>
    public required PresenceState State { get; init; }

    /// <summary>When the event occurred.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Confidence level (0-1).</summary>
    public double Confidence { get; init; }

    /// <summary>Detection source (wifi, camera, input, etc.).</summary>
    public string Source { get; init; } = "";

    /// <summary>Time since last state change.</summary>
    public TimeSpan? TimeSinceLastState { get; init; }
}

/// <summary>
/// Result of a presence check.
/// </summary>
public record PresenceCheckResult
{
    /// <summary>When the check was performed.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Whether user is considered present.</summary>
    public bool IsPresent { get; set; }

    /// <summary>Overall confidence (0-1).</summary>
    public double OverallConfidence { get; set; }

    /// <summary>Number of WiFi devices detected nearby.</summary>
    public int WifiDevicesNearby { get; set; }

    /// <summary>WiFi-based presence confidence.</summary>
    public double WifiPresenceConfidence { get; set; }

    /// <summary>Whether motion was detected via camera.</summary>
    public bool MotionDetected { get; set; }

    /// <summary>Camera-based presence confidence.</summary>
    public double CameraConfidence { get; set; }

    /// <summary>Whether recent input activity detected.</summary>
    public bool RecentInputActivity { get; set; }

    /// <summary>Input activity confidence.</summary>
    public double InputActivityConfidence { get; set; }
}
