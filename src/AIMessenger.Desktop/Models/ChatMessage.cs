namespace AIMessenger.Desktop.Models;

public sealed record ChatMessage(
    long Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAt,
    string? AgentId = null);
