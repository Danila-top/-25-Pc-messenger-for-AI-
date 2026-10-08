using System.IO;
using Microsoft.Data.Sqlite;
using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Storage;

public sealed class WorkspaceStore
{
    private readonly string _dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AI-Messenger",
        "workspace.db");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL,
                agent_id TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS activity (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                event_type TEXT NOT NULL,
                details TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO messages(role, content, created_at, agent_id)
            VALUES($role, $content, $created_at, $agent_id);
            """;
        command.Parameters.AddWithValue("$role", message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$created_at", message.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$agent_id", (object?)message.AgentId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> LoadRecentAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ChatMessage>();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, role, content, created_at, agent_id
            FROM messages
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", count);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ChatMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        result.Reverse();
        return result;
    }

    public async Task LogAsync(
        string eventType,
        string details,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO activity(event_type, details, created_at)
            VALUES($type, $details, $created_at);
            """;
        command.Parameters.AddWithValue("$type", eventType);
        command.Parameters.AddWithValue("$details", details);
        command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
