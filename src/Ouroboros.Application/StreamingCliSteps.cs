#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reactive.Linq;
using System.Threading.Channels;

namespace Ouroboros.Application;

/// <summary>
/// Streaming CLI pipeline steps using System.Reactive for live stream processing.
/// Provides operators for creating, transforming, aggregating, and outputting streams.
/// </summary>
public static class StreamingCliSteps
{
    /// <summary>
    /// Creates a stream from a specified source.
    /// Sources: 'generated' (generates test data), 'file' (reads from file), 'channel' (from channel)
    /// Args: 'source=generated|count=100|interval=100' or 'source=file|path=data.txt'
    /// </summary>
    [PipelineToken("Stream", "UseStream")]
    public static Step<CliPipelineState, CliPipelineState> CreateStream(string? args = null)
        => s =>
        {
            // Ensure streaming context exists
            s.Streaming ??= new StreamingContext();

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string source = options.TryGetValue("source", out string? src) ? src : "generated";

            IObservable<object> stream = source.ToLowerInvariant() switch
            {
                "generated" => CreateGeneratedStream(options, s),
                "file" => CreateFileStream(options, s),
                "channel" => CreateChannelStream(options, s),
                _ => Observable.Empty<object>()
            };

            s.ActiveStream = stream;
            s.Branch = s.Branch.WithIngestEvent($"stream:created:{source}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Stream created: {source}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Applies windowing to the active stream.
    /// Supports tumbling and sliding windows.
    /// Args: 'size=5s' or 'size=10|slide=5' for count-based, 'size=5s|slide=2s' for time-based
    /// </summary>
    [PipelineToken("StreamWindow", "Window")]
    public static Step<CliPipelineState, CliPipelineState> ApplyWindow(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string sizeStr = options.TryGetValue("size", out string? sz) ? sz : "5";
            string? slideStr = options.TryGetValue("slide", out string? sl) ? sl : null;

            // Check if time-based (contains 's' suffix)
            if (sizeStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                int sizeSeconds = int.Parse(sizeStr.TrimEnd('s', 'S'));
                TimeSpan size = TimeSpan.FromSeconds(sizeSeconds);

                if (slideStr != null && slideStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                {
                    int slideSeconds = int.Parse(slideStr.TrimEnd('s', 'S'));
                    TimeSpan slide = TimeSpan.FromSeconds(slideSeconds);
                    s.ActiveStream = s.ActiveStream.Window(size, slide).Select(w => (object)w);
                }
                else
                {
                    s.ActiveStream = s.ActiveStream.Window(size).Select(w => (object)w);
                }
            }
            else
            {
                // Count-based windowing
                int size = int.Parse(sizeStr);
                if (slideStr != null)
                {
                    int slide = int.Parse(slideStr);
                    s.ActiveStream = s.ActiveStream.Window(size, slide).Select(w => (object)w);
                }
                else
                {
                    s.ActiveStream = s.ActiveStream.Window(size).Select(w => (object)w);
                }
            }

            s.Branch = s.Branch.WithIngestEvent($"stream:window:size={sizeStr}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Window applied: size={sizeStr}, slide={slideStr ?? "none"}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Applies aggregations to windowed streams.
    /// Supports: count, sum, mean, min, max, collect
    /// Args: 'count' or 'count,mean' or 'sum|field=value'
    /// </summary>
    [PipelineToken("StreamAggregate", "Aggregate")]
    public static Step<CliPipelineState, CliPipelineState> ApplyAggregate(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            string raw = ParseString(args);
            string[] operations = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string op in operations)
            {
                s.ActiveStream = op.ToLowerInvariant() switch
                {
                    "count" => ApplyCountAggregate(s.ActiveStream),
                    "sum" => ApplySumAggregate(s.ActiveStream),
                    "mean" or "avg" => ApplyMeanAggregate(s.ActiveStream),
                    "min" => ApplyMinAggregate(s.ActiveStream),
                    "max" => ApplyMaxAggregate(s.ActiveStream),
                    "collect" => ApplyCollectAggregate(s.ActiveStream),
                    _ => s.ActiveStream
                };
            }

            s.Branch = s.Branch.WithIngestEvent($"stream:aggregate:{string.Join(",", operations)}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Aggregations applied: {string.Join(", ", operations)}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Maps/transforms elements in the stream.
    /// Args: 'func=...' (not yet implemented - placeholder for future expression support)
    /// </summary>
    [PipelineToken("StreamMap", "Map")]
    public static Step<CliPipelineState, CliPipelineState> ApplyMap(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            // For now, identity map (can be extended with expression evaluation)
            s.ActiveStream = s.ActiveStream.Select(x => x);
            s.Branch = s.Branch.WithIngestEvent("stream:map:identity", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine("[trace] Map applied: identity");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Filters elements in the stream.
    /// Args: 'predicate=...' (not yet implemented - placeholder for future expression support)
    /// </summary>
    [PipelineToken("StreamFilter", "Filter")]
    public static Step<CliPipelineState, CliPipelineState> ApplyFilter(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            // For now, accept all (can be extended with expression evaluation)
            s.ActiveStream = s.ActiveStream.Where(_ => true);
            s.Branch = s.Branch.WithIngestEvent("stream:filter:accept-all", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine("[trace] Filter applied: accept-all");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Outputs stream results to a sink.
    /// Sinks: 'console', 'file', 'null'
    /// Args: 'console' or 'file|path=output.txt' or 'null'
    /// </summary>
    [PipelineToken("StreamSink", "Sink")]
    public static Step<CliPipelineState, CliPipelineState> ApplySink(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string sink = options.TryGetValue("sink", out string? snk) ? snk :
                         (options.ContainsKey("console") ? "console" :
                          options.ContainsKey("file") ? "file" :
                          args?.ToLowerInvariant() ?? "console");

            IDisposable subscription;

            switch (sink.ToLowerInvariant())
            {
                case "console":
                    subscription = s.ActiveStream.Subscribe(
                        onNext: item => Console.WriteLine($"[stream] {FormatStreamItem(item)}"),
                        onError: ex => Console.WriteLine($"[stream:error] {ex.Message}"),
                        onCompleted: () => Console.WriteLine("[stream] completed"));
                    break;

                case "file":
                    string path = options.TryGetValue("path", out string? p) ? p : "stream_output.txt";
                    StreamWriter writer = new StreamWriter(path, append: true);
                    subscription = s.ActiveStream.Subscribe(
                        onNext: item => writer.WriteLine(FormatStreamItem(item)),
                        onError: ex => { writer.WriteLine($"ERROR: {ex.Message}"); writer.Flush(); },
                        onCompleted: () => { writer.WriteLine("COMPLETED"); writer.Flush(); writer.Close(); });
                    break;

                case "null":
                    subscription = s.ActiveStream.Subscribe(_ => { });
                    break;

                default:
                    subscription = s.ActiveStream.Subscribe(_ => { });
                    break;
            }

            // Register for cleanup
            s.Streaming?.Register(subscription);
            s.Branch = s.Branch.WithIngestEvent($"stream:sink:{sink}", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Sink applied: {sink}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Streaming Reasoning pipeline: Thinking -> Draft -> Critique -> Improve.
    /// Uses the configured streaming model.
    /// Args: 'topic=...|k=5'
    /// </summary>
    [PipelineToken("StreamReasoning", "ReasoningStream")]
    public static Step<CliPipelineState, CliPipelineState> StreamingReasoning(string? args = null)
        => async s =>
        {
            if (s.Llm.InnerModel is not LangChainPipeline.Providers.IStreamingChatModel streamingModel)
            {
                Console.WriteLine("[error] Current model does not support streaming reasoning.");
                return s;
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string topic = options.TryGetValue("topic", out string? t) ? t : "General";
            int k = options.TryGetValue("k", out string? kStr) && int.TryParse(kStr, out int kv) ? kv : 5;

            // Use query from state if not provided in args
            string query = s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                 Console.WriteLine("[error] No query provided in state.");
                 return s;
            }

            Console.WriteLine($"[stream] Starting reasoning pipeline for topic: {topic}");

            var pipeline = LangChainPipeline.Pipeline.Reasoning.ReasoningArrows.StreamingReasoningPipeline(
                streamingModel,
                s.Tools ?? new Ouroboros.Tools.ToolRegistry(),
                s.Embed,
                topic,
                query,
                k
            );

            try
            {
                await pipeline.ForEachAsync(item =>
                {
                    Console.Write(item.chunk);
                    s.Branch = item.branch;
                });

                Console.WriteLine(); // Newline at end
                Console.WriteLine("[stream] Reasoning pipeline completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stream:error] {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Streams LLM responses directly through Rx to a console sink with real-time output.
    /// Couples IStreamingChatModel.StreamReasoningContent() → IObservable&lt;string&gt; → Console.Write
    /// Args: 'prefix=[stream]|newline=true'
    /// </summary>
    /// <example>
    /// Pipeline DSL: StreamToConsole('prefix=[ai]|newline=true')
    /// </example>
    [PipelineToken("StreamToConsole", "RxConsole")]
    public static Step<CliPipelineState, CliPipelineState> StreamToConsole(string? args = null)
        => async s =>
        {
            if (s.Llm.InnerModel is not LangChainPipeline.Providers.IStreamingChatModel streamingModel)
            {
                Console.WriteLine("[error] Current model does not support streaming (IStreamingChatModel required).");
                return s;
            }

            string query = s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[error] No query provided. Use SetQuery('your prompt') first.");
                return s;
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            string prefix = options.TryGetValue("prefix", out string? p) ? p : string.Empty;
            bool addNewline = options.TryGetValue("newline", out string? nl) &&
                              (nl.Equals("true", StringComparison.OrdinalIgnoreCase) || nl == "1");

            // Initialize streaming context for subscription management
            s.Streaming ??= new StreamingContext();

            if (!string.IsNullOrEmpty(prefix))
            {
                Console.Write($"{prefix} ");
            }

            // Create the Rx Observable from the streaming model
            IObservable<string> tokenStream = streamingModel.StreamReasoningContent(query);

            // Store active stream as IObservable<object> for composability
            s.ActiveStream = tokenStream.Select(token => (object)token);

            // Collect full response for state tracking
            System.Text.StringBuilder fullResponse = new System.Text.StringBuilder();
            using System.Threading.ManualResetEventSlim completionEvent = new System.Threading.ManualResetEventSlim(false);
            Exception? streamError = null;

            // Subscribe to the Rx stream with console sink
            IDisposable subscription = tokenStream.Subscribe(
                onNext: token =>
                {
                    // Real-time console output
                    Console.Write(token);
                    fullResponse.Append(token);
                },
                onError: ex =>
                {
                    Console.WriteLine();
                    Console.WriteLine($"[stream:error] {ex.Message}");
                    streamError = ex;
                    completionEvent.Set();
                },
                onCompleted: () =>
                {
                    if (addNewline)
                    {
                        Console.WriteLine();
                    }
                    completionEvent.Set();
                }
            );

            // Register subscription for cleanup
            s.Streaming.Register(subscription);

            // Wait for stream completion
            completionEvent.Wait();

            // Update state with the full response
            if (streamError == null)
            {
                s.Output = fullResponse.ToString();
                s.Branch = s.Branch.WithIngestEvent("stream:console:completed", new[] { $"tokens:{fullResponse.Length}" });

                if (s.Trace)
                {
                    Console.WriteLine($"[trace] Stream completed: {fullResponse.Length} chars");
                }
            }

            return s;
        };

    /// <summary>
    /// Creates a live Rx Observable from model streaming that can be piped to other Rx operators.
    /// Use with ApplyMap, ApplyFilter, ApplySink for custom processing.
    /// Args: none (uses current query)
    /// </summary>
    /// <example>
    /// Pipeline DSL: CreateModelStream | ApplyFilter('length > 0') | ApplySink('console')
    /// </example>
    [PipelineToken("CreateModelStream", "ModelStream")]
    public static Step<CliPipelineState, CliPipelineState> CreateModelStream(string? args = null)
        => s =>
        {
            if (s.Llm.InnerModel is not LangChainPipeline.Providers.IStreamingChatModel streamingModel)
            {
                Console.WriteLine("[error] Current model does not support streaming (IStreamingChatModel required).");
                return Task.FromResult(s);
            }

            string query = s.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[error] No query provided. Use SetQuery('your prompt') first.");
                return Task.FromResult(s);
            }

            // Initialize streaming context
            s.Streaming ??= new StreamingContext();

            // Create the Rx Observable from the streaming model
            IObservable<string> tokenStream = streamingModel.StreamReasoningContent(query);

            // Store as IObservable<object> for composability with other Rx operators
            s.ActiveStream = tokenStream.Select(token => (object)token);
            s.Branch = s.Branch.WithIngestEvent("stream:model:created", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Model stream created for query: {query.Substring(0, Math.Min(50, query.Length))}...");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Streaming RAG pipeline: continuously processes queries and retrieves relevant context.
    /// Args: 'interval=5s|k=5'
    /// </summary>
    [PipelineToken("StreamRAG", "RAGStream")]
    public static Step<CliPipelineState, CliPipelineState> StreamingRag(string? args = null)
        => s =>
        {
            s.Streaming ??= new StreamingContext();

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            int intervalSeconds = options.TryGetValue("interval", out string? intv) && int.TryParse(intv.TrimEnd('s', 'S'), out int iv) ? iv : 5;
            int k = options.TryGetValue("k", out string? kStr) && int.TryParse(kStr, out int kv) ? kv : 5;

            // Create a stream that periodically queries for new prompts
            var stream = Observable.Interval(TimeSpan.FromSeconds(intervalSeconds))
                .SelectMany(async _ =>
                {
                    if (!string.IsNullOrWhiteSpace(s.Query) && s.Branch.Store is TrackedVectorStore tvs)
                    {
                        try
                        {
                            IReadOnlyCollection<LangChain.DocumentLoaders.Document> hits = await tvs.GetSimilarDocuments(s.Embed, s.Query, k);
                            string context = string.Join("\n---\n", hits.Select(h => h.PageContent));
                            return new { Query = s.Query, Context = context, Timestamp = DateTime.UtcNow };
                        }
                        catch
                        {
                            return new { Query = s.Query, Context = string.Empty, Timestamp = DateTime.UtcNow };
                        }
                    }
                    return new { Query = string.Empty, Context = string.Empty, Timestamp = DateTime.UtcNow };
                })
                .Where(result => !string.IsNullOrWhiteSpace(result.Query));

            s.ActiveStream = stream.Select(r => (object)r);
            s.Branch = s.Branch.WithIngestEvent($"stream:rag:interval={intervalSeconds}s", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Streaming RAG created: interval={intervalSeconds}s, k={k}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Displays live metrics dashboard for the stream.
    /// Shows count, rate, recent values.
    /// Args: 'refresh=1s|items=5'
    /// </summary>
    [PipelineToken("Dashboard")]
    public static Step<CliPipelineState, CliPipelineState> ShowDashboard(string? args = null)
        => s =>
        {
            if (s.ActiveStream == null)
            {
                s.Branch = s.Branch.WithIngestEvent("stream:error:no-active-stream", Array.Empty<string>());
                return Task.FromResult(s);
            }

            Dictionary<string, string> options = ParseKeyValueArgs(args);
            int refreshSeconds = options.TryGetValue("refresh", out string? ref0) && int.TryParse(ref0.TrimEnd('s', 'S'), out int rs) ? rs : 1;
            int itemsToShow = options.TryGetValue("items", out string? itm) && int.TryParse(itm, out int it) ? it : 5;

            long count = 0;
            DateTime startTime = DateTime.UtcNow;
            Queue<object> recentItems = new Queue<object>();

            IDisposable subscription = s.ActiveStream.Subscribe(
                onNext: item =>
                {
                    count++;
                    recentItems.Enqueue(item);
                    if (recentItems.Count > itemsToShow)
                    {
                        recentItems.Dequeue();
                    }

                    TimeSpan elapsed = DateTime.UtcNow - startTime;
                    double rate = elapsed.TotalSeconds > 0 ? count / elapsed.TotalSeconds : 0;

                    try { Console.Clear(); } catch { /* ignore when output is redirected */ }
                    Console.WriteLine("╔════════════════════════════════════════════════╗");
                    Console.WriteLine("║         STREAM DASHBOARD                       ║");
                    Console.WriteLine("╠════════════════════════════════════════════════╣");
                    Console.WriteLine($"║ Total Count:    {count,10}                     ║");
                    Console.WriteLine($"║ Rate:           {rate,10:F2} items/sec         ║");
                    Console.WriteLine($"║ Elapsed:        {elapsed.TotalSeconds,10:F1}s              ║");
                    Console.WriteLine("╠════════════════════════════════════════════════╣");
                    Console.WriteLine("║ Recent Items:                                  ║");

                    foreach (object recent in recentItems)
                    {
                        string display = FormatStreamItem(recent);
                        if (display.Length > 44)
                        {
                            display = display.Substring(0, 41) + "...";
                        }
                        Console.WriteLine($"║   {display,-44} ║");
                    }

                    Console.WriteLine("╚════════════════════════════════════════════════╝");
                },
                onError: ex => Console.WriteLine($"[dashboard:error] {ex.Message}"),
                onCompleted: () => Console.WriteLine("[dashboard] completed"));

            s.Streaming?.Register(subscription);
            s.Branch = s.Branch.WithIngestEvent($"stream:dashboard:refresh={refreshSeconds}s", Array.Empty<string>());

            if (s.Trace)
            {
                Console.WriteLine($"[trace] Dashboard created: refresh={refreshSeconds}s");
            }

            return Task.FromResult(s);
        };

    // Helper methods

    private static IObservable<object> CreateGeneratedStream(Dictionary<string, string> options, CliPipelineState state)
    {
        int count = options.TryGetValue("count", out string? cntStr) && int.TryParse(cntStr, out int cnt) ? cnt : 100;
        int intervalMs = options.TryGetValue("interval", out string? intvStr) && int.TryParse(intvStr, out int intv) ? intv : 100;

        return Observable.Interval(TimeSpan.FromMilliseconds(intervalMs))
            .Take(count)
            .Select(i => (object)new { Index = i, Value = i * 2, Timestamp = DateTime.UtcNow });
    }

    private static IObservable<object> CreateFileStream(Dictionary<string, string> options, CliPipelineState state)
    {
        string path = options.TryGetValue("path", out string? p) ? p : "data.txt";

        if (!File.Exists(path))
        {
            return Observable.Empty<object>();
        }

        return Observable.Create<object>(async (observer, cancellationToken) =>
        {
            try
            {
                using StreamReader reader = new StreamReader(path);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                {
                    observer.OnNext(line);
                }
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }

    private static IObservable<object> CreateChannelStream(Dictionary<string, string> options, CliPipelineState state)
    {
        Channel<object> channel = Channel.CreateUnbounded<object>();

        // Example: produce some test data
        Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                await channel.Writer.WriteAsync(new { Message = $"Channel item {i}", Timestamp = DateTime.UtcNow });
                await Task.Delay(500);
            }
            channel.Writer.Complete();
        });

        return channel.Reader.AsObservable();
    }

    private static IObservable<object> ApplyCountAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window.Count().Select(c => (object)new { Count = c });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplySumAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Sum()
                        .Select(s => (object)new { Sum = s });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyMeanAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Average()
                        .Select(avg => (object)new { Mean = avg });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyMinAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Min()
                        .Select(min => (object)new { Min = min });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyMaxAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .Select(x => ExtractNumericValue(x))
                        .Max()
                        .Select(max => (object)new { Max = max });
                }
                return Observable.Return(item);
            });
    }

    private static IObservable<object> ApplyCollectAggregate(IObservable<object> stream)
    {
        return stream
            .SelectMany(item =>
            {
                if (item is IObservable<object> window)
                {
                    return window
                        .ToList()
                        .Select(list => (object)new { Items = list, Count = list.Count });
                }
                return Observable.Return(item);
            });
    }

    private static double ExtractNumericValue(object item)
    {
        if (item == null) return 0;

        Type type = item.GetType();

        // Try to get Value property (common in anonymous types)
        System.Reflection.PropertyInfo? valueProp = type.GetProperty("Value");
        if (valueProp != null)
        {
            object? value = valueProp.GetValue(item);
            if (value is IConvertible convertible)
            {
                return Convert.ToDouble(convertible);
            }
        }

        // Try Index property
        System.Reflection.PropertyInfo? indexProp = type.GetProperty("Index");
        if (indexProp != null)
        {
            object? value = indexProp.GetValue(item);
            if (value is IConvertible convertible)
            {
                return Convert.ToDouble(convertible);
            }
        }

        // Direct conversion
        if (item is IConvertible conv)
        {
            try
            {
                return Convert.ToDouble(conv);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    private static string FormatStreamItem(object item)
    {
        if (item == null) return "null";

        Type type = item.GetType();

        // Handle anonymous types
        if (type.Name.Contains("AnonymousType"))
        {
            System.Reflection.PropertyInfo[] properties = type.GetProperties();
            IEnumerable<string> parts = properties.Select(p =>
            {
                object? value = p.GetValue(item);
                return $"{p.Name}={value}";
            });
            return $"{{ {string.Join(", ", parts)} }}";
        }

        return item.ToString() ?? "null";
    }

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(arg, @"^'(?<s>.*)'$");
        if (m.Success) return m.Groups["s"].Value;
        m = System.Text.RegularExpressions.Regex.Match(arg, @"^""(?<s>.*)""$");
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    private static Dictionary<string, string> ParseKeyValueArgs(string? args)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string raw = ParseString(args);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return map;
        }

        foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int idx = part.IndexOf('=');
            if (idx > 0)
            {
                string key = part.Substring(0, idx).Trim();
                string value = part.Substring(idx + 1).Trim();
                map[key] = value;
            }
            else
            {
                map[part.Trim()] = "true";
            }
        }

        return map;
    }
}

/// <summary>
/// Extension methods for Channel readers to convert to observables.
/// </summary>
internal static class ChannelExtensions
{
    public static IObservable<T> AsObservable<T>(this ChannelReader<T> reader)
    {
        return Observable.Create<T>(async (observer, cancellationToken) =>
        {
            try
            {
                await foreach (T? item in reader.ReadAllAsync(cancellationToken))
                {
                    observer.OnNext(item);
                }
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }
}

