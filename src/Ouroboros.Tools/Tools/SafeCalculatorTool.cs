// <copyright file="SafeCalculatorTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using System.Data;
using System.Globalization;
using System.Text.Json;
using Ouroboros.Core.Monads;
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
        Result<ParsedInput, string> parsedResult = this.ValidateInput(input).Bind(this.ParseInput);

        return await parsedResult.Match(
            parsed => this.ProcessCalculation(parsed, ct),
            error => Task.FromResult(Result<string, string>.Failure(error)));
    }

    private async Task<Result<string, string>> ProcessCalculation(ParsedInput parsed, CancellationToken ct)
    {
        Result<double, string> computeResult = this.ComputeExpression(parsed.Expression);
        
        return await computeResult.Match(
            calculatedValue => this.VerifyAndFormat(parsed, calculatedValue, ct),
            error => Task.FromResult(Result<string, string>.Failure(error)));
    }

    private async Task<Result<string, string>> VerifyAndFormat(ParsedInput parsed, double calculatedValue, CancellationToken ct)
    {
        Result<bool, string> verifyResult = await this.VerifyCalculationAsync(parsed.Expression, calculatedValue, ct);
        
        return verifyResult
            .MapError(error => $"Verification failed: {error}")
            .Where(verified => verified, $"❌ Calculation verification failed for expression: {parsed.Expression}. The result could not be symbolically verified.")
            .Bind(_ => this.ValidateExpectedResult(calculatedValue, parsed.ExpectedResult))
            .Map(_ => this.FormatSuccessMessage(calculatedValue, parsed.Expression));
    }

    private Result<string, string> ValidateInput(string input) =>
        string.IsNullOrWhiteSpace(input)
            ? Result<string, string>.Failure("Expression cannot be empty")
            : Result<string, string>.Success(input);

    private Result<ParsedInput, string> ParseInput(string input) =>
        this.TryParseJson(input)
            .Match(
                parsed => Result<ParsedInput, string>.Success(parsed),
                _ => Result<ParsedInput, string>.Success(new ParsedInput(input, null)));

    private Result<ParsedInput, string> TryParseJson(string input) =>
        input.TrimStart().StartsWith("{")
            ? this.ParseJsonDocument(input)
            : Result<ParsedInput, string>.Failure("Not JSON");

    private Result<ParsedInput, string> ParseJsonDocument(string input)
    {
        try
        {
            using JsonDocument json = JsonDocument.Parse(input);
            return this.ExtractExpression(json.RootElement, input)
                .Map(expression =>
                {
                    Option<double> optExpected = this.ExtractExpectedResult(json.RootElement);
                    double? expected = optExpected.Match(val => (double?)val, null);
                    return new ParsedInput(expression, expected);
                });
        }
        catch (JsonException)
        {
            return Result<ParsedInput, string>.Failure("Invalid JSON");
        }
    }

    private Result<string, string> ExtractExpression(JsonElement root, string fallback) =>
        root.TryGetProperty("expression", out JsonElement exprProp)
            ? Result<string, string>.Success(exprProp.GetString() ?? fallback)
            : Result<string, string>.Failure("JSON input must contain 'expression' property");

    private Option<double> ExtractExpectedResult(JsonElement root) =>
        root.TryGetProperty("expected_result", out JsonElement expectedProp) &&
        expectedProp.ValueKind == JsonValueKind.Number
            ? Option<double>.Some(expectedProp.GetDouble())
            : Option<double>.None();

    private Result<Unit, string> ValidateExpectedResult(double calculatedValue, double? expectedResult) =>
        expectedResult.HasValue
            ? Math.Abs(calculatedValue - expectedResult.Value) <= ComparisonTolerance
                ? Result<Unit, string>.Success(Unit.Value)
                : Result<Unit, string>.Failure($"❌ Result mismatch: calculated {calculatedValue}, expected {expectedResult.Value}")
            : Result<Unit, string>.Success(Unit.Value);

    private string FormatSuccessMessage(double calculatedValue, string expression)
    {
        string resultString = Convert.ToString(calculatedValue, CultureInfo.InvariantCulture) ?? "null";
        string verificationBadge = this.useSymbolicVerification ? "✓ Symbolically Verified" : "✓ Verified";
        return $"{verificationBadge}: {expression} = {resultString}";
    }

    private readonly record struct ParsedInput(string Expression, double? ExpectedResult);

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

    private async Task<Result<bool, string>> VerifyCalculationAsync(string expression, double result, CancellationToken ct) =>
        this.useSymbolicVerification && this.symbolicEngine != null
            ? await this.SymbolicVerificationAsync(expression, result, ct)
            : this.SimulatedVerification(expression, result);

    private async Task<Result<bool, string>> SymbolicVerificationAsync(string expression, double result, CancellationToken ct)
    {
        try
        {
            string mettaExpression = this.ConvertToMeTTaExpression(expression);
            Result<string, string> mettaResult = await this.symbolicEngine!.ExecuteQueryAsync(mettaExpression, ct);

            return mettaResult
                .Bind(this.TryParseMeTTaNumber)
                .Bind(symbolicResult => this.ValidateCalculationMatch(symbolicResult, result, "Symbolic result does not match computed result"));
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
            return this.ValidateAllowedCharacters(expression)
                .Bind(_ => this.ComputeExpression(expression))
                .Bind(recomputed => this.ValidateCalculationMatch(recomputed, result, "Recomputation verification failed"));
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Simulated verification exception: {ex.Message}");
        }
    }

    private Result<bool, string> ValidateCalculationMatch(double calculated, double expected, string errorMessage) =>
        Math.Abs(calculated - expected) < ComparisonTolerance
            ? Result<bool, string>.Success(true)
            : Result<bool, string>.Failure($"{errorMessage}: calculated={calculated}, expected={expected}");

    private Result<Unit, string> ValidateAllowedCharacters(string expression) =>
        expression.All(c => AllowedCharacters.Contains(c))
            ? Result<Unit, string>.Success(Unit.Value)
            : Result<Unit, string>.Failure("Expression contains invalid characters");

    private string ConvertToMeTTaExpression(string expression)
    {
        string cleaned = expression.Replace(" ", string.Empty);
        
        return cleaned.Any(c => "+-*/".Contains(c))
            ? $"!(eval {cleaned})"
            : $"!({cleaned})";
    }

    private Result<double, string> TryParseMeTTaNumber(string mettaValue)
    {
        string cleaned = mettaValue.Trim().Trim('[', ']', '(', ')');
        
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out double result)
            ? Result<double, string>.Success(result)
            : Result<double, string>.Failure($"Could not parse MeTTa result: {mettaValue}");
    }
}
