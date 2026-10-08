namespace AIMessenger.Desktop.Models;

public sealed record ToolApproval(
    long Id,
    string ToolCallId,
    string ToolName,
    string ArgumentsJson,
    string Status,
    DateTimeOffset CreatedAt);
