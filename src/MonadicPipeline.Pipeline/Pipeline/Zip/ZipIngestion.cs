using System.Text;
using System.Xml.Linq;
using System.IO.Compression;

namespace LangChainPipeline.Pipeline.Ingestion.Zip;

public enum ZipContentKind { Csv, Xml, Text, Binary }

public sealed record ZipFileRecord(
    string FullPath,
    string? Directory,
    string FileName,
    ZipContentKind Kind,
    long Length,
    long CompressedLength,
    double CompressionRatio,
    Func<Stream> OpenStream,
    IDictionary<string, object>? Parsed,
    string ZipPath, // path to underlying zip for lifecycle management
    double MaxCompressionRatioLimit // the limit that was applied during scan
);

public sealed record CsvTable(string[] Header, List<string[]> Rows);
public sealed record XmlDoc(XDocument Document);

public static class ZipIngestion
{
    public static Task<IReadOnlyList<ZipFileRecord>> ScanAsync(
        string zipPath,
        long maxTotalBytes = 500 * 1024 * 1024,
        double maxCompressionRatio = 200d,
        CancellationToken ct = default)
    {
        var holder = ZipArchiveRegistry.Acquire(zipPath);
        var archive = holder.Archive;

        var results = new List<ZipFileRecord>();
        long total = 0;

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue; // directory
            total += entry.Length;
            if (total > maxTotalBytes)
                throw new InvalidOperationException("Zip content exceeds allowed size budget");

            string full = entry.FullName.Replace('\\', '/');
            string? dir = full.Contains('/') ? Path.GetDirectoryName(full)?.Replace('\\', '/') : null;
            string file = entry.Name;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            var kind = Classify(ext);
            long compressed = entry.CompressedLength;
            double ratio = compressed == 0 ? double.PositiveInfinity : (double)entry.Length / compressed;
            // Don't mutate original classification on ratio exceed; we will decide to skip during parse
            if (kind == ZipContentKind.Binary && entry.Length > 0 && string.IsNullOrEmpty(ext))
            {
                // Heuristic: probe first bytes to detect if likely text
                try
                {
                    using var probeStream = entry.Open();
                    int toRead = (int)Math.Min(2048, entry.Length);
                    byte[] buf = new byte[toRead];
                    int read = probeStream.Read(buf, 0, toRead);
                    if (IsLikelyText(buf.AsSpan(0, read)))
                        kind = ZipContentKind.Text;
                }
                catch { /* ignore heuristic failure */ }
            }
            var captured = entry; // capture entry while archive kept alive by registry
            Func<Stream> opener = () => captured.Open();
            results.Add(new ZipFileRecord(full, dir, file, kind, entry.Length, compressed, ratio, opener, null, zipPath, maxCompressionRatio));
        }
        return Task.FromResult<IReadOnlyList<ZipFileRecord>>(results);
    }

    private static ZipContentKind Classify(string ext) => ext switch
    {
        ".csv" => ZipContentKind.Csv,
        ".xml" => ZipContentKind.Xml,
        ".txt" => ZipContentKind.Text,
        _ => ZipContentKind.Binary
    };

    public static async Task<IReadOnlyList<ZipFileRecord>> ParseAsync(
        IEnumerable<ZipFileRecord> items,
        int csvMaxLines = 50,
        int binaryMaxBytes = 128 * 1024,
        bool includeXmlText = true,
        CancellationToken ct = default)
    {
        var list = new List<ZipFileRecord>();
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            IDictionary<string, object>? parsed = null;
            try
            {
                bool ratioExceeded = !double.IsInfinity(item.CompressionRatio) && item.CompressionRatio > item.MaxCompressionRatioLimit;
                if (ratioExceeded)
                {
                    parsed = new Dictionary<string, object>
                    {
                        ["type"] = "skipped",
                        ["reason"] = "compression-ratio-exceeded",
                        ["ratio"] = item.CompressionRatio
                    };
                }
                else
                {
                    parsed = item.Kind switch
                    {
                        ZipContentKind.Csv => await SafeCsvAsync(item, csvMaxLines, ct),
                        ZipContentKind.Xml => await SafeXmlAsync(item, includeXmlText, ct),
                        ZipContentKind.Text => await ReadTextAsync(item, binaryMaxBytes, ct),
                        ZipContentKind.Binary => await ReadBinarySummaryAsync(item, binaryMaxBytes, ct),
                        _ => null
                    };
                }
            }
            catch (Exception ex)
            {
                // Last resort: preserve kind intent
                parsed = item.Kind switch
                {
                    ZipContentKind.Csv => new Dictionary<string, object>{{"type","csv"},{"table", new CsvTable(Array.Empty<string>(), [])},{"error", ex.Message}},
                    ZipContentKind.Xml => new Dictionary<string, object>{{"type","xml"},{"root", string.Empty},{"textPreview", string.Empty},{"error", ex.Message}},
                    ZipContentKind.Text => new Dictionary<string, object>{{"type","text"},{"preview", string.Empty},{"truncated", true},{"error", ex.Message}},
                    ZipContentKind.Binary => new Dictionary<string, object>{{"type","binary"},{"size", 0L},{"sha256", string.Empty},{"error", ex.Message}},
                    _ => new Dictionary<string, object>{{"type","error"},{"message", ex.Message}}
                };
            }
            list.Add(item with { Parsed = parsed });
        }
        // release archives referenced
        foreach (var group in list.Select(r => r.ZipPath).Distinct())
        {
            ZipArchiveRegistry.Release(group);
        }
        return list;
    }

    private static async Task<IDictionary<string, object>> SafeCsvAsync(ZipFileRecord rec, int maxLines, CancellationToken ct)
    {
        try
        {
            return await ParseCsvAsync(rec, maxLines, ct);
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "csv",
                ["table"] = new CsvTable(Array.Empty<string>(), []),
                ["error"] = ex.Message
            };
        }
    }

    private static async Task<IDictionary<string, object>> SafeXmlAsync(ZipFileRecord rec, bool includeText, CancellationToken ct)
    {
        try
        {
            return await ParseXmlAsync(rec, includeText, ct);
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "xml",
                ["root"] = string.Empty,
                ["textPreview"] = string.Empty,
                ["error"] = ex.Message
            };
        }
    }

    private static async Task<IDictionary<string, object>> ParseCsvAsync(ZipFileRecord rec, int maxLines, CancellationToken ct)
    {
        using var s = rec.OpenStream();
        using var reader = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        string? headerLine = await reader.ReadLineAsync();
        if (headerLine == null)
            return new Dictionary<string, object> { ["type"] = "csv", ["empty"] = true };
        var header = SplitCsv(headerLine);
        var rows = new List<string[]>();
        string? line;
        while (rows.Count < maxLines && (line = await reader.ReadLineAsync()) != null)
        {
            ct.ThrowIfCancellationRequested();
            rows.Add(SplitCsv(line));
        }
        return new Dictionary<string, object>
        {
            ["type"] = "csv",
            ["table"] = new CsvTable(header, rows),
            ["truncated"] = !reader.EndOfStream
        };
    }

    private static string[] SplitCsv(string line)
    {
        // Robust-ish CSV splitter handling quotes and escaped quotes.
        if (string.IsNullOrEmpty(line)) return Array.Empty<string>();
        List<string> fields = [];
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++; // skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.Select(f => f.Trim()).ToArray();
    }

    private static async Task<IDictionary<string, object>> ParseXmlAsync(ZipFileRecord rec, bool includeText, CancellationToken ct)
    {
        using var s = rec.OpenStream();
        var doc = await Task.Run(() => XDocument.Load(s), ct);
        var allElements = doc.Descendants().ToList();
        int elementCount = allElements.Count;
        int maxDepth = 0;
        var stack = new Stack<(XElement el, int depth)>();
        if (doc.Root != null) stack.Push((doc.Root, 1));
        while (stack.Count > 0)
        {
            var (el, depth) = stack.Pop();
            if (depth > maxDepth) maxDepth = depth;
            foreach (var child in el.Elements()) stack.Push((child, depth + 1));
        }
        var attributeCount = allElements.Sum(e => e.Attributes().Count());
        var topChildren = doc.Root?.Elements().GroupBy(e => e.Name.LocalName)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(10)
            .ToList();
        return new Dictionary<string, object>
        {
            ["type"] = "xml",
            ["root"] = doc.Root?.Name.LocalName ?? string.Empty,
            ["elementCount"] = elementCount,
            ["attributeCount"] = attributeCount,
            ["maxDepth"] = maxDepth,
            ["topChildren"] = topChildren?.Select(tc => new Dictionary<string, object>{{"name", tc.Name},{"count", tc.Count}}).ToList() ?? [],
            ["doc"] = new XmlDoc(doc),
            ["textPreview"] = includeText ? (doc.Root?.Value ?? string.Empty) : string.Empty
        };
    }

    private static async Task<IDictionary<string, object>> ReadTextAsync(ZipFileRecord rec, int maxBytes, CancellationToken ct)
    {
        using var s = rec.OpenStream();
        using var reader = new StreamReader(s, Encoding.UTF8, true);
        char[] buffer = new char[maxBytes];
        int read = await reader.ReadAsync(buffer, 0, buffer.Length);
        string text = new(buffer, 0, read);
        return new Dictionary<string, object>
        {
            ["type"] = "text",
            ["preview"] = text,
            ["truncated"] = !reader.EndOfStream
        };
    }

    private static async Task<IDictionary<string, object>> ReadBinarySummaryAsync(ZipFileRecord rec, int maxBytes, CancellationToken ct)
    {
        using var s = rec.OpenStream();
        byte[] buf = new byte[Math.Min(maxBytes, rec.Length)];
        int read = await s.ReadAsync(buf, 0, buf.Length, ct);
        var hash = ComputeSha256(buf.AsSpan(0, read));
        return new Dictionary<string, object>
        {
            ["type"] = "binary",
            ["size"] = rec.Length,
            ["sha256"] = hash,
            ["sampleHex"] = BitConverter.ToString(buf, 0, Math.Min(read, 64)).Replace("-", "")
        };
    }

    private static string ComputeSha256(ReadOnlySpan<byte> data)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(data.ToArray());
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static bool IsLikelyText(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return true;
        int control = 0;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            // Allow common whitespace and ASCII range
            if (b == 9 || b == 10 || b == 13) continue; // tab/lf/cr
            if (b >= 32 && b < 127) continue;
            control++;
            if (control > data.Length / 10) return false; // >10% control -> binary
        }
        return true;
    }
}

public static class ZipIngestionStreaming
{
    public static async IAsyncEnumerable<ZipFileRecord> EnumerateAsync(string zipPath,
        long maxTotalBytes = 500 * 1024 * 1024,
        double maxCompressionRatio = 200d,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Ensure asynchronous nature even if iteration is fast
        await Task.Yield();
        using var fs = File.OpenRead(zipPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue;
            total += entry.Length;
            if (total > maxTotalBytes)
                yield break; // stop early
            string full = entry.FullName.Replace('\\', '/');
            string? dir = full.Contains('/') ? Path.GetDirectoryName(full)?.Replace('\\', '/') : null;
            string file = entry.Name;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            var kind = ext switch
            {
                ".csv" => ZipContentKind.Csv,
                ".xml" => ZipContentKind.Xml,
                ".txt" => ZipContentKind.Text,
                _ => ZipContentKind.Binary
            };
            long compressed = entry.CompressedLength;
            double ratio = compressed == 0 ? double.PositiveInfinity : (double)entry.Length / compressed;
            if (ratio > maxCompressionRatio) kind = ZipContentKind.Binary;
            else if (kind == ZipContentKind.Binary && entry.Length > 0 && string.IsNullOrEmpty(ext))
            {
                try
                {
                    using var ps = entry.Open();
                    int toRead = (int)Math.Min(2048, entry.Length);
                    byte[] buf = new byte[toRead];
                    int read = ps.Read(buf, 0, toRead);
                    if (ZipIngestionStreamingHelpers.IsLikelyText(buf.AsSpan(0, read))) kind = ZipContentKind.Text;
                }
                catch { }
            }
            var captured = entry;
            Func<Stream> opener = () => captured.Open();
            yield return new ZipFileRecord(full, dir, file, kind, entry.Length, compressed, ratio, opener, null, zipPath, maxCompressionRatio);
        }
    }
}

internal static class ZipIngestionStreamingHelpers
{
    public static bool IsLikelyText(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return true;
        int control = 0;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (b == 9 || b == 10 || b == 13) continue;
            if (b >= 32 && b < 127) continue;
            control++;
            if (control > data.Length / 10) return false;
        }
        return true;
    }
}

public static class DeferredZipTextCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> Map = new();
    public static void Store(string id, string text) => Map[id] = text;
    public static bool TryTake(string id, out string text)
    {
        if (Map.TryRemove(id, out text!)) return true;
        text = string.Empty; return false;
    }
    public static bool TryPeek(string id, out string text) => Map.TryGetValue(id, out text!);
}

internal sealed class ZipArchiveHolder : IDisposable
{
    public FileStream Stream { get; }
    public ZipArchive Archive { get; }
    private int _refCount;
    public ZipArchiveHolder(string path)
    {
        Stream = File.OpenRead(path);
        Archive = new ZipArchive(Stream, ZipArchiveMode.Read, leaveOpen: false);
        _refCount = 1;
    }
    public void AddRef() => Interlocked.Increment(ref _refCount);
    public int ReleaseRef() => Interlocked.Decrement(ref _refCount);
    public void Dispose()
    {
        try { Archive.Dispose(); } catch { }
        try { Stream.Dispose(); } catch { }
    }
}

internal static class ZipArchiveRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ZipArchiveHolder> Map = new(StringComparer.OrdinalIgnoreCase);
    public static ZipArchiveHolder Acquire(string path)
    {
        return Map.AddOrUpdate(path,
            p => new ZipArchiveHolder(p),
            (p, existing) => { existing.AddRef(); return existing; });
    }
    public static void Release(string path)
    {
        if (Map.TryGetValue(path, out var holder))
        {
            if (holder.ReleaseRef() <= 0)
            {
                holder.Dispose();
                Map.TryRemove(path, out _);
            }
        }
    }
}
