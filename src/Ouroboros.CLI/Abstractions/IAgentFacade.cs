// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Abstractions;

using Ouroboros.Core.Steps;
using Ouroboros.Core.Monads;

/// <summary>
/// Thin, monadic-friendly façade over the unified agent. Intended for programmatic composition
/// and testing, while keeping the legacy string-returning methods intact on the concrete agent.
/// </summary>
public interface IAgentFacade
{
    // Core monadic operations
    Task<Result<string, string>> AskResultAsync(string question);
    Task<Result<string, string>> RunPipelineResultAsync(string dsl);
    Task<Result<string, string>> RunMeTTaExpressionResultAsync(string expression);
    Task<Result<string, string>> QueryMeTTaResultAsync(string query);

    // Step-based composition API
    Step<string, Result<string, string>> AskStep();
    Step<string, Result<string, string>> PipelineStep();
    Step<string, Result<string, string>> MeTTaExpressionStep();
    Step<string, Result<string, string>> MeTTaQueryStep();
    Step<string, Result<string, string>> OrchestrateStep();
    Step<string, Result<string, string>> PlanStep();
    Step<string, Result<string, string>> ExecuteStep();
    Step<string, Result<string, string>> FetchResearchStep();
    Step<string, Result<string, string>> ProcessLargeInputStep();
    Step<string, Result<string, string>> RememberStep();
    Step<string, Result<string, string>> RecallStep();
    Step<string, Result<string, string>> RunSkillStep();
    Step<string, Result<string, string>> UseToolStep(string toolName);
}
