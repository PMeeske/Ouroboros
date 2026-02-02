// <copyright file="AgentPresenceController.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ouroboros.Domain.Voice;

/// <summary>
/// Manages agent presence state transitions as a reactive state machine.
/// Orchestrates the flow between Idle → Listening → Processing → Speaking.
/// Handles barge-in (user interruption) and provides cancellation tokens for each phase.
/// </summary>
public sealed class AgentPresenceController : IDisposable
{
    private readonly InteractionStream _stream;
    private readonly BehaviorSubject<AgentPresenceState> _state;
    private readonly CompositeDisposable _disposables = new();

    private CancellationTokenSource? _currentSpeechCts;
    private CancellationTokenSource? _currentProcessingCts;
    private CancellationTokenSource? _currentListeningCts;

    private readonly TimeSpan _bargeInDebounce = TimeSpan.FromMilliseconds(200);
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(5);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentPresenceController"/> class.
    /// </summary>
    /// <param name="stream">The interaction stream to observe and control.</param>
    public AgentPresenceController(InteractionStream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _state = new BehaviorSubject<AgentPresenceState>(AgentPresenceState.Idle);

        WireUpStateTransitions();
        WireUpControlEvents();
    }

    /// <summary>
    /// Gets the current state as an observable.
    /// </summary>
    public IObservable<AgentPresenceState> State => _state.AsObservable();

    /// <summary>
    /// Gets the current state synchronously.
    /// </summary>
    public AgentPresenceState CurrentState => _state.Value;

    /// <summary>
    /// Gets a cancellation token that is cancelled when speech is interrupted.
    /// </summary>
    public CancellationToken SpeechCancellation =>
        _currentSpeechCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Gets a cancellation token that is cancelled when processing is cancelled.
    /// </summary>
    public CancellationToken ProcessingCancellation =>
        _currentProcessingCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Gets a cancellation token that is cancelled when listening stops.
    /// </summary>
    public CancellationToken ListeningCancellation =>
        _currentListeningCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    public event EventHandler<StateChangeEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when barge-in is detected.
    /// </summary>
    public event EventHandler<BargeInEventArgs>? BargeInDetected;

    private void WireUpStateTransitions()
    {
        // Audio chunks arriving -> Transition to Listening (if Idle)
        _disposables.Add(
            _stream.AudioChunks
                .Where(_ => _state.Value == AgentPresenceState.Idle)
                .Subscribe(_ => TransitionTo(AgentPresenceState.Listening, "Audio input started")));

        // Final voice input -> Transition to Processing (if Listening)
        _disposables.Add(
            _stream.VoiceInputs
                .Where(e => !e.IsInterim && _state.Value == AgentPresenceState.Listening)
                .Subscribe(_ => TransitionTo(AgentPresenceState.Processing, "Voice input complete")));

        // Complete text input -> Transition to Processing (if Idle)
        _disposables.Add(
            _stream.TextInputs
                .Where(e => !e.IsPartial && _state.Value == AgentPresenceState.Idle)
                .Subscribe(_ => TransitionTo(AgentPresenceState.Processing, "Text input received")));

        // Agent response starts -> Transition to Speaking (if Processing)
        _disposables.Add(
            _stream.AgentResponses
                .Where(e => !e.IsComplete && _state.Value == AgentPresenceState.Processing)
                .Take(1)
                .Repeat()
                .Subscribe(_ => TransitionTo(AgentPresenceState.Speaking, "Response generation started")));

        // Agent response complete -> Back to Idle (if Speaking)
        _disposables.Add(
            _stream.AgentResponses
                .Where(e => e.IsComplete && _state.Value == AgentPresenceState.Speaking)
                .Subscribe(_ => TransitionTo(AgentPresenceState.Idle, "Response complete")));

        // Voice output complete -> Back to Idle (if Speaking)
        _disposables.Add(
            _stream.VoiceOutputs
                .Where(e => e.IsComplete && _state.Value == AgentPresenceState.Speaking)
                .Subscribe(_ => TransitionTo(AgentPresenceState.Idle, "Voice output complete")));

        // Handle barge-in: user input while speaking
        _disposables.Add(
            _stream.UserInput
                .Where(_ => _state.Value == AgentPresenceState.Speaking)
                .Throttle(_bargeInDebounce)
                .Subscribe(e => HandleBargeIn(e)));

        // Handle barge-in: user input while processing (cancel generation)
        _disposables.Add(
            _stream.UserInput
                .Where(_ => _state.Value == AgentPresenceState.Processing)
                .Throttle(_bargeInDebounce)
                .Subscribe(e => HandleBargeInDuringProcessing(e)));

        // Idle timeout -> trigger heartbeat
        _disposables.Add(
            _state
                .Where(s => s == AgentPresenceState.Idle)
                .SelectMany(_ => Observable.Timer(_idleTimeout))
                .Subscribe(_ =>
                {
                    if (_state.Value == AgentPresenceState.Idle)
                    {
                        _stream.PublishHeartbeat();
                    }
                }));
    }

    private void WireUpControlEvents()
    {
        _disposables.Add(
            _stream.ControlEvents
                .Subscribe(HandleControlEvent));
    }

    /// <summary>
    /// Transitions to a new state with cleanup and setup.
    /// </summary>
    /// <param name="newState">The new state.</param>
    /// <param name="reason">The reason for the transition.</param>
    public void TransitionTo(AgentPresenceState newState, string? reason = null)
    {
        var oldState = _state.Value;
        if (oldState == newState) return;

        // Cleanup based on leaving state
        CleanupState(oldState);

        // Setup based on entering state
        SetupState(newState);

        _state.OnNext(newState);
        _stream.SetPresenceState(newState, reason);

        StateChanged?.Invoke(this, new StateChangeEventArgs(oldState, newState, reason));
    }

    private void CleanupState(AgentPresenceState state)
    {
        switch (state)
        {
            case AgentPresenceState.Speaking:
                _currentSpeechCts?.Cancel();
                _currentSpeechCts?.Dispose();
                _currentSpeechCts = null;
                break;

            case AgentPresenceState.Processing:
                _currentProcessingCts?.Cancel();
                _currentProcessingCts?.Dispose();
                _currentProcessingCts = null;
                break;

            case AgentPresenceState.Listening:
                _currentListeningCts?.Cancel();
                _currentListeningCts?.Dispose();
                _currentListeningCts = null;
                break;
        }
    }

    private void SetupState(AgentPresenceState state)
    {
        switch (state)
        {
            case AgentPresenceState.Speaking:
                _currentSpeechCts = new CancellationTokenSource();
                break;

            case AgentPresenceState.Processing:
                _currentProcessingCts = new CancellationTokenSource();
                break;

            case AgentPresenceState.Listening:
                _currentListeningCts = new CancellationTokenSource();
                break;
        }
    }

    private void HandleBargeIn(InteractionEvent triggerEvent)
    {
        // User started speaking/typing while agent is speaking
        var inputText = triggerEvent switch
        {
            TextInputEvent t => t.Text,
            VoiceInputEvent v => v.TranscribedText,
            _ => null,
        };

        _currentSpeechCts?.Cancel();
        _stream.SendControl(ControlAction.InterruptSpeech, $"User barge-in: {inputText?[..Math.Min(50, inputText?.Length ?? 0)]}");

        BargeInDetected?.Invoke(this, new BargeInEventArgs(
            AgentPresenceState.Speaking,
            inputText,
            BargeInType.SpeechInterrupt));

        TransitionTo(AgentPresenceState.Interrupted, "User interrupted speech");

        // Quickly transition to processing the new input
        Observable.Timer(TimeSpan.FromMilliseconds(100))
            .Subscribe(_ => TransitionTo(AgentPresenceState.Processing, "Processing barge-in input"));
    }

    private void HandleBargeInDuringProcessing(InteractionEvent triggerEvent)
    {
        // User provided new input while agent is still processing
        var inputText = triggerEvent switch
        {
            TextInputEvent t => t.Text,
            VoiceInputEvent v => v.TranscribedText,
            _ => null,
        };

        _currentProcessingCts?.Cancel();
        _stream.SendControl(ControlAction.CancelGeneration, $"User barge-in during processing: {inputText?[..Math.Min(50, inputText?.Length ?? 0)]}");

        BargeInDetected?.Invoke(this, new BargeInEventArgs(
            AgentPresenceState.Processing,
            inputText,
            BargeInType.ProcessingCancel));

        // Reset and restart processing with new input
        TransitionTo(AgentPresenceState.Interrupted, "User interrupted processing");
        Observable.Timer(TimeSpan.FromMilliseconds(50))
            .Subscribe(_ => TransitionTo(AgentPresenceState.Processing, "Processing new input"));
    }

    private void HandleControlEvent(ControlEvent e)
    {
        switch (e.Action)
        {
            case ControlAction.StartListening:
                if (_state.Value == AgentPresenceState.Idle)
                {
                    TransitionTo(AgentPresenceState.Listening, e.Reason);
                }

                break;

            case ControlAction.StopListening:
                if (_state.Value == AgentPresenceState.Listening)
                {
                    TransitionTo(AgentPresenceState.Idle, e.Reason);
                }

                break;

            case ControlAction.InterruptSpeech:
                if (_state.Value == AgentPresenceState.Speaking)
                {
                    _currentSpeechCts?.Cancel();
                    TransitionTo(AgentPresenceState.Interrupted, e.Reason);
                    Observable.Timer(TimeSpan.FromMilliseconds(100))
                        .Subscribe(_ => TransitionTo(AgentPresenceState.Idle, "Speech interrupted"));
                }

                break;

            case ControlAction.CancelGeneration:
                if (_state.Value == AgentPresenceState.Processing)
                {
                    _currentProcessingCts?.Cancel();
                    TransitionTo(AgentPresenceState.Idle, e.Reason);
                }

                break;

            case ControlAction.Reset:
                _currentSpeechCts?.Cancel();
                _currentProcessingCts?.Cancel();
                _currentListeningCts?.Cancel();
                TransitionTo(AgentPresenceState.Idle, e.Reason ?? "Reset");
                break;

            case ControlAction.Pause:
                if (_state.Value != AgentPresenceState.Paused)
                {
                    TransitionTo(AgentPresenceState.Paused, e.Reason);
                }

                break;

            case ControlAction.Resume:
                if (_state.Value == AgentPresenceState.Paused)
                {
                    TransitionTo(AgentPresenceState.Idle, e.Reason);
                }

                break;
        }
    }

    /// <summary>
    /// Forces a state transition (use with caution).
    /// </summary>
    /// <param name="state">The state to force.</param>
    /// <param name="reason">The reason.</param>
    public void ForceState(AgentPresenceState state, string? reason = null)
    {
        // Cleanup all states
        CleanupState(AgentPresenceState.Speaking);
        CleanupState(AgentPresenceState.Processing);
        CleanupState(AgentPresenceState.Listening);

        SetupState(state);
        _state.OnNext(state);
        _stream.SetPresenceState(state, reason ?? "Forced state change");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _currentSpeechCts?.Cancel();
        _currentSpeechCts?.Dispose();
        _currentProcessingCts?.Cancel();
        _currentProcessingCts?.Dispose();
        _currentListeningCts?.Cancel();
        _currentListeningCts?.Dispose();

        _disposables.Dispose();
        _state.Dispose();
    }
}

/// <summary>
/// Event args for state changes.
/// </summary>
public sealed class StateChangeEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StateChangeEventArgs"/> class.
    /// </summary>
    public StateChangeEventArgs(AgentPresenceState previousState, AgentPresenceState newState, string? reason)
    {
        PreviousState = previousState;
        NewState = newState;
        Reason = reason;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the previous state.</summary>
    public AgentPresenceState PreviousState { get; }

    /// <summary>Gets the new state.</summary>
    public AgentPresenceState NewState { get; }

    /// <summary>Gets the reason for the change.</summary>
    public string? Reason { get; }

    /// <summary>Gets the timestamp of the change.</summary>
    public DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Event args for barge-in detection.
/// </summary>
public sealed class BargeInEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BargeInEventArgs"/> class.
    /// </summary>
    public BargeInEventArgs(AgentPresenceState interruptedState, string? userInput, BargeInType type)
    {
        InterruptedState = interruptedState;
        UserInput = userInput;
        Type = type;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets the state that was interrupted.</summary>
    public AgentPresenceState InterruptedState { get; }

    /// <summary>Gets the user input that caused the barge-in.</summary>
    public string? UserInput { get; }

    /// <summary>Gets the type of barge-in.</summary>
    public BargeInType Type { get; }

    /// <summary>Gets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Types of barge-in events.
/// </summary>
public enum BargeInType
{
    /// <summary>User interrupted agent speech.</summary>
    SpeechInterrupt,

    /// <summary>User cancelled ongoing processing.</summary>
    ProcessingCancel,
}
