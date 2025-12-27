// <copyright file="VisionService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

/// <summary>
/// Vision service that bridges visual information (screen, camera, images) to AI understanding.
/// Supports multiple vision backends: Ollama (llava), OpenAI GPT-4V, local models.
/// </summary>
public class VisionService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly VisionConfig _config;
    private bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisionService"/> class.
    /// </summary>
    public VisionService(VisionConfig? config = null)
    {
        _config = config ?? new VisionConfig();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    /// <summary>
    /// Event fired when vision analysis is complete.
    /// </summary>
    public event Action<VisionResult>? OnVisionResult;

    /// <summary>
    /// Event fired when something interesting is detected.
    /// </summary>
    public event Action<string>? OnInterestingDetection;

    /// <summary>
    /// Initialize the vision service.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        try
        {
            // Test connection to vision model
            var testResult = await AnalyzeTextAsync("Hello, can you see?", ct);
            _isInitialized = !string.IsNullOrEmpty(testResult);
        }
        catch
        {
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Analyze an image file and describe what's in it.
    /// </summary>
    public async Task<VisionResult> AnalyzeImageAsync(string imagePath, string? prompt = null, CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
        {
            return VisionResult.Failure($"Image not found: {imagePath}");
        }

        try
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, ct);
            var base64Image = Convert.ToBase64String(imageBytes);
            var mimeType = GetMimeType(imagePath);

            return await AnalyzeBase64ImageAsync(base64Image, mimeType, prompt, ct);
        }
        catch (Exception ex)
        {
            return VisionResult.Failure($"Failed to read image: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyze a bitmap directly (e.g., from screen capture).
    /// </summary>
    public async Task<VisionResult> AnalyzeBitmapAsync(Bitmap bitmap, string? prompt = null, CancellationToken ct = default)
    {
        try
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            var base64Image = Convert.ToBase64String(ms.ToArray());

            return await AnalyzeBase64ImageAsync(base64Image, "image/png", prompt, ct);
        }
        catch (Exception ex)
        {
            return VisionResult.Failure($"Failed to process bitmap: {ex.Message}");
        }
    }

    /// <summary>
    /// Capture the screen and analyze it.
    /// </summary>
    public async Task<VisionResult> CaptureAndAnalyzeScreenAsync(string? prompt = null, Rectangle? region = null, CancellationToken ct = default)
    {
#if NET10_0_OR_GREATER_WINDOWS
        try
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            var captureBounds = region ?? screen;

            using var bitmap = new Bitmap(captureBounds.Width, captureBounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(captureBounds.X, captureBounds.Y, 0, 0, captureBounds.Size);

            var defaultPrompt = prompt ?? "Describe what you see on this screen. What application is open? What is the user doing? Are there any notable elements?";
            return await AnalyzeBitmapAsync(bitmap, defaultPrompt, ct);
        }
        catch (Exception ex)
        {
            return VisionResult.Failure($"Screen capture failed: {ex.Message}");
        }
#else
        await Task.CompletedTask; // Suppress async warning
        return VisionResult.Failure("Screen capture is only supported on Windows");
#endif
    }

    /// <summary>
    /// Analyze what changed between two images.
    /// </summary>
    public async Task<VisionResult> AnalyzeChangeAsync(string beforeImagePath, string afterImagePath, CancellationToken ct = default)
    {
        // For Ollama, we'll describe each image and compare
        var beforeResult = await AnalyzeImageAsync(beforeImagePath, "Describe this image in detail.", ct);
        var afterResult = await AnalyzeImageAsync(afterImagePath, "Describe this image in detail.", ct);

        if (!beforeResult.Success || !afterResult.Success)
        {
            return VisionResult.Failure("Failed to analyze one or both images.");
        }

        // Use text model to compare descriptions
        var comparisonPrompt = $@"Compare these two scene descriptions and identify what changed:

BEFORE:
{beforeResult.Description}

AFTER:
{afterResult.Description}

What are the key differences?";

        var comparison = await AnalyzeTextAsync(comparisonPrompt, ct);

        return new VisionResult
        {
            Success = true,
            Description = comparison,
            Timestamp = DateTime.Now,
            AnalysisType = "change_detection",
        };
    }

    /// <summary>
    /// Detect specific objects or text in an image.
    /// </summary>
    public async Task<VisionResult> DetectInImageAsync(string imagePath, string[] targetObjects, CancellationToken ct = default)
    {
        var objectList = string.Join(", ", targetObjects);
        var prompt = $"Look at this image and tell me if you can see any of these: {objectList}. For each one found, describe its location and state.";

        return await AnalyzeImageAsync(imagePath, prompt, ct);
    }

    /// <summary>
    /// Read text (OCR) from an image.
    /// </summary>
    public async Task<VisionResult> ReadTextFromImageAsync(string imagePath, CancellationToken ct = default)
    {
        var prompt = "Read and transcribe all visible text in this image. Include any text in windows, dialogs, buttons, menus, or documents.";
        return await AnalyzeImageAsync(imagePath, prompt, ct);
    }

    /// <summary>
    /// Describe the user's current activity based on screen capture.
    /// </summary>
    public async Task<VisionResult> DescribeUserActivityAsync(CancellationToken ct = default)
    {
        var prompt = @"Analyze this screen and describe:
1. What application is the user using?
2. What task are they working on?
3. What is the current state of their work?
4. Any suggestions or observations that might be helpful?";

        return await CaptureAndAnalyzeScreenAsync(prompt, ct: ct);
    }

    /// <summary>
    /// Watch for specific conditions on screen (polling).
    /// </summary>
    public async IAsyncEnumerable<VisionResult> WatchForConditionAsync(
        string condition,
        TimeSpan interval,
        TimeSpan duration,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var startTime = DateTime.Now;
        var prompt = $"Look at this screen and determine: {condition}. Answer YES or NO first, then explain briefly.";

        while (!ct.IsCancellationRequested && (DateTime.Now - startTime) < duration)
        {
            var result = await CaptureAndAnalyzeScreenAsync(prompt, ct: ct);

            if (result.Success && result.Description?.Contains("YES", StringComparison.OrdinalIgnoreCase) == true)
            {
                OnInterestingDetection?.Invoke($"Condition met: {condition}");
                yield return result;
            }

            await Task.Delay(interval, ct);
        }
    }

    private async Task<VisionResult> AnalyzeBase64ImageAsync(string base64Image, string mimeType, string? prompt, CancellationToken ct)
    {
        prompt ??= "Describe what you see in this image in detail.";

        try
        {
            return _config.Backend switch
            {
                VisionBackend.Ollama => await AnalyzeWithOllamaAsync(base64Image, prompt, ct),
                VisionBackend.OpenAI => await AnalyzeWithOpenAIAsync(base64Image, mimeType, prompt, ct),
                _ => await AnalyzeWithOllamaAsync(base64Image, prompt, ct),
            };
        }
        catch (Exception ex)
        {
            return VisionResult.Failure($"Vision analysis failed: {ex.Message}");
        }
    }

    private async Task<VisionResult> AnalyzeWithOllamaAsync(string base64Image, string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _config.OllamaVisionModel,
            prompt = prompt,
            images = new[] { base64Image },
            stream = false,
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_config.OllamaEndpoint}/api/generate", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return VisionResult.Failure($"Ollama error: {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var description = responseObj.GetProperty("response").GetString() ?? "";

        var result = new VisionResult
        {
            Success = true,
            Description = description,
            Timestamp = DateTime.Now,
            AnalysisType = "image_description",
            Model = _config.OllamaVisionModel,
        };

        OnVisionResult?.Invoke(result);
        return result;
    }

    private async Task<VisionResult> AnalyzeWithOpenAIAsync(string base64Image, string mimeType, string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _config.OpenAIVisionModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } },
                    },
                },
            },
            max_tokens = 1000,
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.OpenAIApiKey}");

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return VisionResult.Failure($"OpenAI error: {error}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        var description = responseObj
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        var result = new VisionResult
        {
            Success = true,
            Description = description,
            Timestamp = DateTime.Now,
            AnalysisType = "image_description",
            Model = _config.OpenAIVisionModel,
        };

        OnVisionResult?.Invoke(result);
        return result;
    }

    private async Task<string> AnalyzeTextAsync(string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _config.OllamaVisionModel.Replace("llava", "llama3.2"),
            prompt = prompt,
            stream = false,
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_config.OllamaEndpoint}/api/generate", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            return "";
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        return responseObj.GetProperty("response").GetString() ?? "";
    }

    /// <summary>
    /// Analyzes a screenshot to detect UI elements, buttons, links, and interactive components.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot image.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Structured detection result with identified elements.</returns>
    public async Task<ScreenshotDetectionResult> DetectScreenElementsAsync(string imagePath, CancellationToken ct = default)
    {
        var prompt = @"Analyze this screenshot and identify all interactive UI elements. For each element, provide:
1. Type (button, link, input field, dropdown, checkbox, menu item, tab, icon)
2. Label or text content
3. Approximate position (top-left, top-center, top-right, middle-left, center, middle-right, bottom-left, bottom-center, bottom-right)
4. Visual state (enabled, disabled, focused, selected, hovered)

Format your response as a list with one element per line:
[TYPE] ""LABEL"" at POSITION - STATE

Also identify:
- The page title or header
- Any error messages or alerts
- Forms and their fields
- Navigation elements";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);
        if (!result.Success)
        {
            return new ScreenshotDetectionResult
            {
                Success = false,
                ErrorMessage = result.ErrorMessage,
            };
        }

        return ParseDetectionResult(result.Description ?? "");
    }

    /// <summary>
    /// Extracts all readable text from a screenshot using OCR.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot image.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<TextExtractionResult> ExtractTextFromScreenshotAsync(string imagePath, CancellationToken ct = default)
    {
        var prompt = @"Extract ALL text visible in this screenshot. Include:
- Page titles and headers
- Button labels
- Menu items
- Form labels and input placeholders
- Error messages
- Status text
- Any other readable text

Organize the text by region:
HEADER:
NAVIGATION:
MAIN CONTENT:
SIDEBAR:
FOOTER:
DIALOGS/POPUPS:";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);

        return new TextExtractionResult
        {
            Success = result.Success,
            ExtractedText = result.Description ?? "",
            ErrorMessage = result.ErrorMessage,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Determines what action the user should take based on the current screen state.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot.</param>
    /// <param name="goal">The user's goal or what they're trying to accomplish.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ActionSuggestionResult> SuggestNextActionAsync(string imagePath, string goal, CancellationToken ct = default)
    {
        var prompt = $@"The user's goal is: {goal}

Looking at this screenshot, suggest the next action to take. Provide:
1. RECOMMENDED ACTION: What to click, type, or do
2. TARGET ELEMENT: The specific button, link, or field to interact with
3. REASONING: Why this action helps achieve the goal
4. ALTERNATIVES: Other possible actions if the main one fails
5. WARNINGS: Any potential issues or confirmations to expect";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);

        return new ActionSuggestionResult
        {
            Success = result.Success,
            Suggestion = result.Description ?? "",
            ErrorMessage = result.ErrorMessage,
            Goal = goal,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Compares two screenshots to detect what changed.
    /// </summary>
    /// <param name="beforePath">Path to the before screenshot.</param>
    /// <param name="afterPath">Path to the after screenshot.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ScreenshotDiffResult> CompareScreenshotsAsync(string beforePath, string afterPath, CancellationToken ct = default)
    {
        // Analyze both screenshots
        var beforePrompt = "Describe the key elements, text, and state of this screenshot in detail.";
        var afterPrompt = "Describe the key elements, text, and state of this screenshot in detail.";

        var beforeResult = await AnalyzeImageAsync(beforePath, beforePrompt, ct);
        var afterResult = await AnalyzeImageAsync(afterPath, afterPrompt, ct);

        if (!beforeResult.Success || !afterResult.Success)
        {
            return new ScreenshotDiffResult
            {
                Success = false,
                ErrorMessage = "Failed to analyze one or both screenshots",
            };
        }

        // Compare the descriptions
        var comparePrompt = $@"Compare these two screenshot descriptions and identify all changes:

BEFORE:
{beforeResult.Description}

AFTER:
{afterResult.Description}

List the changes:
1. NEW ELEMENTS: What appeared
2. REMOVED ELEMENTS: What disappeared
3. CHANGED ELEMENTS: What was modified
4. TEXT CHANGES: Any text that changed
5. STATE CHANGES: Elements that changed state (enabled/disabled, selected/unselected, etc.)
6. OVERALL CHANGE: Brief summary of what happened";

        var diffResult = await AnalyzeTextAsync(comparePrompt, ct);

        return new ScreenshotDiffResult
        {
            Success = true,
            BeforeDescription = beforeResult.Description,
            AfterDescription = afterResult.Description,
            Changes = diffResult,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Validates that a page matches expected content or state.
    /// </summary>
    /// <param name="imagePath">Path to the screenshot.</param>
    /// <param name="expectations">List of expectations to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ValidationResult> ValidateScreenStateAsync(string imagePath, string[] expectations, CancellationToken ct = default)
    {
        var expectationList = string.Join("\n- ", expectations);
        var prompt = $@"Validate this screenshot against these expectations:
- {expectationList}

For each expectation, respond with:
[PASS] or [FAIL] - Explanation

Then provide an overall summary.";

        var result = await AnalyzeImageAsync(imagePath, prompt, ct);

        var passed = result.Description?.Contains("[PASS]", StringComparison.OrdinalIgnoreCase) == true;
        var failed = result.Description?.Contains("[FAIL]", StringComparison.OrdinalIgnoreCase) == true;

        return new ValidationResult
        {
            Success = result.Success,
            AllPassed = passed && !failed,
            Details = result.Description ?? "",
            Expectations = expectations,
            Timestamp = DateTime.UtcNow,
        };
    }

    private static ScreenshotDetectionResult ParseDetectionResult(string description)
    {
        var result = new ScreenshotDetectionResult
        {
            Success = true,
            RawDescription = description,
            Elements = new List<DetectedElement>(),
        };

        // Parse structured elements from description
        var lines = description.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[") && trimmed.Contains("]"))
            {
                var endBracket = trimmed.IndexOf(']');
                var elementType = trimmed[1..endBracket];

                // Extract label if quoted
                var labelStart = trimmed.IndexOf('"');
                var labelEnd = trimmed.LastIndexOf('"');
                var label = labelStart >= 0 && labelEnd > labelStart
                    ? trimmed[(labelStart + 1)..labelEnd]
                    : "";

                result.Elements.Add(new DetectedElement
                {
                    Type = elementType,
                    Label = label,
                    RawLine = trimmed,
                });
            }
        }

        return result;
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/png",
        };
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Result of vision analysis.
/// </summary>
public record VisionResult
{
    public bool Success { get; init; }
    public string? Description { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; }
    public string? AnalysisType { get; init; }
    public string? Model { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    public static VisionResult Failure(string message) => new()
    {
        Success = false,
        ErrorMessage = message,
        Timestamp = DateTime.Now,
    };
}

/// <summary>
/// Configuration for the vision service.
/// </summary>
public class VisionConfig
{
    public VisionBackend Backend { get; set; } = VisionBackend.Ollama;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaVisionModel { get; set; } = "llava:latest";
    public string? OpenAIApiKey { get; set; }
    public string OpenAIVisionModel { get; set; } = "gpt-4-vision-preview";
}

/// <summary>
/// Supported vision backends.
/// </summary>
public enum VisionBackend
{
    Ollama,
    OpenAI,
}

/// <summary>
/// Result of screenshot element detection.
/// </summary>
public record ScreenshotDetectionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawDescription { get; init; }
    public List<DetectedElement> Elements { get; init; } = new();
}

/// <summary>
/// A detected UI element in a screenshot.
/// </summary>
public record DetectedElement
{
    public string Type { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Position { get; init; }
    public string? State { get; init; }
    public string? RawLine { get; init; }
}

/// <summary>
/// Result of text extraction from a screenshot.
/// </summary>
public record TextExtractionResult
{
    public bool Success { get; init; }
    public string ExtractedText { get; init; } = "";
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Result of action suggestion based on screenshot analysis.
/// </summary>
public record ActionSuggestionResult
{
    public bool Success { get; init; }
    public string Suggestion { get; init; } = "";
    public string? ErrorMessage { get; init; }
    public string? Goal { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Result of comparing two screenshots.
/// </summary>
public record ScreenshotDiffResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? BeforeDescription { get; init; }
    public string? AfterDescription { get; init; }
    public string? Changes { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Result of validating screen state against expectations.
/// </summary>
public record ValidationResult
{
    public bool Success { get; init; }
    public bool AllPassed { get; init; }
    public string Details { get; init; } = "";
    public string[] Expectations { get; init; } = Array.Empty<string>();
    public DateTime Timestamp { get; init; }
}
