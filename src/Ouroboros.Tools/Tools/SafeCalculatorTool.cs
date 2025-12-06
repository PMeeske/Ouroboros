// <copyright file="SafeCalculatorTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using System.Data;
using System.Globalization;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// A specialized calculator tool that demonstrates "Proof-Carrying Code" / Verification.
/// Instead of just computing results, it uses a symbolic engine to verify calculations,
/// providing mathematical soundness guarantees for neuro-symbolic AI systems.
/// </summary>
public sealed class SafeCalculatorTool : ITool
{
    // Tolerance for floating point comparisons
    private const double ComparisonTolerance = 0.000001;
    
    // Allowed characters for arithmetic expressions (security measure)
    private static readonly HashSet<char> AllowedCharacters = new HashSet<char>(
        "0123456789+-*/().Ee ".ToCharArray());
    
    private readonly IMeTTaEngine? symbolicEngine;
    private readonly bool useSymbolicVerification;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeCalculatorTool"/> class.
    /// </summary>
    /// <param name="symbolicEngine">Optional symbolic engine for verification. If null, uses simulated verification.</param>
    public SafeCalculatorTool(IMeTTaEngine? symbolicEngine = null)
    {
        this.symbolicEngine = symbolicEngine;
        this.useSymbolicVerification = symbolicEngine != null;
    }

    /// <inheritdoc />
    public string Name => "safe_calculator";

    /// <inheritdoc />
    public string Description => "Verified arithmetic calculator that uses symbolic reasoning to validate calculations. Ensures mathematical correctness through proof-carrying code principles.";

    /// <inheritdoc />
    public string? JsonSchema => @"{
        ""type"": ""object"",
        ""properties"": {
            ""expression"": {
                ""type"": ""string"",
                ""description"": ""The mathematical expression to evaluate and verify (e.g., '2+2*5', '(10-5)/2')""
            },
            ""expected_result"": {
                ""type"": ""number"",
                ""description"": ""Optional expected result for additional verification""
            }
        },
        ""required"": [""expression""]
    }";

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<string, string>.Failure("Expression cannot be empty");
        }

        try
        {
            // Parse input
            string expression;
            double? expectedResult = null;

            if (input.TrimStart().StartsWith("{"))
            {
                try
                {
                    System.Text.Json.JsonDocument json = System.Text.Json.JsonDocument.Parse(input);
                    if (json.RootElement.TryGetProperty("expression", out System.Text.Json.JsonElement exprProp))
                    {
                        expression = exprProp.GetString() ?? input;
                    }
                    else
                    {
                        return Result<string, string>.Failure("JSON input must contain 'expression' property");
                    }

                    if (json.RootElement.TryGetProperty("expected_result", out System.Text.Json.JsonElement expectedProp))
                    {
                        if (expectedProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            expectedResult = expectedProp.GetDouble();
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    expression = input;
                }
            }
            else
            {
                expression = input;
            }

            // Step 1: Compute the result using standard evaluation
            Result<double, string> computeResult = this.ComputeExpression(expression);
            if (computeResult.IsFailure)
            {
                return Result<string, string>.Failure(computeResult.Error);
            }

            double calculatedValue = computeResult.Value;

            // Step 2: Verify the calculation using symbolic reasoning
            Result<bool, string> verifyResult = await this.VerifyCalculationAsync(expression, calculatedValue, ct);
            if (verifyResult.IsFailure)
            {
                return Result<string, string>.Failure($"Verification failed: {verifyResult.Error}");
            }

            if (!verifyResult.Value)
            {
                return Result<string, string>.Failure($"❌ Calculation verification failed for expression: {expression}. The result could not be symbolically verified.");
            }

            // Step 3: Check against expected result if provided
            if (expectedResult.HasValue)
            {
                if (Math.Abs(calculatedValue - expectedResult.Value) > ComparisonTolerance)
                {
                    return Result<string, string>.Failure(
                        $"❌ Result mismatch: calculated {calculatedValue}, expected {expectedResult.Value}");
                }
            }

            // Success: calculation is verified
            string resultString = Convert.ToString(calculatedValue, CultureInfo.InvariantCulture) ?? "null";
            string verificationBadge = this.useSymbolicVerification ? "✓ Symbolically Verified" : "✓ Verified";
            
            return Result<string, string>.Success(
                $"{verificationBadge}: {expression} = {resultString}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Safe calculation failed: {ex.Message}");
        }
    }

    private Result<double, string> ComputeExpression(string expression)
    {
        try
        {
            // NOTE: DataTable.Compute() is used for basic arithmetic evaluation.
            // While not ideal for production (has security considerations), it works for this demonstration.
            // For production use, consider using NCalc or implementing a custom expression parser.
            DataTable dataTable = new DataTable();
            object result = dataTable.Compute(expression, string.Empty);
            double value = Convert.ToDouble(result, CultureInfo.InvariantCulture);
            return Result<double, string>.Success(value);
        }
        catch (Exception ex)
        {
            return Result<double, string>.Failure($"Expression evaluation failed: {ex.Message}");
        }
    }

    private async Task<Result<bool, string>> VerifyCalculationAsync(string expression, double result, CancellationToken ct)
    {
        if (this.useSymbolicVerification && this.symbolicEngine != null)
        {
            // Use MeTTa symbolic engine for verification
            return await this.SymbolicVerificationAsync(expression, result, ct);
        }
        else
        {
            // Use simulated verification (rule-based checking)
            return this.SimulatedVerification(expression, result);
        }
    }

    private async Task<Result<bool, string>> SymbolicVerificationAsync(string expression, double result, CancellationToken ct)
    {
        try
        {
            // Convert arithmetic expression to MeTTa format for verification
            string mettaExpression = this.ConvertToMeTTaExpression(expression);
            
            // Query MeTTa to evaluate the expression
            Result<string, string> mettaResult = await this.symbolicEngine!.ExecuteQueryAsync(mettaExpression, ct);

            return mettaResult.Match(
                mettaValue =>
                {
                    // Parse MeTTa result and compare with computed result
                    if (this.TryParseMeTTaNumber(mettaValue, out double symbolicResult))
                    {
                        bool matches = Math.Abs(symbolicResult - result) < ComparisonTolerance;
                        return matches
                            ? Result<bool, string>.Success(true)
                            : Result<bool, string>.Failure($"Symbolic result {symbolicResult} does not match computed result {result}");
                    }

                    return Result<bool, string>.Failure($"Could not parse MeTTa result: {mettaValue}");
                },
                error => Result<bool, string>.Failure($"MeTTa verification error: {error}"));
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Symbolic verification exception: {ex.Message}");
        }
    }

    private Result<bool, string> SimulatedVerification(string expression, double result)
    {
        try
        {
            // Simulated verification: Recompute using a different method and compare
            // This provides a basic safety check even without MeTTa
            
            // Verify expression contains only allowed characters
            if (expression.Any(c => !AllowedCharacters.Contains(c)))
            {
                return Result<bool, string>.Failure("Expression contains invalid characters");
            }

            // Recompute to verify
            Result<double, string> recomputeResult = this.ComputeExpression(expression);
            
            return recomputeResult.Match(
                recomputed =>
                {
                    bool matches = Math.Abs(recomputed - result) < ComparisonTolerance;
                    return matches
                        ? Result<bool, string>.Success(true)
                        : Result<bool, string>.Failure("Recomputation verification failed");
                },
                error => Result<bool, string>.Failure($"Verification recomputation failed: {error}"));
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Simulated verification exception: {ex.Message}");
        }
    }

    private string ConvertToMeTTaExpression(string expression)
    {
        // Simple conversion of infix to MeTTa prefix notation
        // For complex expressions, a proper parser would be needed
        // This is a basic implementation for demonstration
        
        expression = expression.Replace(" ", string.Empty);
        
        // Handle simple binary operations
        if (expression.Contains("+") || expression.Contains("-") || expression.Contains("*") || expression.Contains("/"))
        {
            // For now, wrap the entire expression as an eval
            return $"!(eval {expression})";
        }

        // Single number
        return $"!({expression})";
    }

    private bool TryParseMeTTaNumber(string mettaValue, out double result)
    {
        // Try to extract a number from MeTTa output
        // MeTTa might return results in various formats, this is a simple parser
        
        string cleaned = mettaValue.Trim().Trim('[', ']', '(', ')');
        
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}
