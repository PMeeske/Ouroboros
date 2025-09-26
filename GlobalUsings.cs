// ==========================================================
// Global Using Statements for Enhanced LangChain Pipeline
// Ensures all namespaces are available throughout the project
// ==========================================================

// Core monadic and functional programming
global using LangChainPipeline.Core;
global using LangChainPipeline.Core.Monads;
global using LangChainPipeline.Core.Steps;
global using LangChainPipeline.Core.Kleisli;
global using LangChainPipeline.Core.Interop;

// Domain models and state management
global using LangChainPipeline.Domain;
global using LangChainPipeline.Domain.States;
global using LangChainPipeline.Domain.Events;
global using LangChainPipeline.Domain.Vectors;

// Pipeline components
global using LangChainPipeline.Pipeline.Branches;
global using LangChainPipeline.Pipeline.Reasoning;
global using LangChainPipeline.Pipeline.Ingestion;
global using LangChainPipeline.Pipeline.Replay;

// Tools and providers
global using LangChainPipeline.Tools;
global using LangChainPipeline.Providers;

// Examples and demonstrations
global using LangChainPipeline.Examples;

// System imports
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;