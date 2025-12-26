// <copyright file="PlaywrightMcpTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Core.Monads;
using Ouroboros.Application.Services;
using Ouroboros.Tools;

namespace Ouroboros.Application.Mcp;

/// <summary>
/// Playwright browser automation tool that connects to the Playwright MCP server.
/// Provides web scraping, browser automation, and testing capabilities to Ouroboros.
/// Uses accessibility snapshots with element references (e.g., ref=e1, ref=e2) for reliable interaction.
/// </summary>
public class PlaywrightMcpTool : ITool, IAsyncDisposable
{
    private readonly McpClient _client;
    private readonly string _screenshotDirectory;
    private readonly VisionService _visionService;
    private readonly string _visionModel;
    private bool _initialized;
    private List<McpToolInfo>? _availableTools;
    private string? _lastScreenshotPath;

    /// <summary>
    /// Gets the path to the last captured screenshot, or null if no screenshots taken.
    /// </summary>
    public string? LastScreenshotPath => _lastScreenshotPath;

    /// <summary>
    /// Gets the directory where screenshots are saved.
    /// </summary>
    public string ScreenshotDirectory => _screenshotDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaywrightMcpTool"/> class.
    /// </summary>
    /// <param name="headless">If true, browser runs in headless mode (invisible). Default is false (visible).</param>
    /// <param name="browser">Browser to use: chrome, firefox, webkit, msedge. Default is chrome.</param>
    /// <param name="screenshotDirectory">Directory to save screenshots. Default is temp folder.</param>
    /// <param name="visionModel">Vision model to use for screenshot analysis. Default is ministral.</param>
    /// <param name="ollamaEndpoint">Ollama endpoint for vision model. Default is localhost:11434.</param>
    public PlaywrightMcpTool(
        bool headless = false,
        string browser = "edge",
        string? screenshotDirectory = null,
        string visionModel = "minicpm-v",
        string ollamaEndpoint = "http://localhost:11434")
    {
        // Use -y to auto-confirm package installation and @latest to get current version
        var args = new List<string> { "-y", "@playwright/mcp@latest", "--browser", browser };
        if (headless)
        {
            args.Add("--headless");
        }

        _client = new McpClient("npx", args.ToArray());

        // Setup screenshot directory
        _screenshotDirectory = screenshotDirectory ?? Path.Combine(Path.GetTempPath(), "ouroboros_screenshots");
        Directory.CreateDirectory(_screenshotDirectory);

        // Setup vision service with Ministral model
        _visionModel = visionModel;
        _visionService = new VisionService(new VisionConfig
        {
            Backend = VisionBackend.Ollama,
            OllamaEndpoint = ollamaEndpoint,
            OllamaVisionModel = visionModel,
        });
    }

    /// <inheritdoc/>
    public string Name => "playwright";

    /// <inheritdoc/>
    public string Description =>
        "Browser automation using Playwright with AI vision. WORKFLOW: 1) Use 'navigate' to go to a URL, " +
        "2) Use 'snapshot' to get the accessibility tree with element refs like [ref=e1], " +
        "3) Use 'click' or 'type' with the element description and ref. " +
        "VISION ACTIONS: 'screenshot' captures and analyzes page, 'detect_elements' finds UI components, " +
        "'extract_text' performs OCR, 'suggest_action' recommends next step, 'validate' checks page state. " +
        "Available actions: navigate, snapshot, screenshot, detect_elements, extract_text, suggest_action, validate, click, type, hover, evaluate, list_tools.";

    /// <inheritdoc/>
    public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "description": "The action to perform",
                    "enum": ["navigate", "snapshot", "screenshot", "detect_elements", "extract_text", "suggest_action", "validate", "click", "type", "hover", "evaluate", "list_tools"]
                },
                "url": {
                    "type": "string",
                    "description": "URL to navigate to (for navigate action)"
                },
                "element": {
                    "type": "string",
                    "description": "Human-readable element description, e.g., 'Search button' or 'Email input'"
                },
                "ref": {
                    "type": "string",
                    "description": "Element reference from snapshot, e.g., 'e1', 'e2', 'e15'. Get this from the snapshot output."
                },
                "text": {
                    "type": "string",
                    "description": "Text to type (for type action)"
                },
                "code": {
                    "type": "string",
                    "description": "JavaScript code to evaluate (for evaluate action)"
                },
                "fullPage": {
                    "type": "boolean",
                    "description": "Capture full scrollable page for screenshots (default: false)"
                },
                "goal": {
                    "type": "string",
                    "description": "User's goal for suggest_action - what are they trying to accomplish?"
                },
                "expectations": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "List of expectations to validate against the current page state"
                }
            },
            "required": ["action"]
        }
        """;

    /// <summary>
    /// Initializes the Playwright MCP connection.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized && _client.IsConnected) return;

        await _client.ConnectAsync(ct);
        _availableTools = await _client.ListToolsAsync(ct);

        // Log what tools we got (useful for debugging)
        if (_availableTools.Count == 0)
        {
            Console.WriteLine("  ⚠ Playwright MCP: No tools returned from server");
        }

        _initialized = true;
    }

    /// <summary>
    /// Gets the available Playwright tools.
    /// </summary>
    public IReadOnlyList<McpToolInfo> AvailableTools => _availableTools ?? [];

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string args, CancellationToken ct = default)
    {
        try
        {
            // Ensure connection is healthy before each call
            if (!_initialized || !_client.IsConnected)
            {
                _initialized = false;
                await InitializeAsync(ct);
            }

            PlaywrightArgs? parsed = JsonSerializer.Deserialize<PlaywrightArgs>(args);
            if (parsed == null)
            {
                return Result<string, string>.Failure("Invalid arguments format");
            }

            // Validate that parameters are actual values, not placeholder descriptions
            (bool isValid, string? validationError) = parsed.Validate();
            if (!isValid)
            {
                return Result<string, string>.Failure(validationError!);
            }

            // Map high-level actions to Playwright MCP tool calls
            string result = parsed.Action.ToLowerInvariant() switch
            {
                "list_tools" => await ListAvailableToolsAsync(),
                "navigate" => await NavigateAsync(parsed.Url ?? "", ct),
                "snapshot" => await GetSnapshotAsync(ct),
                "screenshot" => await TakeScreenshotAsync(parsed.FullPage, ct),
                "detect_elements" => await DetectElementsAsync(ct),
                "extract_text" => await ExtractTextAsync(ct),
                "suggest_action" => await SuggestActionAsync(parsed.Goal ?? "complete the current task", ct),
                "validate" => await ValidatePageStateAsync(parsed.Expectations ?? Array.Empty<string>(), ct),
                "click" => await ClickAsync(parsed.Element ?? "", parsed.Ref ?? "", ct),
                "type" => await TypeAsync(parsed.Element ?? "", parsed.Ref ?? "", parsed.Text ?? "", ct),
                "hover" => await HoverAsync(parsed.Element ?? "", parsed.Ref ?? "", ct),
                "evaluate" => await EvaluateAsync(parsed.Code ?? "", ct),
                _ => await CallRawToolAsync(parsed.Action, parsed, ct),
            };

            // Sanitize output - remove binary/corrupted data that could confuse the LLM
            result = SanitizeOutput(result);

            return Result<string, string>.Success(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not connected") || ex.Message.Contains("No response"))
        {
            // Server crashed or disconnected - try to reconnect
            _initialized = false;
            return Result<string, string>.Failure($"Playwright MCP server disconnected. Please retry the action. ({ex.Message})");
        }
        catch (Exception ex)
        {
            string connectionStatus = _client.IsConnected ? "connected" : "disconnected";
            return Result<string, string>.Failure($"Playwright error ({connectionStatus}): {ex.Message}");
        }
    }

    private Task<string> ListAvailableToolsAsync()
    {
        if (_availableTools == null || _availableTools.Count == 0)
        {
            return Task.FromResult("No tools available. Playwright MCP server may not be running.");
        }

        var toolList = string.Join("\n", _availableTools.Select(t => $"- {t.Name}: {t.Description}"));
        return Task.FromResult($"Available Playwright tools:\n{toolList}");
    }

    private async Task<string> NavigateAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "Error: URL is required for navigate action";
        }

        // Verify the tool exists
        if (_availableTools?.Any(t => t.Name == "browser_navigate") != true)
        {
            var available = _availableTools != null
                ? string.Join(", ", _availableTools.Select(t => t.Name))
                : "none";
            return $"Error: browser_navigate tool not available. Available tools: {available}";
        }

        // Human-like delay before navigation (simulates typing URL or clicking link)
        await Task.Delay(Random.Shared.Next(400, 900), ct);

        var result = await _client.CallToolAsync(
            "browser_navigate",
            new Dictionary<string, object?> { ["url"] = url },
            ct);

        // Wait for page to settle (human would wait to see page load)
        if (!result.IsError)
        {
            await Task.Delay(Random.Shared.Next(800, 1500), ct);
        }

        return result.IsError ? $"Navigation failed: {result.Content}" : $"Navigated to {url}\n\n{result.Content}";
    }

    private async Task<string> GetSnapshotAsync(CancellationToken ct)
    {
        // Small delay to simulate looking at the page
        await Task.Delay(Random.Shared.Next(200, 400), ct);

        var result = await _client.CallToolAsync("browser_snapshot", null, ct);
        return result.IsError ? $"Snapshot failed: {result.Content}" : result.Content;
    }

    private async Task<string> TakeScreenshotAsync(bool fullPage, CancellationToken ct)
    {
        var args = fullPage ? new Dictionary<string, object?> { ["fullPage"] = true } : null;
        var result = await _client.CallToolAsync("browser_take_screenshot", args, ct);
        if (result.IsError)
        {
            return $"Screenshot failed: {result.Content}";
        }

        // Process and understand the screenshot
        return await ProcessScreenshotAsync(result.Content, ct);
    }

    private async Task<string> ProcessScreenshotAsync(string mcpResponse, CancellationToken ct)
    {
        // Extract base64 image data from the response
        string? base64Data = ExtractBase64ImageData(mcpResponse);
        if (string.IsNullOrEmpty(base64Data))
        {
            return $"Screenshot captured but could not extract image data. Response length: {mcpResponse.Length} chars";
        }

        // Decode and save the image
        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return $"Screenshot captured but base64 decoding failed. Data length: {base64Data.Length} chars";
        }

        // Save to file with timestamp
        string filename = $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Get basic metadata
        var metadata = AnalyzeScreenshot(imageBytes);

        // Use Ministral vision model to understand the screenshot content
        string visionDescription;
        try
        {
            var visionResult = await _visionService.AnalyzeImageAsync(
                filePath,
                "Describe what you see in this browser screenshot. What page is shown? What are the main elements, text content, and any notable UI components? Be concise but comprehensive.",
                ct);

            visionDescription = visionResult.Success
                ? visionResult.Description ?? "No description available"
                : $"Vision analysis unavailable: {visionResult.ErrorMessage}";
        }
        catch (Exception ex)
        {
            visionDescription = $"Vision analysis failed: {ex.Message}";
        }

        return $"Screenshot captured and analyzed:\n" +
               $"  Path: {filePath}\n" +
               $"  Dimensions: {metadata.Width}x{metadata.Height} pixels\n" +
               $"  Size: {imageBytes.Length:N0} bytes ({imageBytes.Length / 1024.0:F1} KB)\n" +
               $"  Format: {metadata.Format}\n" +
               $"  Model: {_visionModel}\n" +
               $"\nVision Analysis:\n{visionDescription}";
    }

    private static string? ExtractBase64ImageData(string response)
    {
        // Try various formats the MCP server might return

        // 1. Look for data:image URI format
        int dataUriStart = response.IndexOf("data:image/", StringComparison.OrdinalIgnoreCase);
        if (dataUriStart >= 0)
        {
            int base64Start = response.IndexOf(";base64,", dataUriStart, StringComparison.OrdinalIgnoreCase);
            if (base64Start >= 0)
            {
                base64Start += 8; // Skip ";base64,"
                // Find the end - either a quote, whitespace, or end of string
                int end = base64Start;
                while (end < response.Length && IsBase64Char(response[end]))
                {
                    end++;
                }
                return response[base64Start..end];
            }
        }

        // 2. Try to parse as JSON and look for image field
        try
        {
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("image", out var imageEl))
            {
                return imageEl.GetString();
            }
            if (doc.RootElement.TryGetProperty("data", out var dataEl))
            {
                return dataEl.GetString();
            }
            if (doc.RootElement.TryGetProperty("screenshot", out var screenshotEl))
            {
                return screenshotEl.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON, continue
        }

        // 3. If the entire response looks like base64, use it
        string trimmed = response.Trim();
        if (trimmed.Length > 100 && trimmed.All(IsBase64Char))
        {
            return trimmed;
        }

        return null;
    }

    private static bool IsBase64Char(char c) =>
        char.IsLetterOrDigit(c) || c == '+' || c == '/' || c == '=';

    private static ScreenshotAnalysis AnalyzeScreenshot(byte[] imageBytes)
    {
        // Detect image format and dimensions from header
        int width = 0, height = 0;
        string format = "unknown";

        if (imageBytes.Length >= 24)
        {
            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
            {
                format = "PNG";
                // Width at bytes 16-19, height at 20-23 (big-endian)
                width = (imageBytes[16] << 24) | (imageBytes[17] << 16) | (imageBytes[18] << 8) | imageBytes[19];
                height = (imageBytes[20] << 24) | (imageBytes[21] << 16) | (imageBytes[22] << 8) | imageBytes[23];
            }
            // JPEG signature: FF D8 FF
            else if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
            {
                format = "JPEG";
                // JPEG dimensions require parsing SOF markers - simplified for now
                (width, height) = ParseJpegDimensions(imageBytes);
            }
        }

        // Generate description based on dimensions
        string description;
        if (width > 0 && height > 0)
        {
            float aspectRatio = (float)width / height;
            string orientation = aspectRatio > 1.2 ? "landscape" : aspectRatio < 0.8 ? "portrait" : "square-ish";
            string sizeCategory = (width * height) switch
            {
                > 2_000_000 => "high-resolution",
                > 500_000 => "medium-resolution",
                _ => "low-resolution"
            };
            description = $"{sizeCategory} {orientation} image";
        }
        else
        {
            description = "Image captured (dimensions could not be determined)";
        }

        return new ScreenshotAnalysis(width, height, format, description);
    }

    private static (int Width, int Height) ParseJpegDimensions(byte[] jpegBytes)
    {
        try
        {
            for (int i = 0; i < jpegBytes.Length - 9; i++)
            {
                // Look for a SOF (Start Of Frame) marker (SOF0, SOF1, SOF2)
                if (jpegBytes[i] == 0xFF && (jpegBytes[i + 1] >= 0xC0 && jpegBytes[i + 1] <= 0xC2))
                {
                    // The marker is found. The structure is:
                    // FF C0 (SOF marker)
                    // XX XX (length)
                    // XX (precision)
                    // HH HH (height, big-endian)
                    // WW WW (width, big-endian)
                    int height = (jpegBytes[i + 5] << 8) | jpegBytes[i + 6];
                    int width = (jpegBytes[i + 7] << 8) | jpegBytes[i + 8];
                    return (width, height);
                }
            }
        }
        catch
        {
            // If parsing fails for any reason, return 0,0.
        }
        return (0, 0);
    }

    /// <summary>
    /// Gets the vision analysis for the last screenshot taken. Internal use for clean data transfer.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw vision analysis text, or an error message.</returns>
    internal async Task<Result<string, string>> GetVisionAnalysisForLastScreenshotAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(LastScreenshotPath))
        {
            return Result<string, string>.Failure("No screenshot has been taken yet.");
        }

        try
        {
            var visionResult = await _visionService.AnalyzeImageAsync(
                LastScreenshotPath,
                "Describe what you see in this browser screenshot. What page is shown? What are the main elements, text content, and any notable UI components? Be concise but comprehensive.",
                ct);

            return visionResult.Success
                ? Result<string, string>.Success(visionResult.Description ?? "No description available")
                : Result<string, string>.Failure(visionResult.ErrorMessage ?? "Vision analysis failed for an unknown reason.");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Vision analysis failed: {ex.Message}");
        }
    }

    private record ScreenshotAnalysis(int Width, int Height, string Format, string Description);

    /// <summary>
    /// Detects UI elements in the current page using vision analysis.
    /// </summary>
    private async Task<string> DetectElementsAsync(CancellationToken ct)
    {
        // First capture a screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for element detection: {screenshotResult.Content}";
        }

        // Save the screenshot
        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for element detection";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for element detection";
        }

        string filename = $"detect_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service to detect elements
        var detection = await _visionService.DetectScreenElementsAsync(filePath, ct);
        if (!detection.Success)
        {
            return $"Element detection failed: {detection.ErrorMessage}";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Detected UI Elements (using {_visionModel}):");
        sb.AppendLine($"Screenshot: {filePath}");
        sb.AppendLine();

        if (detection.Elements.Count > 0)
        {
            sb.AppendLine($"Found {detection.Elements.Count} interactive elements:");
            foreach (var elem in detection.Elements)
            {
                sb.AppendLine($"  [{elem.Type}] {elem.Label}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Raw Analysis:");
        sb.AppendLine(detection.RawDescription);

        return sb.ToString();
    }

    /// <summary>
    /// Extracts all visible text from the current page using OCR.
    /// </summary>
    private async Task<string> ExtractTextAsync(CancellationToken ct)
    {
        // Capture screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for text extraction: {screenshotResult.Content}";
        }

        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for text extraction";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for text extraction";
        }

        string filename = $"ocr_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service for OCR
        var extraction = await _visionService.ExtractTextFromScreenshotAsync(filePath, ct);
        if (!extraction.Success)
        {
            return $"Text extraction failed: {extraction.ErrorMessage}";
        }

        return $"Extracted Text (using {_visionModel}):\n\n{extraction.ExtractedText}";
    }

    /// <summary>
    /// Suggests the next action based on the current page state and user's goal.
    /// </summary>
    private async Task<string> SuggestActionAsync(string goal, CancellationToken ct)
    {
        // Capture screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for action suggestion: {screenshotResult.Content}";
        }

        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for action suggestion";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for action suggestion";
        }

        string filename = $"suggest_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service to suggest action
        var suggestion = await _visionService.SuggestNextActionAsync(filePath, goal, ct);
        if (!suggestion.Success)
        {
            return $"Action suggestion failed: {suggestion.ErrorMessage}";
        }

        return $"Action Suggestion (Goal: {goal})\nModel: {_visionModel}\n\n{suggestion.Suggestion}";
    }

    /// <summary>
    /// Validates that the current page matches expected state.
    /// </summary>
    private async Task<string> ValidatePageStateAsync(string[] expectations, CancellationToken ct)
    {
        if (expectations.Length == 0)
        {
            return "No expectations provided for validation. Use 'expectations' parameter with an array of strings describing what you expect to see.";
        }

        // Capture screenshot
        var screenshotResult = await _client.CallToolAsync("browser_take_screenshot", null, ct);
        if (screenshotResult.IsError)
        {
            return $"Failed to capture screenshot for validation: {screenshotResult.Content}";
        }

        string? base64Data = ExtractBase64ImageData(screenshotResult.Content);
        if (string.IsNullOrEmpty(base64Data))
        {
            return "Could not extract image data for validation";
        }

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(base64Data);
        }
        catch (FormatException)
        {
            return "Failed to decode screenshot for validation";
        }

        string filename = $"validate_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
        string filePath = Path.Combine(_screenshotDirectory, filename);
        await File.WriteAllBytesAsync(filePath, imageBytes, ct);
        _lastScreenshotPath = filePath;

        // Use vision service to validate
        var validation = await _visionService.ValidateScreenStateAsync(filePath, expectations, ct);
        if (!validation.Success)
        {
            return $"Validation failed: Details unavailable";
        }

        var status = validation.AllPassed ? "✅ ALL PASSED" : "❌ SOME FAILED";
        return $"Page Validation: {status}\nModel: {_visionModel}\n\nExpectations checked:\n{string.Join("\n", expectations.Select(e => $"  - {e}"))}\n\nResults:\n{validation.Details}";
    }

    private async Task<string> ClickAsync(string element, string @ref, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(@ref))
        {
            return "Error: 'ref' is required for click action. Use 'snapshot' first to get element refs like 'e1', 'e2'.";
        }

        // Human-like delay before clicking (simulates mouse movement)
        await Task.Delay(Random.Shared.Next(200, 600), ct);

        var result = await _client.CallToolAsync(
            "browser_click",
            new Dictionary<string, object?>
            {
                ["element"] = string.IsNullOrEmpty(element) ? "element" : element,
                ["ref"] = @ref,
            },
            ct);

        return result.IsError ? $"Click failed: {result.Content}" : $"Clicked {element} [{@ref}]\n\n{result.Content}";
    }

    private async Task<string> TypeAsync(string element, string @ref, string text, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(@ref))
        {
            return "Error: 'ref' is required for type action. Use 'snapshot' first to get element refs like 'e1', 'e2'.";
        }

        if (string.IsNullOrEmpty(text))
        {
            return "Error: 'text' is required for type action.";
        }

        // Human-like delay before typing (simulates focusing on input)
        await Task.Delay(Random.Shared.Next(300, 700), ct);

        var result = await _client.CallToolAsync(
            "browser_type",
            new Dictionary<string, object?>
            {
                ["element"] = string.IsNullOrEmpty(element) ? "input" : element,
                ["ref"] = @ref,
                ["text"] = text,
                // Ask Playwright MCP to type slowly like a human
                ["slowly"] = true,
            },
            ct);

        return result.IsError ? $"Type failed: {result.Content}" : $"Typed into {element} [{@ref}]\n\n{result.Content}";
    }

    private async Task<string> HoverAsync(string element, string @ref, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(@ref))
        {
            return "Error: 'ref' is required for hover action. Use 'snapshot' first to get element refs.";
        }

        // Human-like delay for mouse movement to element
        await Task.Delay(Random.Shared.Next(150, 400), ct);

        var result = await _client.CallToolAsync(
            "browser_hover",
            new Dictionary<string, object?>
            {
                ["element"] = string.IsNullOrEmpty(element) ? "element" : element,
                ["ref"] = @ref,
            },
            ct);

        return result.IsError ? $"Hover failed: {result.Content}" : $"Hovered over {element} [{@ref}]\n\n{result.Content}";
    }

    private async Task<string> EvaluateAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "Error: 'code' is required for evaluate action";
        }

        var result = await _client.CallToolAsync(
            "browser_evaluate",
            new Dictionary<string, object?> { ["function"] = code },
            ct);

        return result.IsError ? $"Evaluate failed: {result.Content}" : result.Content;
    }

    private async Task<string> CallRawToolAsync(string toolName, PlaywrightArgs args, CancellationToken ct)
    {
        // Validate tool name before calling MCP server
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return "Error: Tool name cannot be empty. Valid actions: navigate, snapshot, screenshot, click, type, hover, evaluate, detect_elements, extract_text, suggest_action, validate, list_tools";
        }

        // Check if the tool exists on the MCP server
        if (_availableTools != null && !_availableTools.Any(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
        {
            var availableNames = _availableTools.Select(t => t.Name).ToList();
            var suggestion = availableNames.FirstOrDefault(n => n.Contains(toolName, StringComparison.OrdinalIgnoreCase));
            var suggestionMsg = suggestion != null ? $" Did you mean '{suggestion}'?" : "";
            return $"Error: Unknown action '{toolName}'.{suggestionMsg} Valid Playwright actions: navigate, snapshot, screenshot, click, type, hover, evaluate. Raw MCP tools: {string.Join(", ", availableNames)}";
        }

        // Build arguments dictionary from the PlaywrightArgs
        var arguments = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(args.Url)) arguments["url"] = args.Url;
        if (!string.IsNullOrEmpty(args.Element)) arguments["element"] = args.Element;
        if (!string.IsNullOrEmpty(args.Ref)) arguments["ref"] = args.Ref;
        if (!string.IsNullOrEmpty(args.Text)) arguments["text"] = args.Text;
        if (!string.IsNullOrEmpty(args.Code)) arguments["function"] = args.Code;

        var result = await _client.CallToolAsync(toolName, arguments, ct);
        return result.IsError ? $"Tool call failed: {result.Content}" : SanitizeOutput(result.Content);
    }

    /// <summary>
    /// Sanitizes output to remove binary data, excessive whitespace, or corrupted content.
    /// </summary>
    private static string SanitizeOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return output;
        }

        // Check for binary/corrupted data (high ratio of non-printable characters)
        int nonPrintableCount = output.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsPunctuation(c) && c != '\n' && c != '\r' && c != '\t');
        if (nonPrintableCount > output.Length * 0.3 && output.Length > 100)
        {
            // More than 30% non-printable characters - likely binary or corrupted
            return $"[Binary/corrupted data detected - {output.Length} bytes. Content may be an image or encoded data.]";
        }

        // Limit output length to prevent overwhelming the LLM context
        const int maxLength = 50000;
        if (output.Length > maxLength)
        {
            return output[..maxLength] + $"\n\n[Output truncated - showing first {maxLength} of {output.Length} characters]";
        }

        // Clean up excessive whitespace
        output = System.Text.RegularExpressions.Regex.Replace(output, @"[ \t]{20,}", "    ");
        output = System.Text.RegularExpressions.Regex.Replace(output, @"(\r?\n){5,}", "\n\n\n");

        return output;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        _visionService.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Arguments for Playwright tool invocation.
/// </summary>
internal record PlaywrightArgs
{
    /// <summary>
    /// Gets or sets the action to perform.
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>
    /// Gets or sets the URL for navigation.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Gets or sets the human-readable element description.
    /// </summary>
    public string? Element { get; set; }

    /// <summary>
    /// Gets or sets the element reference from the snapshot (e.g., "e1", "e2").
    /// </summary>
    public string? Ref { get; set; }

    /// <summary>
    /// Gets or sets the text to type.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the JavaScript code to evaluate.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets whether to capture the full scrollable page for screenshots.
    /// </summary>
    public bool FullPage { get; set; }

    /// <summary>
    /// Gets or sets the user's goal for action suggestion.
    /// </summary>
    public string? Goal { get; set; }

    /// <summary>
    /// Gets or sets the expectations to validate against the page state.
    /// </summary>
    public string[]? Expectations { get; set; }

    /// <summary>
    /// Validates that a parameter value is not a placeholder description.
    /// LLMs sometimes output instructions like "URL of the result" instead of actual URLs.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name for error messages.</param>
    /// <returns>A validation result with error message if the value is a placeholder.</returns>
    public static (bool IsValid, string? Error) ValidateNotPlaceholder(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (true, null); // Empty is valid (will be caught by specific action validation)
        }

        // Common placeholder patterns that LLMs generate instead of actual values
        string lower = value.ToLowerInvariant().Trim();

        // Check for instruction-like text
        if (lower.StartsWith("url of") ||
            lower.StartsWith("ref of") ||
            lower.StartsWith("the ") ||
            lower.StartsWith("a ") ||
            lower.StartsWith("an ") ||
            lower.Contains(" of the ") ||
            lower.Contains("from step") ||
            lower.Contains("e.g.,") ||
            lower.Contains("for example") ||
            lower.Contains("placeholder") ||
            lower.Contains("insert ") ||
            lower.Contains("your ") ||
            lower.Contains("specify ") ||
            (lower.Contains("input") && lower.Contains("box")) ||
            (lower.Contains("search") && lower.Contains("button") && !lower.StartsWith("http")))
        {
            return (false, $"'{paramName}' appears to be a placeholder description, not an actual value. Got: '{value}'. Please provide the actual {paramName}.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates all parameters in this args object.
    /// </summary>
    /// <returns>A validation result with error message if any parameter is invalid.</returns>
    public (bool IsValid, string? Error) Validate()
    {
        // Validate action is not empty
        if (string.IsNullOrWhiteSpace(Action))
        {
            return (false, "Action is required. Valid actions: navigate, snapshot, screenshot, click, type, hover, evaluate, detect_elements, extract_text, suggest_action, validate, list_tools");
        }

        (bool IsValid, string? Error) urlCheck = ValidateNotPlaceholder(Url, "url");
        if (!urlCheck.IsValid)
        {
            return urlCheck;
        }

        (bool IsValid, string? Error) refCheck = ValidateNotPlaceholder(Ref, "ref");
        if (!refCheck.IsValid)
        {
            return refCheck;
        }

        (bool IsValid, string? Error) elementCheck = ValidateNotPlaceholder(Element, "element");
        if (!elementCheck.IsValid)
        {
            return elementCheck;
        }

        // For 'ref', also validate it looks like an actual element reference (e.g., "e1", "e15")
        // Valid refs are typically "e" followed by digits, or similar short patterns
        if (!string.IsNullOrWhiteSpace(Ref) && (Ref.Length > 20 || Ref.Contains(' ')))
        {
            return (false, $"'ref' should be a short element reference like 'e1' or 'e15', not a description. Got: '{Ref}'");
        }

        return (true, null);
    }
}
