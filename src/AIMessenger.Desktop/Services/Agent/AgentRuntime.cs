using AIMessenger.Desktop.Models;
using AIMessenger.Desktop.Services.Ai;
using AIMessenger.Desktop.Services.Storage;
using AIMessenger.Desktop.Services.Tools;

namespace AIMessenger.Desktop.Services.Agent;

public sealed class AgentRuntime(
    IAiProvider provider,
    ToolRegistry tools,
    WorkspaceStore store)
{
    public async Task<string> RunAsync(
        AgentDefinition agent,
        IReadOnlyList<ChatMessage> history,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var turns = history
            .TakeLast(40)
            .Select(m => new ProviderTurn(m.Role, m.Content))
            .ToList();

        turns.Add(new ProviderTurn("user", userMessage));

        for (var round = 0; round < 8; round++)
        {
            var response = await provider.SendAsync(
                agent,
                turns,
                tools.Definitions,
                cancellationToken);

            if (response.ToolCalls.Count == 0)
            {
                await store.LogAsync(
                    "agent.completed",
                    $"{agent.Name}: round={round + 1}",
                    cancellationToken);

                return string.IsNullOrWhiteSpace(response.Text)
                    ? "The provider returned no text."
                    : response.Text;
            }

            turns.Add(new ProviderTurn(
                "assistant",
                response.Text,
                ToolCalls: response.ToolCalls));

            foreach (var call in response.ToolCalls)
            {
                var definition = tools.Definitions.FirstOrDefault(
                    item => item.Name == call.Name);

                if (definition is null)
                {
                    turns.Add(new ProviderTurn(
                        "tool",
                        "Unknown tool.",
                        call.Id));
                    continue;
                }

                if (definition.Risk == ToolRisk.Confirm)
                {
                    var approval =
                        $"Confirmation required for tool '{call.Name}'. " +
                        $"Arguments: {call.ArgumentsJson}";

                    await store.LogAsync(
                        "approval.requested",
                        approval,
                        cancellationToken);

                    turns.Add(new ProviderTurn(
                        "tool",
                        approval,
                        call.Id));
                    continue;
                }

                var result = await tools.ExecuteAsync(
                    call,
                    cancellationToken);

                await store.LogAsync(
                    "tool.executed",
                    $"{call.Name}: {result}",
                    cancellationToken);

                turns.Add(new ProviderTurn(
                    "tool",
                    result,
                    call.Id));
            }
        }

        return "Agent loop stopped after the safety limit of 8 rounds.";
    }
}
