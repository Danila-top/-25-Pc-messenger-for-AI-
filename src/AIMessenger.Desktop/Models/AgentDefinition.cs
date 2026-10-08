namespace AIMessenger.Desktop.Models;

public sealed record AgentDefinition(
    string Id,
    string Name,
    string Provider,
    string BaseUrl,
    string Model,
    string SystemPrompt,
    bool Enabled = true);
