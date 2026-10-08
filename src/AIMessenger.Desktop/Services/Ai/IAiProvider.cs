using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Ai;

public interface IAiProvider
{
    Task<ProviderResponse> SendAsync(
        AgentDefinition agent,
        IReadOnlyList<ProviderTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default);
}
