// <copyright file="EmbodimentAggregate.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ouroboros.Core.Monads;

namespace Ouroboros.Core.EmbodiedInteraction;

/// <summary>
/// Domain aggregate that manages embodiment state and coordinates between
/// multiple embodiment providers. Follows DDD patterns where the aggregate
/// owns the business logic and providers serve as state sources.
/// </summary>
public sealed class EmbodimentAggregate : IDisposable
{
    private readonly ConcurrentDictionary<string, IEmbodimentProvider> _providers = new();
    private readonly ConcurrentDictionary<string, SensorInfo> _activeSensors = new();
    private readonly ConcurrentDictionary<string, ActuatorInfo> _activeActuators = new();
    private readonly Subject<EmbodimentDomainEvent> _domainEvents = new();
    private readonly Subject<PerceptionData> _unifiedPerceptions = new();
    private readonly List<IDisposable> _subscriptions = [];
    private readonly object _lock = new();

    private EmbodimentAggregateState _state;
    private bool _disposed;

    /// <summary>
    /// Initializes a new embodiment aggregate.
    /// </summary>
    /// <param name="aggregateId">Unique identifier for this aggregate.</param>
    /// <param name="name">Human-readable name.</param>
    public EmbodimentAggregate(string aggregateId, string name = "EmbodimentAggregate")
    {
        AggregateId = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));
        Name = name;
        _state = new EmbodimentAggregateState(
            AggregateId,
            Name,
            AggregateStatus.Inactive,
            EmbodimentCapabilities.None,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Gets the aggregate identifier.
    /// </summary>
    public string AggregateId { get; }

    /// <summary>
    /// Gets the aggregate name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the current aggregate state.
    /// </summary>
    public EmbodimentAggregateState State => _state;

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public IReadOnlyDictionary<string, IEmbodimentProvider> Providers => _providers;

    /// <summary>
    /// Gets all active sensors across all providers.
    /// </summary>
    public IReadOnlyDictionary<string, SensorInfo> ActiveSensors => _activeSensors;

    /// <summary>
    /// Gets all active actuators across all providers.
    /// </summary>
    public IReadOnlyDictionary<string, ActuatorInfo> ActiveActuators => _activeActuators;

    /// <summary>
    /// Observable stream of domain events.
    /// </summary>
    public IObservable<EmbodimentDomainEvent> DomainEvents => _domainEvents.AsObservable();

    /// <summary>
    /// Observable stream of unified perceptions from all providers.
    /// </summary>
    public IObservable<PerceptionData> UnifiedPerceptions => _unifiedPerceptions.AsObservable();

    /// <summary>
    /// Registers an embodiment provider with the aggregate.
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    /// <returns>Result indicating success or failure.</returns>
    public Result<EmbodimentAggregate> RegisterProvider(IEmbodimentProvider provider)
    {
        if (_disposed) return Result<EmbodimentAggregate>.Failure("Aggregate is disposed");
        if (provider == null) return Result<EmbodimentAggregate>.Failure("Provider is required");

        if (!_providers.TryAdd(provider.ProviderId, provider))
        {
            return Result<EmbodimentAggregate>.Failure($"Provider '{provider.ProviderId}' is already registered");
        }

        // Subscribe to provider perceptions
        var perceptionSub = provider.Perceptions
            .Subscribe(perception =>
            {
                _unifiedPerceptions.OnNext(perception);
                RaiseDomainEvent(new EmbodimentDomainEvent(
                    EmbodimentDomainEventType.PerceptionReceived,
                    DateTime.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["providerId"] = provider.ProviderId,
                        ["sensorId"] = perception.SensorId,
                        ["modality"] = perception.Modality.ToString()
                    }));
            });

        // Subscribe to provider events
        var eventSub = provider.Events
            .Subscribe(providerEvent =>
            {
                HandleProviderEvent(provider.ProviderId, providerEvent);
            });

        lock (_lock)
        {
            _subscriptions.Add(perceptionSub);
            _subscriptions.Add(eventSub);
        }

        RaiseDomainEvent(new EmbodimentDomainEvent(
            EmbodimentDomainEventType.ProviderRegistered,
            DateTime.UtcNow,
            new Dictionary<string, object>
            {
                ["providerId"] = provider.ProviderId,
                ["providerName"] = provider.ProviderName
            }));

        return Result<EmbodimentAggregate>.Success(this);
    }

    /// <summary>
    /// Unregisters an embodiment provider.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<EmbodimentAggregate>> UnregisterProviderAsync(
        string providerId,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<EmbodimentAggregate>.Failure("Aggregate is disposed");

        if (!_providers.TryRemove(providerId, out var provider))
        {
            return Result<EmbodimentAggregate>.Failure($"Provider '{providerId}' not found");
        }

        // Disconnect and dispose the provider
        await provider.DisconnectAsync(ct);
        provider.Dispose();

        // Remove associated sensors and actuators
        foreach (var key in _activeSensors.Keys.Where(k => k.StartsWith($"{providerId}:")).ToList())
        {
            _activeSensors.TryRemove(key, out _);
        }

        foreach (var key in _activeActuators.Keys.Where(k => k.StartsWith($"{providerId}:")).ToList())
        {
            _activeActuators.TryRemove(key, out _);
        }

        UpdateAggregateCapabilities();

        RaiseDomainEvent(new EmbodimentDomainEvent(
            EmbodimentDomainEventType.ProviderUnregistered,
            DateTime.UtcNow,
            new Dictionary<string, object> { ["providerId"] = providerId }));

        return Result<EmbodimentAggregate>.Success(this);
    }

    /// <summary>
    /// Connects all registered providers and activates the aggregate.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the aggregate capabilities.</returns>
    public async Task<Result<EmbodimentCapabilities>> ActivateAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<EmbodimentCapabilities>.Failure("Aggregate is disposed");
        if (_state.Status == AggregateStatus.Active)
        {
            return Result<EmbodimentCapabilities>.Success(_state.Capabilities);
        }

        UpdateState(_state with { Status = AggregateStatus.Activating });

        var aggregateCapabilities = EmbodimentCapabilities.None;
        var errors = new List<string>();

        foreach (var (providerId, provider) in _providers)
        {
            try
            {
                var connectResult = await provider.ConnectAsync(ct);
                if (connectResult.IsSuccess)
                {
                    aggregateCapabilities |= connectResult.Value;

                    // Load sensors and actuators
                    await LoadProviderResourcesAsync(providerId, provider, ct);
                }
                else
                {
                    errors.Add($"{providerId}: {connectResult.Error}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{providerId}: {ex.Message}");
            }
        }

        if (_providers.Count > 0 && errors.Count == _providers.Count)
        {
            UpdateState(_state with { Status = AggregateStatus.Failed });
            return Result<EmbodimentCapabilities>.Failure(
                $"All providers failed to connect: {string.Join("; ", errors)}");
        }

        UpdateState(_state with
        {
            Status = AggregateStatus.Active,
            Capabilities = aggregateCapabilities,
            LastUpdatedAt = DateTime.UtcNow
        });

        RaiseDomainEvent(new EmbodimentDomainEvent(
            EmbodimentDomainEventType.AggregateActivated,
            DateTime.UtcNow,
            new Dictionary<string, object>
            {
                ["capabilities"] = aggregateCapabilities.ToString(),
                ["providerCount"] = _providers.Count,
                ["sensorCount"] = _activeSensors.Count,
                ["actuatorCount"] = _activeActuators.Count
            }));

        return Result<EmbodimentCapabilities>.Success(aggregateCapabilities);
    }

    /// <summary>
    /// Deactivates the aggregate and disconnects all providers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit>> DeactivateAsync(CancellationToken ct = default)
    {
        if (_disposed) return Result<Unit>.Failure("Aggregate is disposed");
        if (_state.Status == AggregateStatus.Inactive)
        {
            return Result<Unit>.Success(Unit.Default);
        }

        UpdateState(_state with { Status = AggregateStatus.Deactivating });

        foreach (var (_, provider) in _providers)
        {
            await provider.DisconnectAsync(ct);
        }

        _activeSensors.Clear();
        _activeActuators.Clear();

        UpdateState(_state with
        {
            Status = AggregateStatus.Inactive,
            Capabilities = EmbodimentCapabilities.None,
            LastUpdatedAt = DateTime.UtcNow
        });

        RaiseDomainEvent(new EmbodimentDomainEvent(
            EmbodimentDomainEventType.AggregateDeactivated,
            DateTime.UtcNow));

        return Result<Unit>.Success(Unit.Default);
    }

    /// <summary>
    /// Activates a sensor across the aggregate.
    /// </summary>
    /// <param name="sensorId">Full sensor ID (providerId:sensorId).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<SensorInfo>> ActivateSensorAsync(
        string sensorId,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<SensorInfo>.Failure("Aggregate is disposed");

        var (providerId, localSensorId) = ParseResourceId(sensorId);
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            return Result<SensorInfo>.Failure($"Provider '{providerId}' not found");
        }

        var result = await provider.ActivateSensorAsync(localSensorId, ct);
        if (result.IsFailure)
        {
            return Result<SensorInfo>.Failure(result.Error);
        }

        // Update local state
        var sensorsResult = await provider.GetSensorsAsync(ct);
        if (sensorsResult.IsSuccess)
        {
            var sensor = sensorsResult.Value.FirstOrDefault(s => s.SensorId == localSensorId);
            if (sensor != null)
            {
                var fullId = $"{providerId}:{localSensorId}";
                _activeSensors[fullId] = sensor;

                RaiseDomainEvent(new EmbodimentDomainEvent(
                    EmbodimentDomainEventType.SensorActivated,
                    DateTime.UtcNow,
                    new Dictionary<string, object>
                    {
                        ["sensorId"] = fullId,
                        ["modality"] = sensor.Modality.ToString()
                    }));

                return Result<SensorInfo>.Success(sensor);
            }
        }

        return Result<SensorInfo>.Failure("Sensor activated but not found in provider");
    }

    /// <summary>
    /// Deactivates a sensor.
    /// </summary>
    /// <param name="sensorId">Full sensor ID (providerId:sensorId).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit>> DeactivateSensorAsync(
        string sensorId,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<Unit>.Failure("Aggregate is disposed");

        var (providerId, localSensorId) = ParseResourceId(sensorId);
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            return Result<Unit>.Failure($"Provider '{providerId}' not found");
        }

        var result = await provider.DeactivateSensorAsync(localSensorId, ct);
        if (result.IsSuccess)
        {
            _activeSensors.TryRemove(sensorId, out _);

            RaiseDomainEvent(new EmbodimentDomainEvent(
                EmbodimentDomainEventType.SensorDeactivated,
                DateTime.UtcNow,
                new Dictionary<string, object> { ["sensorId"] = sensorId }));
        }

        return result;
    }

    /// <summary>
    /// Reads perception data from a sensor.
    /// </summary>
    /// <param name="sensorId">Full sensor ID (providerId:sensorId).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the perception data.</returns>
    public async Task<Result<PerceptionData>> ReadSensorAsync(
        string sensorId,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<PerceptionData>.Failure("Aggregate is disposed");

        var (providerId, localSensorId) = ParseResourceId(sensorId);
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            return Result<PerceptionData>.Failure($"Provider '{providerId}' not found");
        }

        return await provider.ReadSensorAsync(localSensorId, ct);
    }

    /// <summary>
    /// Executes an action through an actuator.
    /// </summary>
    /// <param name="actuatorId">Full actuator ID (providerId:actuatorId).</param>
    /// <param name="action">The action to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the action outcome.</returns>
    public async Task<Result<ActionOutcome>> ExecuteActionAsync(
        string actuatorId,
        ActuatorAction action,
        CancellationToken ct = default)
    {
        if (_disposed) return Result<ActionOutcome>.Failure("Aggregate is disposed");

        var (providerId, localActuatorId) = ParseResourceId(actuatorId);
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            return Result<ActionOutcome>.Failure($"Provider '{providerId}' not found");
        }

        var result = await provider.ExecuteActionAsync(localActuatorId, action, ct);

        if (result.IsSuccess)
        {
            RaiseDomainEvent(new EmbodimentDomainEvent(
                EmbodimentDomainEventType.ActionExecuted,
                DateTime.UtcNow,
                new Dictionary<string, object>
                {
                    ["actuatorId"] = actuatorId,
                    ["actionType"] = action.ActionType,
                    ["success"] = result.Value.Success
                }));
        }

        return result;
    }

    /// <summary>
    /// Creates a BodySchema from the current aggregate state.
    /// </summary>
    /// <returns>A BodySchema representing the aggregate's embodiment.</returns>
    public BodySchema ToBodySchema()
    {
        var schema = new BodySchema()
            .WithCapability(Capability.Reasoning)
            .WithCapability(Capability.Remembering);

        foreach (var (id, sensor) in _activeSensors)
        {
            var capabilities = sensor.Modality switch
            {
                SensorModality.Audio => new HashSet<Capability> { Capability.Hearing },
                SensorModality.Visual => new HashSet<Capability> { Capability.Seeing },
                SensorModality.Text => new HashSet<Capability> { Capability.Reading },
                _ => new HashSet<Capability>()
            };

            schema = schema.WithSensor(new SensorDescriptor(
                id,
                sensor.Modality,
                sensor.Name,
                sensor.IsActive,
                capabilities,
                sensor.Properties));
        }

        foreach (var (id, actuator) in _activeActuators)
        {
            var capabilities = actuator.Modality switch
            {
                ActuatorModality.Voice => new HashSet<Capability> { Capability.Speaking },
                ActuatorModality.Text => new HashSet<Capability> { Capability.Writing },
                _ => new HashSet<Capability>()
            };

            schema = schema.WithActuator(new ActuatorDescriptor(
                id,
                actuator.Modality,
                actuator.Name,
                actuator.IsActive,
                capabilities,
                actuator.Properties));
        }

        return schema;
    }

    private async Task LoadProviderResourcesAsync(
        string providerId,
        IEmbodimentProvider provider,
        CancellationToken ct)
    {
        var sensorsResult = await provider.GetSensorsAsync(ct);
        if (sensorsResult.IsSuccess)
        {
            foreach (var sensor in sensorsResult.Value)
            {
                var fullId = $"{providerId}:{sensor.SensorId}";
                if (sensor.IsActive)
                {
                    _activeSensors[fullId] = sensor;
                }
            }
        }

        var actuatorsResult = await provider.GetActuatorsAsync(ct);
        if (actuatorsResult.IsSuccess)
        {
            foreach (var actuator in actuatorsResult.Value)
            {
                var fullId = $"{providerId}:{actuator.ActuatorId}";
                if (actuator.IsActive)
                {
                    _activeActuators[fullId] = actuator;
                }
            }
        }
    }

    private void HandleProviderEvent(string providerId, EmbodimentProviderEvent providerEvent)
    {
        var domainEventType = providerEvent.EventType switch
        {
            EmbodimentProviderEventType.Connected => EmbodimentDomainEventType.ProviderConnected,
            EmbodimentProviderEventType.Disconnected => EmbodimentDomainEventType.ProviderDisconnected,
            EmbodimentProviderEventType.MotionDetected => EmbodimentDomainEventType.MotionDetected,
            EmbodimentProviderEventType.PersonDetected => EmbodimentDomainEventType.PersonDetected,
            EmbodimentProviderEventType.Error => EmbodimentDomainEventType.ProviderError,
            _ => EmbodimentDomainEventType.StateChanged
        };

        var details = new Dictionary<string, object> { ["providerId"] = providerId };
        if (providerEvent.Details != null)
        {
            foreach (var (key, value) in providerEvent.Details)
            {
                details[key] = value;
            }
        }

        RaiseDomainEvent(new EmbodimentDomainEvent(domainEventType, providerEvent.Timestamp, details));
    }

    private void UpdateAggregateCapabilities()
    {
        var capabilities = EmbodimentCapabilities.None;
        foreach (var provider in _providers.Values.Where(p => p.IsConnected))
        {
            // We'd need to track capabilities per provider
            // For now, just update based on active sensors/actuators
        }

        UpdateState(_state with { Capabilities = capabilities, LastUpdatedAt = DateTime.UtcNow });
    }

    private void UpdateState(EmbodimentAggregateState newState)
    {
        _state = newState;
        RaiseDomainEvent(new EmbodimentDomainEvent(
            EmbodimentDomainEventType.StateChanged,
            DateTime.UtcNow,
            new Dictionary<string, object> { ["status"] = newState.Status.ToString() }));
    }

    private void RaiseDomainEvent(EmbodimentDomainEvent domainEvent)
    {
        _domainEvents.OnNext(domainEvent);
    }

    private static (string ProviderId, string ResourceId) ParseResourceId(string fullId)
    {
        var parts = fullId.Split(':', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (fullId, fullId);
    }

    /// <summary>
    /// Disposes the aggregate and all providers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }

        foreach (var provider in _providers.Values)
        {
            provider.Dispose();
        }

        _providers.Clear();
        _activeSensors.Clear();
        _activeActuators.Clear();

        _domainEvents.OnCompleted();
        _unifiedPerceptions.OnCompleted();

        _domainEvents.Dispose();
        _unifiedPerceptions.Dispose();
    }
}

/// <summary>
/// State of the embodiment aggregate.
/// </summary>
/// <param name="AggregateId">Aggregate identifier.</param>
/// <param name="Name">Aggregate name.</param>
/// <param name="Status">Current status.</param>
/// <param name="Capabilities">Aggregate capabilities.</param>
/// <param name="LastUpdatedAt">Last update timestamp.</param>
public sealed record EmbodimentAggregateState(
    string AggregateId,
    string Name,
    AggregateStatus Status,
    EmbodimentCapabilities Capabilities,
    DateTime LastUpdatedAt);

/// <summary>
/// Status of the aggregate.
/// </summary>
public enum AggregateStatus
{
    /// <summary>Aggregate is inactive.</summary>
    Inactive,

    /// <summary>Aggregate is activating.</summary>
    Activating,

    /// <summary>Aggregate is active.</summary>
    Active,

    /// <summary>Aggregate is deactivating.</summary>
    Deactivating,

    /// <summary>Aggregate failed to activate.</summary>
    Failed
}

/// <summary>
/// Domain event from the embodiment aggregate.
/// </summary>
/// <param name="EventType">Type of domain event.</param>
/// <param name="Timestamp">When the event occurred.</param>
/// <param name="Details">Event details.</param>
public sealed record EmbodimentDomainEvent(
    EmbodimentDomainEventType EventType,
    DateTime Timestamp,
    IReadOnlyDictionary<string, object>? Details = null);

/// <summary>
/// Types of embodiment domain events.
/// </summary>
public enum EmbodimentDomainEventType
{
    /// <summary>Aggregate activated.</summary>
    AggregateActivated,

    /// <summary>Aggregate deactivated.</summary>
    AggregateDeactivated,

    /// <summary>Provider registered.</summary>
    ProviderRegistered,

    /// <summary>Provider unregistered.</summary>
    ProviderUnregistered,

    /// <summary>Provider connected.</summary>
    ProviderConnected,

    /// <summary>Provider disconnected.</summary>
    ProviderDisconnected,

    /// <summary>Provider error.</summary>
    ProviderError,

    /// <summary>Sensor activated.</summary>
    SensorActivated,

    /// <summary>Sensor deactivated.</summary>
    SensorDeactivated,

    /// <summary>Perception received.</summary>
    PerceptionReceived,

    /// <summary>Action executed.</summary>
    ActionExecuted,

    /// <summary>State changed.</summary>
    StateChanged,

    /// <summary>Motion detected.</summary>
    MotionDetected,

    /// <summary>Person detected.</summary>
    PersonDetected
}
