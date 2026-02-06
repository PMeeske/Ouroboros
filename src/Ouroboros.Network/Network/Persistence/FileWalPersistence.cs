// <copyright file="FileWalPersistence.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.IO;
using System.Runtime.CompilerServices;

namespace Ouroboros.Network.Persistence;

/// <summary>
/// File-based implementation of the Write-Ahead Log for Merkle-DAG persistence.
/// Uses newline-delimited JSON (NDJSON) format for append-only durability.
/// Thread-safe via internal locking.
/// </summary>
public sealed class FileWalPersistence : IGraphPersistence
{
    private readonly string walFilePath;
    private readonly SemaphoreSlim writeLock;
    private readonly StreamWriter? writer;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWalPersistence"/> class.
    /// </summary>
    /// <param name="walFilePath">The path to the WAL file.</param>
    public FileWalPersistence(string walFilePath)
    {
        this.walFilePath = walFilePath ?? throw new ArgumentNullException(nameof(walFilePath));
        this.writeLock = new SemaphoreSlim(1, 1);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(walFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Open file in append mode
        this.writer = new StreamWriter(
            new FileStream(walFilePath, FileMode.Append, FileAccess.Write, FileShare.Read),
            System.Text.Encoding.UTF8)
        {
            AutoFlush = false, // Batch writes for performance
        };
    }

    /// <summary>
    /// Appends a node addition to the Write-Ahead Log.
    /// </summary>
    /// <param name="node">The node to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AppendNodeAsync(MonadNode node, CancellationToken ct = default)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var nodeJson = JsonSerializer.Serialize(node, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        var entry = new WalEntry(WalEntryType.AddNode, DateTimeOffset.UtcNow, nodeJson);
        await this.AppendEntryAsync(entry, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Appends an edge addition to the Write-Ahead Log.
    /// </summary>
    /// <param name="edge">The edge to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AppendEdgeAsync(TransitionEdge edge, CancellationToken ct = default)
    {
        if (edge == null)
        {
            throw new ArgumentNullException(nameof(edge));
        }

        var edgeJson = JsonSerializer.Serialize(edge, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        var entry = new WalEntry(WalEntryType.AddEdge, DateTimeOffset.UtcNow, edgeJson);
        await this.AppendEntryAsync(entry, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Flushes all pending writes to durable storage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        this.ThrowIfDisposed();

        await this.writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (this.writer != null)
            {
                await this.writer.FlushAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            this.writeLock.Release();
        }
    }

    /// <summary>
    /// Replays all entries from the Write-Ahead Log in chronological order.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of WAL entries.</returns>
    public async IAsyncEnumerable<WalEntry> ReplayAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!File.Exists(this.walFilePath))
        {
            yield break; // Empty WAL - nothing to replay
        }

        using var reader = new StreamReader(
            new FileStream(this.walFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
            System.Text.Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // Skip empty lines
            }

            WalEntry? entry = null;
            try
            {
                entry = JsonSerializer.Deserialize<WalEntry>(line);
            }
            catch (JsonException)
            {
                // Skip corrupted entries - log in production
                continue;
            }

            if (entry != null)
            {
                yield return entry;
            }
        }
    }

    /// <summary>
    /// Disposes the persistence layer and closes the WAL file.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        await this.writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this.writer != null)
            {
                await this.writer.FlushAsync().ConfigureAwait(false);
                await this.writer.DisposeAsync().ConfigureAwait(false);
            }

            this.disposed = true;
        }
        finally
        {
            this.writeLock.Release();
            this.writeLock.Dispose();
        }
    }

    private async Task AppendEntryAsync(WalEntry entry, CancellationToken ct)
    {
        this.ThrowIfDisposed();

        var entryJson = JsonSerializer.Serialize(entry, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        await this.writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (this.writer != null)
            {
                await this.writer.WriteLineAsync(entryJson).ConfigureAwait(false);
            }
        }
        finally
        {
            this.writeLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(FileWalPersistence));
        }
    }
}
