using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Ai;

public sealed class AiProviderRouter(
    OpenAiCompatibleProvider openAi,
    GeminiInteractionsProvider gemini) : IAiProvider
{
    public Task<ProviderResponse> SendAsync(
        AgentDefinition agent,
        IReadOnlyList<ProviderTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default) =>
        agent.Provider.Equals(
            "GEMINI",
            StringComparison.OrdinalIgnoreCase)
            ? gemini.SendAsync(
                agent,
                turns,
                tools,
                cancellationToken)
            : openAi.SendAsync(
                agent,
                turns,
                tools,
                cancellationToken);
}
