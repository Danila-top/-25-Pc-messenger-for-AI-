using System.Text.Json;

namespace AIMessenger.Desktop.Models;

public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonElement Parameters,
    ToolRisk Risk = ToolRisk.Safe);

public enum ToolRisk
{
    Safe,
    Confirm
}

public sealed record ToolCall(
    string Id,
    string Name,
    string ArgumentsJson);

public sealed record ProviderResponse(
    string Text,
    IReadOnlyList<ToolCall> ToolCalls);

public sealed record ProviderTurn(
    string Role,
    string Content,
    string? ToolCallId = null,
    IReadOnlyList<ToolCall>? ToolCalls = null)
{
    public object ToOpenAiMessage() => ToolCalls is { Count: > 0 }
        ? new
        {
            role = Role,
            content = string.IsNullOrWhiteSpace(Content) ? null : Content,
            tool_calls = ToolCalls.Select(t => new
            {
                id = t.Id,
                type = "function",
                function = new
                {
                    name = t.Name,
                    arguments = t.ArgumentsJson
                }
            }).ToArray()
        }
        : ToolCallId is not null
            ? new { role = "tool", tool_call_id = ToolCallId, content = Content }
            : new { role = Role, content = Content };
}
