using SQLite;

namespace MonadicPipeline.Android.Services;

/// <summary>
/// Service for managing command history with SQLite
/// </summary>
public class CommandHistoryService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly int _maxHistorySize;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHistoryService"/> class.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database</param>
    /// <param name="maxHistorySize">Maximum number of history entries</param>
    public CommandHistoryService(string databasePath, int maxHistorySize = 1000)
    {
        _database = new SQLiteAsyncConnection(databasePath);
        _maxHistorySize = maxHistorySize;
        InitializeAsync().Wait();
    }

    private async Task InitializeAsync()
    {
        await _database.CreateTableAsync<CommandHistoryEntry>();
    }

    /// <summary>
    /// Add a command to history
    /// </summary>
    /// <param name="command">The command to add</param>
    /// <returns>Task representing the operation</returns>
    public async Task AddCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var entry = new CommandHistoryEntry
        {
            Command = command,
            ExecutedAt = DateTime.UtcNow
        };

        await _database.InsertAsync(entry);

        // Clean up old entries if exceeding max size
        var count = await _database.Table<CommandHistoryEntry>().CountAsync();
        if (count > _maxHistorySize)
        {
            var toDelete = count - _maxHistorySize;
            var oldEntries = await _database.Table<CommandHistoryEntry>()
                .OrderBy(e => e.ExecutedAt)
                .Take(toDelete)
                .ToListAsync();

            foreach (var old in oldEntries)
            {
                await _database.DeleteAsync(old);
            }
        }
    }

    /// <summary>
    /// Get recent command history
    /// </summary>
    /// <param name="count">Number of entries to retrieve</param>
    /// <returns>List of recent commands</returns>
    public async Task<List<CommandHistoryEntry>> GetRecentHistoryAsync(int count = 50)
    {
        return await _database.Table<CommandHistoryEntry>()
            .OrderByDescending(e => e.ExecutedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Search command history
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Maximum results</param>
    /// <returns>List of matching commands</returns>
    public async Task<List<CommandHistoryEntry>> SearchHistoryAsync(string query, int limit = 20)
    {
        return await _database.Table<CommandHistoryEntry>()
            .Where(e => e.Command.Contains(query))
            .OrderByDescending(e => e.ExecutedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Get command statistics
    /// </summary>
    /// <returns>Dictionary of command frequency</returns>
    public async Task<Dictionary<string, int>> GetCommandStatisticsAsync()
    {
        var entries = await _database.Table<CommandHistoryEntry>().ToListAsync();
        
        // Extract first word (command name) and count frequency
        return entries
            .Select(e => e.Command.Split(' ')[0].ToLowerInvariant())
            .GroupBy(cmd => cmd)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Clear all history
    /// </summary>
    /// <returns>Task representing the operation</returns>
    public async Task ClearHistoryAsync()
    {
        await _database.DeleteAllAsync<CommandHistoryEntry>();
    }
}

/// <summary>
/// Command history entry stored in SQLite
/// </summary>
[Table("CommandHistory")]
public class CommandHistoryEntry
{
    /// <summary>
    /// Gets or sets the entry ID
    /// </summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the command text
    /// </summary>
    [MaxLength(1000)]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the execution timestamp
    /// </summary>
    public DateTime ExecutedAt { get; set; }
}
