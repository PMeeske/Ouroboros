#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Parallel Executor - Execute independent steps concurrently
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a step dependency graph for parallel execution.
/// </summary>
public sealed class StepDependencyGraph
{
    private readonly Dictionary<int, List<int>> _dependencies = new();
    private readonly List<PlanStep> _steps = new();

    public StepDependencyGraph(List<PlanStep> steps)
    {
        _steps = steps;
        AnalyzeDependencies();
    }

    /// <summary>
    /// Groups steps that can be executed in parallel.
    /// </summary>
    public List<List<int>> GetParallelGroups()
    {
        var groups = new List<List<int>>();
        var executed = new HashSet<int>();

        while (executed.Count < _steps.Count)
        {
            var group = new List<int>();

            for (int i = 0; i < _steps.Count; i++)
            {
                if (executed.Contains(i))
                    continue;

                // Can execute if all dependencies are satisfied
                if (!_dependencies.TryGetValue(i, out var deps) ||
                    deps.All(d => executed.Contains(d)))
                {
                    group.Add(i);
                }
            }

            if (group.Count == 0)
                break; // Circular dependency or error

            groups.Add(group);
            executed.UnionWith(group);
        }

        return groups;
    }

    private void AnalyzeDependencies()
    {
        // Analyze parameter dependencies between steps
        for (int i = 0; i < _steps.Count; i++)
        {
            var deps = new List<int>();
            var step = _steps[i];

            // Check if this step uses outputs from previous steps
            for (int j = 0; j < i; j++)
            {
                var prevStep = _steps[j];

                // Check if current step's parameters reference previous step's output
                if (HasDependency(step, prevStep))
                {
                    deps.Add(j);
                }
            }

            if (deps.Any())
            {
                _dependencies[i] = deps;
            }
        }
    }

    private bool HasDependency(PlanStep current, PlanStep previous)
    {
        // Check if current step references previous step's action or expected outcome
        var prevActionRef = $"${previous.Action}";
        var prevOutputRef = $"output_{previous.Action}";

        foreach (var param in current.Parameters.Values)
        {
            var paramStr = param?.ToString() ?? "";
            if (paramStr.Contains(prevActionRef) || paramStr.Contains(prevOutputRef))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Executes plan steps in parallel when they are independent.
/// </summary>
public sealed class ParallelExecutor
{
    private readonly ISafetyGuard _safety;
    private readonly Func<PlanStep, CancellationToken, Task<StepResult>> _executeStep;

    public ParallelExecutor(
        ISafetyGuard safety,
        Func<PlanStep, CancellationToken, Task<StepResult>> executeStep)
    {
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _executeStep = executeStep ?? throw new ArgumentNullException(nameof(executeStep));
    }

    /// <summary>
    /// Executes a plan with parallel execution of independent steps.
    /// </summary>
    public async Task<(List<StepResult> results, bool success, string output)> ExecuteParallelAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        var dependencyGraph = new StepDependencyGraph(plan.Steps);
        var parallelGroups = dependencyGraph.GetParallelGroups();

        var allResults = new ConcurrentDictionary<int, StepResult>();
        var overallSuccess = true;
        var outputs = new ConcurrentBag<string>();

        foreach (var group in parallelGroups)
        {
            if (ct.IsCancellationRequested)
                break;

            // Execute all steps in this group in parallel
            var groupTasks = group.Select(async stepIndex =>
            {
                var step = plan.Steps[stepIndex];
                var sandboxedStep = _safety.SandboxStep(step);
                var result = await _executeStep(sandboxedStep, ct);

                allResults[stepIndex] = result;

                if (!result.Success)
                {
                    overallSuccess = false;
                }

                outputs.Add(result.Output);

                return result;
            });

            await Task.WhenAll(groupTasks);
        }

        // Order results by step index
        var orderedResults = Enumerable.Range(0, plan.Steps.Count)
            .Select(i => allResults.TryGetValue(i, out var result) ? result : null)
            .Where(r => r != null)
            .Select(r => r!)
            .ToList();

        var finalOutput = string.Join("\n", outputs.Where(o => !string.IsNullOrEmpty(o)));

        return (orderedResults, overallSuccess, finalOutput);
    }

    /// <summary>
    /// Estimates the speedup from parallel execution.
    /// </summary>
    public double EstimateSpeedup(Plan plan)
    {
        var dependencyGraph = new StepDependencyGraph(plan.Steps);
        var parallelGroups = dependencyGraph.GetParallelGroups();

        // Speedup = total steps / number of parallel groups
        var sequentialSteps = plan.Steps.Count;
        var parallelSteps = parallelGroups.Count;

        return parallelSteps > 0 ? (double)sequentialSteps / parallelSteps : 1.0;
    }
}
