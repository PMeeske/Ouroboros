// <copyright file="TapoVisionModelConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Configuration for vision models used with Tapo camera embodiment.
/// Provides defaults for strong vision capabilities from the multi-model swarm.
/// </summary>
public sealed record TapoVisionModelConfig
{
    /// <summary>
    /// Default vision model for Tapo camera analysis (LLaVA 13B for strong vision capabilities).
    /// </summary>
    public const string DefaultVisionModel = "llava:13b";

    /// <summary>
    /// Alternative lightweight vision model for faster processing.
    /// </summary>
    public const string LightweightVisionModel = "llava:7b";

    /// <summary>
    /// High-quality vision model for detailed analysis.
    /// </summary>
    public const string HighQualityVisionModel = "llava:34b";

    /// <summary>
    /// Gets the vision model to use for camera analysis.
    /// </summary>
    public string VisionModel { get; init; } = DefaultVisionModel;

    /// <summary>
    /// Gets the Ollama endpoint for vision model inference.
    /// </summary>
    public string OllamaEndpoint { get; init; } = "http://localhost:11434";

    /// <summary>
    /// Gets the timeout for vision model requests.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// Gets whether to enable detailed object detection.
    /// </summary>
    public bool EnableObjectDetection { get; init; } = true;

    /// <summary>
    /// Gets whether to enable face detection and emotion analysis.
    /// </summary>
    public bool EnableFaceDetection { get; init; } = true;

    /// <summary>
    /// Gets whether to enable scene classification.
    /// </summary>
    public bool EnableSceneClassification { get; init; } = true;

    /// <summary>
    /// Gets the maximum number of objects to detect per frame.
    /// </summary>
    public int MaxObjectsPerFrame { get; init; } = 20;

    /// <summary>
    /// Gets the minimum confidence threshold for detections.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.5;

    /// <summary>
    /// Creates a default configuration with strong vision capabilities.
    /// Uses llava:13b for balanced speed and accuracy.
    /// </summary>
    /// <returns>A default vision model configuration.</returns>
    public static TapoVisionModelConfig CreateDefault() => new();

    /// <summary>
    /// Creates a lightweight configuration for faster processing.
    /// Uses llava:7b for reduced latency at the cost of some accuracy.
    /// </summary>
    /// <returns>A lightweight vision model configuration.</returns>
    public static TapoVisionModelConfig CreateLightweight() => new()
    {
        VisionModel = LightweightVisionModel,
        RequestTimeout = TimeSpan.FromSeconds(60),
        MaxObjectsPerFrame = 10
    };

    /// <summary>
    /// Creates a high-quality configuration for detailed analysis.
    /// Uses llava:34b for maximum accuracy but slower processing.
    /// </summary>
    /// <returns>A high-quality vision model configuration.</returns>
    public static TapoVisionModelConfig CreateHighQuality() => new()
    {
        VisionModel = HighQualityVisionModel,
        RequestTimeout = TimeSpan.FromSeconds(180),
        MaxObjectsPerFrame = 50,
        ConfidenceThreshold = 0.3
    };
}
