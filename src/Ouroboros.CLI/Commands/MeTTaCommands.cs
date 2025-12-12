using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Diagnostics;
using LangChainPipeline.Options;
using LangChainPipeline.Providers;
using Ouroboros.Application.Services;
using IEmbeddingModel = LangChainPipeline.Domain.IEmbeddingModel;

namespace Ouroboros.CLI.Commands;

public static class MeTTaCommands
{
    public static async Task RunMeTTaAsync(MeTTaOptions o)
    {
        // Interactive mode
        if (o.Interactive)
        {
            await MeTTaInteractiveMode.RunInteractiveAsync();
            return;
        }

        // Voice mode integration
        if (o.Voice)
        {
            await RunMeTTaVoiceModeAsync(o);
            return;
        }

        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   MeTTa Orchestrator v3.0 - Symbolic Reasoning            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        if (o.Debug) Environment.SetEnvironmentVariable("MONADIC_DEBUG", "1");

        try
        {
            OllamaProvider provider = new OllamaProvider();
            ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false);

            (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(
                o.Endpoint,
                o.ApiKey,
                o.EndpointType);

            IChatCompletionModel chatModel;
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                chatModel = ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, o.Model, settings, endpointType);
                string backend = $"remote-{endpointType.ToString().ToLowerInvariant()}";
                Console.WriteLine($"[INIT] Backend={backend} Endpoint={endpoint}");
                Console.WriteLine($"✓ Using remote model: {o.Model}");
            }
            else
            {
                chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model));
                Console.WriteLine($"[INIT] Backend=ollama-local");
                Console.WriteLine($"✓ Using local model: {o.Model}");
            }

            IEmbeddingModel embedModel = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, o.Embed, provider);
            Console.WriteLine($"✓ Using embedding model: {o.Embed}");

            Console.WriteLine("✓ Initializing MeTTa orchestrator...");
            MeTTaOrchestratorBuilder orchestratorBuilder = MeTTaOrchestratorBuilder.CreateDefault(embedModel)
                .WithLLM(chatModel);

            MeTTaOrchestrator orchestrator = orchestratorBuilder.Build();
            Console.WriteLine("✓ MeTTa orchestrator v3.0 initialized\n");

            Console.WriteLine($"Goal: {o.Goal}\n");

            Console.WriteLine("=== Planning Phase ===");
            Stopwatch sw = Stopwatch.StartNew();
            Result<Plan, string> planResult = await orchestrator.PlanAsync(o.Goal);

            Plan plan = planResult.Match(
                success => success,
                error =>
                {
                    Console.Error.WriteLine($"Planning failed: {error}");
                    Environment.Exit(1);
                    return null!;
                }
            );

            sw.Stop();
            Console.WriteLine($"✓ Plan generated in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Steps: {plan.Steps.Count}");
            Console.WriteLine($"  Overall confidence: {plan.ConfidenceScores.GetValueOrDefault("overall", 0):P2}\n");

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                PlanStep step = plan.Steps[i];
                Console.WriteLine($"  {i + 1}. {step.Action}");
                Console.WriteLine($"     Expected: {step.ExpectedOutcome}");
                Console.WriteLine($"     Confidence: {step.ConfidenceScore:P2}");
            }
            Console.WriteLine();

            if (o.PlanOnly)
            {
                Console.WriteLine("✓ Plan-only mode - skipping execution");
                return;
            }

            Console.WriteLine("=== Execution Phase ===");
            sw.Restart();
            Result<ExecutionResult, string> executionResult = await orchestrator.ExecuteAsync(plan);
            sw.Stop();

            executionResult.Match(
                success =>
                {
                    Console.WriteLine($"✓ Execution completed in {sw.ElapsedMilliseconds}ms");
                    Console.WriteLine("\nFinal Result:");
                    Console.WriteLine($"  Success: {success.Success}");
                    Console.WriteLine($"  Duration: {success.Duration.TotalSeconds:F2}s");
                    if (!string.IsNullOrWhiteSpace(success.FinalOutput))
                    {
                        Console.WriteLine($"  Output: {success.FinalOutput}");
                    }
                    Console.WriteLine("\nStep Results:");
                    for (int i = 0; i < success.StepResults.Count; i++)
                    {
                        StepResult stepResult = success.StepResults[i];
                        Console.WriteLine($"  {i + 1}. {stepResult.Step.Action}");
                        Console.WriteLine($"     Success: {stepResult.Success}");
                        Console.WriteLine($"     Output: {stepResult.Output}");
                        if (!string.IsNullOrEmpty(stepResult.Error))
                        {
                            Console.WriteLine($"     Error: {stepResult.Error}");
                        }
                    }
                },
                error =>
                {
                    Console.Error.WriteLine($"Execution failed: {error}");
                    Environment.Exit(1);
                }
            );

            if (o.ShowMetrics)
            {
                Console.WriteLine("\n=== Performance Metrics ===");
                IReadOnlyDictionary<string, PerformanceMetrics> metrics = orchestrator.GetMetrics();

                foreach ((string key, PerformanceMetrics metric) in metrics)
                {
                    Console.WriteLine($"\n{key}:");
                    Console.WriteLine($"  Executions: {metric.ExecutionCount}");
                    Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs:F2}ms");
                    Console.WriteLine($"  Success Rate: {metric.SuccessRate:P2}");
                    Console.WriteLine($"  Last Used: {metric.LastUsed:g}");
                }
            }

            Console.WriteLine("\n✓ MeTTa orchestrator execution completed successfully");
        }
        catch (Exception ex) when (ex.Message.Contains("Connection refused") || ex.Message.Contains("ECONNREFUSED"))
        {
            Console.Error.WriteLine("⚠ Error: Ollama is not running. Please start Ollama before using the MeTTa orchestrator.");
            Console.Error.WriteLine("   Run: ollama serve");
            Environment.Exit(1);
        }
        catch (Exception ex) when (ex.Message.Contains("metta") && (ex.Message.Contains("not found") || ex.Message.Contains("No such file")))
        {
            Console.Error.WriteLine("⚠ Error: MeTTa engine not found. Please install MeTTa:");
            Console.Error.WriteLine("   Install from: https://github.com/trueagi-io/hyperon-experimental");
            Console.Error.WriteLine("   Ensure 'metta' executable is in your PATH");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("\n=== ❌ MeTTa Orchestrator Failed ===");
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (o.Debug)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Runs the MeTTa orchestrator in voice mode with conversational interaction.
    /// </summary>
    private static async Task RunMeTTaVoiceModeAsync(MeTTaOptions o)
    {
        var voiceService = VoiceModeExtensions.CreateVoiceService(
            voice: true,
            persona: o.Persona,
            voiceOnly: o.VoiceOnly,
            localTts: o.LocalTts,
            voiceLoop: o.VoiceLoop,
            model: o.Model,
            endpoint: o.Endpoint ?? "http://localhost:11434");

        await voiceService.InitializeAsync();
        voiceService.PrintHeader("METTA ORCHESTRATOR");

        // Initialize orchestrator
        OllamaProvider provider = new OllamaProvider();
        ChatRuntimeSettings settings = new ChatRuntimeSettings(o.Temperature, o.MaxTokens, o.TimeoutSeconds, false);
        (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.ResolveWithOverrides(o.Endpoint, o.ApiKey, o.EndpointType);

        IChatCompletionModel chatModel;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            chatModel = ServiceFactory.CreateRemoteChatModel(endpoint, apiKey, o.Model, settings, endpointType);
        }
        else
        {
            chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, o.Model));
        }

        IEmbeddingModel embedModel = ServiceFactory.CreateEmbeddingModel(endpoint, apiKey, endpointType, o.Embed, provider);
        MeTTaOrchestrator orchestrator = MeTTaOrchestratorBuilder.CreateDefault(embedModel).WithLLM(chatModel).Build();

        await voiceService.SayAsync("MeTTa orchestrator is ready! Give me a goal and I'll plan and execute it using symbolic reasoning.");

        // Initial goal if provided
        if (!string.IsNullOrWhiteSpace(o.Goal) && !o.Goal.Equals("interactive", StringComparison.OrdinalIgnoreCase))
        {
            await ProcessGoalAsync(orchestrator, o.Goal, o.PlanOnly, voiceService);
            if (!o.VoiceLoop)
            {
                voiceService.Dispose();
                return;
            }
        }

        // Voice loop
        bool running = true;
        while (running)
        {
            var input = await voiceService.GetInputAsync("\n  Goal: ");
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (IsExitCommand(input))
            {
                await voiceService.SayAsync("Goodbye! The symbolic reasoning engine awaits your return.");
                running = false;
                continue;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await voiceService.SayAsync("Tell me a goal and I'll create a plan using MeTTa symbolic reasoning, then execute it step by step. Say 'plan only' before your goal to skip execution.");
                continue;
            }

            bool planOnly = input.StartsWith("plan only", StringComparison.OrdinalIgnoreCase) ||
                           input.StartsWith("just plan", StringComparison.OrdinalIgnoreCase);
            string goal = planOnly ? input.Replace("plan only", "", StringComparison.OrdinalIgnoreCase)
                                         .Replace("just plan", "", StringComparison.OrdinalIgnoreCase).Trim() : input;

            await ProcessGoalAsync(orchestrator, goal, planOnly, voiceService);
        }

        voiceService.Dispose();
    }

    private static async Task ProcessGoalAsync(MeTTaOrchestrator orchestrator, string goal, bool planOnly, VoiceModeService voiceService)
    {
        try
        {
            await voiceService.SayAsync($"Planning for: {goal}");

            var planResult = await orchestrator.PlanAsync(goal);
            var plan = planResult.Match<MeTTaPlan?>(
                success => success,
                error =>
                {
                    voiceService.SayAsync($"Planning failed: {error}").Wait();
                    return null;
                });

            if (plan == null) return;

            var steps = string.Join(", ", plan.Steps.Take(3).Select(s => s.Action));
            await voiceService.SayAsync($"I've created a plan with {plan.Steps.Count} steps: {steps}. Overall confidence is {plan.ConfidenceScores.GetValueOrDefault("overall", 0):P0}.");

            if (planOnly)
            {
                await voiceService.SayAsync("Plan only mode - skipping execution as requested.");
                return;
            }

            await voiceService.SayAsync("Executing the plan now...");
            var executionResult = await orchestrator.ExecuteAsync(plan);

            executionResult.Match(
                success =>
                {
                    var summary = success.Success
                        ? $"Execution completed successfully in {success.Duration.TotalSeconds:F1} seconds."
                        : "Execution completed with some issues.";
                    if (!string.IsNullOrWhiteSpace(success.FinalOutput))
                    {
                        summary += $" Result: {success.FinalOutput}";
                    }
                    voiceService.SayAsync(summary).Wait();
                },
                error => voiceService.SayAsync($"Execution failed: {error}").Wait());
        }
        catch (Exception ex)
        {
            await voiceService.SayAsync($"Something went wrong: {ex.Message}");
        }
    }

    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }
}
