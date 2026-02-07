// <copyright file="OllamaVisionModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Core.Monads;

/// <summary>
/// Ollama-based implementation of <see cref="IVisionModel"/> that routes vision requests
/// to multimodal models like qwen3-vl, llava, or minicpm-v via the Ollama API.
/// Integrates with the multi-model swarm for strong visual understanding.
/// </summary>
public sealed class OllamaVisionModel : IVisionModel
{
    /// <summary>
    /// Default vision model — Qwen3-VL 235B cloud for strongest visual understanding.
    /// </summary>
    public const string DefaultModel = "qwen3-vl:235b-cloud";

    /// <summary>
    /// Lightweight fallback vision model for faster processing.
    /// </summary>
    public const string LightweightModel = "llava:7b";

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly TimeSpan _timeout;
    private readonly ILogger<OllamaVisionModel>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaVisionModel"/> class.
    /// </summary>
    /// <param name="endpoint">Ollama API endpoint (e.g. http://localhost:11434).</param>
    /// <param name="model">Vision model name (e.g. qwen3-vl:235b-cloud).</param>
    /// <param name="timeout">Request timeout. Defaults to 120 seconds.</param>
    /// <param name="logger">Optional logger.</param>
    public OllamaVisionModel(
        string endpoint = "http://localhost:11434",
        string model = DefaultModel,
        TimeSpan? timeout = null,
        ILogger<OllamaVisionModel>? logger = null)
    {
        _endpoint = endpoint.TrimEnd('/');
        _model = model;
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_endpoint, UriKind.Absolute),
            Timeout = _timeout,
        };
    }

    /// <inheritdoc/>
    public string ModelName => _model;

    /// <inheritdoc/>
    public bool SupportsStreaming => false;

    /// <inheritdoc/>
    public async Task<Result<VisionAnalysisResult, string>> AnalyzeImageAsync(
        byte[] imageData,
        string format,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new VisionAnalysisOptions();
        string prompt = BuildAnalysisPrompt(options);
        string base64Image = Convert.ToBase64String(imageData);

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            string response = await CallOllamaVisionAsync(base64Image, prompt, ct);
            sw.Stop();

            _logger?.LogDebug("Vision analysis completed in {ElapsedMs}ms using {Model}", sw.ElapsedMilliseconds, _model);

            VisionAnalysisResult result = ParseAnalysisResponse(response, sw.ElapsedMilliseconds, options);
            return Result<VisionAnalysisResult, string>.Success(result);
        }
        catch (TaskCanceledException)
        {
            return Result<VisionAnalysisResult, string>.Failure($"Vision analysis timed out after {_timeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Vision analysis failed with {Model}", _model);
            return Result<VisionAnalysisResult, string>.Failure($"Vision analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<VisionAnalysisResult, string>> AnalyzeImageFileAsync(
        string filePath,
        VisionAnalysisOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<VisionAnalysisResult, string>.Failure($"File not found: {filePath}");
        }

        byte[] imageData = await File.ReadAllBytesAsync(filePath, ct);
        string extension = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        string imageFormat = extension switch
        {
            "jpg" or "jpeg" => "jpeg",
            "png" => "png",
            "bmp" => "bmp",
            "gif" => "gif",
            "webp" => "webp",
            _ => "jpeg",
        };

        return await AnalyzeImageAsync(imageData, imageFormat, options, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> AnswerQuestionAsync(
        byte[] imageData,
        string format,
        string question,
        CancellationToken ct = default)
    {
        string base64Image = Convert.ToBase64String(imageData);

        try
        {
            string response = await CallOllamaVisionAsync(base64Image, question, ct);
            return Result<string, string>.Success(response);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Vision Q&A failed with {Model}", _model);
            return Result<string, string>.Failure($"Vision Q&A failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DetectedObject>, string>> DetectObjectsAsync(
        byte[] imageData,
        string format,
        int maxObjects = 20,
        CancellationToken ct = default)
    {
        string base64Image = Convert.ToBase64String(imageData);
        string prompt = $"List all objects you can see in this image (up to {maxObjects}). " +
                        "For each object, provide: label, confidence (0-1), and approximate bounding box " +
                        "as x,y,width,height (0-1 normalized). Format as JSON array: " +
                        "[{\"label\":\"...\",\"confidence\":0.9,\"x\":0.1,\"y\":0.2,\"w\":0.3,\"h\":0.4}]";

        try
        {
            string response = await CallOllamaVisionAsync(base64Image, prompt, ct);
            List<DetectedObject> objects = ParseDetectedObjects(response, maxObjects);
            return Result<IReadOnlyList<DetectedObject>, string>.Success(objects);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Object detection failed with {Model}", _model);
            return Result<IReadOnlyList<DetectedObject>, string>.Failure($"Object detection failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<DetectedFace>, string>> DetectFacesAsync(
        byte[] imageData,
        string format,
        bool analyzeEmotion = true,
        CancellationToken ct = default)
    {
        string base64Image = Convert.ToBase64String(imageData);
        string emotionPart = analyzeEmotion ? " and their emotion (happy, sad, neutral, surprised, angry, fearful, disgusted)" : "";
        string prompt = $"Detect all human faces in this image. For each face, describe their approximate location" +
                        $"{emotionPart} and estimated age. Format as JSON array: " +
                        "[{\"emotion\":\"happy\",\"age\":30,\"x\":0.1,\"y\":0.2,\"w\":0.3,\"h\":0.4}]";

        try
        {
            string response = await CallOllamaVisionAsync(base64Image, prompt, ct);
            List<DetectedFace> faces = ParseDetectedFaces(response, analyzeEmotion);
            return Result<IReadOnlyList<DetectedFace>, string>.Success(faces);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Face detection failed with {Model}", _model);
            return Result<IReadOnlyList<DetectedFace>, string>.Failure($"Face detection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests connectivity to the Ollama vision model.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the model is available.</returns>
    public async Task<bool> TestConnectivityAsync(CancellationToken ct = default)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/tags", ct);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string json = await response.Content.ReadAsStringAsync(ct);
            return json.Contains(_model, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> CallOllamaVisionAsync(string base64Image, string prompt, CancellationToken ct)
    {
        object requestBody = new
        {
            model = _model,
            prompt = prompt,
            images = new[] { base64Image },
            stream = false,
        };

        StringContent content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await _httpClient.PostAsync("/api/generate", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Ollama vision request failed ({response.StatusCode}): {error}");
        }

        string responseJson = await response.Content.ReadAsStringAsync(ct);
        JsonElement responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);

        return responseObj.GetProperty("response").GetString() ?? string.Empty;
    }

    private static string BuildAnalysisPrompt(VisionAnalysisOptions options)
    {
        StringBuilder sb = new StringBuilder();

        if (options.IncludeDescription)
        {
            sb.Append("Describe what you see in this image in detail. ");
        }

        if (options.DetectObjects)
        {
            sb.Append($"Identify and list all distinct objects (up to {options.MaxObjects}). ");
        }

        if (options.DetectFaces)
        {
            sb.Append("Note any human faces, their approximate emotions, and estimated ages. ");
        }

        if (options.ClassifyScene)
        {
            sb.Append("Classify the scene type (indoor, outdoor, office, nature, street, etc.). ");
        }

        if (options.ExtractText)
        {
            sb.Append("Extract any visible text (OCR). ");
        }

        if (options.AnalyzeColors)
        {
            sb.Append("Note the dominant colors in the scene. ");
        }

        if (sb.Length == 0)
        {
            sb.Append("Describe what you see in this image.");
        }

        return sb.ToString().Trim();
    }

    private static VisionAnalysisResult ParseAnalysisResponse(
        string response,
        long processingTimeMs,
        VisionAnalysisOptions options)
    {
        // Parse structured elements from the free-text response
        List<DetectedObject> objects = options.DetectObjects
            ? ParseDetectedObjects(response, options.MaxObjects)
            : new List<DetectedObject>();

        List<DetectedFace> faces = options.DetectFaces
            ? ParseDetectedFaces(response, true)
            : new List<DetectedFace>();

        string? sceneType = options.ClassifyScene
            ? ExtractSceneType(response)
            : null;

        List<string>? colors = options.AnalyzeColors
            ? ExtractColors(response)
            : null;

        string? text = options.ExtractText
            ? ExtractOcrText(response)
            : null;

        return new VisionAnalysisResult(
            Description: response,
            Objects: objects,
            Faces: faces,
            SceneType: sceneType,
            DominantColors: colors,
            Text: text,
            Confidence: 0.85, // Default confidence for VLM analysis
            ProcessingTimeMs: processingTimeMs);
    }

    private static List<DetectedObject> ParseDetectedObjects(string response, int maxObjects)
    {
        List<DetectedObject> objects = new List<DetectedObject>();

        // Try JSON parsing first
        try
        {
            int jsonStart = response.IndexOf('[');
            int jsonEnd = response.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonStr = response[jsonStart..(jsonEnd + 1)];
                JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                foreach (JsonElement item in jsonArray.EnumerateArray())
                {
                    string label = item.GetProperty("label").GetString() ?? "unknown";
                    double confidence = item.TryGetProperty("confidence", out JsonElement conf) ? conf.GetDouble() : 0.7;
                    double x = item.TryGetProperty("x", out JsonElement xVal) ? xVal.GetDouble() : 0;
                    double y = item.TryGetProperty("y", out JsonElement yVal) ? yVal.GetDouble() : 0;
                    double w = item.TryGetProperty("w", out JsonElement wVal) ? wVal.GetDouble() : 0;
                    double h = item.TryGetProperty("h", out JsonElement hVal) ? hVal.GetDouble() : 0;

                    objects.Add(new DetectedObject(label, confidence, (x, y, w, h), null));

                    if (objects.Count >= maxObjects)
                    {
                        break;
                    }
                }
            }
        }
        catch
        {
            // Fall back to extracting object mentions from natural language
            // VLMs often describe objects without strict JSON
        }

        return objects;
    }

    private static List<DetectedFace> ParseDetectedFaces(string response, bool analyzeEmotion)
    {
        List<DetectedFace> faces = new List<DetectedFace>();

        try
        {
            int jsonStart = response.IndexOf('[');
            int jsonEnd = response.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                string jsonStr = response[jsonStart..(jsonEnd + 1)];
                JsonElement jsonArray = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                foreach (JsonElement item in jsonArray.EnumerateArray())
                {
                    string? emotion = analyzeEmotion && item.TryGetProperty("emotion", out JsonElement emo)
                        ? emo.GetString()
                        : null;
                    int? age = item.TryGetProperty("age", out JsonElement ageVal)
                        ? ageVal.GetInt32()
                        : null;
                    double x = item.TryGetProperty("x", out JsonElement xVal) ? xVal.GetDouble() : 0;
                    double y = item.TryGetProperty("y", out JsonElement yVal) ? yVal.GetDouble() : 0;
                    double w = item.TryGetProperty("w", out JsonElement wVal) ? wVal.GetDouble() : 0;
                    double h = item.TryGetProperty("h", out JsonElement hVal) ? hVal.GetDouble() : 0;

                    faces.Add(new DetectedFace(
                        FaceId: Guid.NewGuid().ToString("N")[..8],
                        Confidence: 0.8,
                        BoundingBox: (x, y, w, h),
                        Emotion: emotion,
                        Age: age,
                        IsKnown: false,
                        PersonId: null));
                }
            }
        }
        catch
        {
            // Natural language responses may not contain JSON
        }

        return faces;
    }

    private static string? ExtractSceneType(string response)
    {
        string lower = response.ToLowerInvariant();
        string[] sceneTypes = ["outdoor", "indoor", "office", "nature", "street", "kitchen", "bedroom",
            "living room", "bathroom", "garden", "park", "beach", "forest", "city", "rural",
            "restaurant", "store", "classroom", "laboratory", "warehouse", "parking"];

        foreach (string scene in sceneTypes)
        {
            if (lower.Contains(scene))
            {
                return scene;
            }
        }

        return null;
    }

    private static List<string>? ExtractColors(string response)
    {
        string lower = response.ToLowerInvariant();
        string[] colorNames = ["red", "blue", "green", "yellow", "orange", "purple", "pink",
            "white", "black", "gray", "brown", "beige", "teal", "cyan", "magenta"];

        List<string> found = new List<string>();
        foreach (string color in colorNames)
        {
            if (lower.Contains(color))
            {
                found.Add(color);
            }
        }

        return found.Count > 0 ? found : null;
    }

    private static string? ExtractOcrText(string response)
    {
        // Look for quoted text or text following "text:" patterns
        string[] markers = ["reads:", "says:", "text:", "written:", "\""];
        foreach (string marker in markers)
        {
            int idx = response.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string after = response[(idx + marker.Length)..].Trim();
                int endIdx = after.IndexOfAny(['.', '\n', '"']);
                if (endIdx > 0)
                {
                    return after[..endIdx].Trim();
                }

                if (after.Length > 0 && after.Length < 200)
                {
                    return after.Trim();
                }
            }
        }

        return null;
    }
}
