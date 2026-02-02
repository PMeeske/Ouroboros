// <copyright file="MessageBus.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Ouroboros.Core.Monads;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Defines the priority level for agent messages.
/// </summary>
public enum MessagePriority
{
    /// <summary>
    /// Low priority messages that can be processed when resources are available.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority messages for standard communication.
    /// </summary>
    Normal,

    /// <summary>
    /// High priority messages that should be processed before normal messages.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority messages that require immediate processing.
    /// </summary>
    Critical,
}

/// <summary>
/// Defines the type of agent message.
/// </summary>
public enum MessageType
{
    /// <summary>
    /// A request message expecting a response.
    /// </summary>
    Request,

    /// <summary>
    /// A response to a previous request.
    /// </summary>
    Response,

    /// <summary>
    /// A broadcast message sent to all subscribers.
    /// </summary>
    Broadcast,

    /// <summary>
    /// A notification message that does not expect a response.
    /// </summary>
    Notification,

    /// <summary>
    /// An error message indicating a failure.
    /// </summary>
    Error,
}

/// <summary>
/// Represents a communication unit between agents in a multi-agent system.
/// </summary>
/// <param name="Id">The unique identifier of the message.</param>
/// <param name="SenderId">The unique identifier of the sending agent.</param>
/// <param name="ReceiverId">The unique identifier of the receiving agent, or null for broadcasts.</param>
/// <param name="Type">The type of message.</param>
/// <param name="Priority">The priority level of the message.</param>
/// <param name="Topic">The topic or channel of the message.</param>
/// <param name="Payload">The message payload data.</param>
/// <param name="Timestamp">The timestamp when the message was created.</param>
/// <param name="CorrelationId">The correlation identifier linking requests and responses.</param>
public sealed record AgentMessage(
    Guid Id,
    Guid SenderId,
    Guid? ReceiverId,
    MessageType Type,
    MessagePriority Priority,
    string Topic,
    object Payload,
    DateTime Timestamp,
    Guid? CorrelationId)
{
    /// <summary>
    /// Gets a value indicating whether this message is a request expecting a response.
    /// </summary>
    public bool IsRequest => Type == MessageType.Request;

    /// <summary>
    /// Gets a value indicating whether this message is a broadcast to all subscribers.
    /// </summary>
    public bool IsBroadcast => Type == MessageType.Broadcast || ReceiverId is null;

    /// <summary>
    /// Creates a request message expecting a response from a specific agent.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="receiverId">The unique identifier of the receiving agent.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="payload">The message payload data.</param>
    /// <returns>A new request message.</returns>
    public static AgentMessage CreateRequest(Guid senderId, Guid receiverId, string topic, object payload)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        Guid messageId = Guid.NewGuid();
        return new AgentMessage(
            Id: messageId,
            SenderId: senderId,
            ReceiverId: receiverId,
            Type: MessageType.Request,
            Priority: MessagePriority.Normal,
            Topic: topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: messageId);
    }

    /// <summary>
    /// Creates a response message to a previous request.
    /// </summary>
    /// <param name="request">The original request message being responded to.</param>
    /// <param name="payload">The response payload data.</param>
    /// <returns>A new response message correlated to the original request.</returns>
    public static AgentMessage CreateResponse(AgentMessage request, object payload)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(payload);

        if (request.Type != MessageType.Request)
        {
            throw new ArgumentException("Can only create response for request messages.", nameof(request));
        }

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: request.ReceiverId ?? throw new ArgumentException("Request must have a receiver.", nameof(request)),
            ReceiverId: request.SenderId,
            Type: MessageType.Response,
            Priority: request.Priority,
            Topic: request.Topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: request.CorrelationId);
    }

    /// <summary>
    /// Creates a broadcast message sent to all subscribers of a topic.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="payload">The message payload data.</param>
    /// <returns>A new broadcast message.</returns>
    public static AgentMessage CreateBroadcast(Guid senderId, string topic, object payload)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: senderId,
            ReceiverId: null,
            Type: MessageType.Broadcast,
            Priority: MessagePriority.Normal,
            Topic: topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: null);
    }

    /// <summary>
    /// Creates a notification message that does not expect a response.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="payload">The message payload data.</param>
    /// <returns>A new notification message.</returns>
    public static AgentMessage CreateNotification(Guid senderId, string topic, object payload)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: senderId,
            ReceiverId: null,
            Type: MessageType.Notification,
            Priority: MessagePriority.Normal,
            Topic: topic,
            Payload: payload,
            Timestamp: DateTime.UtcNow,
            CorrelationId: null);
    }

    /// <summary>
    /// Creates an error message indicating a failure.
    /// </summary>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="receiverId">The unique identifier of the receiving agent, or null for broadcasts.</param>
    /// <param name="topic">The topic or channel of the message.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A new error message.</returns>
    public static AgentMessage CreateError(Guid senderId, Guid? receiverId, string topic, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(errorMessage);

        return new AgentMessage(
            Id: Guid.NewGuid(),
            SenderId: senderId,
            ReceiverId: receiverId,
            Type: MessageType.Error,
            Priority: MessagePriority.High,
            Topic: topic,
            Payload: errorMessage,
            Timestamp: DateTime.UtcNow,
            CorrelationId: null);
    }
}

/// <summary>
/// Represents a subscription for receiving messages from the message bus.
/// </summary>
/// <param name="Id">The unique identifier of the subscription.</param>
/// <param name="AgentId">The unique identifier of the subscribing agent.</param>
/// <param name="TopicFilter">The topic filter, or null to receive all topics.</param>
/// <param name="Handler">The async handler function for processing received messages.</param>
public sealed record Subscription(
    Guid Id,
    Guid AgentId,
    string? TopicFilter,
    Func<AgentMessage, Task> Handler)
{
    /// <summary>
    /// Determines whether the subscription matches the given message.
    /// </summary>
    /// <param name="message">The message to check.</param>
    /// <returns>True if the subscription matches the message; otherwise, false.</returns>
    public bool Matches(AgentMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Check if message is targeted at this agent or is a broadcast
        bool isTargeted = message.ReceiverId == AgentId || message.IsBroadcast;

        // Check topic filter
        bool topicMatches = TopicFilter is null ||
                           string.Equals(TopicFilter, message.Topic, StringComparison.OrdinalIgnoreCase);

        return isTargeted && topicMatches;
    }
}

/// <summary>
/// Defines the contract for a message bus enabling inter-agent communication.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to the message bus asynchronously.
    /// </summary>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishAsync(AgentMessage message, CancellationToken ct = default);

    /// <summary>
    /// Sends a request message and awaits a response with the specified timeout.
    /// </summary>
    /// <param name="request">The request message to send.</param>
    /// <param name="timeout">The maximum time to wait for a response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task containing the response message.</returns>
    Task<AgentMessage> RequestAsync(AgentMessage request, TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// Subscribes an agent to receive messages matching the specified topic filter.
    /// </summary>
    /// <param name="agentId">The unique identifier of the subscribing agent.</param>
    /// <param name="topicFilter">The topic filter, or null to receive all topics.</param>
    /// <param name="handler">The async handler function for processing received messages.</param>
    /// <returns>The subscription for managing the subscription lifecycle.</returns>
    Subscription Subscribe(Guid agentId, string? topicFilter, Func<AgentMessage, Task> handler);

    /// <summary>
    /// Unsubscribes a subscription from the message bus.
    /// </summary>
    /// <param name="subscriptionId">The unique identifier of the subscription to remove.</param>
    void Unsubscribe(Guid subscriptionId);

    /// <summary>
    /// Gets all pending messages for a specific agent.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <returns>A read-only list of pending messages.</returns>
    IReadOnlyList<AgentMessage> GetPendingMessages(Guid agentId);
}

/// <summary>
/// Provides a thread-safe in-memory implementation of the message bus for inter-agent communication.
/// </summary>
public sealed class InMemoryMessageBus : IMessageBus, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions;
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<AgentMessage>> _pendingMessages;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<AgentMessage>> _pendingRequests;
    private readonly Channel<AgentMessage> _messageChannel;
    private readonly ConcurrentQueue<AgentMessage> _messageHistory;
    private readonly int _maxHistorySize;
    private readonly CancellationTokenSource _disposalTokenSource;
    private readonly Task _processingTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryMessageBus"/> class.
    /// </summary>
    /// <param name="maxHistorySize">The maximum number of messages to retain in history.</param>
    public InMemoryMessageBus(int maxHistorySize = 1000)
    {
        if (maxHistorySize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHistorySize), "Max history size must be non-negative.");
        }

        _subscriptions = new ConcurrentDictionary<Guid, Subscription>();
        _pendingMessages = new ConcurrentDictionary<Guid, ConcurrentQueue<AgentMessage>>();
        _pendingRequests = new ConcurrentDictionary<Guid, TaskCompletionSource<AgentMessage>>();
        _messageChannel = Channel.CreateUnbounded<AgentMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _messageHistory = new ConcurrentQueue<AgentMessage>();
        _maxHistorySize = maxHistorySize;
        _disposalTokenSource = new CancellationTokenSource();
        _processingTask = ProcessMessagesAsync(_disposalTokenSource.Token);
    }

    /// <summary>
    /// Gets the current number of active subscriptions.
    /// </summary>
    public int SubscriptionCount => _subscriptions.Count;

    /// <summary>
    /// Gets the current number of messages in history.
    /// </summary>
    public int MessageHistoryCount => _messageHistory.Count;

    /// <summary>
    /// Gets an immutable snapshot of the message history.
    /// </summary>
    public ImmutableList<AgentMessage> MessageHistory => _messageHistory.ToImmutableList();

    /// <inheritdoc/>
    public async Task PublishAsync(AgentMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _messageChannel.Writer.WriteAsync(message, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AgentMessage> RequestAsync(AgentMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (request.Type != MessageType.Request)
        {
            throw new ArgumentException("Message must be of type Request.", nameof(request));
        }

        if (request.CorrelationId is null)
        {
            throw new ArgumentException("Request message must have a correlation ID.", nameof(request));
        }

        Guid correlationId = request.CorrelationId.Value;
        TaskCompletionSource<AgentMessage> tcs = new TaskCompletionSource<AgentMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pendingRequests.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException($"A request with correlation ID {correlationId} is already pending.");
        }

        try
        {
            await PublishAsync(request, ct).ConfigureAwait(false);

            using CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            Task completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);

            if (completedTask == tcs.Task)
            {
                return await tcs.Task.ConfigureAwait(false);
            }

            throw new TimeoutException($"Request timed out after {timeout.TotalMilliseconds}ms.");
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    /// <inheritdoc/>
    public Subscription Subscribe(Guid agentId, string? topicFilter, Func<AgentMessage, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        Subscription subscription = new Subscription(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            TopicFilter: topicFilter,
            Handler: handler);

        if (!_subscriptions.TryAdd(subscription.Id, subscription))
        {
            throw new InvalidOperationException("Failed to add subscription. Please try again.");
        }

        // Ensure pending message queue exists for this agent
        _pendingMessages.GetOrAdd(agentId, _ => new ConcurrentQueue<AgentMessage>());

        return subscription;
    }

    /// <inheritdoc/>
    public void Unsubscribe(Guid subscriptionId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _subscriptions.TryRemove(subscriptionId, out _);
    }

    /// <inheritdoc/>
    public IReadOnlyList<AgentMessage> GetPendingMessages(Guid agentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_pendingMessages.TryGetValue(agentId, out ConcurrentQueue<AgentMessage>? queue))
        {
            List<AgentMessage> messages = new List<AgentMessage>();
            while (queue.TryDequeue(out AgentMessage? message))
            {
                messages.Add(message);
            }

            return messages.AsReadOnly();
        }

        return Array.Empty<AgentMessage>();
    }

    /// <summary>
    /// Gets all active subscriptions as an immutable list.
    /// </summary>
    /// <returns>An immutable list of all active subscriptions.</returns>
    public ImmutableList<Subscription> GetAllSubscriptions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _subscriptions.Values.ToImmutableList();
    }

    /// <summary>
    /// Gets all subscriptions for a specific agent.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <returns>A list of subscriptions for the agent.</returns>
    public IReadOnlyList<Subscription> GetSubscriptionsForAgent(Guid agentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _subscriptions.Values
            .Where(s => s.AgentId == agentId)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Clears all message history.
    /// </summary>
    public void ClearHistory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (_messageHistory.TryDequeue(out _))
        {
            // Drain the queue
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _messageChannel.Writer.Complete();
        _disposalTokenSource.Cancel();

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions during disposal
        }

        // Complete all pending requests with cancellation
        foreach (KeyValuePair<Guid, TaskCompletionSource<AgentMessage>> kvp in _pendingRequests)
        {
            kvp.Value.TrySetCanceled();
        }

        _pendingRequests.Clear();
        _disposalTokenSource.Dispose();
    }

    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (AgentMessage message in _messageChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await ProcessMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during disposal
        }
    }

    private async Task ProcessMessageAsync(AgentMessage message)
    {
        // Add to history
        AddToHistory(message);

        // Handle response messages for pending requests
        if (message.Type == MessageType.Response && message.CorrelationId.HasValue)
        {
            if (_pendingRequests.TryRemove(message.CorrelationId.Value, out TaskCompletionSource<AgentMessage>? tcs))
            {
                tcs.TrySetResult(message);
            }
        }

        // Get matching subscriptions ordered by priority
        List<Subscription> matchingSubscriptions = _subscriptions.Values
            .Where(s => s.Matches(message))
            .ToList();

        // Dispatch to all matching subscriptions
        List<Task> dispatchTasks = new List<Task>();
        foreach (Subscription subscription in matchingSubscriptions)
        {
            dispatchTasks.Add(DispatchToSubscriptionAsync(message, subscription));
        }

        // Store as pending for agents without active handlers
        if (message.ReceiverId.HasValue && matchingSubscriptions.Count == 0)
        {
            ConcurrentQueue<AgentMessage> queue = _pendingMessages.GetOrAdd(
                message.ReceiverId.Value,
                _ => new ConcurrentQueue<AgentMessage>());
            queue.Enqueue(message);
        }

        await Task.WhenAll(dispatchTasks).ConfigureAwait(false);
    }

    private async Task DispatchToSubscriptionAsync(AgentMessage message, Subscription subscription)
    {
        try
        {
            await subscription.Handler(message).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Log error in production; for now, we silently handle to prevent one handler from affecting others
            // Consider adding an error event or callback mechanism
        }
    }

    private void AddToHistory(AgentMessage message)
    {
        _messageHistory.Enqueue(message);

        // Trim history if needed
        while (_messageHistory.Count > _maxHistorySize && _messageHistory.TryDequeue(out _))
        {
            // Continue trimming
        }
    }
}

/// <summary>
/// Provides extension methods for working with the message bus.
/// </summary>
public static class MessageBusExtensions
{
    /// <summary>
    /// Publishes a request and returns the response wrapped in a Result monad.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="request">The request message.</param>
    /// <param name="timeout">The maximum time to wait for a response.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A Result containing the response message or an error.</returns>
    public static async Task<Result<AgentMessage>> TryRequestAsync(
        this IMessageBus bus,
        AgentMessage request,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AgentMessage response = await bus.RequestAsync(request, timeout, ct).ConfigureAwait(false);
            return Result<AgentMessage>.Success(response);
        }
        catch (TimeoutException ex)
        {
            return Result<AgentMessage>.Failure($"Request timed out: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return Result<AgentMessage>.Failure("Request was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<AgentMessage>.Failure($"Request failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Publishes a message and returns a Result indicating success or failure.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="message">The message to publish.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A Result indicating success or containing an error.</returns>
    public static async Task<Result<Unit>> TryPublishAsync(
        this IMessageBus bus,
        AgentMessage message,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await bus.PublishAsync(message, ct).ConfigureAwait(false);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit>.Failure("Publish was cancelled.");
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure($"Publish failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Subscribes to messages and wraps the handler result in an Option monad.
    /// </summary>
    /// <typeparam name="T">The expected payload type.</typeparam>
    /// <param name="bus">The message bus.</param>
    /// <param name="agentId">The unique identifier of the subscribing agent.</param>
    /// <param name="topicFilter">The topic filter, or null to receive all topics.</param>
    /// <param name="handler">The handler function that receives the typed payload.</param>
    /// <returns>The subscription for managing the subscription lifecycle.</returns>
    public static Subscription SubscribeTyped<T>(
        this IMessageBus bus,
        Guid agentId,
        string? topicFilter,
        Func<AgentMessage, Option<T>, Task> handler)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(handler);

        return bus.Subscribe(agentId, topicFilter, async message =>
        {
            Option<T> typedPayload = message.Payload is T payload
                ? Option<T>.Some(payload)
                : Option<T>.None();

            await handler(message, typedPayload).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Creates a broadcast message and publishes it.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic of the broadcast.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task BroadcastAsync(
        this IMessageBus bus,
        Guid senderId,
        string topic,
        object payload,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        AgentMessage broadcast = AgentMessage.CreateBroadcast(senderId, topic, payload);
        await bus.PublishAsync(broadcast, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a notification message and publishes it.
    /// </summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="senderId">The unique identifier of the sending agent.</param>
    /// <param name="topic">The topic of the notification.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task NotifyAsync(
        this IMessageBus bus,
        Guid senderId,
        string topic,
        object payload,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(payload);

        AgentMessage notification = AgentMessage.CreateNotification(senderId, topic, payload);
        await bus.PublishAsync(notification, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Represents a unit type for Result monad operations that don't return a value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Gets the singleton unit value.
    /// </summary>
    public static Unit Value => default;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc/>
    public bool Equals(Unit other) => true;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Determines whether two Unit values are equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>Always true.</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two Unit values are not equal.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>Always false.</returns>
    public static bool operator !=(Unit left, Unit right) => false;
}
