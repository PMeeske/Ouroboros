// <copyright file="MockMeTTaEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.Shared.Mocks;

/// <summary>
/// Mock MeTTa engine for testing without requiring actual MeTTa installation.
/// </summary>
public sealed class MockMeTTaEngine : IMeTTaEngine
{
    private readonly List<string> facts = new();

    public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        // Simulate simple query responses
        var result = query switch
        {
            "(+ 1 2)" => "3",
            _ => $"[Result of: {query}]",
        };

        return Task.FromResult(Result<string, string>.Success(result));
    }

    public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        this.facts.Add(fact);
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        return Task.FromResult(Result<string, string>.Success($"Rule applied: {rule}"));
    }

    public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        // Simple mock verification - always returns true
        return Task.FromResult(Result<bool, string>.Success(true));
    }

    public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        this.facts.Clear();
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    public void Dispose()
    {
        // Nothing to dispose in mock
    }
}
