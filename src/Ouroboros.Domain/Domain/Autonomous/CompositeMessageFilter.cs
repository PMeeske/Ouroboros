// <copyright file="CompositeMessageFilter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Composite message filter that chains multiple filters together.
/// A message is only routed if ALL filters approve it.
/// </summary>
public sealed class CompositeMessageFilter : IMessageFilter
{
    private readonly IReadOnlyList<IMessageFilter> _filters;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeMessageFilter"/> class.
    /// </summary>
    /// <param name="filters">The filters to chain together.</param>
    public CompositeMessageFilter(IReadOnlyList<IMessageFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        _filters = filters;
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldRouteAsync(NeuronMessage message, CancellationToken ct = default)
    {
        // All filters must approve for the message to be routed
        foreach (var filter in _filters)
        {
            if (!await filter.ShouldRouteAsync(message, ct))
            {
                return false;
            }
        }

        return true;
    }
}
