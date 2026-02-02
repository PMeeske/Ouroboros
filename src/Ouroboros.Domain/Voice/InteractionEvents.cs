// <copyright file="InteractionEvents.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Voice;

/// <summary>
/// Base type for all interaction events in the unified voice stream.
/// All modalities (text, voice, agent output) share this common base,
/// enabling unified stream composition via IObservable{InteractionEvent}.
/// </summary>
public abstract record InteractionEvent
{
    /// <summary>Gets unique identifier for correlation.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets timestamp when the event was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets source of the event (User, Agent, System).</summary>
    public required InteractionSource Source { get; init; }

    /// <summary>Gets optional correlation ID for request/response pairing.</summary>
    public Guid? CorrelationId { get; init; }
}

/// <summary>
/// Source of an interaction event.
/// </summary>
public enum InteractionSource
{
    /// <summary>Event originated from user input.</summary>
    User,

    /// <summary>Event originated from the agent.</summary>
    Agent,

    /// <summary>Event originated from system/infrastructure.</summary>
    System,
}

// ============================================================================
// USER INPUT EVENTS
// ============================================================================

/// <summary>
/// Text typed by user via keyboard.
/// Can be partial (character-by-character streaming) or complete.
/// </summary>
public sealed record TextInputEvent : InteractionEvent
{
    /// <summary>Gets the text content.</summary>
    public required string Text { get; init; }

    /// <summary>Gets whether this is a partial input (streaming) or complete.</summary>
    public bool IsPartial { get; init; }
}

/// <summary>
/// Voice input from user via microphone, after transcription.
/// </summary>
public sealed record VoiceInputEvent : InteractionEvent
{
    /// <summary>Gets the transcribed text from speech.</summary>
    public required string TranscribedText { get; init; }

    /// <summary>Gets the confidence score (0.0-1.0) of transcription.</summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>Gets the duration of the speech segment.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets the detected language code (e.g., "en-US").</summary>
    public string? DetectedLanguage { get; init; }

    /// <summary>Gets whether this is a partial/interim result.</summary>
    public bool IsInterim { get; init; }
}

/// <summary>
/// Raw audio chunk for streaming speech-to-text.
/// Published as microphone captures audio in real-time.
/// </summary>
public sealed record AudioChunkEvent : InteractionEvent
{
    /// <summary>Gets the raw audio data.</summary>
    public required byte[] AudioData { get; init; }

    /// <summary>Gets the audio format (e.g., "pcm16", "wav").</summary>
    public required string Format { get; init; }

    /// <summary>Gets the sample rate in Hz.</summary>
    public int SampleRate { get; init; } = 16000;

    /// <summary>Gets the number of audio channels.</summary>
    public int Channels { get; init; } = 1;

    /// <summary>Gets whether this is the final chunk in a recording.</summary>
    public bool IsFinal { get; init; }
}

// ============================================================================
// AGENT EVENTS
// ============================================================================

/// <summary>
/// Agent internal thinking/reasoning displayed in real-time.
/// Shown dimmed to distinguish from actual response.
/// </summary>
public sealed record AgentThinkingEvent : InteractionEvent
{
    /// <summary>Gets the thought chunk text.</summary>
    public required string ThoughtChunk { get; init; }

    /// <summary>Gets the current phase of thinking.</summary>
    public ThinkingPhase Phase { get; init; } = ThinkingPhase.Reasoning;

    /// <summary>Gets whether this completes the thinking phase.</summary>
    public bool IsComplete { get; init; }
}

/// <summary>
/// Phases of agent thinking/reasoning.
/// </summary>
public enum ThinkingPhase
{
    /// <summary>Analyzing the user's input.</summary>
    Analyzing,

    /// <summary>Reasoning about the response.</summary>
    Reasoning,

    /// <summary>Planning actions or steps.</summary>
    Planning,

    /// <summary>Reflecting on the response.</summary>
    Reflecting,
}

/// <summary>
/// Agent response token/chunk streamed word-by-word or sentence-by-sentence.
/// </summary>
public sealed record AgentResponseEvent : InteractionEvent
{
    /// <summary>Gets the text chunk.</summary>
    public required string TextChunk { get; init; }

    /// <summary>Gets whether this completes the response.</summary>
    public bool IsComplete { get; init; }

    /// <summary>Gets the type of response content.</summary>
    public ResponseType Type { get; init; } = ResponseType.Direct;

    /// <summary>Gets whether this chunk ends a sentence (natural pause point for TTS).</summary>
    public bool IsSentenceEnd { get; init; }
}

/// <summary>
/// Types of agent response content.
/// </summary>
public enum ResponseType
{
    /// <summary>Direct answer to user's question.</summary>
    Direct,

    /// <summary>Contextual narration or explanation.</summary>
    Narration,

    /// <summary>Describing a tool/action being taken.</summary>
    Action,

    /// <summary>Asking for clarification.</summary>
    Clarification,

    /// <summary>Inner thought spoken aloud (whisper style).</summary>
    InnerThought,
}

// ============================================================================
// OUTPUT EVENTS
// ============================================================================

/// <summary>
/// Voice synthesis output - streamed audio chunks.
/// </summary>
public sealed record VoiceOutputEvent : InteractionEvent
{
    /// <summary>Gets the synthesized audio chunk.</summary>
    public required byte[] AudioChunk { get; init; }

    /// <summary>Gets the audio format (e.g., "pcm16", "mp3", "wav").</summary>
    public required string Format { get; init; }

    /// <summary>Gets the sample rate in Hz.</summary>
    public int SampleRate { get; init; } = 24000;

    /// <summary>Gets the duration of this audio chunk in seconds.</summary>
    public double DurationSeconds { get; init; }

    /// <summary>Gets whether this is the final audio chunk.</summary>
    public bool IsComplete { get; init; }

    /// <summary>Gets the emotion/style applied (for expressive TTS).</summary>
    public string? Emotion { get; init; }

    /// <summary>Gets the text this audio represents (for debugging/display).</summary>
    public string? Text { get; init; }
}

/// <summary>
/// Text displayed to console/UI.
/// </summary>
public sealed record TextOutputEvent : InteractionEvent
{
    /// <summary>Gets the text to display.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the display style.</summary>
    public OutputStyle Style { get; init; } = OutputStyle.Normal;

    /// <summary>Gets whether to append (no newline) or write line.</summary>
    public bool Append { get; init; } = true;
}

/// <summary>
/// Styles for text output display.
/// </summary>
public enum OutputStyle
{
    /// <summary>Normal conversational output.</summary>
    Normal,

    /// <summary>Dimmed style for internal thoughts.</summary>
    Thinking,

    /// <summary>Emphasized/highlighted text.</summary>
    Emphasis,

    /// <summary>Softer/quieter display for whispers.</summary>
    Whisper,

    /// <summary>System messages.</summary>
    System,

    /// <summary>Error messages.</summary>
    Error,

    /// <summary>User input echo.</summary>
    UserInput,
}

// ============================================================================
// PRESENCE/STATE EVENTS
// ============================================================================

/// <summary>
/// Agent presence state change event.
/// </summary>
public sealed record PresenceStateEvent : InteractionEvent
{
    /// <summary>Gets the new presence state.</summary>
    public required AgentPresenceState State { get; init; }

    /// <summary>Gets the previous presence state.</summary>
    public AgentPresenceState? PreviousState { get; init; }

    /// <summary>Gets the reason for the state change.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Agent presence states for the embodiment state machine.
/// </summary>
public enum AgentPresenceState
{
    /// <summary>Waiting for input, ready to listen.</summary>
    Idle,

    /// <summary>Actively hearing/recording user speech.</summary>
    Listening,

    /// <summary>Processing input, generating response.</summary>
    Processing,

    /// <summary>Voice output is active.</summary>
    Speaking,

    /// <summary>User interrupted (barge-in detected).</summary>
    Interrupted,

    /// <summary>Paused/suspended state.</summary>
    Paused,
}

/// <summary>
/// Control event for stream management (barge-in, cancel, etc.).
/// </summary>
public sealed record ControlEvent : InteractionEvent
{
    /// <summary>Gets the control action to perform.</summary>
    public required ControlAction Action { get; init; }

    /// <summary>Gets the reason for the control action.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Control actions for managing the interaction stream.
/// </summary>
public enum ControlAction
{
    /// <summary>Start listening for voice input.</summary>
    StartListening,

    /// <summary>Stop listening for voice input.</summary>
    StopListening,

    /// <summary>Interrupt current speech output (barge-in).</summary>
    InterruptSpeech,

    /// <summary>Cancel ongoing response generation.</summary>
    CancelGeneration,

    /// <summary>Reset to idle state.</summary>
    Reset,

    /// <summary>Pause all processing.</summary>
    Pause,

    /// <summary>Resume from paused state.</summary>
    Resume,
}

// ============================================================================
// UTILITY EVENTS
// ============================================================================

/// <summary>
/// Heartbeat event for presence detection and timeout handling.
/// </summary>
public sealed record HeartbeatEvent : InteractionEvent
{
    /// <summary>Gets the current state at heartbeat time.</summary>
    public AgentPresenceState CurrentState { get; init; }

    /// <summary>Gets the time since last user interaction.</summary>
    public TimeSpan TimeSinceLastInteraction { get; init; }
}

/// <summary>
/// Error event for stream error handling.
/// </summary>
public sealed record ErrorEvent : InteractionEvent
{
    /// <summary>Gets the error message.</summary>
    public required string Message { get; init; }

    /// <summary>Gets the exception if available.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Gets the error category.</summary>
    public ErrorCategory Category { get; init; } = ErrorCategory.Unknown;

    /// <summary>Gets whether the error is recoverable.</summary>
    public bool IsRecoverable { get; init; } = true;
}

/// <summary>
/// Categories of errors in the voice stream.
/// </summary>
public enum ErrorCategory
{
    /// <summary>Unknown error.</summary>
    Unknown,

    /// <summary>Speech recognition error.</summary>
    SpeechRecognition,

    /// <summary>Speech synthesis error.</summary>
    SpeechSynthesis,

    /// <summary>LLM/generation error.</summary>
    Generation,

    /// <summary>Audio hardware error.</summary>
    AudioHardware,

    /// <summary>Network/connectivity error.</summary>
    Network,
}
