// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Abstractions;

using Ouroboros.CLI.Commands;

/// <summary>
/// Central holder for agent-wide configuration and, in future steps, shared services.
/// Introduced as a stable seam for extracting controllers/builders without changing behavior.
/// </summary>
public sealed record AgentContext(
    OuroborosConfig Config);
