// <copyright file="NeuralNetwork.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Types of neurons in the Ouroboros neural network.
/// </summary>
public enum NeuronType
{
    /// <summary>The core reasoning neuron.</summary>
    Core,

    /// <summary>Handles memory operations.</summary>
    Memory,

    /// <summary>Manages code reflection and modification.</summary>
    CodeReflection,

    /// <summary>Handles MeTTa symbolic reasoning.</summary>
    Symbolic,

    /// <summary>Manages user interaction.</summary>
    Communication,

    /// <summary>Handles safety and ethics.</summary>
    Safety,

    /// <summary>Manages emotional state.</summary>
    Affect,

    /// <summary>Handles goal and task management.</summary>
    Executive,

    /// <summary>Specialized for learning from experience.</summary>
    Learning,

    /// <summary>Simulates user behavior for training.</summary>
    Cognitive,

    /// <summary>Custom/plugin neuron.</summary>
    Custom,
}

/// <summary>
/// Represents a message passed between neurons.
/// </summary>
public sealed record NeuronMessage
{
    /// <summary>Unique message identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Source neuron identifier.</summary>
    public required string SourceNeuron { get; init; }

    /// <summary>Target neuron identifier (null = broadcast).</summary>
    public string? TargetNeuron { get; init; }

    /// <summary>Message type/topic.</summary>
    public required string Topic { get; init; }

    /// <summary>Message payload.</summary>
    public required object Payload { get; init; }

    /// <summary>Message priority.</summary>
    public IntentionPriority Priority { get; init; } = IntentionPriority.Normal;

    /// <summary>When the message was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Time-to-live in seconds (0 = no expiry).</summary>
    public int TtlSeconds { get; init; } = 0;

    /// <summary>Whether this message expects a response.</summary>
    public bool ExpectsResponse { get; init; } = false;

    /// <summary>Correlation ID for request-response patterns.</summary>
    public Guid? CorrelationId { get; init; }

    /// <summary>Vector embedding for semantic routing.</summary>
    public float[]? Embedding { get; init; }
}

/// <summary>
/// Represents a neuron in the Ouroboros internal neural network.
/// </summary>
public abstract class Neuron : IDisposable
{
    private readonly Subject<NeuronMessage> _incomingMessages = new();
    private readonly Subject<NeuronMessage> _outgoingMessages = new();
    private readonly ConcurrentQueue<NeuronMessage> _messageQueue = new();
    private readonly CancellationTokenSource _cts = new();

    private bool _isActive;
    private Task? _processingTask;

    /// <summary>
    /// Unique identifier for this neuron.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The type of this neuron.
    /// </summary>
    public abstract NeuronType Type { get; }

    /// <summary>
    /// Topics this neuron subscribes to.
    /// </summary>
    public abstract IReadOnlySet<string> SubscribedTopics { get; }

    /// <summary>
    /// Observable stream of outgoing messages.
    /// </summary>
    public IObservable<NeuronMessage> OutgoingMessages => _outgoingMessages;

    /// <summary>
    /// Whether this neuron is active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Reference to the neural network (set by network).
    /// </summary>
    public OuroborosNeuralNetwork? Network { get; internal set; }

    /// <summary>
    /// Reference to the intention bus (set by network).
    /// </summary>
    public IntentionBus? IntentionBus { get; internal set; }

    /// <summary>
    /// Starts the neuron.
    /// </summary>
    public void Start()
    {
        if (_isActive) return;
        _isActive = true;
        _processingTask = Task.Run(ProcessMessagesAsync);
        OnStarted();
    }

    /// <summary>
    /// Stops the neuron.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isActive) return;
        _isActive = false;
        _cts.Cancel();
        if (_processingTask != null) await _processingTask;
        OnStopped();
    }

    /// <summary>
    /// Receives a message from the network.
    /// </summary>
    public void ReceiveMessage(NeuronMessage message)
    {
        _messageQueue.Enqueue(message);
        _incomingMessages.OnNext(message);
    }

    /// <summary>
    /// Sends a message to the network.
    /// </summary>
    protected void SendMessage(string topic, object payload, string? targetNeuron = null,
        IntentionPriority priority = IntentionPriority.Normal, bool expectsResponse = false)
    {
        var message = new NeuronMessage
        {
            SourceNeuron = Id,
            TargetNeuron = targetNeuron,
            Topic = topic,
            Payload = payload,
            Priority = priority,
            ExpectsResponse = expectsResponse,
        };

        _outgoingMessages.OnNext(message);
        Network?.RouteMessage(message);
    }

    /// <summary>
    /// Sends a response to a request message.
    /// </summary>
    protected void SendResponse(NeuronMessage request, object payload)
    {
        var response = new NeuronMessage
        {
            SourceNeuron = Id,
            TargetNeuron = request.SourceNeuron,
            Topic = $"{request.Topic}.response",
            Payload = payload,
            CorrelationId = request.Id,
        };

        _outgoingMessages.OnNext(response);
        Network?.RouteMessage(response);
    }

    /// <summary>
    /// Proposes an intention through the bus.
    /// </summary>
    protected void ProposeIntention(
        string title,
        string description,
        string rationale,
        IntentionCategory category,
        IntentionAction? action = null,
        IntentionPriority priority = IntentionPriority.Normal)
    {
        IntentionBus?.ProposeIntention(
            title, description, rationale, category, Id, action, priority);
    }

    /// <summary>
    /// Called when the neuron is started.
    /// </summary>
    protected virtual void OnStarted() { }

    /// <summary>
    /// Called when the neuron is stopped.
    /// </summary>
    protected virtual void OnStopped() { }

    /// <summary>
    /// Processes an incoming message. Override to implement neuron behavior.
    /// </summary>
    protected abstract Task ProcessMessageAsync(NeuronMessage message, CancellationToken ct);

    /// <summary>
    /// Called periodically for autonomous behavior.
    /// </summary>
    protected virtual Task OnTickAsync(CancellationToken ct) => Task.CompletedTask;

    private async Task ProcessMessagesAsync()
    {
        while (_isActive && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Process queued messages
                while (_messageQueue.TryDequeue(out var message))
                {
                    try
                    {
                        await ProcessMessageAsync(message, _cts.Token);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{Id}] Error processing message: {ex.Message}");
                    }
                }

                // Periodic tick
                await OnTickAsync(_cts.Token);

                // Small delay to prevent busy-waiting
                await Task.Delay(100, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        _isActive = false;
        _cts.Cancel();
        _cts.Dispose();
        _incomingMessages.Dispose();
        _outgoingMessages.Dispose();
    }
}

/// <summary>
/// The central neural network that connects all Ouroboros neurons.
/// Implements pub/sub messaging with semantic routing via Qdrant.
/// </summary>
public sealed class OuroborosNeuralNetwork : IDisposable
{
    private readonly ConcurrentDictionary<string, Neuron> _neurons = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _topicSubscribers = new();
    private readonly Subject<NeuronMessage> _messageStream = new();
    private readonly ConcurrentQueue<NeuronMessage> _messageHistory = new();
    private readonly IntentionBus _intentionBus;
    private readonly int _maxHistorySize;
    private ConnectionTopology? _topology;

    private bool _isActive;

    /// <summary>
    /// Creates a new neural network.
    /// </summary>
    /// <param name="intentionBus">The intention bus for proposals.</param>
    /// <param name="maxHistorySize">Maximum messages to retain in history.</param>
    /// <param name="topology">Optional connection topology for weighted routing.</param>
    public OuroborosNeuralNetwork(IntentionBus intentionBus, int maxHistorySize = 1000, ConnectionTopology? topology = null)
    {
        _intentionBus = intentionBus;
        _maxHistorySize = maxHistorySize;
        _topology = topology;
    }

    /// <summary>
    /// Observable stream of all network messages.
    /// </summary>
    public IObservable<NeuronMessage> MessageStream => _messageStream;

    /// <summary>
    /// The intention bus for this network.
    /// </summary>
    public IntentionBus IntentionBus => _intentionBus;

    /// <summary>
    /// Gets or sets the connection topology for weighted routing.
    /// If null, all connections are treated as equal (default behavior).
    /// </summary>
    public ConnectionTopology? Topology
    {
        get => _topology;
        set => _topology = value;
    }

    /// <summary>
    /// Whether the network is active.
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Gets all registered neurons.
    /// </summary>
    public IReadOnlyDictionary<string, Neuron> Neurons => _neurons;

    /// <summary>
    /// Delegate for semantic embedding (for routing).
    /// </summary>
    public Func<string, CancellationToken, Task<float[]>>? EmbedFunction { get; set; }

    /// <summary>
    /// Delegate for storing messages in Qdrant.
    /// </summary>
    public Func<NeuronMessage, CancellationToken, Task>? PersistMessageFunction { get; set; }

    /// <summary>
    /// Delegate for searching similar messages.
    /// </summary>
    public Func<float[], int, CancellationToken, Task<IReadOnlyList<NeuronMessage>>>? SearchSimilarFunction { get; set; }

    /// <summary>
    /// Registers a neuron with the network.
    /// </summary>
    public void RegisterNeuron(Neuron neuron)
    {
        neuron.Network = this;
        neuron.IntentionBus = _intentionBus;
        _neurons[neuron.Id] = neuron;

        // Subscribe to topics
        foreach (var topic in neuron.SubscribedTopics)
        {
            if (!_topicSubscribers.TryGetValue(topic, out var subscribers))
            {
                subscribers = [];
                _topicSubscribers[topic] = subscribers;
            }
            subscribers.Add(neuron.Id);
        }

        // Create default connections based on topic overlap
        if (_topology != null)
        {
            foreach (var existingNeuron in _neurons.Values)
            {
                if (existingNeuron.Id == neuron.Id)
                {
                    continue;
                }

                var sharedTopics = neuron.SubscribedTopics
                    .Intersect(existingNeuron.SubscribedTopics).Count();

                if (sharedTopics > 0)
                {
                    // Neurons with shared interests get default excitatory connection
                    // Only create if connection doesn't already exist
                    var weight = Math.Min(0.5 + (sharedTopics * 0.1), 0.9);
                    if (_topology.GetConnection(existingNeuron.Id, neuron.Id) == null)
                    {
                        _topology.SetConnection(existingNeuron.Id, neuron.Id, weight);
                    }
                    if (_topology.GetConnection(neuron.Id, existingNeuron.Id) == null)
                    {
                        _topology.SetConnection(neuron.Id, existingNeuron.Id, weight);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Unregisters a neuron.
    /// </summary>
    public async Task UnregisterNeuronAsync(string neuronId)
    {
        if (_neurons.TryRemove(neuronId, out var neuron))
        {
            await neuron.StopAsync();

            foreach (var topic in neuron.SubscribedTopics)
            {
                if (_topicSubscribers.TryGetValue(topic, out var subscribers))
                {
                    subscribers.Remove(neuronId);
                }
            }

            neuron.Dispose();
        }
    }

    /// <summary>
    /// Starts the neural network and all neurons.
    /// </summary>
    public void Start()
    {
        if (_isActive) return;
        _isActive = true;

        _intentionBus.Start();

        foreach (var neuron in _neurons.Values)
        {
            neuron.Start();
        }
    }

    /// <summary>
    /// Stops the neural network and all neurons.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isActive) return;
        _isActive = false;

        await _intentionBus.StopAsync();

        foreach (var neuron in _neurons.Values)
        {
            await neuron.StopAsync();
        }
    }

    /// <summary>
    /// Routes a message to appropriate neurons.
    /// </summary>
    public void RouteMessage(NeuronMessage message)
    {
        // Store in history
        _messageHistory.Enqueue(message);
        while (_messageHistory.Count > _maxHistorySize)
        {
            _messageHistory.TryDequeue(out _);
        }

        // Broadcast to stream
        _messageStream.OnNext(message);

        // Persist to Qdrant (async, fire-and-forget)
        if (PersistMessageFunction != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await PersistMessageFunction(message, CancellationToken.None);
                }
                catch { /* Ignore persistence errors */ }
            });
        }

        // Route to specific target
        if (!string.IsNullOrEmpty(message.TargetNeuron))
        {
            if (_neurons.TryGetValue(message.TargetNeuron, out var target))
            {
                target.ReceiveMessage(message);
            }
            return;
        }

        // Route by topic subscription WITH weight modulation
        if (_topicSubscribers.TryGetValue(message.Topic, out var subscribers))
        {
            foreach (var subscriberId in subscribers)
            {
                if (subscriberId != message.SourceNeuron && _neurons.TryGetValue(subscriberId, out var subscriber))
                {
                    // Only apply weight-based routing if topology exists
                    if (_topology != null)
                    {
                        var weight = _topology.GetWeight(message.SourceNeuron, subscriberId);

                        if (weight <= -0.8)
                        {
                            // Strong inhibition — suppress message entirely
                            continue;
                        }
                        else if (weight < 0)
                        {
                            // Weak inhibition — deliver with reduced priority
                            var inhibitedMessage = message with { Priority = IntentionPriority.Low };
                            subscriber.ReceiveMessage(inhibitedMessage);
                        }
                        else
                        {
                            // Excitatory — deliver normally (optionally boost priority for high weights)
                            if (weight > 0.8)
                            {
                                var boostedMessage = message with { Priority = IntentionPriority.High };
                                subscriber.ReceiveMessage(boostedMessage);
                            }
                            else
                            {
                                subscriber.ReceiveMessage(message);
                            }
                        }

                        // Record activation for Hebbian learning
                        _topology.GetConnection(message.SourceNeuron, subscriberId)?.RecordActivation();
                    }
                    else
                    {
                        // No topology - use default behavior
                        subscriber.ReceiveMessage(message);
                    }
                }
            }
        }

        // Also match wildcard subscriptions
        var wildcardTopic = message.Topic.Contains('.') ? message.Topic[..message.Topic.LastIndexOf('.')] + ".*" : "*";
        if (_topicSubscribers.TryGetValue(wildcardTopic, out var wildcardSubscribers))
        {
            foreach (var subscriberId in wildcardSubscribers)
            {
                if (subscriberId != message.SourceNeuron && _neurons.TryGetValue(subscriberId, out var subscriber))
                {
                    // Only apply weight-based routing if topology exists
                    if (_topology != null)
                    {
                        var weight = _topology.GetWeight(message.SourceNeuron, subscriberId);

                        if (weight <= -0.8)
                        {
                            // Strong inhibition — suppress message entirely
                            continue;
                        }
                        else if (weight < 0)
                        {
                            // Weak inhibition — deliver with reduced priority
                            var inhibitedMessage = message with { Priority = IntentionPriority.Low };
                            subscriber.ReceiveMessage(inhibitedMessage);
                        }
                        else
                        {
                            // Excitatory — deliver normally (optionally boost priority for high weights)
                            if (weight > 0.8)
                            {
                                var boostedMessage = message with { Priority = IntentionPriority.High };
                                subscriber.ReceiveMessage(boostedMessage);
                            }
                            else
                            {
                                subscriber.ReceiveMessage(message);
                            }
                        }

                        // Record activation for Hebbian learning
                        _topology.GetConnection(message.SourceNeuron, subscriberId)?.RecordActivation();
                    }
                    else
                    {
                        // No topology - use default behavior
                        subscriber.ReceiveMessage(message);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Broadcasts a message to all neurons.
    /// </summary>
    public void Broadcast(string topic, object payload, string sourceNeuron)
    {
        var message = new NeuronMessage
        {
            SourceNeuron = sourceNeuron,
            Topic = topic,
            Payload = payload,
        };

        foreach (var neuron in _neurons.Values)
        {
            if (neuron.Id != sourceNeuron)
            {
                neuron.ReceiveMessage(message);
            }
        }

        _messageStream.OnNext(message);
    }

    /// <summary>
    /// Gets recent message history.
    /// </summary>
    public IReadOnlyList<NeuronMessage> GetRecentMessages(int count = 50)
    {
        return _messageHistory.TakeLast(count).ToList();
    }

    /// <summary>
    /// Gets the network state summary.
    /// </summary>
    public string GetNetworkState()
    {
        var sb = new StringBuilder();
        sb.AppendLine("🧠 **Ouroboros Neural Network**\n");
        sb.AppendLine($"**Status:** {(_isActive ? "Active 🟢" : "Inactive 🔴")}");
        sb.AppendLine($"**Neurons:** {_neurons.Count}");
        sb.AppendLine($"**Messages in History:** {_messageHistory.Count}");

        // Include topology information if available
        if (_topology != null)
        {
            var weights = _topology.GetWeightSnapshot();
            sb.AppendLine($"**Weighted Connections:** {weights.Count}");

            var excitatoryCount = weights.Count(w => w.Value > 0);
            var inhibitoryCount = weights.Count(w => w.Value < 0);
            sb.AppendLine($"  Excitatory: {excitatoryCount}, Inhibitory: {inhibitoryCount}");
        }

        sb.AppendLine();

        foreach (var neuron in _neurons.Values.OrderBy(n => n.Type))
        {
            var status = neuron.IsActive ? "🟢" : "🔴";
            sb.AppendLine($"  {status} **{neuron.Name}** ({neuron.Type})");
            sb.AppendLine($"     ID: {neuron.Id}, Topics: {string.Join(", ", neuron.SubscribedTopics.Take(3))}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a neuron by ID.
    /// </summary>
    public Neuron? GetNeuron(string id) => _neurons.GetValueOrDefault(id);

    /// <summary>
    /// Gets neurons by type.
    /// </summary>
    public IEnumerable<Neuron> GetNeuronsByType(NeuronType type) => _neurons.Values.Where(n => n.Type == type);

    /// <inheritdoc/>
    public void Dispose()
    {
        _isActive = false;
        _intentionBus.Dispose();

        foreach (var neuron in _neurons.Values)
        {
            neuron.Dispose();
        }

        _neurons.Clear();
        _messageStream.Dispose();
    }
}
