// <copyright file="EthicsEnforcementWrapper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Interface for action executors that can be wrapped with ethics enforcement.
/// </summary>
/// <typeparam name="TAction">The type of action to execute.</typeparam>
/// <typeparam name="TResult">The type of result produced.</typeparam>
public interface IActionExecutor<in TAction, TResult>
{
    /// <summary>
    /// Executes an action and returns the result.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of execution.</returns>
    Task<Result<TResult, string>> ExecuteAsync(TAction action, CancellationToken ct = default);
}

/// <summary>
/// Generic wrapper that enforces ethical evaluation before action execution.
/// This wrapper ensures NO action is executed without ethical clearance.
/// </summary>
/// <typeparam name="TAction">The type of action being executed.</typeparam>
/// <typeparam name="TResult">The type of result produced by execution.</typeparam>
public sealed class EthicsEnforcementWrapper<TAction, TResult> : IActionExecutor<TAction, TResult>
{
    private readonly IActionExecutor<TAction, TResult> _innerExecutor;
    private readonly IEthicsFramework _ethicsFramework;
    private readonly Func<TAction, ProposedAction> _actionConverter;
    private readonly ActionContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="EthicsEnforcementWrapper{TAction, TResult}"/> class.
    /// </summary>
    /// <param name="innerExecutor">The underlying executor to wrap.</param>
    /// <param name="ethicsFramework">The ethics framework for evaluation.</param>
    /// <param name="actionConverter">Function to convert TAction to ProposedAction.</param>
    /// <param name="context">The action context for evaluation.</param>
    public EthicsEnforcementWrapper(
        IActionExecutor<TAction, TResult> innerExecutor,
        IEthicsFramework ethicsFramework,
        Func<TAction, ProposedAction> actionConverter,
        ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(innerExecutor);
        ArgumentNullException.ThrowIfNull(ethicsFramework);
        ArgumentNullException.ThrowIfNull(actionConverter);
        ArgumentNullException.ThrowIfNull(context);

        _innerExecutor = innerExecutor;
        _ethicsFramework = ethicsFramework;
        _actionConverter = actionConverter;
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<Result<TResult, string>> ExecuteAsync(TAction action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            // Convert action to ProposedAction for ethical evaluation
            var proposedAction = _actionConverter(action);

            // Evaluate action ethically
            var evaluationResult = await _ethicsFramework.EvaluateActionAsync(proposedAction, _context, ct);

            if (evaluationResult.IsFailure)
            {
                return Result<TResult, string>.Failure($"Ethics evaluation failed: {evaluationResult.Error}");
            }

            var clearance = evaluationResult.Value;

            // Block execution if not permitted
            if (!clearance.IsPermitted)
            {
                var reason = clearance.Level == EthicalClearanceLevel.Denied
                    ? $"Action blocked due to ethical violations: {clearance.Reasoning}"
                    : $"Action requires human approval: {clearance.Reasoning}";

                return Result<TResult, string>.Failure(reason);
            }

            // Log concerns if any
            if (clearance.Concerns.Count > 0)
            {
                // In a real implementation, this would log to a monitoring system
                System.Diagnostics.Debug.WriteLine(
                    $"Action executed with {clearance.Concerns.Count} ethical concern(s)");
            }

            // Execute the action
            return await _innerExecutor.ExecuteAsync(action, ct);
        }
        catch (Exception ex)
        {
            return Result<TResult, string>.Failure($"Execution failed: {ex.Message}");
        }
    }
}
