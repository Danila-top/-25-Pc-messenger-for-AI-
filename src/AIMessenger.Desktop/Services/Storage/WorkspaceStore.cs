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

        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

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

            CREATE TABLE IF NOT EXISTS approvals (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tool_call_id TEXT NOT NULL,
                tool_name TEXT NOT NULL,
                arguments_json TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO messages(role, content, created_at, agent_id)
            VALUES($role, $content, $created_at, $agent_id);
            """;

        command.Parameters.AddWithValue("$role", message.Role);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue(
            "$created_at",
            message.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "$agent_id",
            (object?)message.AgentId ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> LoadRecentAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ChatMessage>();

        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT id, role, content, created_at, agent_id
            FROM messages
            ORDER BY id DESC
            LIMIT $limit;
            """;

        command.Parameters.AddWithValue("$limit", count);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ChatMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3)),
                reader.IsDBNull(4)
                    ? null
                    : reader.GetString(4)));
        }

        result.Reverse();
        return result;
    }

    public async Task<ToolApproval> CreateApprovalAsync(
        ToolCall call,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

        await connection.OpenAsync(cancellationToken);

        var createdAt = DateTimeOffset.UtcNow;

        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO approvals(
                tool_call_id,
                tool_name,
                arguments_json,
                status,
                created_at)
            VALUES(
                $tool_call_id,
                $tool_name,
                $arguments_json,
                'PENDING',
                $created_at);

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue(
            "$tool_call_id",
            call.Id);

        command.Parameters.AddWithValue(
            "$tool_name",
            call.Name);

        command.Parameters.AddWithValue(
            "$arguments_json",
            call.ArgumentsJson);

        command.Parameters.AddWithValue(
            "$created_at",
            createdAt.ToString("O"));

        var id = (long)(await command.ExecuteScalarAsync(
            cancellationToken) ?? 0L);

        return new ToolApproval(
            id,
            call.Id,
            call.Name,
            call.ArgumentsJson,
            "PENDING",
            createdAt);
    }

    public async Task<IReadOnlyList<ToolApproval>> LoadPendingApprovalsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new List<ToolApproval>();

        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                id,
                tool_call_id,
                tool_name,
                arguments_json,
                status,
                created_at
            FROM approvals
            WHERE status = 'PENDING'
            ORDER BY id ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ToolApproval(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5))));
        }

        return result;
    }

    public async Task<ToolApproval?> GetApprovalAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT
                id,
                tool_call_id,
                tool_name,
                arguments_json,
                status,
                created_at
            FROM approvals
            WHERE id = $id
            LIMIT 1;
            """;

        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new ToolApproval(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5)));
    }

    public async Task<bool> ResolveApprovalAsync(
        long id,
        string status,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE approvals
            SET status = $status
            WHERE id = $id
              AND status = 'PENDING';
            """;

        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task LogAsync(
        string eventType,
        string details,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_dbPath}");

        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO activity(
                event_type,
                details,
                created_at)
            VALUES(
                $type,
                $details,
                $created_at);
            """;

        command.Parameters.AddWithValue("$type", eventType);
        command.Parameters.AddWithValue("$details", details);
        command.Parameters.AddWithValue(
            "$created_at",
            DateTimeOffset.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
