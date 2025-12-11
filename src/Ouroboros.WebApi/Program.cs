// <copyright file="Program.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using LangChainPipeline.Agent.MetaAI.SelfModel;
using Microsoft.OpenApi.Models;
using Ouroboros.WebApi.Models;
using Ouroboros.WebApi.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ouroboros API",
        Version = "v1",
        Description = "Kubernetes-friendly ASP.NET Core Web API for Ouroboros - A functional programming-based AI pipeline system",
    });
});

// Register pipeline service
builder.Services.AddSingleton<IPipelineService, PipelineService>();

// Register self-model components (Phase 2)
builder.Services.AddSingleton<IIdentityGraph>(sp => new IdentityGraph(
    Guid.NewGuid(),
    "OuroborosAgent",
    sp.GetService<ICapabilityRegistry>() ?? throw new InvalidOperationException("ICapabilityRegistry not registered"),
    Path.Combine(Path.GetTempPath(), "ouroboros_identity.json")));

builder.Services.AddSingleton<IGlobalWorkspace>(sp => new GlobalWorkspace());
builder.Services.AddSingleton<IPredictiveMonitor>(sp => new PredictiveMonitor());
builder.Services.AddSingleton<ISelfModelService, SelfModelService>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Only allow unrestricted access in local development
        if (LangChainPipeline.Core.EnvironmentDetector.IsLocalDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // In production/staging, configure specific origins
            // This is a placeholder - configure with actual allowed origins
            policy.WithOrigins("https://yourdomain.com")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Add health checks for Kubernetes
builder.Services.AddHealthChecks();

WebApplication app = builder.Build();

// Configure middleware
// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ouroboros API v1");
    c.RoutePrefix = "swagger"; // Serve Swagger UI at /swagger
});

app.UseCors();

// Health check endpoints for Kubernetes
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");

// Root endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "Ouroboros Web API",
    version = "1.0.0",
    status = "running",
    environment = new
    {
        name = LangChainPipeline.Core.EnvironmentDetector.GetEnvironmentName() ?? "Unknown",
        isLocalDevelopment = LangChainPipeline.Core.EnvironmentDetector.IsLocalDevelopment(),
        isProduction = LangChainPipeline.Core.EnvironmentDetector.IsProduction(),
        isStaging = LangChainPipeline.Core.EnvironmentDetector.IsStaging(),
        isKubernetes = LangChainPipeline.Core.EnvironmentDetector.IsRunningInKubernetes(),
    },
    endpoints = new[]
    {
        "/health - Health check endpoint",
        "/ready - Readiness check endpoint",
        "/api/ask - Ask a question to the AI",
        "/api/pipeline - Execute a pipeline DSL",
        "/api/self/state - Get agent identity state",
        "/api/self/forecast - Get forecasts and predictions",
        "/api/self/commitments - Get active commitments",
        "/api/self/explain - Generate self-explanation from DAG",
        "/swagger - API documentation"
    },
}))
.WithName("Root")
.WithTags("System")
.Produces(200);

// Ask endpoint - Main question answering
app.MapPost("/api/ask", async (AskRequest request, IPipelineService service, CancellationToken ct) =>
{
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        string answer = await service.AskAsync(request, ct);
        sw.Stop();

        return Results.Ok(ApiResponse<AskResponse>.Ok(
            new AskResponse
            {
                Answer = answer,
                Model = request.Model ?? "llama3",
            },
            sw.ElapsedMilliseconds));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<AskResponse>.Fail($"Error: {ex.Message}"));
    }
})
.WithName("Ask")
.WithTags("AI Pipeline")
.WithOpenApi(op =>
{
    op.Summary = "Ask a question to the AI pipeline";
    op.Description = "Supports both simple question-answering and RAG (Retrieval Augmented Generation) mode.";
    return op;
})
.Produces<ApiResponse<AskResponse>>(200)
.Produces<ApiResponse<AskResponse>>(400);

// Pipeline endpoint - Execute DSL pipeline
app.MapPost("/api/pipeline", async (PipelineRequest request, IPipelineService service, CancellationToken ct) =>
{
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        string result = await service.ExecutePipelineAsync(request, ct);
        sw.Stop();

        return Results.Ok(ApiResponse<PipelineResponse>.Ok(
            new PipelineResponse
            {
                Result = result,
                FinalState = "Completed",
            },
            sw.ElapsedMilliseconds));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<PipelineResponse>.Fail($"Error: {ex.Message}"));
    }
})
.WithName("ExecutePipeline")
.WithTags("AI Pipeline")
.WithOpenApi(op =>
{
    op.Summary = "Execute a pipeline using DSL";
    op.Description = "Execute a pipeline using DSL syntax (e.g., 'SetTopic(\"AI\") | UseDraft | UseCritique | UseImprove')";
    return op;
})
.Produces<ApiResponse<PipelineResponse>>(200)
.Produces<ApiResponse<PipelineResponse>>(400);

// Phase 2 Self-Model Endpoints

// Get agent identity state
app.MapGet("/api/self/state", async (ISelfModelService service, CancellationToken ct) =>
{
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        SelfStateResponse state = await service.GetStateAsync(ct);
        sw.Stop();

        return Results.Ok(ApiResponse<SelfStateResponse>.Ok(state, sw.ElapsedMilliseconds));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<SelfStateResponse>.Fail($"Error: {ex.Message}"));
    }
})
.WithName("GetSelfState")
.WithTags("Self-Model")
.WithOpenApi(op =>
{
    op.Summary = "Get current agent identity state";
    op.Description = "Returns the agent's complete identity state including capabilities, resources, commitments, and performance metrics.";
    return op;
})
.Produces<ApiResponse<SelfStateResponse>>(200)
.Produces<ApiResponse<SelfStateResponse>>(400);

// Get forecasts and predictions
app.MapGet("/api/self/forecast", async (ISelfModelService service, CancellationToken ct) =>
{
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        SelfForecastResponse forecasts = await service.GetForecastsAsync(ct);
        sw.Stop();

        return Results.Ok(ApiResponse<SelfForecastResponse>.Ok(forecasts, sw.ElapsedMilliseconds));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<SelfForecastResponse>.Fail($"Error: {ex.Message}"));
    }
})
.WithName("GetSelfForecasts")
.WithTags("Self-Model")
.WithOpenApi(op =>
{
    op.Summary = "Get agent forecasts and predictions";
    op.Description = "Returns pending forecasts, calibration metrics, and recent anomalies detected by the predictive monitor.";
    return op;
})
.Produces<ApiResponse<SelfForecastResponse>>(200)
.Produces<ApiResponse<SelfForecastResponse>>(400);

// Get active commitments
app.MapGet("/api/self/commitments", async (ISelfModelService service, CancellationToken ct) =>
{
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<CommitmentDto> commitments = await service.GetCommitmentsAsync(ct);
        sw.Stop();

        return Results.Ok(ApiResponse<List<CommitmentDto>>.Ok(commitments, sw.ElapsedMilliseconds));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<List<CommitmentDto>>.Fail($"Error: {ex.Message}"));
    }
})
.WithName("GetSelfCommitments")
.WithTags("Self-Model")
.WithOpenApi(op =>
{
    op.Summary = "Get active agent commitments";
    op.Description = "Returns all active commitments with their status, progress, and priority.";
    return op;
})
.Produces<ApiResponse<List<CommitmentDto>>>(200)
.Produces<ApiResponse<List<CommitmentDto>>>(400);

// Generate self-explanation
app.MapPost("/api/self/explain", async (SelfExplainRequest request, ISelfModelService service, CancellationToken ct) =>
{
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        SelfExplainResponse explanation = await service.ExplainAsync(request, ct);
        sw.Stop();

        return Results.Ok(ApiResponse<SelfExplainResponse>.Ok(explanation, sw.ElapsedMilliseconds));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<SelfExplainResponse>.Fail($"Error: {ex.Message}"));
    }
})
.WithName("SelfExplain")
.WithTags("Self-Model")
.WithOpenApi(op =>
{
    op.Summary = "Generate self-explanation from execution DAG";
    op.Description = "Generates a narrative explanation of the agent's execution history and current state based on the execution DAG.";
    return op;
})
.Produces<ApiResponse<SelfExplainResponse>>(200)
.Produces<ApiResponse<SelfExplainResponse>>(400);

app.Run();
