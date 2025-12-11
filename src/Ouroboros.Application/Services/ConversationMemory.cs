// <copyright file="ConversationMemory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Text;
using System.Text.Json;
using LangChainPipeline.Domain;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// A single conversation turn.
/// </summary>
public sealed record ConversationTurn(
    string Role,
    string Content,
    DateTime Timestamp,
    string? SessionId = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// A conversation session containing multiple turns.
/// </summary>
public sealed record ConversationSession(
    string SessionId,
    string PersonaName,
    DateTime StartedAt,
    DateTime LastActivityAt,
    List<ConversationTurn> Turns,
    string? Summary = null);

/// <summary>
/// Configuration for conversation memory.
/// </summary>
public sealed record ConversationMemoryConfig
{
    /// <summary>Directory for storing conversation files.</summary>
    public string StorageDirectory { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ouroboros", "conversations");

    /// <summary>Qdrant endpoint for semantic memory.</summary>
    public string QdrantEndpoint { get; init; } = "http://localhost:6334";

    /// <summary>Collection name for conversation embeddings.</summary>
    public string CollectionName { get; init; } = "ouroboros_conversations";

    /// <summary>Max turns to keep in active memory.</summary>
    public int MaxActiveTurns { get; init; } = 50;

    /// <summary>How many recent sessions to load on startup.</summary>
    public int RecentSessionsToLoad { get; init; } = 5;

    /// <summary>Vector dimensions.</summary>
    public int VectorSize { get; init; } = 768;
}

/// <summary>
/// Persistent conversation memory with semantic search capabilities.
/// Saves conversations to disk and indexes them in Qdrant for semantic recall.
/// </summary>
public sealed class PersistentConversationMemory : IAsyncDisposable
{
    private readonly ConversationMemoryConfig _config;
    private readonly IEmbeddingModel? _embedding;
    private readonly QdrantClient? _qdrantClient;
    private readonly List<ConversationSession> _recentSessions = new();
    private ConversationSession? _currentSession;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _isInitialized;

    /// <summary>
    /// Gets the current active session.
    /// </summary>
    public ConversationSession? CurrentSession => _currentSession;

    /// <summary>
    /// Gets recent sessions.
    /// </summary>
    public IReadOnlyList<ConversationSession> RecentSessions => _recentSessions;

    /// <summary>
    /// Creates a new persistent conversation memory instance.
    /// </summary>
    public PersistentConversationMemory(
        IEmbeddingModel? embedding = null,
        ConversationMemoryConfig? config = null)
    {
        _config = config ?? new ConversationMemoryConfig();
        _embedding = embedding;

        if (_embedding != null)
        {
            try
            {
                var uri = new Uri(_config.QdrantEndpoint);
                _qdrantClient = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");
            }
            catch
            {
                // Qdrant not available, continue without semantic memory
            }
        }
    }

    /// <summary>
    /// Initializes the memory system, loading recent sessions.
    /// </summary>
    public async Task InitializeAsync(string personaName, CancellationToken ct = default)
    {
        if (_isInitialized) return;

        // Ensure storage directory exists
        Directory.CreateDirectory(_config.StorageDirectory);

        // Load recent sessions
        await LoadRecentSessionsAsync(personaName, ct);

        // Create new session
        _currentSession = new ConversationSession(
            SessionId: Guid.NewGuid().ToString("N")[..8],
            PersonaName: personaName,
            StartedAt: DateTime.UtcNow,
            LastActivityAt: DateTime.UtcNow,
            Turns: new List<ConversationTurn>());

        // Ensure Qdrant collection exists
        if (_qdrantClient != null)
        {
            await EnsureCollectionExistsAsync(ct);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Adds a conversation turn and persists it.
    /// </summary>
    public async Task AddTurnAsync(
        string role,
        string content,
        CancellationToken ct = default)
    {
        if (_currentSession == null) return;

        var turn = new ConversationTurn(
            Role: role,
            Content: content,
            Timestamp: DateTime.UtcNow,
            SessionId: _currentSession.SessionId);

        _currentSession.Turns.Add(turn);
        _currentSession = _currentSession with { LastActivityAt = DateTime.UtcNow };

        // Persist to disk
        await SaveCurrentSessionAsync(ct);

        // Index to Qdrant for semantic search
        if (_embedding != null && _qdrantClient != null)
        {
            await IndexTurnAsync(turn, ct);
        }
    }

    /// <summary>
    /// Gets conversation history for the prompt.
    /// </summary>
    public List<(string Role, string Content)> GetActiveHistory(int maxTurns = 20)
    {
        var result = new List<(string Role, string Content)>();

        // Add summary of previous sessions if available
        foreach (var session in _recentSessions.TakeLast(2))
        {
            if (!string.IsNullOrEmpty(session.Summary))
            {
                result.Add(("system", $"[Previous conversation summary from {session.StartedAt:g}]: {session.Summary}"));
            }
            else if (session.Turns.Count > 0)
            {
                // Add a few turns from previous session as context
                foreach (var turn in session.Turns.TakeLast(3))
                {
                    result.Add((turn.Role, $"[From {turn.Timestamp:g}]: {turn.Content}"));
                }
            }
        }

        // Add current session turns
        if (_currentSession != null)
        {
            foreach (var turn in _currentSession.Turns.TakeLast(maxTurns))
            {
                result.Add((turn.Role, turn.Content));
            }
        }

        return result;
    }

    /// <summary>
    /// Searches past conversations semantically.
    /// </summary>
    public async Task<List<ConversationTurn>> SearchMemoryAsync(
        string query,
        int limit = 5,
        CancellationToken ct = default)
    {
        if (_embedding == null || _qdrantClient == null)
        {
            // Fall back to simple text search
            return SearchMemoryLocal(query, limit);
        }

        try
        {
            var queryEmbedding = await _embedding.CreateEmbeddingsAsync(query, ct);

            var exists = await _qdrantClient.CollectionExistsAsync(_config.CollectionName, ct);
            if (!exists) return new List<ConversationTurn>();

            var results = await _qdrantClient.SearchAsync(
                _config.CollectionName,
                queryEmbedding,
                limit: (ulong)limit,
                cancellationToken: ct);

            return results.Select(r => new ConversationTurn(
                Role: r.Payload.TryGetValue("role", out var role) ? role.StringValue : "unknown",
                Content: r.Payload.TryGetValue("content", out var content) ? content.StringValue : "",
                Timestamp: r.Payload.TryGetValue("timestamp", out var ts)
                    ? DateTime.Parse(ts.StringValue)
                    : DateTime.MinValue,
                SessionId: r.Payload.TryGetValue("session_id", out var sid) ? sid.StringValue : null
            )).ToList();
        }
        catch
        {
            return SearchMemoryLocal(query, limit);
        }
    }

    /// <summary>
    /// Gets a summary of what Ouroboros remembers about a topic.
    /// </summary>
    public async Task<string> RecallAboutAsync(string topic, CancellationToken ct = default)
    {
        var memories = await SearchMemoryAsync(topic, limit: 10, ct);

        if (memories.Count == 0)
        {
            return $"I don't have any specific memories about '{topic}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Here's what I remember about '{topic}':");
        sb.AppendLine();

        var groupedBySession = memories.GroupBy(m => m.SessionId);
        foreach (var group in groupedBySession)
        {
            var firstTurn = group.First();
            sb.AppendLine($"From conversation on {firstTurn.Timestamp:g}:");
            foreach (var turn in group.Take(3))
            {
                var preview = turn.Content.Length > 100
                    ? turn.Content.Substring(0, 100) + "..."
                    : turn.Content;
                sb.AppendLine($"  [{turn.Role}]: {preview}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets conversation statistics.
    /// </summary>
    public ConversationStats GetStats()
    {
        var totalTurns = _recentSessions.Sum(s => s.Turns.Count) + (_currentSession?.Turns.Count ?? 0);
        var totalSessions = _recentSessions.Count + (_currentSession != null ? 1 : 0);

        return new ConversationStats
        {
            TotalSessions = totalSessions,
            TotalTurns = totalTurns,
            CurrentSessionTurns = _currentSession?.Turns.Count ?? 0,
            OldestMemory = _recentSessions.FirstOrDefault()?.StartedAt,
            CurrentSessionStart = _currentSession?.StartedAt
        };
    }

    /// <summary>
    /// Ends the current session and creates a summary.
    /// </summary>
    public async Task EndSessionAsync(string? summary = null, CancellationToken ct = default)
    {
        if (_currentSession == null) return;

        if (!string.IsNullOrEmpty(summary))
        {
            _currentSession = _currentSession with { Summary = summary };
        }

        await SaveCurrentSessionAsync(ct);
        _recentSessions.Add(_currentSession);
        _currentSession = null;
    }

    private async Task LoadRecentSessionsAsync(string personaName, CancellationToken ct)
    {
        try
        {
            var sessionFiles = Directory.GetFiles(_config.StorageDirectory, $"{personaName}_*.json")
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                .Take(_config.RecentSessionsToLoad);

            foreach (var file in sessionFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var session = JsonSerializer.Deserialize<ConversationSession>(json);
                    if (session != null)
                    {
                        _recentSessions.Add(session);
                    }
                }
                catch
                {
                    // Skip corrupted files
                }
            }

            // Reverse to have oldest first
            _recentSessions.Reverse();
        }
        catch
        {
            // Storage directory doesn't exist yet
        }
    }

    private async Task SaveCurrentSessionAsync(CancellationToken ct)
    {
        if (_currentSession == null) return;

        await _saveLock.WaitAsync(ct);
        try
        {
            var filename = $"{_currentSession.PersonaName}_{_currentSession.SessionId}.json";
            var filepath = Path.Combine(_config.StorageDirectory, filename);

            var json = JsonSerializer.Serialize(_currentSession, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filepath, json, ct);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task IndexTurnAsync(ConversationTurn turn, CancellationToken ct)
    {
        if (_embedding == null || _qdrantClient == null) return;

        try
        {
            var embedding = await _embedding.CreateEmbeddingsAsync(turn.Content, ct);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["role"] = turn.Role,
                    ["content"] = turn.Content,
                    ["timestamp"] = turn.Timestamp.ToString("O"),
                    ["session_id"] = turn.SessionId ?? ""
                }
            };

            await _qdrantClient.UpsertAsync(_config.CollectionName, new[] { point }, cancellationToken: ct);
        }
        catch
        {
            // Indexing failed, continue without semantic memory
        }
    }

    private async Task EnsureCollectionExistsAsync(CancellationToken ct)
    {
        if (_qdrantClient == null) return;

        try
        {
            var exists = await _qdrantClient.CollectionExistsAsync(_config.CollectionName, ct);
            if (!exists)
            {
                await _qdrantClient.CreateCollectionAsync(
                    _config.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_config.VectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);
            }
        }
        catch
        {
            // Collection creation failed
        }
    }

    private List<ConversationTurn> SearchMemoryLocal(string query, int limit)
    {
        var queryLower = query.ToLowerInvariant();
        var allTurns = _recentSessions.SelectMany(s => s.Turns)
            .Concat(_currentSession?.Turns ?? Enumerable.Empty<ConversationTurn>())
            .Where(t => t.Content.ToLowerInvariant().Contains(queryLower))
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToList();

        return allTurns;
    }

    public ValueTask DisposeAsync()
    {
        _saveLock.Dispose();
        _qdrantClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Statistics about conversation memory.
/// </summary>
public sealed record ConversationStats
{
    public int TotalSessions { get; init; }
    public int TotalTurns { get; init; }
    public int CurrentSessionTurns { get; init; }
    public DateTime? OldestMemory { get; init; }
    public DateTime? CurrentSessionStart { get; init; }
}
