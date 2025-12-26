#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Core monadic and functional programming
// System imports
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
// Agent
global using Ouroboros.Agent;
// CLI
global using Ouroboros.Application;
global using Ouroboros.Core.Kleisli;
global using Ouroboros.Core.Monads;
global using Ouroboros.Core.Steps;
// Domain models and state management
global using Ouroboros.Domain;
global using Ouroboros.Domain.Events;
global using Ouroboros.Domain.States;
global using Ouroboros.Domain.Vectors;
// Pipeline components
global using Ouroboros.Pipeline.Branches;
global using Ouroboros.Pipeline.Ingestion;
global using Ouroboros.Pipeline.Reasoning;
global using Ouroboros.Providers;
// Tools and providers
global using Ouroboros.Tools;
