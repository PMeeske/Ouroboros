namespace MonadicPipeline.Android.Services;

/// <summary>
/// Service for managing Ollama models
/// </summary>
public class ModelManager
{
    private readonly OllamaService _ollamaService;
    private readonly List<string> _recommendedModels;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModelManager"/> class.
    /// </summary>
    /// <param name="ollamaService">The Ollama service</param>
    public ModelManager(OllamaService ollamaService)
    {
        _ollamaService = ollamaService;
        _recommendedModels = new List<string>
        {
            "tinyllama",
            "phi",
            "qwen:0.5b",
            "gemma:2b",
            "llama2:7b"
        };
    }

    /// <summary>
    /// Get all available models
    /// </summary>
    /// <returns>List of models with metadata</returns>
    public async Task<List<ModelInfo>> GetAvailableModelsAsync()
    {
        try
        {
            var ollamaModels = await _ollamaService.ListModelsAsync();
            return ollamaModels.Select(m => new ModelInfo
            {
                Name = m.Name,
                Size = m.Size,
                FormattedSize = m.GetFormattedSize(),
                ModifiedAt = m.ModifiedAt,
                IsRecommended = _recommendedModels.Any(r => m.Name.Contains(r, StringComparison.OrdinalIgnoreCase))
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new ModelManagerException($"Failed to get models: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get recommended models for mobile devices
    /// </summary>
    /// <returns>List of recommended model names</returns>
    public List<RecommendedModel> GetRecommendedModels()
    {
        return new List<RecommendedModel>
        {
            new RecommendedModel
            {
                Name = "tinyllama",
                Parameters = "1.1B",
                EstimatedMemory = "~1.1 GB",
                Description = "Very fast, good for quick questions",
                UseCase = "Quick Q&A, simple tasks"
            },
            new RecommendedModel
            {
                Name = "phi",
                Parameters = "2.7B",
                EstimatedMemory = "~2.7 GB",
                Description = "Better reasoning, still efficient",
                UseCase = "Code help, explanations"
            },
            new RecommendedModel
            {
                Name = "qwen:0.5b",
                Parameters = "0.5B",
                EstimatedMemory = "~0.5 GB",
                Description = "Ultra lightweight",
                UseCase = "Basic queries, testing"
            },
            new RecommendedModel
            {
                Name = "gemma:2b",
                Parameters = "2B",
                EstimatedMemory = "~2 GB",
                Description = "Good balance of capability and efficiency",
                UseCase = "General purpose"
            }
        };
    }

    /// <summary>
    /// Pull a model from Ollama
    /// </summary>
    /// <param name="modelName">Name of the model</param>
    /// <param name="progressCallback">Progress callback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    public async Task PullModelAsync(
        string modelName,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _ollamaService.PullModelAsync(modelName, progressCallback, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ModelManagerException($"Failed to pull model: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Delete a model
    /// </summary>
    /// <param name="modelName">Name of the model to delete</param>
    /// <returns>Task representing the operation</returns>
    public async Task DeleteModelAsync(string modelName)
    {
        try
        {
            await _ollamaService.DeleteModelAsync(modelName);
        }
        catch (Exception ex)
        {
            throw new ModelManagerException($"Failed to delete model: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get the smallest available model
    /// </summary>
    /// <returns>Name of the smallest model, or null if none available</returns>
    public async Task<string?> GetSmallestAvailableModelAsync()
    {
        try
        {
            var models = await GetAvailableModelsAsync();
            return models.OrderBy(m => m.Size).FirstOrDefault()?.Name;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if a model is available
    /// </summary>
    /// <param name="modelName">Name of the model</param>
    /// <returns>True if model is available</returns>
    public async Task<bool> IsModelAvailableAsync(string modelName)
    {
        try
        {
            var models = await GetAvailableModelsAsync();
            return models.Any(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Model information
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the formatted size string
    /// </summary>
    public string FormattedSize { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the modified date
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a recommended model
    /// </summary>
    public bool IsRecommended { get; set; }
}

/// <summary>
/// Recommended model information
/// </summary>
public class RecommendedModel
{
    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter count
    /// </summary>
    public string Parameters { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated memory usage
    /// </summary>
    public string EstimatedMemory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recommended use case
    /// </summary>
    public string UseCase { get; set; } = string.Empty;
}

/// <summary>
/// Exception thrown by model manager
/// </summary>
public class ModelManagerException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelManagerException"/> class.
    /// </summary>
    /// <param name="message">The exception message</param>
    /// <param name="innerException">The inner exception</param>
    public ModelManagerException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
