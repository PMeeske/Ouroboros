#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Ouroboros.Pipeline.Ingestion.Zip;

/// <summary>
/// Specifies the type of content found in a zip archive entry.
/// </summary>
public enum ZipContentKind
{
    /// <summary>
    /// CSV (Comma-Separated Values) file content.
    /// </summary>
    Csv,

    /// <summary>
    /// XML (Extensible Markup Language) document content.
    /// </summary>
    Xml,

    /// <summary>
    /// Plain text content.
    /// </summary>
    Text,

    /// <summary>
    /// Binary or unknown content type.
    /// </summary>
    Binary
}

/// <summary>
/// Represents metadata and access to a file within a zip archive.
/// </summary>
/// <param name="FullPath">The full path of the file within the zip archive.</param>
/// <param name="Directory">The directory path of the file, if any.</param>
/// <param name="FileName">The name of the file.</param>
/// <param name="Kind">The classified content type of the file.</param>
/// <param name="Length">The uncompressed size of the file in bytes.</param>
/// <param name="CompressedLength">The compressed size of the file in bytes.</param>
/// <param name="CompressionRatio">The compression ratio (uncompressed/compressed).</param>
/// <param name="OpenStream">A function to open a stream to the file's content.</param>
/// <param name="Parsed">Optional parsed content metadata, populated after parsing.</param>
/// <param name="ZipPath">The file path to the underlying zip archive for lifecycle management.</param>
/// <param name="MaxCompressionRatioLimit">The compression ratio limit that was applied during scan.</param>
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

/// <summary>
/// Represents a parsed CSV table with header and data rows.
/// </summary>
/// <param name="Header">The header row containing column names.</param>
/// <param name="Rows">The data rows of the table.</param>
public sealed record CsvTable(string[] Header, List<string[]> Rows);

/// <summary>
/// Wraps an XML document loaded from a zip entry.
/// </summary>
/// <param name="Document">The loaded XML document.</param>
public sealed record XmlDoc(XDocument Document);

/// <summary>
/// Provides utilities for ingesting and parsing files from zip archives.
/// </summary>
public static class ZipIngestion
{
    /// <summary>
    /// Scans a zip archive and returns metadata for all files, applying size and compression ratio limits.
    /// </summary>
    /// <param name="zipPath">The file path to the zip archive to scan.</param>
    /// <param name="maxTotalBytes">Maximum total uncompressed bytes allowed across all entries (default: 500 MB).</param>
    /// <param name="maxCompressionRatio">Maximum allowed compression ratio to detect zip bombs (default: 200).</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A read-only list of file records from the zip archive.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the total size exceeds maxTotalBytes.</exception>
    public static Task<IReadOnlyList<ZipFileRecord>> ScanAsync(
        string zipPath,
        long maxTotalBytes = 500 * 1024 * 1024,
        double maxCompressionRatio = 200d,
        CancellationToken ct = default)
    {
        ZipArchiveHolder holder = ZipArchiveRegistry.Acquire(zipPath);
        ZipArchive archive = holder.Archive;

        List<ZipFileRecord> results = new List<ZipFileRecord>();
        long total = 0;

        foreach (ZipArchiveEntry entry in archive.Entries)
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
            ZipContentKind kind = Classify(ext);
            long compressed = entry.CompressedLength;
            double ratio = compressed == 0 ? double.PositiveInfinity : (double)entry.Length / compressed;
            // Don't mutate original classification on ratio exceed; we will decide to skip during parse
            if (kind == ZipContentKind.Binary && entry.Length > 0 && string.IsNullOrEmpty(ext))
            {
                // Heuristic: probe first bytes to detect if likely text
                try
                {
                    using Stream probeStream = entry.Open();
                    int toRead = (int)Math.Min(2048, entry.Length);
                    byte[] buf = new byte[toRead];
                    int read = probeStream.Read(buf, 0, toRead);
                    if (IsLikelyText(buf.AsSpan(0, read)))
                        kind = ZipContentKind.Text;
                }
                catch { /* ignore heuristic failure */ }
            }
            ZipArchiveEntry captured = entry; // capture entry while archive kept alive by registry
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

    /// <summary>
    /// Parses the content of zip file records based on their detected type.
    /// </summary>
    /// <param name="items">The collection of zip file records to parse.</param>
    /// <param name="csvMaxLines">Maximum number of CSV lines to read (default: 50).</param>
    /// <param name="binaryMaxBytes">Maximum bytes to read from binary/text files (default: 128 KB).</param>
    /// <param name="includeXmlText">Whether to include text preview for XML documents (default: true).</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>A read-only list of file records with parsed content populated.</returns>
    public static async Task<IReadOnlyList<ZipFileRecord>> ParseAsync(
        IEnumerable<ZipFileRecord> items,
        int csvMaxLines = 50,
        int binaryMaxBytes = 128 * 1024,
        bool includeXmlText = true,
        CancellationToken ct = default)
    {
        List<ZipFileRecord> list = new List<ZipFileRecord>();
        foreach (ZipFileRecord item in items)
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
                    ZipContentKind.Csv => new Dictionary<string, object> { { "type", "csv" }, { "table", new CsvTable(Array.Empty<string>(), []) }, { "error", ex.Message } },
                    ZipContentKind.Xml => new Dictionary<string, object> { { "type", "xml" }, { "root", string.Empty }, { "textPreview", string.Empty }, { "error", ex.Message } },
                    ZipContentKind.Text => new Dictionary<string, object> { { "type", "text" }, { "preview", string.Empty }, { "truncated", true }, { "error", ex.Message } },
                    ZipContentKind.Binary => new Dictionary<string, object> { { "type", "binary" }, { "size", 0L }, { "sha256", string.Empty }, { "error", ex.Message } },
                    _ => new Dictionary<string, object> { { "type", "error" }, { "message", ex.Message } }
                };
            }
            list.Add(item with { Parsed = parsed });
        }
        // release archives referenced
        foreach (string? group in list.Select(r => r.ZipPath).Distinct())
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
        using Stream s = rec.OpenStream();
        using StreamReader reader = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        string? headerLine = await reader.ReadLineAsync();
        if (headerLine == null)
            return new Dictionary<string, object> { ["type"] = "csv", ["empty"] = true };
        string[] header = SplitCsv(headerLine);
        List<string[]> rows = new List<string[]>();
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
        StringBuilder sb = new StringBuilder();
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
        using Stream s = rec.OpenStream();
        XDocument doc = await Task.Run(() => XDocument.Load(s), ct);
        List<XElement> allElements = doc.Descendants().ToList();
        int elementCount = allElements.Count;
        int maxDepth = 0;
        Stack<(XElement el, int depth)> stack = new Stack<(XElement el, int depth)>();
        if (doc.Root != null) stack.Push((doc.Root, 1));
        while (stack.Count > 0)
        {
            (XElement el, int depth) = stack.Pop();
            if (depth > maxDepth) maxDepth = depth;
            foreach (XElement child in el.Elements()) stack.Push((child, depth + 1));
        }
        int attributeCount = allElements.Sum(e => e.Attributes().Count());
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
            ["topChildren"] = topChildren?.Select(tc => new Dictionary<string, object> { { "name", tc.Name }, { "count", tc.Count } }).ToList() ?? [],
            ["doc"] = new XmlDoc(doc),
            ["textPreview"] = includeText ? (doc.Root?.Value ?? string.Empty) : string.Empty
        };
    }

    private static async Task<IDictionary<string, object>> ReadTextAsync(ZipFileRecord rec, int maxBytes, CancellationToken ct)
    {
        using Stream s = rec.OpenStream();
        using StreamReader reader = new StreamReader(s, Encoding.UTF8, true);
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
        using Stream s = rec.OpenStream();
        byte[] buf = new byte[Math.Min(maxBytes, rec.Length)];
        int read = await s.ReadAsync(buf, 0, buf.Length, ct);
        string hash = ComputeSha256(buf.AsSpan(0, read));
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
        using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
        byte[] hash = sha.ComputeHash(data.ToArray());
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

/// <summary>
/// Provides streaming enumeration of zip archive entries for memory-efficient processing.
/// </summary>
public static class ZipIngestionStreaming
{
    /// <summary>
    /// Asynchronously enumerates files in a zip archive with applied size and compression limits.
    /// This method provides streaming access without loading all entries into memory at once.
    /// </summary>
    /// <param name="zipPath">The file path to the zip archive to enumerate.</param>
    /// <param name="maxTotalBytes">Maximum total uncompressed bytes allowed across all entries (default: 500 MB).</param>
    /// <param name="maxCompressionRatio">Maximum allowed compression ratio to detect zip bombs (default: 200).</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>An async enumerable of zip file records.</returns>
    public static async IAsyncEnumerable<ZipFileRecord> EnumerateAsync(string zipPath,
        long maxTotalBytes = 500 * 1024 * 1024,
        double maxCompressionRatio = 200d,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Ensure asynchronous nature even if iteration is fast
        await Task.Yield();
        using FileStream fs = File.OpenRead(zipPath);
        using ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
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
            ZipContentKind kind = ext switch
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
                    using Stream ps = entry.Open();
                    int toRead = (int)Math.Min(2048, entry.Length);
                    byte[] buf = new byte[toRead];
                    int read = ps.Read(buf, 0, toRead);
                    if (ZipIngestionStreamingHelpers.IsLikelyText(buf.AsSpan(0, read))) kind = ZipContentKind.Text;
                }
                catch { }
            }
            ZipArchiveEntry captured = entry;
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

/// <summary>
/// Provides a thread-safe cache for storing and retrieving deferred zip text content.
/// </summary>
public static class DeferredZipTextCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Map = new();

    /// <summary>
    /// Stores text content associated with an identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the cached text.</param>
    /// <param name="text">The text content to store.</param>
    public static void Store(string id, string text) => Map[id] = text;

    /// <summary>
    /// Attempts to retrieve and remove text content associated with an identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the cached text.</param>
    /// <param name="text">When this method returns, contains the retrieved text if found; otherwise, an empty string.</param>
    /// <returns>True if the text was found and removed; otherwise, false.</returns>
    public static bool TryTake(string id, out string text)
    {
        if (Map.TryRemove(id, out text!)) return true;
        text = string.Empty; return false;
    }

    /// <summary>
    /// Attempts to retrieve text content associated with an identifier without removing it.
    /// </summary>
    /// <param name="id">The unique identifier for the cached text.</param>
    /// <param name="text">When this method returns, contains the retrieved text if found; otherwise, null.</param>
    /// <returns>True if the text was found; otherwise, false.</returns>
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
        if (Map.TryGetValue(path, out ZipArchiveHolder? holder))
        {
            if (holder.ReleaseRef() <= 0)
            {
                holder.Dispose();
                Map.TryRemove(path, out _);
            }
        }
    }
}
