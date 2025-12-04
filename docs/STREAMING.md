# Streaming Pipeline Engine

Ouroboros now includes a powerful streaming engine built on **System.Reactive** (Rx) for real-time data processing with live aggregations, windowing, and composable transformations.

## Features

- **Stream Sources**: Generate, file, or channel-based streams
- **Windowing**: Tumbling, sliding, and time-based windows
- **Aggregations**: Count, sum, mean, min, max, and collect
- **Transformations**: Map and filter operations
- **Sinks**: Console, file, or null output
- **Live Dashboard**: Real-time metrics visualization
- **Streaming RAG**: Continuous retrieval-augmented generation
- **Resource Management**: Automatic cleanup with observer pattern
- **Type-Safe**: Fully integrated with the monadic pipeline system

## Quick Start

### DSL Example

```bash
dotnet run --project src/Ouroboros.CLI -- pipeline -d "Stream('source=generated|count=100|interval=50') | Window('5s') | Aggregate('count,mean') | Sink('console')"
```

### Programmatic Example

```csharp
var state = CreatePipelineState();

// Create a stream of generated data
state = await StreamingCliSteps.CreateStream("source=generated|count=100|interval=50")(state);

// Apply 5-second time windows
state = await StreamingCliSteps.ApplyWindow("size=5s")(state);

// Aggregate: count and mean
state = await StreamingCliSteps.ApplyAggregate("count,mean")(state);

// Output to console
state = await StreamingCliSteps.ApplySink("console")(state);

// Cleanup when done
state.Streaming?.Dispose();
```

## Streaming Operators

### Stream/UseStream
Creates a stream from a source.

**Syntax**: `Stream('source=<type>|...')`

**Sources**:
- `generated`: Generates test data
  - `count=N`: Number of items (default: 100)
  - `interval=Ms`: Milliseconds between items (default: 100)
- `file`: Reads from a file line by line
  - `path=<filepath>`: Path to the file
- `channel`: Creates from a System.Threading.Channel

**Examples**:
```
Stream('source=generated|count=50|interval=100')
Stream('source=file|path=data.txt')
```

### StreamWindow/Window
Applies windowing operations.

**Syntax**: `Window('size=<N>|slide=<N>')`

**Window Types**:
- **Tumbling** (count-based): `Window('size=10')`
- **Sliding** (count-based): `Window('size=10|slide=5')`
- **Time-based tumbling**: `Window('size=5s')`
- **Time-based sliding**: `Window('size=5s|slide=2s')`

**Examples**:
```
Window('size=5')              # Tumbling window of 5 items
Window('size=10|slide=3')     # Sliding window: size 10, slide 3
Window('size=2s')             # 2-second time window
Window('size=5s|slide=2s')    # 5s window, slides every 2s
```

### StreamAggregate/Aggregate
Performs live aggregations on windowed streams.

**Syntax**: `Aggregate('op1,op2,...')`

**Operations**:
- `count`: Count items in window
- `sum`: Sum numeric values
- `mean` or `avg`: Average value
- `min`: Minimum value
- `max`: Maximum value
- `collect`: Collect all items in window

**Examples**:
```
Aggregate('count')
Aggregate('count,sum,mean')
Aggregate('min,max')
```

### StreamMap/Map
Transforms stream elements (identity by default).

**Syntax**: `Map()`

### StreamFilter/Filter
Filters stream elements (accepts all by default).

**Syntax**: `Filter()`

### StreamSink/Sink
Outputs stream results to a destination.

**Syntax**: `Sink('<destination>')`

**Destinations**:
- `console`: Write to console (default)
- `file|path=<filepath>`: Write to file
- `null`: Discard output

**Examples**:
```
Sink('console')
Sink('file|path=output.txt')
Sink('null')
```

### StreamRAG/RAGStream
Streaming version of RAG pipeline for continuous queries.

**Syntax**: `StreamRAG('interval=<N>s|k=<N>')`

**Parameters**:
- `interval`: Query interval in seconds (default: 5)
- `k`: Number of documents to retrieve (default: 5)

**Example**:
```
StreamRAG('interval=10s|k=8')
```

### Dashboard
Displays a live metrics dashboard.

**Syntax**: `Dashboard('refresh=<N>s|items=<N>')`

**Parameters**:
- `refresh`: Dashboard refresh rate in seconds (default: 1)
- `items`: Number of recent items to display (default: 5)

**Example**:
```
Dashboard('refresh=1s|items=10')
```

## Complete Pipeline Examples

### Example 1: Basic Windowing and Counting
```
Stream('source=generated|count=100|interval=50') | Window('size=10') | Aggregate('count') | Sink('console')
```

### Example 2: Multiple Aggregations
```
Stream('source=generated|count=200|interval=30') | Window('size=20') | Aggregate('count,sum,mean,min,max') | Sink('console')
```

### Example 3: Time-Based Windows
```
Stream('source=generated|count=500|interval=20') | Window('size=3s') | Aggregate('count') | Sink('console')
```

### Example 4: Sliding Windows
```
Stream('source=generated|count=100|interval=40') | Window('size=10|slide=5') | Aggregate('mean') | Sink('console')
```

### Example 5: File Processing
```
Stream('source=file|path=data.txt') | Window('size=100') | Aggregate('count') | Sink('file|path=results.txt')
```

### Example 6: Live Dashboard
```
Stream('source=generated|count=1000|interval=20') | Dashboard('refresh=1s|items=5')
```

## Resource Management

The streaming engine uses an **observer-based cleanup pattern** to ensure automatic resource cleanup:

```csharp
// Create a streaming context
var streamingContext = new StreamingContext();

// Register disposables for automatic cleanup
var subscription = stream.Subscribe(...);
streamingContext.Register(subscription);

// Dispose all resources when done
streamingContext.Dispose();
```

### Request Isolation

Each `CliPipelineState` has its own `StreamingContext`, ensuring:
- **No cross-contamination** between concurrent requests
- **Automatic cleanup** when context is disposed
- **Thread-safe** resource management

## Backward Compatibility

All existing CLI steps and DSL pipelines continue to work without changes:

```
UseDir('root=src') | UseDraft | UseCritique | UseImprove
```

Streaming steps are purely additive and don't affect existing functionality.

## Implementation Details

### Core Components

1. **StreamingContext**: Manages lifecycle and cleanup
2. **StreamingCliSteps**: Provides streaming operators
3. **CliPipelineState**: Holds active stream and context
4. **StepRegistry**: Automatically discovers streaming operators

### Dependencies

- `System.Reactive` (6.1.0)
- `System.Reactive.Linq` (6.1.0)
- `System.Threading.Channels` (8.0.0)

### Architecture

```
┌─────────────────────┐
│  CliPipelineState   │
├─────────────────────┤
│ StreamingContext    │──┐
│ IObservable<object> │  │
└─────────────────────┘  │
                         │
                         ├──> StreamingContext
                         │    ├─ Subscriptions
                         │    ├─ Cleanup Actions
                         │    └─ Disposal Logic
                         │
                         └──> Observable Pipeline
                              ├─ Source
                              ├─ Windows
                              ├─ Aggregates
                              └─ Sinks
```

## Testing

Comprehensive tests are available in `StreamingEngineTests.cs`:

```bash
dotnet test --filter StreamingEngineTests
```

Tests cover:
- StreamingContext lifecycle
- Concurrent registration
- All streaming operators
- Window operations (tumbling, sliding, time-based)
- Aggregations (count, sum, mean, min, max)
- Complete pipeline integration
- Backward compatibility

## Examples

Full examples are available in `StreamingPipelineExample.cs`:

```csharp
await StreamingPipelineExample.RunAllExamples();
```

Available examples:
1. Basic streaming with windowing
2. Multiple aggregations
3. Time-based windows
4. Sliding windows
5. Live dashboard
6. File processing
7. DSL-based pipelines
8. Complete streaming scenarios

## Performance Considerations

- **Memory**: Windows buffer items in memory
- **Backpressure**: Use appropriate buffer sizes
- **Cleanup**: Always dispose StreamingContext when done
- **Concurrency**: Each state has isolated context

## Future Enhancements

Potential improvements (not yet implemented):
- Expression-based Map and Filter
- Custom aggregation functions
- Parallelization options
- Advanced backpressure strategies
- Persistence for long-running streams
- Integration with external stream sources (Kafka, RabbitMQ, etc.)

## Contributing

When adding new streaming operators:

1. Add method to `StreamingCliSteps.cs`
2. Annotate with `[PipelineToken("Name", "Alias")]`
3. Return `Step<CliPipelineState, CliPipelineState>`
4. Register resources with `StreamingContext`
5. Add tests to `StreamingEngineTests.cs`
6. Update this documentation

## License

Same as Ouroboros project license.
