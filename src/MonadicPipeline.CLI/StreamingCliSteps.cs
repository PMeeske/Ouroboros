#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;

namespace LangChainPipeline.CLI;

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

            var options = ParseKeyValueArgs(args);
            string source = options.TryGetValue("source", out var src) ? src : "generated";

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

            var options = ParseKeyValueArgs(args);
            string sizeStr = options.TryGetValue("size", out var sz) ? sz : "5";
            string? slideStr = options.TryGetValue("slide", out var sl) ? sl : null;

            // Check if time-based (contains 's' suffix)
            if (sizeStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                var sizeSeconds = int.Parse(sizeStr.TrimEnd('s', 'S'));
                var size = TimeSpan.FromSeconds(sizeSeconds);

                if (slideStr != null && slideStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                {
                    var slideSeconds = int.Parse(slideStr.TrimEnd('s', 'S'));
                    var slide = TimeSpan.FromSeconds(slideSeconds);
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

            var raw = ParseString(args);
            var operations = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var op in operations)
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

            var options = ParseKeyValueArgs(args);
            string sink = options.TryGetValue("sink", out var snk) ? snk : 
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
                    string path = options.TryGetValue("path", out var p) ? p : "stream_output.txt";
                    var writer = new StreamWriter(path, append: true);
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
    /// Streaming RAG pipeline: continuously processes queries and retrieves relevant context.
    /// Args: 'interval=5s|k=5'
    /// </summary>
    [PipelineToken("StreamRAG", "RAGStream")]
    public static Step<CliPipelineState, CliPipelineState> StreamingRag(string? args = null)
        => s =>
        {
            s.Streaming ??= new StreamingContext();

            var options = ParseKeyValueArgs(args);
            int intervalSeconds = options.TryGetValue("interval", out var intv) && int.TryParse(intv.TrimEnd('s', 'S'), out var iv) ? iv : 5;
            int k = options.TryGetValue("k", out var kStr) && int.TryParse(kStr, out var kv) ? kv : 5;

            // Create a stream that periodically queries for new prompts
            var stream = Observable.Interval(TimeSpan.FromSeconds(intervalSeconds))
                .SelectMany(async _ =>
                {
                    if (!string.IsNullOrWhiteSpace(s.Query) && s.Branch.Store is TrackedVectorStore tvs)
                    {
                        try
                        {
                            var hits = await tvs.GetSimilarDocuments(s.Embed, s.Query, k);
                            var context = string.Join("\n---\n", hits.Select(h => h.PageContent));
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

            var options = ParseKeyValueArgs(args);
            int refreshSeconds = options.TryGetValue("refresh", out var ref0) && int.TryParse(ref0.TrimEnd('s', 'S'), out var rs) ? rs : 1;
            int itemsToShow = options.TryGetValue("items", out var itm) && int.TryParse(itm, out var it) ? it : 5;

            long count = 0;
            DateTime startTime = DateTime.UtcNow;
            var recentItems = new Queue<object>();

            var subscription = s.ActiveStream.Subscribe(
                onNext: item =>
                {
                    count++;
                    recentItems.Enqueue(item);
                    if (recentItems.Count > itemsToShow)
                    {
                        recentItems.Dequeue();
                    }

                    var elapsed = DateTime.UtcNow - startTime;
                    var rate = elapsed.TotalSeconds > 0 ? count / elapsed.TotalSeconds : 0;

                    Console.Clear();
                    Console.WriteLine("╔════════════════════════════════════════════════╗");
                    Console.WriteLine("║         STREAM DASHBOARD                       ║");
                    Console.WriteLine("╠════════════════════════════════════════════════╣");
                    Console.WriteLine($"║ Total Count:    {count,10}                     ║");
                    Console.WriteLine($"║ Rate:           {rate,10:F2} items/sec         ║");
                    Console.WriteLine($"║ Elapsed:        {elapsed.TotalSeconds,10:F1}s              ║");
                    Console.WriteLine("╠════════════════════════════════════════════════╣");
                    Console.WriteLine("║ Recent Items:                                  ║");
                    
                    foreach (var recent in recentItems)
                    {
                        var display = FormatStreamItem(recent);
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
        int count = options.TryGetValue("count", out var cntStr) && int.TryParse(cntStr, out var cnt) ? cnt : 100;
        int intervalMs = options.TryGetValue("interval", out var intvStr) && int.TryParse(intvStr, out var intv) ? intv : 100;

        return Observable.Interval(TimeSpan.FromMilliseconds(intervalMs))
            .Take(count)
            .Select(i => (object)new { Index = i, Value = i * 2, Timestamp = DateTime.UtcNow });
    }

    private static IObservable<object> CreateFileStream(Dictionary<string, string> options, CliPipelineState state)
    {
        string path = options.TryGetValue("path", out var p) ? p : "data.txt";
        
        if (!File.Exists(path))
        {
            return Observable.Empty<object>();
        }

        return Observable.Create<object>(async (observer, cancellationToken) =>
        {
            try
            {
                using var reader = new StreamReader(path);
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
        var channel = Channel.CreateUnbounded<object>();
        
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
        
        var type = item.GetType();
        
        // Try to get Value property (common in anonymous types)
        var valueProp = type.GetProperty("Value");
        if (valueProp != null)
        {
            var value = valueProp.GetValue(item);
            if (value is IConvertible convertible)
            {
                return Convert.ToDouble(convertible);
            }
        }

        // Try Index property
        var indexProp = type.GetProperty("Index");
        if (indexProp != null)
        {
            var value = indexProp.GetValue(item);
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

        var type = item.GetType();

        // Handle anonymous types
        if (type.Name.Contains("AnonymousType"))
        {
            var properties = type.GetProperties();
            var parts = properties.Select(p =>
            {
                var value = p.GetValue(item);
                return $"{p.Name}={value}";
            });
            return $"{{ {string.Join(", ", parts)} }}";
        }

        return item.ToString() ?? "null";
    }

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        var m = System.Text.RegularExpressions.Regex.Match(arg, @"^'(?<s>.*)'$");
        if (m.Success) return m.Groups["s"].Value;
        m = System.Text.RegularExpressions.Regex.Match(arg, @"^""(?<s>.*)""$");
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    private static Dictionary<string, string> ParseKeyValueArgs(string? args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var raw = ParseString(args);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return map;
        }

        foreach (var part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
                await foreach (var item in reader.ReadAllAsync(cancellationToken))
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
