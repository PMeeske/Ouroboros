// <copyright file="HybridModelRouter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Routing;

/// <summary>
/// Intelligent model router that selects models based on task type detection.
/// Routes simple queries to lightweight models and complex tasks to specialized models.
/// Implements IChatCompletionModel for seamless integration with existing code.
/// </summary>
public sealed class HybridModelRouter : IChatCompletionModel
{
    private readonly HybridRoutingConfig _config;
    private readonly Dictionary<TaskType, IChatCompletionModel> _taskModels;

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridModelRouter"/> class.
    /// </summary>
    /// <param name="config">Routing configuration with model assignments.</param>
    public HybridModelRouter(HybridRoutingConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Build task-to-model mapping
        _taskModels = new Dictionary<TaskType, IChatCompletionModel>
        {
            [TaskType.Simple] = config.DefaultModel,
            [TaskType.Unknown] = config.DefaultModel,
            [TaskType.Reasoning] = config.ReasoningModel ?? config.DefaultModel,
            [TaskType.Planning] = config.PlanningModel ?? config.DefaultModel,
            [TaskType.Coding] = config.CodingModel ?? config.DefaultModel,
        };
    }

    /// <summary>
    /// Gets the detection strategy being used.
    /// </summary>
    public TaskDetectionStrategy DetectionStrategy => _config.DetectionStrategy;

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        // Detect task type
        TaskType taskType = TaskDetector.DetectTaskType(prompt, _config.DetectionStrategy);

        // Select appropriate model
        IChatCompletionModel selectedModel = _taskModels[taskType];

        Console.WriteLine($"[HybridModelRouter] Detected task: {taskType}, routing to appropriate model");

        try
        {
            // Attempt generation with selected model
            string result = await selectedModel.GenerateTextAsync(prompt, ct);
            return result;
        }
        catch (Exception ex)
        {
            // If selected model fails and we have a fallback, try it
            if (_config.FallbackModel != null && selectedModel != _config.FallbackModel)
            {
                Console.WriteLine($"[HybridModelRouter] Primary model failed ({ex.Message}), trying fallback");
                try
                {
                    return await _config.FallbackModel.GenerateTextAsync(prompt, ct);
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"[HybridModelRouter] Fallback also failed: {fallbackEx.Message}");
                }
            }

            // If no fallback or fallback also failed, return error message
            return $"[hybrid-router-error] Task: {taskType}, Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Detects the task type for a given prompt without executing it.
    /// Useful for debugging or logging routing decisions.
    /// </summary>
    /// <param name="prompt">The prompt to analyze.</param>
    /// <returns>The detected task type.</returns>
    public TaskType DetectTaskTypeForPrompt(string prompt)
    {
        return TaskDetector.DetectTaskType(prompt, _config.DetectionStrategy);
    }

    /// <summary>
    /// Gets the model that would be selected for a given task type.
    /// </summary>
    /// <param name="taskType">The task type.</param>
    /// <returns>The model assigned to handle this task type.</returns>
    public IChatCompletionModel GetModelForTaskType(TaskType taskType)
    {
        return _taskModels.TryGetValue(taskType, out IChatCompletionModel? model)
            ? model
            : _config.DefaultModel;
    }
}
