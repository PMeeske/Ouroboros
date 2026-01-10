# World Model Learning (F2.2)

## Overview

The World Model Learning feature enables model-based reinforcement learning through learned environment models. It provides tools for learning predictive models from experience data and using them for imagination-based planning.

## Key Features

- **Model Learning**: Learn transition, reward, and terminal predictors from experience data
- **Multiple Architectures**: Support for MLP, Transformer, GNN, and Hybrid architectures (MLP currently implemented)
- **Imagination Planning**: Plan action sequences using the learned model without environment interaction
- **Model Evaluation**: Comprehensive quality metrics including prediction accuracy, reward correlation, and calibration
- **Synthetic Experience**: Generate synthetic trajectories for data augmentation

## Architecture

### Core Types

#### `State`
Represents an environment state with:
- `Features`: Dictionary of named features
- `Embedding`: Float vector representation

#### `Action`
Represents an action with:
- `Name`: Action identifier
- `Parameters`: Dictionary of action parameters

#### `Transition`
Complete experience tuple containing:
- `PreviousState`: State before action
- `ActionTaken`: Executed action
- `NextState`: Resulting state
- `Reward`: Received reward
- `Terminal`: Whether next state is terminal

#### `WorldModel`
Learned model containing:
- `TransitionModel`: Predicts next states (IStatePredictor)
- `RewardModel`: Predicts rewards (IRewardPredictor)
- `TerminalModel`: Predicts terminal states (ITerminalPredictor)
- `Hyperparameters`: Model configuration

### Predictors

#### `MlpStatePredictor`
Multi-layer perceptron for state prediction:
- Xavier initialization
- ReLU activation
- Simple feed-forward architecture

#### `SimpleRewardPredictor`
Linear regression for reward prediction

#### `SimpleTerminalPredictor`
Logistic regression for terminal state classification

## Usage

### Basic Workflow

```csharp
using Ouroboros.Agent.MetaAI.WorldModel;

// 1. Create engine
var engine = new WorldModelEngine(seed: 42);

// 2. Collect experience data
var transitions = new List<Transition>();
// ... collect transitions from environment ...

// 3. Learn model
var modelResult = await engine.LearnModelAsync(
    transitions, 
    ModelArchitecture.MLP);

if (modelResult.IsSuccess)
{
    var model = modelResult.Value;
    
    // 4. Evaluate model
    var evalResult = await engine.EvaluateModelAsync(model, testSet);
    
    // 5. Plan with imagination
    var planResult = await engine.PlanInImaginationAsync(
        initialState,
        goal: "Maximize reward",
        model,
        lookaheadDepth: 5);
    
    // 6. Generate synthetic experience
    var syntheticResult = await engine.GenerateSyntheticExperienceAsync(
        model,
        startState,
        trajectoryLength: 10);
}
```

### Example Application

See `Ouroboros.Examples/WorldModelExample.cs` for a complete working example demonstrating:
- Data generation
- Model learning
- Quality evaluation
- Imagination-based planning
- Synthetic experience generation

Run the example:
```csharp
await WorldModelExample.RunAsync();
```

## API Reference

### IWorldModelEngine

#### `LearnModelAsync`
Learns a world model from experience transitions.

**Parameters:**
- `transitions`: Training data (List<Transition>)
- `architecture`: Model architecture (ModelArchitecture enum)
- `ct`: Cancellation token

**Returns:** `Result<WorldModel, string>`

#### `PredictNextStateAsync`
Predicts the next state given current state and action.

**Parameters:**
- `currentState`: Current state
- `action`: Action to simulate
- `model`: Learned world model
- `ct`: Cancellation token

**Returns:** `Result<State, string>`

#### `PlanInImaginationAsync`
Plans action sequences using model-based imagination.

**Parameters:**
- `initialState`: Starting state
- `goal`: Natural language goal description
- `model`: World model for simulation
- `lookaheadDepth`: Planning horizon
- `ct`: Cancellation token

**Returns:** `Result<Plan, string>`

#### `EvaluateModelAsync`
Evaluates model quality on test data.

**Parameters:**
- `model`: Model to evaluate
- `testSet`: Test transitions
- `ct`: Cancellation token

**Returns:** `Result<ModelQuality, string>`

#### `GenerateSyntheticExperienceAsync`
Generates synthetic trajectories using the model.

**Parameters:**
- `model`: World model
- `startState`: Initial state
- `trajectoryLength`: Number of steps
- `ct`: Cancellation token

**Returns:** `Result<List<Transition>, string>`

## Model Quality Metrics

### `ModelQuality` Record

- **PredictionAccuracy** (0-1): Accuracy of state predictions
- **RewardCorrelation** (0-1): Correlation between predicted and actual rewards
- **TerminalAccuracy** (0-1): Accuracy of terminal state predictions
- **CalibrationError**: Mean calibration error for predictions
- **TestSamples**: Number of test samples used

## Design Principles

### Functional Programming
- All types are immutable records
- Result monad for error handling (no exceptions thrown)
- Pure functions where possible
- Async/await throughout

### Error Handling
All operations return `Result<T, string>` types:
```csharp
var result = await engine.LearnModelAsync(transitions, architecture);

result.Match(
    onSuccess: model => Console.WriteLine($"Model ID: {model.Id}"),
    onFailure: error => Console.WriteLine($"Error: {error}")
);
```

### Cancellation Support
All async operations support cancellation:
```csharp
var cts = new CancellationTokenSource();
var result = await engine.PlanInImaginationAsync(
    state, 
    goal, 
    model, 
    depth: 5,
    ct: cts.Token);
```

## Testing

The implementation includes 25 comprehensive unit tests covering:
- Model learning with various inputs
- State prediction
- Imagination-based planning
- Model evaluation
- Synthetic experience generation
- End-to-end workflow

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~WorldModelEngineTests"
```

All tests follow AAA pattern (Arrange-Act-Assert) and use FluentAssertions.

## Future Enhancements

### Planned Features
1. **Transformer Architecture**: Attention-based models for sequential data
2. **GNN Architecture**: Graph neural networks for structured state spaces
3. **MCTS Planning**: Monte Carlo Tree Search instead of greedy planning
4. **Training Optimization**: SGD, Adam optimizer for actual neural network training
5. **Ensemble Models**: Multiple models for uncertainty estimation
6. **Model Persistence**: Save/load trained models
7. **Visualization**: Tools for visualizing learned dynamics

### Integration Points
- Integrate with existing `Plan` type in `Ouroboros.Pipeline.Verification`
- Connect to RL environments for online learning
- Use with `HierarchicalPlanner` for high-level planning
- Integrate with `ExperienceReplay` for memory management

## References

- Model-Based Reinforcement Learning: [Sutton & Barto, Chapter 8]
- World Models: Ha & Schmidhuber (2018)
- Dreamer: Hafner et al. (2019)
- MuZero: Schrittwieser et al. (2020)

## Contributing

When extending this feature:
1. Maintain immutable types (use records)
2. Use Result<T, string> for error handling
3. Add XML documentation to all public APIs
4. Write comprehensive tests (target >90% coverage)
5. Follow existing patterns in the codebase
6. Update this README with new features
