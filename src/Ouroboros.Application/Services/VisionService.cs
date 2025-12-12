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
