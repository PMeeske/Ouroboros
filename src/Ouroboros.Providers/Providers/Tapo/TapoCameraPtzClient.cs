// <copyright file="TapoCameraPtzClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// PTZ (Pan/Tilt/Zoom) controller for Tapo cameras with motor capabilities.
/// Uses ONVIF Profile S for standardized PTZ control.
/// Supported cameras: C200 (360° pan, 114° tilt), C210, C500, C520.
/// </summary>
public sealed class TapoCameraPtzClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TapoCameraPtzClient>? _logger;
    private readonly string _cameraIp;
    private readonly string _username;
    private readonly string _password;
    private readonly string _onvifUrl;
    private string? _profileToken;
    private bool _disposed;

    /// <summary>
    /// Pan/tilt speed range: -1.0 (full reverse) to 1.0 (full forward).
    /// </summary>
    private const float MaxSpeed = 1.0f;

    /// <summary>
    /// Default movement duration in milliseconds.
    /// </summary>
    private const int DefaultMoveDurationMs = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="TapoCameraPtzClient"/> class.
    /// </summary>
    /// <param name="cameraIp">IP address of the Tapo camera.</param>
    /// <param name="username">Camera account username.</param>
    /// <param name="password">Camera account password.</param>
    /// <param name="logger">Optional logger.</param>
    public TapoCameraPtzClient(
        string cameraIp,
        string username,
        string password,
        ILogger<TapoCameraPtzClient>? logger = null)
    {
        _cameraIp = cameraIp ?? throw new ArgumentNullException(nameof(cameraIp));
        _username = username ?? throw new ArgumentNullException(nameof(username));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _logger = logger;
        _onvifUrl = $"http://{_cameraIp}:2020/onvif/ptz_service";

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Gets the camera IP address.
    /// </summary>
    public string CameraIp => _cameraIp;

    /// <summary>
    /// Gets the PTZ capabilities of this camera.
    /// </summary>
    public PtzCapabilities Capabilities { get; private set; } = PtzCapabilities.Default;

    /// <summary>
    /// Initializes the PTZ client by discovering the ONVIF profile token.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<PtzCapabilities>> InitializeAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzCapabilities>.Failure("Client is disposed");

        try
        {
            _logger?.LogInformation("Initializing PTZ for camera at {CameraIp}", _cameraIp);

            // Try ONVIF GetProfiles to discover media profile token
            var profileResult = await GetOnvifProfileTokenAsync(ct);
            if (profileResult.IsSuccess)
            {
                _profileToken = profileResult.Value;
                _logger?.LogInformation("ONVIF profile token: {Token}", _profileToken);
            }
            else
            {
                // Default profile token for Tapo cameras
                _profileToken = "profile_1";
                _logger?.LogWarning(
                    "Could not discover ONVIF profile, using default: {Token}. Error: {Error}",
                    _profileToken, profileResult.Error);
            }

            // Determine capabilities based on camera model
            Capabilities = new PtzCapabilities(
                CanPan: true,
                CanTilt: true,
                CanZoom: false, // C200 has no optical zoom
                PanRange: (-1.0f, 1.0f),
                TiltRange: (-1.0f, 1.0f),
                ZoomRange: (0f, 0f),
                SupportsAbsoluteMove: false, // Tapo only supports relative/continuous
                SupportsContinuousMove: true,
                SupportsRelativeMove: true,
                SupportsPresets: true,
                MaxPresets: 8);

            _logger?.LogInformation("PTZ initialized for {CameraIp}: Pan={Pan}, Tilt={Tilt}",
                _cameraIp, Capabilities.CanPan, Capabilities.CanTilt);

            return Result<PtzCapabilities>.Success(Capabilities);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize PTZ for {CameraIp}", _cameraIp);
            return Result<PtzCapabilities>.Failure($"PTZ initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Pans the camera left at the specified speed.
    /// </summary>
    /// <param name="speed">Pan speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> PanLeftAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(-Math.Abs(speed), 0, 0, durationMs, ct);
    }

    /// <summary>
    /// Pans the camera right at the specified speed.
    /// </summary>
    /// <param name="speed">Pan speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> PanRightAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(Math.Abs(speed), 0, 0, durationMs, ct);
    }

    /// <summary>
    /// Tilts the camera up at the specified speed.
    /// </summary>
    /// <param name="speed">Tilt speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> TiltUpAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(0, Math.Abs(speed), 0, durationMs, ct);
    }

    /// <summary>
    /// Tilts the camera down at the specified speed.
    /// </summary>
    /// <param name="speed">Tilt speed (0.0 to 1.0).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> TiltDownAsync(
        float speed = 0.5f,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(0, -Math.Abs(speed), 0, durationMs, ct);
    }

    /// <summary>
    /// Moves the camera diagonally (combined pan and tilt).
    /// </summary>
    /// <param name="panSpeed">Pan speed (-1.0 left to 1.0 right).</param>
    /// <param name="tiltSpeed">Tilt speed (-1.0 down to 1.0 up).</param>
    /// <param name="durationMs">Duration of movement in ms.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> MoveAsync(
        float panSpeed,
        float tiltSpeed,
        int durationMs = DefaultMoveDurationMs,
        CancellationToken ct = default)
    {
        return await ContinuousMoveAsync(panSpeed, tiltSpeed, 0, durationMs, ct);
    }

    /// <summary>
    /// Stops all PTZ movement immediately.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> StopAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifStop(_profileToken ?? "profile_1");
            var result = await SendOnvifCommandAsync(soapBody, ct);

            if (result.IsSuccess)
            {
                _logger?.LogDebug("PTZ stopped on {CameraIp}", _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "stop", TimeSpan.Zero, "Movement stopped"));
            }

            return Result<PtzMoveResult>.Failure($"Stop failed: {result.Error}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop PTZ on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Stop failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves the camera to its home position (factory default center).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> GoToHomeAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifGotoHomePosition(_profileToken ?? "profile_1");
            var result = await SendOnvifCommandAsync(soapBody, ct);

            if (result.IsSuccess)
            {
                _logger?.LogInformation("Moving to home position on {CameraIp}", _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "go_home", TimeSpan.Zero, "Moving to home position"));
            }

            return Result<PtzMoveResult>.Failure($"Go home failed: {result.Error}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to go home on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Go home failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the current position as a preset.
    /// </summary>
    /// <param name="presetName">Name for the preset.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> SetPresetAsync(
        string presetName,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifSetPreset(_profileToken ?? "profile_1", presetName);
            var result = await SendOnvifCommandAsync(soapBody, ct);

            if (result.IsSuccess)
            {
                _logger?.LogInformation("Preset '{Preset}' saved on {CameraIp}", presetName, _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "set_preset", TimeSpan.Zero, $"Preset '{presetName}' saved"));
            }

            return Result<PtzMoveResult>.Failure($"Set preset failed: {result.Error}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set preset on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Set preset failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves the camera to a saved preset position.
    /// </summary>
    /// <param name="presetToken">Preset token or number.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> GoToPresetAsync(
        string presetToken,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            var soapBody = BuildOnvifGotoPreset(_profileToken ?? "profile_1", presetToken);
            var result = await SendOnvifCommandAsync(soapBody, ct);

            if (result.IsSuccess)
            {
                _logger?.LogInformation("Moving to preset '{Preset}' on {CameraIp}", presetToken, _cameraIp);
                return Result<PtzMoveResult>.Success(
                    new PtzMoveResult(true, "go_to_preset", TimeSpan.Zero, $"Moving to preset '{presetToken}'"));
            }

            return Result<PtzMoveResult>.Failure($"Go to preset failed: {result.Error}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to go to preset on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Go to preset failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Performs a patrol sweep: pan fully left, then fully right, then return home.
    /// Useful for periodic area scanning.
    /// </summary>
    /// <param name="speed">Sweep speed (0.0 to 1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<PtzMoveResult>> PatrolSweepAsync(
        float speed = 0.3f,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        try
        {
            _logger?.LogInformation("Starting patrol sweep on {CameraIp}", _cameraIp);
            var sw = Stopwatch.StartNew();

            // Pan left for 3 seconds
            var leftResult = await ContinuousMoveAsync(-speed, 0, 0, 3000, ct);
            if (leftResult.IsFailure) return leftResult;

            // Pan right for 6 seconds (left + center + right)
            var rightResult = await ContinuousMoveAsync(speed, 0, 0, 6000, ct);
            if (rightResult.IsFailure) return rightResult;

            // Return to center
            var centerResult = await ContinuousMoveAsync(-speed, 0, 0, 3000, ct);
            if (centerResult.IsFailure) return centerResult;

            sw.Stop();
            _logger?.LogInformation("Patrol sweep completed in {Duration}ms", sw.ElapsedMilliseconds);

            return Result<PtzMoveResult>.Success(
                new PtzMoveResult(true, "patrol_sweep", sw.Elapsed, "Patrol sweep completed"));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Patrol sweep failed on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Patrol sweep failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests PTZ connectivity by sending a stop command.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<string>> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var stopResult = await StopAsync(ct);
            return stopResult.IsSuccess
                ? Result<string>.Success($"PTZ control available at {_cameraIp}")
                : Result<string>.Failure($"PTZ not responding: {stopResult.Error}");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"PTZ connection test failed: {ex.Message}");
        }
    }

    #region ONVIF Protocol

    private async Task<Result<PtzMoveResult>> ContinuousMoveAsync(
        float panSpeed, float tiltSpeed, float zoomSpeed,
        int durationMs, CancellationToken ct)
    {
        if (_disposed) return Result<PtzMoveResult>.Failure("Client is disposed");

        // Clamp speeds to valid range
        panSpeed = Math.Clamp(panSpeed, -MaxSpeed, MaxSpeed);
        tiltSpeed = Math.Clamp(tiltSpeed, -MaxSpeed, MaxSpeed);
        zoomSpeed = Math.Clamp(zoomSpeed, -MaxSpeed, MaxSpeed);

        try
        {
            var sw = Stopwatch.StartNew();

            var soapBody = BuildOnvifContinuousMove(
                _profileToken ?? "profile_1", panSpeed, tiltSpeed, zoomSpeed);
            var startResult = await SendOnvifCommandAsync(soapBody, ct);

            if (startResult.IsFailure)
            {
                return Result<PtzMoveResult>.Failure($"Move failed: {startResult.Error}");
            }

            // Wait for the specified duration
            await Task.Delay(durationMs, ct);

            // Stop the movement
            await StopAsync(ct);

            sw.Stop();

            var direction = (panSpeed, tiltSpeed) switch
            {
                ( < 0, 0) => "pan_left",
                ( > 0, 0) => "pan_right",
                (0, > 0) => "tilt_up",
                (0, < 0) => "tilt_down",
                ( < 0, > 0) => "pan_left+tilt_up",
                ( > 0, > 0) => "pan_right+tilt_up",
                ( < 0, < 0) => "pan_left+tilt_down",
                ( > 0, < 0) => "pan_right+tilt_down",
                _ => "stop"
            };

            _logger?.LogDebug("PTZ move {Direction} at speed ({Pan},{Tilt}) for {Duration}ms on {CameraIp}",
                direction, panSpeed, tiltSpeed, durationMs, _cameraIp);

            return Result<PtzMoveResult>.Success(
                new PtzMoveResult(true, direction, sw.Elapsed,
                    $"Moved {direction} for {durationMs}ms"));
        }
        catch (OperationCanceledException)
        {
            await StopAsync(CancellationToken.None);
            return Result<PtzMoveResult>.Failure("Movement cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "PTZ move failed on {CameraIp}", _cameraIp);
            return Result<PtzMoveResult>.Failure($"Move failed: {ex.Message}");
        }
    }

    private async Task<Result<string>> SendOnvifCommandAsync(string soapEnvelope, CancellationToken ct)
    {
        try
        {
            // Inject WS-Security header into SOAP envelope
            var authenticatedEnvelope = InjectWsseHeader(soapEnvelope);
            var content = new StringContent(authenticatedEnvelope, Encoding.UTF8, "application/soap+xml");
            content.Headers.Add("SOAPAction", "\"\"");

            var request = new HttpRequestMessage(HttpMethod.Post, _onvifUrl)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return Result<string>.Success(body);
            }

            // If ONVIF port 2020 fails, try port 80
            if (response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return await TryAlternateOnvifPortAsync(soapEnvelope, ct);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            return Result<string>.Failure(
                $"ONVIF returned {response.StatusCode}: {errorBody}");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            // Port 2020 not available, try port 80
            return await TryAlternateOnvifPortAsync(soapEnvelope, ct);
        }
        catch (TaskCanceledException)
        {
            return Result<string>.Failure("ONVIF request timed out");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"ONVIF error: {ex.Message}");
        }
    }

    private async Task<Result<string>> TryAlternateOnvifPortAsync(
        string soapEnvelope, CancellationToken ct)
    {
        try
        {
            // Tapo cameras often expose ONVIF on port 80 or 8080
            var alternateUrls = new[]
            {
                $"http://{_cameraIp}:80/onvif/ptz_service",
                $"http://{_cameraIp}:8080/onvif/ptz_service",
                $"http://{_cameraIp}:80/onvif/device_service",
            };

            foreach (var url in alternateUrls)
            {
                try
                {
                    var content = new StringContent(soapEnvelope, Encoding.UTF8, "application/soap+xml");
                    var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                    var response = await _httpClient.SendAsync(request, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync(ct);
                        _logger?.LogDebug("ONVIF succeeded on alternate URL: {Url}", url);
                        return Result<string>.Success(body);
                    }
                }
                catch
                {
                    // Try next URL
                }
            }

            return Result<string>.Failure("ONVIF not available on any port");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Alternate port probe failed: {ex.Message}");
        }
    }

    private async Task<Result<string>> GetOnvifProfileTokenAsync(CancellationToken ct)
    {
        var getProfilesSoap = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
                <s:Header>{BuildWsseHeader()}</s:Header>
                <s:Body>
                    <trt:GetProfiles/>
                </s:Body>
            </s:Envelope>
            """;

        var mediaUrl = $"http://{_cameraIp}:2020/onvif/media_service";
        try
        {
            var content = new StringContent(getProfilesSoap, Encoding.UTF8, "application/soap+xml");
            var request = new HttpRequestMessage(HttpMethod.Post, mediaUrl) { Content = content };
            var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                // Parse profile token from SOAP response
                var tokenStart = body.IndexOf("token=\"", StringComparison.Ordinal);
                if (tokenStart >= 0)
                {
                    tokenStart += 7;
                    var tokenEnd = body.IndexOf('"', tokenStart);
                    if (tokenEnd > tokenStart)
                    {
                        return Result<string>.Success(body[tokenStart..tokenEnd]);
                    }
                }
                return Result<string>.Success("profile_1");
            }

            return Result<string>.Failure($"GetProfiles failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"GetProfiles failed: {ex.Message}");
        }
    }

    #endregion

    #region ONVIF SOAP Builders

    private static string BuildOnvifContinuousMove(
        string profileToken, float panSpeed, float tiltSpeed, float zoomSpeed)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl"
                        xmlns:tt="http://www.onvif.org/ver10/schema">
                <s:Header/>
                <s:Body>
                    <tptz:ContinuousMove>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:Velocity>
                            <tt:PanTilt x="{panSpeed:F2}" y="{tiltSpeed:F2}"/>
                            <tt:Zoom x="{zoomSpeed:F2}"/>
                        </tptz:Velocity>
                    </tptz:ContinuousMove>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifStop(string profileToken)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl">
                <s:Header/>
                <s:Body>
                    <tptz:Stop>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:PanTilt>true</tptz:PanTilt>
                        <tptz:Zoom>true</tptz:Zoom>
                    </tptz:Stop>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifGotoHomePosition(string profileToken)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl">
                <s:Header/>
                <s:Body>
                    <tptz:GotoHomePosition>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                    </tptz:GotoHomePosition>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifSetPreset(string profileToken, string presetName)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl">
                <s:Header/>
                <s:Body>
                    <tptz:SetPreset>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:PresetName>{presetName}</tptz:PresetName>
                    </tptz:SetPreset>
                </s:Body>
            </s:Envelope>
            """;
    }

    private static string BuildOnvifGotoPreset(string profileToken, string presetToken)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:tptz="http://www.onvif.org/ver20/ptz/wsdl"
                        xmlns:tt="http://www.onvif.org/ver10/schema">
                <s:Header/>
                <s:Body>
                    <tptz:GotoPreset>
                        <tptz:ProfileToken>{profileToken}</tptz:ProfileToken>
                        <tptz:PresetToken>{presetToken}</tptz:PresetToken>
                        <tptz:Speed>
                            <tt:PanTilt x="0.5" y="0.5"/>
                            <tt:Zoom x="0.5"/>
                        </tptz:Speed>
                    </tptz:GotoPreset>
                </s:Body>
            </s:Envelope>
            """;
    }

    private string BuildWsseHeader()
    {
        // WS-Security UsernameToken with PasswordDigest per ONVIF spec:
        // PasswordDigest = Base64(SHA1(nonce_raw + created_utf8 + password_utf8))
        var nonceBytes = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(nonceBytes);
        }

        var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes(_password);

        // Digest = Base64(SHA1(nonce + created + password))
        var digestInput = new byte[nonceBytes.Length + createdBytes.Length + passwordBytes.Length];
        Buffer.BlockCopy(nonceBytes, 0, digestInput, 0, nonceBytes.Length);
        Buffer.BlockCopy(createdBytes, 0, digestInput, nonceBytes.Length, createdBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, digestInput, nonceBytes.Length + createdBytes.Length, passwordBytes.Length);

        var digestHash = SHA1.HashData(digestInput);
        var passwordDigest = Convert.ToBase64String(digestHash);
        var nonceBase64 = Convert.ToBase64String(nonceBytes);

        return $"""
            <Security xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"
                      s:mustUnderstand="true">
                <UsernameToken>
                    <Username>{_username}</Username>
                    <Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest">{passwordDigest}</Password>
                    <Nonce EncodingType="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary">{nonceBase64}</Nonce>
                    <Created xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">{created}</Created>
                </UsernameToken>
            </Security>
            """;
    }

    /// <summary>
    /// Replaces the empty <c>&lt;s:Header/&gt;</c> placeholder in a SOAP envelope
    /// with a freshly computed WS-Security UsernameToken header.
    /// </summary>
    private string InjectWsseHeader(string soapEnvelope)
    {
        var wsseHeader = BuildWsseHeader();
        // Replace empty header placeholder with auth header
        return soapEnvelope.Replace("<s:Header/>", $"<s:Header>{wsseHeader}</s:Header>");
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
        _logger?.LogDebug("TapoCameraPtzClient disposed for {CameraIp}", _cameraIp);
    }
}

/// <summary>
/// PTZ capabilities of a camera.
/// </summary>
/// <param name="CanPan">Whether the camera can pan (horizontal rotation).</param>
/// <param name="CanTilt">Whether the camera can tilt (vertical rotation).</param>
/// <param name="CanZoom">Whether the camera has motorized zoom.</param>
/// <param name="PanRange">Pan speed range (min, max).</param>
/// <param name="TiltRange">Tilt speed range (min, max).</param>
/// <param name="ZoomRange">Zoom range (min, max).</param>
/// <param name="SupportsAbsoluteMove">Whether absolute positioning is supported.</param>
/// <param name="SupportsContinuousMove">Whether continuous movement is supported.</param>
/// <param name="SupportsRelativeMove">Whether relative movement is supported.</param>
/// <param name="SupportsPresets">Whether position presets are supported.</param>
/// <param name="MaxPresets">Maximum number of presets.</param>
public sealed record PtzCapabilities(
    bool CanPan,
    bool CanTilt,
    bool CanZoom,
    (float Min, float Max) PanRange,
    (float Min, float Max) TiltRange,
    (float Min, float Max) ZoomRange,
    bool SupportsAbsoluteMove,
    bool SupportsContinuousMove,
    bool SupportsRelativeMove,
    bool SupportsPresets,
    int MaxPresets)
{
    /// <summary>
    /// Default capabilities for a Tapo C200 camera.
    /// </summary>
    public static PtzCapabilities Default => new(
        CanPan: true,
        CanTilt: true,
        CanZoom: false,
        PanRange: (-1.0f, 1.0f),
        TiltRange: (-1.0f, 1.0f),
        ZoomRange: (0f, 0f),
        SupportsAbsoluteMove: false,
        SupportsContinuousMove: true,
        SupportsRelativeMove: true,
        SupportsPresets: true,
        MaxPresets: 8);
}

/// <summary>
/// Result of a PTZ movement operation.
/// </summary>
/// <param name="Success">Whether the movement succeeded.</param>
/// <param name="Direction">Direction of movement.</param>
/// <param name="Duration">How long the movement took.</param>
/// <param name="Message">Descriptive message.</param>
public sealed record PtzMoveResult(
    bool Success,
    string Direction,
    TimeSpan Duration,
    string? Message = null);
