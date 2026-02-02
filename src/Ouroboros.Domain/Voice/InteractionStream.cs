// <copyright file="InteractionStream.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Ouroboros.Domain.Voice;

/// <summary>
/// Unified reactive stream that merges all interaction modalities.
/// This is the central nervous system of the voice embodiment architecture.
/// All events flow through typed subjects and can be observed via merged or typed streams.
/// </summary>
public sealed class InteractionStream : IDisposable
{
    // Internal subjects for each event type
    private readonly Subject<TextInputEvent> _textInput = new();
    private readonly Subject<VoiceInputEvent> _voiceInput = new();
    private readonly Subject<AudioChunkEvent> _audioChunks = new();
    private readonly Subject<AgentThinkingEvent> _agentThinking = new();
    private readonly Subject<AgentResponseEvent> _agentResponse = new();
    private readonly Subject<VoiceOutputEvent> _voiceOutput = new();
    private readonly Subject<TextOutputEvent> _textOutput = new();
    private readonly Subject<PresenceStateEvent> _presenceState = new();
    private readonly Subject<ControlEvent> _control = new();
    private readonly Subject<HeartbeatEvent> _heartbeat = new();
    private readonly Subject<ErrorEvent> _errors = new();

    private readonly BehaviorSubject<AgentPresenceState> _currentState;
    private readonly BehaviorSubject<DateTimeOffset> _lastInteractionTime;
    private readonly CompositeDisposable _disposables = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionStream"/> class.
    /// </summary>
    public InteractionStream()
    {
        _currentState = new BehaviorSubject<AgentPresenceState>(AgentPresenceState.Idle);
        _lastInteractionTime = new BehaviorSubject<DateTimeOffset>(DateTimeOffset.UtcNow);

        // Wire up state changes from presence events
        _disposables.Add(
            _presenceState.Subscribe(e => _currentState.OnNext(e.State)));

        // Track last interaction time from user inputs
        _disposables.Add(
            UserInput.Subscribe(_ => _lastInteractionTime.OnNext(DateTimeOffset.UtcNow)));
    }

    // ========================================================================
    // MERGED STREAMS
    // ========================================================================

    /// <summary>
    /// Gets the unified stream of ALL interaction events.
    /// Subscribe to this for a complete view of all interactions.
    /// </summary>
    public IObservable<InteractionEvent> All => Observable.Merge<InteractionEvent>(
        _textInput,
        _voiceInput,
        _audioChunks,
        _agentThinking,
        _agentResponse,
        _voiceOutput,
        _textOutput,
        _presenceState,
        _control,
        _heartbeat,
        _errors);

    /// <summary>
    /// Gets all user input events (text + voice merged).
    /// </summary>
    public IObservable<InteractionEvent> UserInput => Observable.Merge<InteractionEvent>(
        _textInput,
        _voiceInput);

    /// <summary>
    /// Gets all agent output events (thinking + response merged).
    /// </summary>
    public IObservable<InteractionEvent> AgentOutput => Observable.Merge<InteractionEvent>(
        _agentThinking,
        _agentResponse);

    /// <summary>
    /// Gets all voice-related events (voice input + audio chunks + voice output).
    /// </summary>
    public IObservable<InteractionEvent> VoiceEvents => Observable.Merge<InteractionEvent>(
        _voiceInput,
        _audioChunks,
        _voiceOutput);

    /// <summary>
    /// Gets all display output events (text + voice output).
    /// </summary>
    public IObservable<InteractionEvent> DisplayOutput => Observable.Merge<InteractionEvent>(
        _textOutput,
        _voiceOutput);

    // ========================================================================
    // TYPED STREAM ACCESSORS
    // ========================================================================

    /// <summary>Gets the stream of text input events.</summary>
    public IObservable<TextInputEvent> TextInputs => _textInput.AsObservable();

    /// <summary>Gets the stream of voice input events.</summary>
    public IObservable<VoiceInputEvent> VoiceInputs => _voiceInput.AsObservable();

    /// <summary>Gets the stream of raw audio chunk events.</summary>
    public IObservable<AudioChunkEvent> AudioChunks => _audioChunks.AsObservable();

    /// <summary>Gets the stream of agent thinking events.</summary>
    public IObservable<AgentThinkingEvent> AgentThoughts => _agentThinking.AsObservable();

    /// <summary>Gets the stream of agent response events.</summary>
    public IObservable<AgentResponseEvent> AgentResponses => _agentResponse.AsObservable();

    /// <summary>Gets the stream of voice output events.</summary>
    public IObservable<VoiceOutputEvent> VoiceOutputs => _voiceOutput.AsObservable();

    /// <summary>Gets the stream of text output events.</summary>
    public IObservable<TextOutputEvent> TextOutputs => _textOutput.AsObservable();

    /// <summary>Gets the stream of presence state change events.</summary>
    public IObservable<PresenceStateEvent> PresenceChanges => _presenceState.AsObservable();

    /// <summary>Gets the stream of control events.</summary>
    public IObservable<ControlEvent> ControlEvents => _control.AsObservable();

    /// <summary>Gets the stream of heartbeat events.</summary>
    public IObservable<HeartbeatEvent> Heartbeats => _heartbeat.AsObservable();

    /// <summary>Gets the stream of error events.</summary>
    public IObservable<ErrorEvent> Errors => _errors.AsObservable();

    // ========================================================================
    // STATE ACCESSORS
    // ========================================================================

    /// <summary>Gets the current presence state as an observable (emits current + future values).</summary>
    public IObservable<AgentPresenceState> CurrentState => _currentState.AsObservable();

    /// <summary>Gets the current presence state synchronously.</summary>
    public AgentPresenceState State => _currentState.Value;

    /// <summary>Gets the last interaction time as an observable.</summary>
    public IObservable<DateTimeOffset> LastInteractionTime => _lastInteractionTime.AsObservable();

    /// <summary>Gets the time since last user interaction.</summary>
    public TimeSpan TimeSinceLastInteraction => DateTimeOffset.UtcNow - _lastInteractionTime.Value;

    // ========================================================================
    // PUBLISHING METHODS - USER INPUT
    // ========================================================================

    /// <summary>
    /// Publishes a text input event.
    /// </summary>
    /// <param name="text">The input text.</param>
    /// <param name="isPartial">Whether this is partial/streaming input.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    public void PublishTextInput(string text, bool isPartial = false, Guid? correlationId = null)
    {
        if (_disposed) return;
        _textInput.OnNext(new TextInputEvent
        {
            Text = text,
            IsPartial = isPartial,
            Source = InteractionSource.User,
            CorrelationId = correlationId,
        });
    }

    /// <summary>
    /// Publishes a voice input event (transcribed speech).
    /// </summary>
    /// <param name="text">The transcribed text.</param>
    /// <param name="confidence">Confidence score 0.0-1.0.</param>
    /// <param name="duration">Duration of the speech segment.</param>
    /// <param name="language">Detected language code.</param>
    /// <param name="isInterim">Whether this is an interim result.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    public void PublishVoiceInput(
        string text,
        double confidence = 1.0,
        TimeSpan? duration = null,
        string? language = null,
        bool isInterim = false,
        Guid? correlationId = null)
    {
        if (_disposed) return;
        _voiceInput.OnNext(new VoiceInputEvent
        {
            TranscribedText = text,
            Confidence = confidence,
            Duration = duration ?? TimeSpan.Zero,
            DetectedLanguage = language,
            IsInterim = isInterim,
            Source = InteractionSource.User,
            CorrelationId = correlationId,
        });
    }

    /// <summary>
    /// Publishes a raw audio chunk event.
    /// </summary>
    /// <param name="data">Raw audio data.</param>
    /// <param name="format">Audio format (e.g., "pcm16", "wav").</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="isFinal">Whether this is the final chunk.</param>
    public void PublishAudioChunk(byte[] data, string format, int sampleRate = 16000, bool isFinal = false)
    {
        if (_disposed) return;
        _audioChunks.OnNext(new AudioChunkEvent
        {
            AudioData = data,
            Format = format,
            SampleRate = sampleRate,
            IsFinal = isFinal,
            Source = InteractionSource.User,
        });
    }

    // ========================================================================
    // PUBLISHING METHODS - AGENT OUTPUT
    // ========================================================================

    /// <summary>
    /// Publishes an agent thinking event.
    /// </summary>
    /// <param name="thought">The thought chunk text.</param>
    /// <param name="phase">The thinking phase.</param>
    /// <param name="isComplete">Whether thinking is complete.</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    public void PublishThinking(
        string thought,
        ThinkingPhase phase = ThinkingPhase.Reasoning,
        bool isComplete = false,
        Guid? correlationId = null)
    {
        if (_disposed) return;
        _agentThinking.OnNext(new AgentThinkingEvent
        {
            ThoughtChunk = thought,
            Phase = phase,
            IsComplete = isComplete,
            Source = InteractionSource.Agent,
            CorrelationId = correlationId,
        });
    }

    /// <summary>
    /// Publishes an agent response event.
    /// </summary>
    /// <param name="text">The response text chunk.</param>
    /// <param name="isComplete">Whether this completes the response.</param>
    /// <param name="type">The response type.</param>
    /// <param name="isSentenceEnd">Whether this ends a sentence (TTS pause point).</param>
    /// <param name="correlationId">Optional correlation ID.</param>
    public void PublishResponse(
        string text,
        bool isComplete = false,
        ResponseType type = ResponseType.Direct,
        bool isSentenceEnd = false,
        Guid? correlationId = null)
    {
        if (_disposed) return;
        _agentResponse.OnNext(new AgentResponseEvent
        {
            TextChunk = text,
            IsComplete = isComplete,
            Type = type,
            IsSentenceEnd = isSentenceEnd,
            Source = InteractionSource.Agent,
            CorrelationId = correlationId,
        });
    }

    // ========================================================================
    // PUBLISHING METHODS - OUTPUT
    // ========================================================================

    /// <summary>
    /// Publishes a voice output event (synthesized audio).
    /// </summary>
    /// <param name="audio">The audio data.</param>
    /// <param name="format">Audio format.</param>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <param name="isComplete">Whether this is the final chunk.</param>
    /// <param name="emotion">Optional emotion/style.</param>
    /// <param name="text">Optional text this audio represents.</param>
    public void PublishVoiceOutput(
        byte[] audio,
        string format,
        double durationSeconds = 0,
        bool isComplete = false,
        string? emotion = null,
        string? text = null)
    {
        if (_disposed) return;
        _voiceOutput.OnNext(new VoiceOutputEvent
        {
            AudioChunk = audio,
            Format = format,
            DurationSeconds = durationSeconds,
            IsComplete = isComplete,
            Emotion = emotion,
            Text = text,
            Source = InteractionSource.Agent,
        });
    }

    /// <summary>
    /// Publishes a text output event for display.
    /// </summary>
    /// <param name="text">The text to display.</param>
    /// <param name="style">The display style.</param>
    /// <param name="append">Whether to append (no newline) or write line.</param>
    public void PublishTextOutput(string text, OutputStyle style = OutputStyle.Normal, bool append = true)
    {
        if (_disposed) return;
        _textOutput.OnNext(new TextOutputEvent
        {
            Text = text,
            Style = style,
            Append = append,
            Source = InteractionSource.Agent,
        });
    }

    // ========================================================================
    // PUBLISHING METHODS - STATE & CONTROL
    // ========================================================================

    /// <summary>
    /// Sets the agent presence state.
    /// </summary>
    /// <param name="state">The new state.</param>
    /// <param name="reason">Optional reason for the change.</param>
    public void SetPresenceState(AgentPresenceState state, string? reason = null)
    {
        if (_disposed) return;
        var previousState = _currentState.Value;
        if (previousState == state) return;

        _presenceState.OnNext(new PresenceStateEvent
        {
            State = state,
            PreviousState = previousState,
            Reason = reason,
            Source = InteractionSource.System,
        });
    }

    /// <summary>
    /// Sends a control event.
    /// </summary>
    /// <param name="action">The control action.</param>
    /// <param name="reason">Optional reason.</param>
    public void SendControl(ControlAction action, string? reason = null)
    {
        if (_disposed) return;
        _control.OnNext(new ControlEvent
        {
            Action = action,
            Reason = reason,
            Source = InteractionSource.System,
        });
    }

    /// <summary>
    /// Publishes a heartbeat event.
    /// </summary>
    public void PublishHeartbeat()
    {
        if (_disposed) return;
        _heartbeat.OnNext(new HeartbeatEvent
        {
            CurrentState = _currentState.Value,
            TimeSinceLastInteraction = TimeSinceLastInteraction,
            Source = InteractionSource.System,
        });
    }

    /// <summary>
    /// Publishes an error event.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="exception">Optional exception.</param>
    /// <param name="category">Error category.</param>
    /// <param name="isRecoverable">Whether the error is recoverable.</param>
    public void PublishError(
        string message,
        Exception? exception = null,
        ErrorCategory category = ErrorCategory.Unknown,
        bool isRecoverable = true)
    {
        if (_disposed) return;
        _errors.OnNext(new ErrorEvent
        {
            Message = message,
            Exception = exception,
            Category = category,
            IsRecoverable = isRecoverable,
            Source = InteractionSource.System,
        });
    }

    // ========================================================================
    // CONVENIENCE METHODS
    // ========================================================================

    /// <summary>
    /// Gets user input text from either text or voice input events.
    /// Filters to only complete (non-partial, non-interim) inputs.
    /// </summary>
    public IObservable<string> CompletedUserInputText => UserInput
        .Where(e => e is TextInputEvent { IsPartial: false } or VoiceInputEvent { IsInterim: false })
        .Select(e => e switch
        {
            TextInputEvent t => t.Text,
            VoiceInputEvent v => v.TranscribedText,
            _ => string.Empty,
        })
        .Where(text => !string.IsNullOrWhiteSpace(text));

    /// <summary>
    /// Gets complete agent responses (full text accumulated).
    /// </summary>
    public IObservable<string> CompletedAgentResponses => AgentResponses
        .Buffer(AgentResponses.Where(e => e.IsComplete))
        .Select(chunks => string.Concat(chunks.Select(c => c.TextChunk)));

    /// <summary>
    /// Gets sentence-boundary response chunks for TTS.
    /// </summary>
    public IObservable<string> SentenceChunks => AgentResponses
        .Buffer(AgentResponses.Where(e => e.IsSentenceEnd || e.IsComplete))
        .Where(chunks => chunks.Count > 0)
        .Select(chunks => string.Concat(chunks.Select(c => c.TextChunk)));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Complete all subjects
        _textInput.OnCompleted();
        _voiceInput.OnCompleted();
        _audioChunks.OnCompleted();
        _agentThinking.OnCompleted();
        _agentResponse.OnCompleted();
        _voiceOutput.OnCompleted();
        _textOutput.OnCompleted();
        _presenceState.OnCompleted();
        _control.OnCompleted();
        _heartbeat.OnCompleted();
        _errors.OnCompleted();

        _disposables.Dispose();
        _currentState.Dispose();
        _lastInteractionTime.Dispose();

        // Dispose all subjects
        _textInput.Dispose();
        _voiceInput.Dispose();
        _audioChunks.Dispose();
        _agentThinking.Dispose();
        _agentResponse.Dispose();
        _voiceOutput.Dispose();
        _textOutput.Dispose();
        _presenceState.Dispose();
        _control.Dispose();
        _heartbeat.Dispose();
        _errors.Dispose();
    }
}
