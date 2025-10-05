using System.Diagnostics;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MonadicPipeline API",
        Version = "v1",
        Description = "Kubernetes-friendly ASP.NET Core Web API for MonadicPipeline - A functional programming-based AI pipeline system"
    });
});

// Register pipeline service
builder.Services.AddSingleton<IPipelineService, PipelineService>();

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

var app = builder.Build();

// Configure middleware
// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MonadicPipeline API v1");
    c.RoutePrefix = "swagger"; // Serve Swagger UI at /swagger
});

app.UseCors();

// Health check endpoints for Kubernetes
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");

// Root endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "MonadicPipeline Web API",
    version = "1.0.0",
    status = "running",
    environment = new 
    {
        name = LangChainPipeline.Core.EnvironmentDetector.GetEnvironmentName() ?? "Unknown",
        isLocalDevelopment = LangChainPipeline.Core.EnvironmentDetector.IsLocalDevelopment(),
        isProduction = LangChainPipeline.Core.EnvironmentDetector.IsProduction(),
        isStaging = LangChainPipeline.Core.EnvironmentDetector.IsStaging(),
        isKubernetes = LangChainPipeline.Core.EnvironmentDetector.IsRunningInKubernetes()
    },
    endpoints = new[]
    {
        "/health - Health check endpoint",
        "/ready - Readiness check endpoint",
        "/api/ask - Ask a question to the AI",
        "/api/pipeline - Execute a pipeline DSL",
        "/swagger - API documentation"
    }
}))
.WithName("Root")
.WithTags("System")
.Produces(200);

// Ask endpoint - Main question answering
app.MapPost("/api/ask", async (AskRequest request, IPipelineService service, CancellationToken ct) =>
{
    try
    {
        var sw = Stopwatch.StartNew();
        var answer = await service.AskAsync(request, ct);
        sw.Stop();

        return Results.Ok(ApiResponse<AskResponse>.Ok(
            new AskResponse
            {
                Answer = answer,
                Model = request.Model ?? "llama3"
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
        var sw = Stopwatch.StartNew();
        var result = await service.ExecutePipelineAsync(request, ct);
        sw.Stop();

        return Results.Ok(ApiResponse<PipelineResponse>.Ok(
            new PipelineResponse
            {
                Result = result,
                FinalState = "Completed"
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

app.Run();

