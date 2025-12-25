// <copyright file="OuroborosCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text;
using LangChainPipeline.Agent.MetaAI;
using Ouroboros.Tools.MeTTa;

// Alias to avoid naming conflict with the OuroborosConfidence method
using ConfidenceLevel = LangChainPipeline.Agent.MetaAI.OuroborosConfidence;

namespace Ouroboros.Application;

/// <summary>
/// CLI pipeline steps for Ouroboros self-improving AI orchestration.
/// Provides access to the recursive Plan-Execute-Verify-Learn cycle through pipeline DSL.
/// </summary>
public static class OuroborosCliSteps
{
    // Thread-local storage for the current Ouroboros atom
    private static OuroborosAtom? _currentAtom;

    /// <summary>
    /// Initializes an Ouroboros atom for self-improving orchestration.
    /// Usage: OuroborosInit or OuroborosInit('MyAgent')
    /// </summary>
    [PipelineToken("OuroborosInit", "InitOuroboros", "SelfInit")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosInit(string? args = null)
        => s =>
        {
            string name = ParseString(args);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Ouroboros";
            }

            _currentAtom = OuroborosAtom.CreateDefault(name);

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Initialized: {_currentAtom.Name} ({_currentAtom.InstanceId})");
                Console.WriteLine($"[ouroboros] Capabilities: {_currentAtom.Capabilities.Count}");
                Console.WriteLine($"[ouroboros] Current Phase: {_currentAtom.CurrentPhase}");
            }

            s.Output = $"Ouroboros '{name}' initialized with {_currentAtom.Capabilities.Count} capabilities";
            s.Branch = s.Branch.WithIngestEvent($"ouroboros:init:{_currentAtom.InstanceId}", Array.Empty<string>());

            return Task.FromResult(s);
        };

    /// <summary>
    /// Sets a goal for the Ouroboros to pursue.
    /// Usage: OuroborosGoal('Analyze and improve code quality')
    /// </summary>
    [PipelineToken("OuroborosGoal", "SetGoal", "SelfGoal")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosGoal(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string goal = ParseString(args);
            if (string.IsNullOrWhiteSpace(goal))
            {
                goal = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Prompt;
            }

            if (string.IsNullOrWhiteSpace(goal))
            {
                Console.WriteLine("[ouroboros] No goal provided");
                return Task.FromResult(s);
            }

            // Check if goal is safe
            if (!_currentAtom.IsSafeAction(goal))
            {
                Console.WriteLine($"[ouroboros] ⚠ Goal violates safety constraints: {goal}");
                s.Output = "Goal rejected due to safety constraints";
                return Task.FromResult(s);
            }

            _currentAtom.SetGoal(goal);

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Goal set: {goal}");
                Console.WriteLine($"[ouroboros] Confidence: {_currentAtom.AssessConfidence(goal)}");
            }

            s.Query = goal;
            s.Output = $"Goal set: {goal}";
            s.Branch = s.Branch.WithIngestEvent($"ouroboros:goal:{goal}", Array.Empty<string>());

            return Task.FromResult(s);
        };

    /// <summary>
    /// Advances to the next phase in the improvement cycle.
    /// Usage: OuroborosAdvance
    /// </summary>
    [PipelineToken("OuroborosAdvance", "AdvancePhase", "NextPhase")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosAdvance(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            ImprovementPhase previousPhase = _currentAtom.CurrentPhase;
            ImprovementPhase newPhase = _currentAtom.AdvancePhase();

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Phase transition: {previousPhase} -> {newPhase}");
                Console.WriteLine($"[ouroboros] Cycles completed: {_currentAtom.CycleCount}");
            }

            s.Output = $"Phase: {previousPhase} -> {newPhase}";

            // If we completed a cycle, log it
            if (newPhase == ImprovementPhase.Plan && previousPhase == ImprovementPhase.Learn)
            {
                Console.WriteLine($"[ouroboros] ✓ Cycle {_currentAtom.CycleCount} completed!");
                s.Branch = s.Branch.WithIngestEvent($"ouroboros:cycle:{_currentAtom.CycleCount}", Array.Empty<string>());
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Performs self-reflection - the Ouroboros examines its own state.
    /// Usage: OuroborosReflect
    /// </summary>
    [PipelineToken("OuroborosReflect", "SelfReflect", "Introspect")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosReflect(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string reflection = _currentAtom.SelfReflect();

            Console.WriteLine(reflection);

            s.Output = reflection;
            s.Context = reflection;

            return Task.FromResult(s);
        };

    /// <summary>
    /// Assesses confidence for a given action.
    /// Usage: OuroborosConfidence('planning a complex task')
    /// </summary>
    [PipelineToken("OuroborosConfidence", "AssessConfidence", "CheckConfidence")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosConfidence(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string action = ParseString(args);
            if (string.IsNullOrWhiteSpace(action))
            {
                action = s.Query;
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                Console.WriteLine("[ouroboros] No action to assess");
                return Task.FromResult(s);
            }

            ConfidenceLevel confidenceLevel = _currentAtom.AssessConfidence(action);

            string confidenceMessage = confidenceLevel switch
            {
                ConfidenceLevel.High => "High - can proceed confidently",
                ConfidenceLevel.Medium => "Medium - proceed with verification",
                ConfidenceLevel.Low => "Low - requires careful planning and validation",
                _ => "Unknown"
            };

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Action: {action}");
                Console.WriteLine($"[ouroboros] Confidence: {confidenceMessage}");
            }

            s.Output = $"Confidence for '{action}': {confidenceMessage}";

            return Task.FromResult(s);
        };

    /// <summary>
    /// Records an experience for learning.
    /// Usage: OuroborosLearn('goal=Search task|success=true|quality=0.85|insight=Search was effective')
    /// </summary>
    [PipelineToken("OuroborosLearn", "RecordExperience", "Learn")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosLearn(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            // Parse experience data from args
            string parsed = ParseString(args);
            string goal = _currentAtom.CurrentGoal ?? "Unknown goal";
            bool success = true;
            double quality = 0.7;
            List<string> insights = new List<string>();

            if (!string.IsNullOrWhiteSpace(parsed))
            {
                foreach (string part in parsed.Split('|'))
                {
                    string trimmed = part.Trim();
                    if (trimmed.StartsWith("goal=", StringComparison.OrdinalIgnoreCase))
                    {
                        goal = trimmed.Substring(5);
                    }
                    else if (trimmed.StartsWith("success=", StringComparison.OrdinalIgnoreCase))
                    {
                        success = bool.TryParse(trimmed.Substring(8), out bool s2) && s2;
                    }
                    else if (trimmed.StartsWith("quality=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(trimmed.Substring(8), out double q))
                        {
                            quality = Math.Clamp(q, 0.0, 1.0);
                        }
                    }
                    else if (trimmed.StartsWith("insight=", StringComparison.OrdinalIgnoreCase))
                    {
                        insights.Add(trimmed.Substring(8));
                    }
                }
            }

            // If no insights provided, generate a default one
            if (insights.Count == 0)
            {
                insights.Add(success ? "Task completed successfully" : "Task encountered issues");
            }

            OuroborosExperience experience = new OuroborosExperience(
                Guid.NewGuid(),
                goal,
                success,
                quality,
                insights,
                DateTime.UtcNow);

            _currentAtom.RecordExperience(experience);

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Experience recorded:");
                Console.WriteLine($"  Goal: {goal}");
                Console.WriteLine($"  Success: {success}");
                Console.WriteLine($"  Quality: {quality:P0}");
                Console.WriteLine($"  Insights: {string.Join(", ", insights)}");
                Console.WriteLine($"  Total experiences: {_currentAtom.Experiences.Count}");
            }

            s.Output = $"Experience recorded: {goal} ({(success ? "success" : "failure")}, quality: {quality:P0})";
            s.Branch = s.Branch.WithIngestEvent($"ouroboros:experience:{goal}", Array.Empty<string>());

            return Task.FromResult(s);
        };

    /// <summary>
    /// Adds a capability to the Ouroboros.
    /// Usage: OuroborosCapability('name=reasoning|desc=Logical reasoning|conf=0.85')
    /// </summary>
    [PipelineToken("OuroborosCapability", "AddCapability")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosCapability(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string parsed = ParseString(args);
            if (string.IsNullOrWhiteSpace(parsed))
            {
                // List current capabilities
                Console.WriteLine("[ouroboros] Current capabilities:");
                foreach (OuroborosCapability cap in _currentAtom.Capabilities)
                {
                    Console.WriteLine($"  - {cap.Name}: {cap.Description} ({cap.ConfidenceLevel:P0})");
                }

                return Task.FromResult(s);
            }

            string name = "unknown";
            string desc = "No description";
            double conf = 0.5;

            foreach (string part in parsed.Split('|'))
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
                {
                    name = trimmed.Substring(5);
                }
                else if (trimmed.StartsWith("desc=", StringComparison.OrdinalIgnoreCase))
                {
                    desc = trimmed.Substring(5);
                }
                else if (trimmed.StartsWith("conf=", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(trimmed.Substring(5), out double c))
                    {
                        conf = Math.Clamp(c, 0.0, 1.0);
                    }
                }
            }

            _currentAtom.AddCapability(new OuroborosCapability(name, desc, conf));

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Capability added: {name} ({conf:P0})");
            }

            s.Output = $"Capability '{name}' added with {conf:P0} confidence";

            return Task.FromResult(s);
        };

    /// <summary>
    /// Exports the Ouroboros state to MeTTa symbolic representation.
    /// Usage: OuroborosToMeTTa
    /// </summary>
    [PipelineToken("OuroborosToMeTTa", "ExportMeTTa", "ToMeTTa")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosToMeTTa(string? args = null)
        => async s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return s;
            }

            string metta = _currentAtom.ToMeTTa();

            Console.WriteLine("[ouroboros] MeTTa representation:");
            Console.WriteLine(metta);

            s.Output = metta;
            s.Context = metta;

            // If MeTTa engine is available, add facts to it
            if (s.MeTTaEngine != null)
            {
                Result<Unit, string> result = await s.MeTTaEngine.AddFactAsync(metta);
                result.Match(
                    _ =>
                    {
                        if (s.Trace)
                        {
                            Console.WriteLine("[ouroboros] Facts added to MeTTa engine");
                        }
                    },
                    error =>
                    {
                        Console.WriteLine($"[ouroboros] Warning: Failed to add facts to MeTTa: {error}");
                    });
            }

            return s;
        };

    /// <summary>
    /// Runs a complete improvement cycle: Plan -> Execute -> Verify -> Learn.
    /// Usage: OuroborosCycle('Analyze the codebase')
    /// </summary>
    [PipelineToken("OuroborosCycle", "ImprovementCycle", "SelfImprove")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosCycle(string? args = null)
        => async s =>
        {
            if (_currentAtom == null)
            {
                _currentAtom = OuroborosAtom.CreateDefault();
                Console.WriteLine("[ouroboros] Auto-initialized Ouroboros atom");
            }

            string goal = ParseString(args);
            if (string.IsNullOrWhiteSpace(goal))
            {
                goal = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Prompt;
            }

            if (string.IsNullOrWhiteSpace(goal))
            {
                Console.WriteLine("[ouroboros] No goal provided for improvement cycle");
                return s;
            }

            // Check safety
            if (!_currentAtom.IsSafeAction(goal))
            {
                Console.WriteLine($"[ouroboros] ⚠ Goal violates safety constraints");
                s.Output = "Improvement cycle aborted: safety violation";
                return s;
            }

            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"[ouroboros] Starting Improvement Cycle");
            Console.WriteLine($"[ouroboros] Goal: {goal}");
            Console.WriteLine($"[ouroboros] Confidence: {_currentAtom.AssessConfidence(goal)}");
            Console.WriteLine($"{'=',-60}\n");

            _currentAtom.SetGoal(goal);

            StringBuilder cycleOutput = new StringBuilder();
            List<string> insights = new List<string>();
            bool overallSuccess = true;

            // Phase 1: PLAN
            Console.WriteLine("[ouroboros] Phase 1: PLAN");
            _currentAtom.AdvancePhase(); // Move to Execute after planning

            string planPrompt = $@"Create a step-by-step plan to achieve: {goal}

Self-Assessment:
{_currentAtom.SelfReflect()}

Provide a concise plan with 3-5 actionable steps.";

            string planOutput;
            try
            {
                planOutput = await s.Llm.InnerModel.GenerateTextAsync(planPrompt);
                Console.WriteLine($"[ouroboros] Plan generated:\n{planOutput}");
                cycleOutput.AppendLine($"PLAN:\n{planOutput}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Plan generation failed: {ex.Message}");
                overallSuccess = false;
                planOutput = "Planning failed";
                insights.Add($"Planning error: {ex.Message}");
            }

            // Phase 2: EXECUTE
            Console.WriteLine("\n[ouroboros] Phase 2: EXECUTE");
            _currentAtom.AdvancePhase(); // Move to Verify after execution

            string executePrompt = $@"Execute this plan for goal: {goal}

Plan:
{planOutput}

Describe the execution results for each step. Be concise.";

            string executeOutput;
            try
            {
                executeOutput = await s.Llm.InnerModel.GenerateTextAsync(executePrompt);
                Console.WriteLine($"[ouroboros] Execution results:\n{executeOutput}");
                cycleOutput.AppendLine($"EXECUTION:\n{executeOutput}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Execution failed: {ex.Message}");
                overallSuccess = false;
                executeOutput = "Execution failed";
                insights.Add($"Execution error: {ex.Message}");
            }

            // Phase 3: VERIFY
            Console.WriteLine("\n[ouroboros] Phase 3: VERIFY");
            _currentAtom.AdvancePhase(); // Move to Learn after verification

            string verifyPrompt = $@"Verify the execution results for goal: {goal}

Execution Output:
{executeOutput}

Provide:
1. Was the goal achieved? (yes/no)
2. Quality score (0-100%)
3. Any issues identified";

            string verifyOutput;
            double quality = 0.7;
            try
            {
                verifyOutput = await s.Llm.InnerModel.GenerateTextAsync(verifyPrompt);
                Console.WriteLine($"[ouroboros] Verification:\n{verifyOutput}");
                cycleOutput.AppendLine($"VERIFICATION:\n{verifyOutput}\n");

                // Try to extract quality from response
                if (verifyOutput.Contains("100%"))
                {
                    quality = 1.0;
                }
                else if (verifyOutput.Contains("90%"))
                {
                    quality = 0.9;
                }
                else if (verifyOutput.Contains("80%"))
                {
                    quality = 0.8;
                }
                else if (verifyOutput.ToLower().Contains("no") && verifyOutput.ToLower().Contains("achieved"))
                {
                    quality = 0.4;
                    overallSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Verification failed: {ex.Message}");
                verifyOutput = "Verification failed";
                insights.Add($"Verification error: {ex.Message}");
            }

            // Phase 4: LEARN
            Console.WriteLine("\n[ouroboros] Phase 4: LEARN");
            _currentAtom.AdvancePhase(); // Back to Plan, completing the cycle

            string learnPrompt = $@"Extract 2-3 key insights from this improvement cycle:

Goal: {goal}
Plan: {planOutput}
Execution: {executeOutput}
Verification: {verifyOutput}

Provide bullet points starting with '-':";

            try
            {
                string learnOutput = await s.Llm.InnerModel.GenerateTextAsync(learnPrompt);
                Console.WriteLine($"[ouroboros] Insights:\n{learnOutput}");
                cycleOutput.AppendLine($"LEARNING:\n{learnOutput}\n");

                // Parse insights
                foreach (string line in learnOutput.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("-") || trimmed.StartsWith("*"))
                    {
                        insights.Add(trimmed.TrimStart('-', '*', ' '));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Learning failed: {ex.Message}");
                insights.Add($"Learning error: {ex.Message}");
            }

            // Record the experience
            if (insights.Count == 0)
            {
                insights.Add(overallSuccess ? "Cycle completed successfully" : "Cycle encountered issues");
            }

            OuroborosExperience experience = new OuroborosExperience(
                Guid.NewGuid(),
                goal,
                overallSuccess,
                quality,
                insights,
                DateTime.UtcNow);

            _currentAtom.RecordExperience(experience);

            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"[ouroboros] ✓ Cycle {_currentAtom.CycleCount} completed");
            Console.WriteLine($"[ouroboros] Success: {overallSuccess}");
            Console.WriteLine($"[ouroboros] Quality: {quality:P0}");
            Console.WriteLine($"[ouroboros] Insights: {insights.Count}");
            Console.WriteLine($"{'=',-60}\n");

            s.Output = cycleOutput.ToString();
            s.Context = string.Join("\n", insights);
            s.Branch = s.Branch.WithReasoning(new FinalSpec(cycleOutput.ToString()), goal, new List<ToolExecution>());

            return s;
        };

    /// <summary>
    /// Gets the current status of the Ouroboros.
    /// Usage: OuroborosStatus
    /// </summary>
    [PipelineToken("OuroborosStatus", "SelfStatus", "Status")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosStatus(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized");
                s.Output = "Ouroboros not initialized";
                return Task.FromResult(s);
            }

            StringBuilder status = new StringBuilder();
            status.AppendLine($"╔{'═',58}╗");
            status.AppendLine($"║ Ouroboros Status: {_currentAtom.Name,-38}║");
            status.AppendLine($"╠{'═',58}╣");
            status.AppendLine($"║ Instance ID: {_currentAtom.InstanceId,-43}║");
            status.AppendLine($"║ Current Phase: {_currentAtom.CurrentPhase,-41}║");
            status.AppendLine($"║ Cycles Completed: {_currentAtom.CycleCount,-38}║");
            status.AppendLine($"║ Current Goal: {TruncateString(_currentAtom.CurrentGoal ?? "None", 42),-42}║");
            status.AppendLine($"║ Capabilities: {_currentAtom.Capabilities.Count,-42}║");
            status.AppendLine($"║ Experiences: {_currentAtom.Experiences.Count,-43}║");

            if (_currentAtom.Experiences.Count > 0)
            {
                double successRate = _currentAtom.Experiences.Count(e => e.Success) / (double)_currentAtom.Experiences.Count;
                status.AppendLine($"║ Success Rate: {successRate:P0,-42}║");
            }

            status.AppendLine($"╚{'═',58}╝");

            string statusString = status.ToString();
            Console.WriteLine(statusString);
            s.Output = statusString;

            return Task.FromResult(s);
        };

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        if (arg.StartsWith("'") && arg.EndsWith("'") && arg.Length >= 2)
        {
            return arg[1..^1];
        }

        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2)
        {
            return arg[1..^1];
        }

        return arg;
    }

    private static string TruncateString(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }
}
