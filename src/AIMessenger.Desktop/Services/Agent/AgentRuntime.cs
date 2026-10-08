using AIMessenger.Desktop.Models;
using AIMessenger.Desktop.Services.Ai;
using AIMessenger.Desktop.Services.Storage;
using AIMessenger.Desktop.Services.Tools;

namespace AIMessenger.Desktop.Services.Agent;

public sealed class AgentRuntime(
    IAiProvider provider,
    ToolRegistry tools,
    WorkspaceStore store,
    OpenClawBridge openClaw)
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
                    var approval = await store.CreateApprovalAsync(
                        call,
                        cancellationToken);

                    await store.LogAsync(
                        "approval.requested",
                        $"#{approval.Id} {call.Name}: {call.ArgumentsJson}",
                        cancellationToken);

                    return
                        $"Action requires approval #{approval.Id}: " +
                        $"{call.Name}. Open the Approvals panel to continue.";
                }

                var result = call.Name == "openclaw_agent"
                    ? await ExecuteOpenClawAsync(
                        call,
                        cancellationToken)
                    : await tools.ExecuteAsync(
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

    public async Task<string> ApproveAsync(
        long approvalId,
        CancellationToken cancellationToken = default)
    {
        var approval = await store.GetApprovalAsync(
            approvalId,
            cancellationToken);

        if (approval is null)
            return $"Approval #{approvalId} was not found.";

        if (!string.Equals(
                approval.Status,
                "PENDING",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"Approval #{approvalId} is already {approval.Status}.";
        }

        string result;

        if (approval.ToolName == "openclaw_agent")
        {
            result = await ExecuteOpenClawAsync(
                new ToolCall(
                    approval.ToolCallId,
                    approval.ToolName,
                    approval.ArgumentsJson),
                cancellationToken);
        }
        else
        {
            result = await tools.ExecuteAsync(
                new ToolCall(
                    approval.ToolCallId,
                    approval.ToolName,
                    approval.ArgumentsJson),
                cancellationToken);
        }

        await store.ResolveApprovalAsync(
            approvalId,
            "APPROVED",
            cancellationToken);

        await store.LogAsync(
            "approval.approved",
            $"#{approvalId} {approval.ToolName}: {result}",
            cancellationToken);

        return result;
    }

    public async Task<bool> DenyAsync(
        long approvalId,
        CancellationToken cancellationToken = default)
    {
        var approval = await store.GetApprovalAsync(
            approvalId,
            cancellationToken);

        if (approval is null ||
            !string.Equals(
                approval.Status,
                "PENDING",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var resolved = await store.ResolveApprovalAsync(
            approvalId,
            "DENIED",
            cancellationToken);

        if (resolved)
        {
            await store.LogAsync(
                "approval.denied",
                $"#{approvalId} {approval.ToolName}",
                cancellationToken);
        }

        return resolved;
    }

    private async Task<string> ExecuteOpenClawAsync(
        ToolCall call,
        CancellationToken cancellationToken)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(
                call.ArgumentsJson);

            var root = doc.RootElement;

            var message =
                root.GetProperty("message").GetString() ?? string.Empty;

            var agent =
                root.TryGetProperty("agent", out var agentElement)
                    ? agentElement.GetString() ?? "main"
                    : "main";

            var result = await openClaw.RunAgentAsync(
                message,
                agent,
                cancellationToken);

            return result.Success
                ? result.Output
                : $"OpenClaw error: {result.Output}";
        }
        catch (Exception ex)
        {
            return $"OpenClaw tool error: {ex.Message}";
        }
    }
}
