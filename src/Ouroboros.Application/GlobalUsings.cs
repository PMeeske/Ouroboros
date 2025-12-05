#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// Core monadic and functional programming
// System imports
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
// Agent
global using LangChainPipeline.Agent;
// CLI
global using Ouroboros.Application;
global using LangChainPipeline.Core.Kleisli;
global using LangChainPipeline.Core.Monads;
global using LangChainPipeline.Core.Steps;
// Domain models and state management
global using LangChainPipeline.Domain;
global using LangChainPipeline.Domain.Events;
global using LangChainPipeline.Domain.States;
global using LangChainPipeline.Domain.Vectors;
// Pipeline components
global using LangChainPipeline.Pipeline.Branches;
global using LangChainPipeline.Pipeline.Ingestion;
global using LangChainPipeline.Pipeline.Reasoning;
global using LangChainPipeline.Providers;
// Tools and providers
global using Ouroboros.Tools;
