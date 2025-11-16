// <copyright file="MathTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

using System.Data;
using System.Globalization;

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
            DataTable dataTable = new DataTable();
            object result = dataTable.Compute(input, string.Empty);

            // Use InvariantCulture to ensure consistent decimal separator (always '.')
            string resultString = Convert.ToString(result, CultureInfo.InvariantCulture) ?? "null";
            return Task.FromResult(Result<string, string>.Success(resultString));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Math evaluation failed: {ex.Message}"));
        }
    }
}
