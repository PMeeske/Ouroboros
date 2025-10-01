using System.Data;

namespace LangChainPipeline.Tools;

/// <summary>
/// A tool for evaluating simple arithmetic expressions using DataTable.Compute.
/// </summary>
public sealed class MathTool : ITool
{
    /// <inheritdoc />
    public string Name => "math";
    
    /// <inheritdoc />
    public string Description => "Evaluates simple arithmetic expressions like '2+2*5' or '(10-5)/2'";
    
    /// <inheritdoc />
    public string? JsonSchema => null; // Accepts free-form string expressions
    
    /// <inheritdoc />
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(Result<string, string>.Failure("Input expression cannot be empty"));
        }

        try
        {
            var dataTable = new DataTable();
            var result = dataTable.Compute(input, string.Empty);
            return Task.FromResult(Result<string, string>.Success(result?.ToString() ?? "null"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Math evaluation failed: {ex.Message}"));
        }
    }
}