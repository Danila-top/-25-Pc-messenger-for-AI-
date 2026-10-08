using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Ai;

public sealed class AiProviderRouter(
    OpenAiCompatibleProvider openAi,
    GeminiInteractionsProvider gemini,
    AnthropicMessagesProvider anthropic) : IAiProvider
{
    public Task<ProviderResponse> SendAsync(
        AgentDefinition agent,
        IReadOnlyList<ProviderTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default) =>
        agent.Provider.ToUpperInvariant() switch
        {
            "GEMINI" => gemini.SendAsync(
                agent,
                turns,
                tools,
                cancellationToken),

            "ANTHROPIC" => anthropic.SendAsync(
                agent,
                turns,
                tools,
                cancellationToken),

            _ => openAi.SendAsync(
                agent,
                turns,
                tools,
                cancellationToken)
        };
}
