// <copyright file="MeTTaInteractiveMode.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Interactive REPL mode for MeTTa symbolic reasoning.
/// Provides a consistent interface for querying and plan checking.
/// </summary>
public static class MeTTaInteractiveMode
{
    /// <summary>
    /// Runs the interactive MeTTa REPL (Read-Eval-Print Loop).
    /// </summary>
    /// <param name="engine">The MeTTa engine to use.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunInteractiveAsync(IMeTTaEngine? engine = null)
    {
        // Initialize engine if not provided
        engine ??= new SubprocessMeTTaEngine();

        // Initialize plan selector
        var planSelector = new SymbolicPlanSelector(engine);
        await planSelector.InitializeAsync();

        PrintWelcome();

        bool running = true;
        while (running)
        {
            Console.Write("\nmetta> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            string[] parts = input.Trim().Split(' ', 2);
            string command = parts[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "help" or "?":
                        PrintHelp();
                        break;

                    case "query" or "q":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: query <metta-expression>");
                            Console.WriteLine("Example: query (+ 1 2)");
                        }
                        else
                        {
                            await ExecuteQueryAsync(engine, parts[1]);
                        }
                        break;

                    case "plan" or "p":
                        await ExecutePlanCheckInteractiveAsync(planSelector);
                        break;

                    case "fact" or "f":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: fact <metta-fact>");
                            Console.WriteLine("Example: fact (human Socrates)");
                        }
                        else
                        {
                            await AddFactAsync(engine, parts[1]);
                        }
                        break;

                    case "rule" or "r":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: rule <metta-rule>");
                            Console.WriteLine("Example: rule (= (mortal $x) (human $x))");
                        }
                        else
                        {
                            await ApplyRuleAsync(engine, parts[1]);
                        }
                        break;

                    case "reset":
                        await ResetEngineAsync(engine);
                        await planSelector.InitializeAsync();
                        break;

                    case "exit" or "quit" or "q!":
                        running = false;
                        Console.WriteLine("Goodbye!");
                        break;

                    default:
                        // Treat unknown commands as queries
                        await ExecuteQueryAsync(engine, input);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        engine.Dispose();
    }

    private static void PrintWelcome()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║       MeTTa Interactive Symbolic Reasoning Mode          ║");
        Console.WriteLine("║                    Phase 4 Integration                    ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Type 'help' for available commands, 'exit' to quit.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("\nAvailable Commands:");
        Console.WriteLine("  help, ?           - Show this help message");
        Console.WriteLine("  query, q <expr>   - Execute a MeTTa query");
        Console.WriteLine("  fact, f <fact>    - Add a fact to the knowledge base");
        Console.WriteLine("  rule, r <rule>    - Apply a reasoning rule");
        Console.WriteLine("  plan, p           - Interactive plan constraint checking");
        Console.WriteLine("  reset             - Reset the knowledge base");
        Console.WriteLine("  exit, quit, q!    - Exit interactive mode");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  query (+ 1 2)");
        Console.WriteLine("  fact (human Socrates)");
        Console.WriteLine("  rule (= (mortal $x) (human $x))");
        Console.WriteLine("  query (mortal Socrates)");
        Console.WriteLine("  plan");
    }

    private static async Task ExecuteQueryAsync(IMeTTaEngine engine, string query)
    {
        Result<string, string> result = await engine.ExecuteQueryAsync(query, CancellationToken.None);

        result.Match(
            success =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Result: {success}");
                Console.ResetColor();
                return Unit.Value;
            },
            error =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {error}");
                Console.ResetColor();
                return Unit.Value;
            });
    }

    private static async Task AddFactAsync(IMeTTaEngine engine, string fact)
    {
        Result<Unit, string> result = await engine.AddFactAsync(fact, CancellationToken.None);

        result.Match(
            _ =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Fact added");
                Console.ResetColor();
                return Unit.Value;
            },
            error =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {error}");
                Console.ResetColor();
                return Unit.Value;
            });
    }

    private static async Task ApplyRuleAsync(IMeTTaEngine engine, string rule)
    {
        Result<string, string> result = await engine.ApplyRuleAsync(rule, CancellationToken.None);

        result.Match(
            success =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Rule applied: {success}");
                Console.ResetColor();
                return Unit.Value;
            },
            error =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {error}");
                Console.ResetColor();
                return Unit.Value;
            });
    }

    private static async Task ResetEngineAsync(IMeTTaEngine engine)
    {
        Result<Unit, string> result = await engine.ResetAsync(CancellationToken.None);

        result.Match(
            _ =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("✓ Knowledge base reset");
                Console.ResetColor();
                return Unit.Value;
            },
            error =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {error}");
                Console.ResetColor();
                return Unit.Value;
            });
    }

    private static async Task ExecutePlanCheckInteractiveAsync(SymbolicPlanSelector selector)
    {
        Console.WriteLine("\n=== Interactive Plan Constraint Checking ===");
        Console.WriteLine("Enter plan actions (one per line). Type 'done' when finished.\n");

        var actions = new List<PlanAction>();
        Console.WriteLine("Available action types:");
        Console.WriteLine("  1. FileSystem <operation> <path>");
        Console.WriteLine("  2. Network <operation> <endpoint>");
        Console.WriteLine("  3. Tool <name> <args>");
        Console.WriteLine();

        bool enteringActions = true;
        while (enteringActions)
        {
            Console.Write("action> ");
            string? actionInput = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(actionInput))
            {
                continue;
            }

            if (actionInput.Trim().Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                enteringActions = false;
                break;
            }

            // Parse action parts
            string[] actionParts = actionInput.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (actionParts.Length < 1)
            {
                Console.WriteLine("Invalid action format. Try: <type> <operation> [<target>]");
                continue;
            }

            // Parse action with validation
            PlanAction? action = null;
            
            switch (actionParts[0].ToLowerInvariant())
            {
                case "filesystem" or "fs":
                    if (actionParts.Length < 2)
                    {
                        Console.WriteLine("FileSystem action requires operation (e.g., 'filesystem read')");
                        continue;
                    }
                    action = new FileSystemAction(
                        actionParts[1],
                        actionParts.Length > 2 ? actionParts[2] : null);
                    break;
                    
                case "network" or "net":
                    if (actionParts.Length < 2)
                    {
                        Console.WriteLine("Network action requires operation (e.g., 'network get')");
                        continue;
                    }
                    action = new NetworkAction(
                        actionParts[1],
                        actionParts.Length > 2 ? actionParts[2] : null);
                    break;
                    
                case "tool":
                    if (actionParts.Length < 2)
                    {
                        Console.WriteLine("Tool action requires name (e.g., 'tool search_tool')");
                        continue;
                    }
                    action = new ToolAction(
                        actionParts[1],
                        actionParts.Length > 2 ? actionParts[2] : null);
                    break;
                    
                default:
                    Console.WriteLine("Unknown action type. Use: filesystem, network, or tool");
                    continue;
            }

            if (action != null)
            {
                actions.Add(action);
                Console.WriteLine($"✓ Added: {action.ToMeTTaAtom()}");
            }
        }

        if (actions.Count == 0)
        {
            Console.WriteLine("No actions entered. Returning to main prompt.");
            return;
        }

        Console.Write("\nEnter plan description: ");
        string? description = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(description))
        {
            description = "User-defined plan";
        }

        // Build the plan
        var plan = new Ouroboros.Pipeline.Verification.Plan(description);
        foreach (var action in actions)
        {
            plan = plan.WithAction(action);
        }

        // Check against contexts
        var contexts = new[] { SafeContext.ReadOnly, SafeContext.FullAccess };

        foreach (var context in contexts)
        {
            Console.WriteLine($"\nChecking against {context.ToMeTTaAtom()} context:");

            Result<string, string> result = await selector.ExplainPlanAsync(
                plan,
                context,
                CancellationToken.None);

            result.Match(
                explanation =>
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  {explanation}");
                    Console.ResetColor();
                    return Unit.Value;
                },
                error =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  Error: {error}");
                    Console.ResetColor();
                    return Unit.Value;
                });
        }
    }
}
