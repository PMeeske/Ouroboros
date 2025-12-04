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
        List<List<int>> groups = new List<List<int>>();
        HashSet<int> executed = new HashSet<int>();

        while (executed.Count < _steps.Count)
        {
            List<int> group = new List<int>();

            for (int i = 0; i < _steps.Count; i++)
            {
                if (executed.Contains(i))
                    continue;

                // Can execute if all dependencies are satisfied
                if (!_dependencies.TryGetValue(i, out List<int>? deps) ||
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
            List<int> deps = new List<int>();
            PlanStep step = _steps[i];

            // Check if this step uses outputs from previous steps
            for (int j = 0; j < i; j++)
            {
                PlanStep prevStep = _steps[j];

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
        string prevActionRef = $"${previous.Action}";
        string prevOutputRef = $"output_{previous.Action}";

        foreach (object param in current.Parameters.Values)
        {
            string paramStr = param?.ToString() ?? "";
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
        StepDependencyGraph dependencyGraph = new StepDependencyGraph(plan.Steps);
        List<List<int>> parallelGroups = dependencyGraph.GetParallelGroups();

        ConcurrentDictionary<int, StepResult> allResults = new ConcurrentDictionary<int, StepResult>();
        bool overallSuccess = true;
        ConcurrentBag<string> outputs = new ConcurrentBag<string>();

        foreach (List<int> group in parallelGroups)
        {
            if (ct.IsCancellationRequested)
                break;

            // Execute all steps in this group in parallel
            IEnumerable<Task<StepResult>> groupTasks = group.Select(async stepIndex =>
            {
                PlanStep step = plan.Steps[stepIndex];
                PlanStep sandboxedStep = _safety.SandboxStep(step);
                StepResult result = await _executeStep(sandboxedStep, ct);

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
        List<StepResult> orderedResults = Enumerable.Range(0, plan.Steps.Count)
            .Select(i => allResults.TryGetValue(i, out StepResult? result) ? result : null)
            .Where(r => r != null)
            .Select(r => r!)
            .ToList();

        string finalOutput = string.Join("\n", outputs.Where(o => !string.IsNullOrEmpty(o)));

        return (orderedResults, overallSuccess, finalOutput);
    }

    /// <summary>
    /// Estimates the speedup from parallel execution.
    /// </summary>
    public double EstimateSpeedup(Plan plan)
    {
        StepDependencyGraph dependencyGraph = new StepDependencyGraph(plan.Steps);
        List<List<int>> parallelGroups = dependencyGraph.GetParallelGroups();

        // Speedup = total steps / number of parallel groups
        int sequentialSteps = plan.Steps.Count;
        int parallelSteps = parallelGroups.Count;

        return parallelSteps > 0 ? (double)sequentialSteps / parallelSteps : 1.0;
    }
}
