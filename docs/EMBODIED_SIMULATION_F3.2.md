# Embodied Simulation with Unity ML-Agents (F3.2)

## Overview

The Embodied Simulation feature provides a framework for grounding cognition in embodied simulation using Unity ML-Agents integration. This enables agents to learn through sensorimotor experience in simulated environments.

## Key Features

- **Multi-Modal Perception**: Support for visual, proprioceptive, and custom sensor inputs
- **Action Execution**: Execute embodied actions in simulated environments
- **Reinforcement Learning**: Learn from embodied experience using RL algorithms
- **Embodied Planning**: Plan action sequences based on sensorimotor state
- **Unity ML-Agents Integration**: gRPC client for Unity ML-Agents communication
- **Multiple Environment Types**: Support for Unity, OpenAI Gym, custom, and physics simulation environments

## Architecture

### Core Components

#### Domain Types (`Ouroboros.Domain.Embodied`)

- **Vector3** - 3D vector for positions, movements, and velocities
- **Quaternion** - Rotation representation  
- **SensorState** - Complete sensor state including position, rotation, velocity, visual observations, and proprioceptive data
- **EmbodiedAction** - Action representation with movement, rotation, and custom parameters
- **ActionResult** - Result of action execution with reward and terminal state
- **EmbodiedTransition** - State transition for reinforcement learning
- **EnvironmentConfig** - Configuration for environment creation
- **EnvironmentHandle** - Handle to active environment instance
- **Plan** - Sequence of actions to achieve a goal

#### Interfaces

##### IEmbodiedAgent

The main interface for embodied agents:

```csharp
public interface IEmbodiedAgent
{
    Task<Result<Unit, string>> InitializeInEnvironmentAsync(
        EnvironmentConfig environment,
        CancellationToken ct = default);

    Task<Result<SensorState, string>> PerceiveAsync(
        CancellationToken ct = default);

    Task<Result<ActionResult, string>> ActAsync(
        EmbodiedAction action,
        CancellationToken ct = default);

    Task<Result<Unit, string>> LearnFromExperienceAsync(
        IReadOnlyList<EmbodiedTransition> transitions,
        CancellationToken ct = default);

    Task<Result<Plan, string>> PlanEmbodiedAsync(
        string goal,
        SensorState currentState,
        CancellationToken ct = default);
}
```

##### IEnvironmentManager

Manages environment lifecycle:

```csharp
public interface IEnvironmentManager
{
    Task<Result<EnvironmentHandle, string>> CreateEnvironmentAsync(
        EnvironmentConfig config,
        CancellationToken ct = default);

    Task<Result<Unit, string>> ResetEnvironmentAsync(
        EnvironmentHandle handle,
        CancellationToken ct = default);

    Task<Result<Unit, string>> DestroyEnvironmentAsync(
        EnvironmentHandle handle,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<EnvironmentInfo>, string>> ListAvailableEnvironmentsAsync(
        CancellationToken ct = default);
}
```

### Implementation Classes (`Ouroboros.Application.Embodied`)

- **EmbodiedAgent** - Implementation of IEmbodiedAgent with experience buffering
- **EnvironmentManager** - Manages multiple environment instances
- **UnityMLAgentsClient** - gRPC client for Unity ML-Agents communication

## Usage

### Basic Example

```csharp
using Ouroboros.Application.Embodied;
using Ouroboros.Domain.Embodied;

// Create environment manager
var environmentManager = new EnvironmentManager(logger);

// Create embodied agent
var agent = new EmbodiedAgent(environmentManager, logger);

// Configure environment
var config = new EnvironmentConfig(
    SceneName: "BasicNavigation",
    Parameters: new Dictionary<string, object> { ["difficulty"] = 1 },
    AvailableActions: new List<string> { "MoveForward", "TurnLeft", "TurnRight" },
    Type: EnvironmentType.Unity);

// Initialize agent
await agent.InitializeInEnvironmentAsync(config);

// Perception-Action loop
for (int i = 0; i < 100; i++)
{
    // Perceive current state
    var stateResult = await agent.PerceiveAsync();
    var state = stateResult.Value;

    // Decide on action (could use planning or RL policy)
    var action = EmbodiedAction.Move(Vector3.UnitX, "MoveForward");

    // Execute action
    var result = await agent.ActAsync(action);
    
    if (result.Value.EpisodeTerminated)
        break;
}
```

### Unity ML-Agents Integration

```csharp
using var client = new UnityMLAgentsClient("localhost", 5005, logger);

// Connect to Unity
await client.ConnectAsync();

// Send action
var action = EmbodiedAction.Move(new Vector3(1, 0, 0));
var result = await client.SendActionAsync(action);

// Get sensor state
var state = await client.GetSensorStateAsync();

// Reset environment
await client.ResetEnvironmentAsync();

// Disconnect
await client.DisconnectAsync();
```

### Learning from Experience

```csharp
var transitions = new List<EmbodiedTransition>();

// Collect experience
for (int i = 0; i < 100; i++)
{
    var stateBefore = await agent.PerceiveAsync();
    var action = /* select action */;
    var result = await agent.ActAsync(action);
    
    transitions.Add(new EmbodiedTransition(
        StateBefore: stateBefore.Value,
        Action: action,
        StateAfter: result.Value.ResultingState,
        Reward: result.Value.Reward,
        Terminal: result.Value.EpisodeTerminated));
}

// Learn from experience
await agent.LearnFromExperienceAsync(transitions);
```

### Embodied Planning

```csharp
var currentState = await agent.PerceiveAsync();

var planResult = await agent.PlanEmbodiedAsync(
    goal: "Navigate to target location",
    currentState: currentState.Value);

if (planResult.IsSuccess)
{
    var plan = planResult.Value;
    foreach (var action in plan.Actions)
    {
        await agent.ActAsync(action);
    }
}
```

## Unity ML-Agents Setup

### Prerequisites

1. Unity 2020.3 or later
2. ML-Agents Unity package (com.unity.ml-agents)
3. ML-Agents Python package

### Environment Setup

1. **Create Unity Scene**:
   - Add ML-Agents components to your agent GameObject
   - Configure sensors (Camera, RayCast, etc.)
   - Implement agent behavior

2. **Configure Communication**:
   - Set communication channel to gRPC
   - Configure port (default: 5005)
   - Enable training mode

3. **Build and Run**:
   ```bash
   # Build Unity executable
   Unity -batchmode -buildWindows64Player build/Environment.exe

   # Run with gRPC server
   ./build/Environment.exe --port 5005
   ```

### Example Unity Agent

```csharp
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class NavigationAgent : Agent
{
    public override void CollectObservations(VectorSensor sensor)
    {
        // Position
        sensor.AddObservation(transform.position);
        
        // Rotation
        sensor.AddObservation(transform.rotation);
        
        // Velocity
        sensor.AddObservation(GetComponent<Rigidbody>().velocity);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Movement
        Vector3 movement = new Vector3(
            actions.ContinuousActions[0],
            0,
            actions.ContinuousActions[1]);
        
        transform.Translate(movement * Time.deltaTime);
        
        // Rotation
        float rotation = actions.ContinuousActions[2];
        transform.Rotate(0, rotation * Time.deltaTime, 0);
    }
}
```

## Testing

The feature includes comprehensive unit tests with >90% coverage:

```bash
# Run all embodied simulation tests
dotnet test --filter "FullyQualifiedName~Embodied"

# Run specific test categories
dotnet test --filter "FullyQualifiedName~Vector3Tests"
dotnet test --filter "FullyQualifiedName~QuaternionTests"
dotnet test --filter "FullyQualifiedName~EmbodiedAgentTests"
dotnet test --filter "FullyQualifiedName~EnvironmentManagerTests"
```

### Test Coverage

- **Vector3Tests**: 11 tests for 3D vector operations
- **QuaternionTests**: 6 tests for rotation operations
- **EmbodiedAgentTests**: 11 tests for agent behavior
- **EnvironmentManagerTests**: 14 tests for environment lifecycle
- **UnityMLAgentsClientTests**: 11 tests for Unity client

## Functional Programming Patterns

The implementation follows Ouroboros functional programming conventions:

### Result Monad

All fallible operations return `Result<T, E>`:

```csharp
Task<Result<SensorState, string>> PerceiveAsync();
Task<Result<Unit, string>> InitializeInEnvironmentAsync(EnvironmentConfig config);
```

### Immutability

All types are immutable records:

```csharp
public sealed record Vector3(float X, float Y, float Z);
public sealed record SensorState(...);
public sealed record EmbodiedAction(...);
```

### Pure Functions

Operations are pure where possible:

```csharp
public Vector3 Normalized() => /* pure computation */;
public static Vector3 Dot(Vector3 a, Vector3 b) => /* pure */;
```

## Future Enhancements

### Phase 4: Learning Algorithms

- Integrate PPO (Proximal Policy Optimization)
- Integrate SAC (Soft Actor-Critic)  
- Add embodied learning pipeline with neural networks

### Phase 5: Planning Integration

- Integrate with world model (F2.2) for model-based planning
- Add sensorimotor prediction for action outcomes
- Implement hierarchical planning with sub-goals

### Additional Features

- Multi-agent environments
- Transfer learning between environments
- Curriculum learning
- Distributed training support
- Visualization and monitoring tools

## API Reference

Full API documentation is available in the XML documentation comments of each type. Key namespaces:

- `Ouroboros.Domain.Embodied` - Core types and interfaces
- `Ouroboros.Application.Embodied` - Implementation classes

## Contributing

When extending the embodied simulation feature:

1. Follow functional programming patterns (immutability, Result monad)
2. Add comprehensive unit tests (>90% coverage)
3. Use XML documentation for all public APIs
4. Follow existing naming conventions
5. Ensure thread safety for concurrent operations

## References

- Unity ML-Agents: https://github.com/Unity-Technologies/ml-agents
- Embodied Cognition: Varela, F. J., Thompson, E., & Rosch, E. (1991)
- Sensorimotor Grounding: O'Regan, J. K., & Noë, A. (2001)
