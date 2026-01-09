// <copyright file="SpeechQueue.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Speech request for the queue.
/// </summary>
public sealed record SpeechRequest(
    string Text,
    string Persona,
    TaskCompletionSource<bool>? Completion = null);

/// <summary>
/// Rx-based speech queue that serializes voice output across all sources.
/// Ensures voices don't overlap and messages play in order.
/// </summary>
public sealed class SpeechQueue : IDisposable
{
    private static readonly Lazy<SpeechQueue> _instance = new(() => new SpeechQueue());

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static SpeechQueue Instance => _instance.Value;

    private readonly Subject<SpeechRequest> _speechSubject = new();
    private readonly IDisposable _subscription;
    private Func<string, string, CancellationToken, Task>? _synthesizer;
    private bool _disposed;

    private SpeechQueue()
    {
        // Process speech requests sequentially using Rx Concat
        _subscription = _speechSubject
            .Select(request => Observable.FromAsync(async ct =>
            {
                if (_synthesizer != null && !string.IsNullOrWhiteSpace(request.Text))
                {
                    try
                    {
                        await _synthesizer(request.Text, request.Persona, ct);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  [SpeechQueue] Error: {ex.Message}");
                    }
                }

                request.Completion?.TrySetResult(true);
                return request;
            }))
            .Concat() // Process one at a time, in order
            .Subscribe(
                _ => { },
                ex => Console.WriteLine($"  [SpeechQueue] Stream error: {ex.Message}"));
    }

    /// <summary>
    /// Sets the speech synthesizer function.
    /// </summary>
    public void SetSynthesizer(Func<string, string, CancellationToken, Task> synthesizer)
    {
        _synthesizer = synthesizer;
    }

    /// <summary>
    /// Enqueues speech (fire-and-forget).
    /// </summary>
    public void Enqueue(string text, string persona = "Ouroboros")
    {
        if (!_disposed && !string.IsNullOrWhiteSpace(text))
        {
            _speechSubject.OnNext(new SpeechRequest(text, persona));
        }
    }

    /// <summary>
    /// Enqueues speech and waits for completion.
    /// </summary>
    public async Task EnqueueAndWaitAsync(string text, string persona = "Ouroboros", CancellationToken ct = default)
    {
        if (_disposed || string.IsNullOrWhiteSpace(text)) return;

        var tcs = new TaskCompletionSource<bool>();
        using var registration = ct.Register(() => tcs.TrySetCanceled());

        _speechSubject.OnNext(new SpeechRequest(text, persona, tcs));

        await tcs.Task;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _speechSubject.OnCompleted();
        _subscription.Dispose();
        _speechSubject.Dispose();
    }
}
