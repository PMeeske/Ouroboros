// <copyright file="OuroborosIntegrationCliSteps.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// CLI command reference for Ouroboros full system integration.
/// Use these commands with the Ouroboros CLI to interact with the integrated system.
/// </summary>
public static class OuroborosIntegrationCli
{
    /// <summary>
    /// CLI commands documentation for Ouroboros integration.
    /// </summary>
    public static class Commands
    {
        public const string Help = @"
=== Ouroboros Full System Integration ===

USAGE:
  ouroboros <command> [options]

COMMANDS:
  init              Initialize the Ouroboros system
  execute           Execute a goal with full cognitive pipeline  
  reason            Perform reasoning using multiple engines
  loop start        Start the autonomous cognitive loop
  loop stop         Stop the cognitive loop
  status            Get current system status
  config validate   Validate configuration file
  help              Show this help message

EXAMPLES:
  ouroboros init --config appsettings.Ouroboros.json
  ouroboros execute ""Analyze performance"" --memory --planning --causal
  ouroboros reason ""What causes delays?"" --symbolic --causal --abductive
  ouroboros loop start --interval 1000 --goals 5
  ouroboros status --verbose
  ouroboros config validate --config appsettings.Ouroboros.json

CONFIGURATION:
  Configuration is loaded from appsettings.Ouroboros.json by default.
  Override with --config option or OUROBOROS_* environment variables.

  Example environment variables:
    OUROBOROS__Features__EnableMeTTa=true
    OUROBOROS__EpisodicMemory__MaxMemorySize=50000
    OUROBOROS__Planning__MaxPlanningDepth=15

For more information, see docs/INTEGRATION_GUIDE.md
";

        public const string InitUsage = @"
INIT - Initialize the Ouroboros system

USAGE:
  ouroboros init [options]

OPTIONS:
  --config <path>      Path to configuration file (default: appsettings.Ouroboros.json)
  --validate           Validate configuration without initializing
  --features <list>    Comma-separated list of features to enable

DESCRIPTION:
  Initializes the Ouroboros system with all configured engines and features.
  Validates configuration and sets up dependency injection container.

EXAMPLES:
  ouroboros init
  ouroboros init --config production.json
  ouroboros init --features memory,planning,reasoning
";

        public const string ExecuteUsage = @"
EXECUTE - Execute a goal with full cognitive pipeline

USAGE:
  ouroboros execute <goal> [options]

ARGUMENTS:
  <goal>               Goal description (required)

OPTIONS:
  --memory             Use episodic memory (default: true)
  --no-memory          Disable episodic memory
  --planning           Use hierarchical planning (default: true)
  --no-planning        Disable hierarchical planning
  --causal             Use causal reasoning (default: true)
  --no-causal          Disable causal reasoning
  --world-model        Use world model simulation
  --depth <n>          Maximum planning depth (default: 10)
  --timeout <seconds>  Execution timeout

DESCRIPTION:
  Executes a goal using the full cognitive pipeline:
  1. Retrieve similar episodes from episodic memory
  2. Analyze causal relationships
  3. Generate hierarchical plan
  4. Execute plan steps
  5. Store new episodes

EXAMPLES:
  ouroboros execute ""Analyze system performance""
  ouroboros execute ""Optimize query"" --no-memory --depth 5
  ouroboros execute ""Plan deployment"" --world-model --planning
";

        public const string ReasonUsage = @"
REASON - Perform multi-engine reasoning

USAGE:
  ouroboros reason <query> [options]

ARGUMENTS:
  <query>              Query or question (required)

OPTIONS:
  --symbolic           Use symbolic reasoning (default: true)
  --no-symbolic        Disable symbolic reasoning
  --causal             Use causal inference (default: true)
  --no-causal          Disable causal inference
  --abductive          Use abductive reasoning (default: true)
  --no-abductive       Disable abductive reasoning
  --steps <n>          Maximum inference steps (default: 100)

DESCRIPTION:
  Performs reasoning by combining multiple engines:
  - Symbolic: MeTTa-based logical reasoning
  - Causal: Pearl's causal inference framework
  - Abductive: Generate explanatory hypotheses

EXAMPLES:
  ouroboros reason ""What causes performance degradation?""
  ouroboros reason ""Why did the system fail?"" --causal --abductive
  ouroboros reason ""Prove theorem X"" --symbolic --steps 500
";

        public const string LoopUsage = @"
LOOP - Autonomous cognitive loop operations

USAGE:
  ouroboros loop <command> [options]

COMMANDS:
  start                Start the cognitive loop
  stop                 Stop the cognitive loop
  pause                Pause the cognitive loop
  resume               Resume a paused loop
  status               Get loop status

OPTIONS (for start):
  --interval <ms>      Cycle interval in milliseconds (default: 1000)
  --goals <n>          Maximum concurrent goals (default: 5)
  --learning           Enable autonomous learning (default: true)

DESCRIPTION:
  Manages the autonomous cognitive loop that continuously:
  - Perceives current state
  - Reasons about goals
  - Acts on decisions
  - Learns from outcomes

EXAMPLES:
  ouroboros loop start
  ouroboros loop start --interval 500 --goals 10
  ouroboros loop stop
  ouroboros loop status
";

        public const string StatusUsage = @"
STATUS - Get system status

USAGE:
  ouroboros status [options]

OPTIONS:
  --verbose, -v        Show detailed status
  --json               Output in JSON format
  --watch              Continuously update status

DESCRIPTION:
  Displays current status of all Ouroboros engines and components.
  Shows which features are enabled, performance metrics, and health status.

EXAMPLES:
  ouroboros status
  ouroboros status --verbose
  ouroboros status --json
  ouroboros status --watch
";

        public const string ConfigUsage = @"
CONFIG - Configuration management

USAGE:
  ouroboros config <command> [options]

COMMANDS:
  validate             Validate configuration file
  show                 Display current configuration
  set                  Set configuration value
  reset                Reset to defaults

OPTIONS:
  --config <path>      Configuration file path
  --key <key>          Configuration key (for set/show)
  --value <value>      Configuration value (for set)

DESCRIPTION:
  Manages Ouroboros configuration with validation and inspection tools.

EXAMPLES:
  ouroboros config validate
  ouroboros config validate --config production.json
  ouroboros config show --key Features.EnableMeTTa
  ouroboros config set --key Planning.MaxDepth --value 20
";
    }

    /// <summary>
    /// Prints help for all commands.
    /// </summary>
    public static void PrintHelp()
    {
        Console.WriteLine(Commands.Help);
    }

    /// <summary>
    /// Prints usage for a specific command.
    /// </summary>
    public static void PrintCommandHelp(string command)
    {
        var usage = command.ToLowerInvariant() switch
        {
            "init" => Commands.InitUsage,
            "execute" => Commands.ExecuteUsage,
            "reason" => Commands.ReasonUsage,
            "loop" => Commands.LoopUsage,
            "status" => Commands.StatusUsage,
            "config" => Commands.ConfigUsage,
            _ => $"Unknown command: {command}\n\n{Commands.Help}"
        };
        
        Console.WriteLine(usage);
    }
}
