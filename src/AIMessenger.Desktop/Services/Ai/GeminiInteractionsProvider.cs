using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Ai;

public sealed class GeminiInteractionsProvider(HttpClient http, SecretStore secrets) : IAiProvider
{
    private readonly Dictionary<string, string> _interactionIds = new();

    public async Task<ProviderResponse> SendAsync(
        AgentDefinition agent,
        IReadOnlyList<ProviderTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        var key = secrets.Get("GEMINI");
        if (string.IsNullOrWhiteSpace(key))
            return new ProviderResponse("No Gemini API key was found.", []);

        if (string.IsNullOrWhiteSpace(agent.Model))
            return new ProviderResponse(
                "No Gemini model is configured for this agent.",
                []);

        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = agent.Model,
            ["tools"] = tools.Select(t => new
            {
                type = "function",
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }).ToArray()
        };

        var previousId = GetPreviousInteraction(agent.Id, turns);

        if (previousId is null)
        {
            requestBody["input"] =
                BuildInitialInput(agent.SystemPrompt, turns);
        }
        else
        {
            requestBody["previous_interaction_id"] = previousId;
            requestBody["input"] = BuildFunctionResults(turns);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/interactions");

        request.Headers.Add("x-goog-api-key", key);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(
            request,
            cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new ProviderResponse(
                $"Gemini error {(int)response.StatusCode}: {raw}",
                []);

        using var doc = JsonDocument.Parse(raw);

        var text = doc.RootElement.TryGetProperty(
            "output_text",
            out var outputText)
            ? outputText.GetString() ?? string.Empty
            : string.Empty;

        var calls = new List<ToolCall>();

        if (doc.RootElement.TryGetProperty(
                "steps",
                out var steps) &&
            steps.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in steps.EnumerateArray())
            {
                if (!step.TryGetProperty(
                        "type",
                        out var type) ||
                    !string.Equals(
                        type.GetString(),
                        "function_call",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var id = step.TryGetProperty("id", out var idElement)
                    ? idElement.GetString() ?? Guid.NewGuid().ToString("N")
                    : Guid.NewGuid().ToString("N");

                var name = step.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString() ?? string.Empty
                    : string.Empty;

                var arguments = step.TryGetProperty(
                        "arguments",
                        out var argsElement)
                    ? argsElement.GetRawText()
                    : "{}";

                calls.Add(new ToolCall(id, name, arguments));
            }
        }

        if (calls.Count > 0 &&
            doc.RootElement.TryGetProperty(
                "id",
                out var interactionId))
        {
            _interactionIds[agent.Id] =
                interactionId.GetString() ?? string.Empty;
        }
        else
        {
            _interactionIds.Remove(agent.Id);
        }

        return new ProviderResponse(text, calls);
    }

    private string? GetPreviousInteraction(
        string agentId,
        IReadOnlyList<ProviderTurn> turns)
    {
        if (!_interactionIds.TryGetValue(
                agentId,
                out var interactionId) ||
            string.IsNullOrWhiteSpace(interactionId))
            return null;

        var last = turns.LastOrDefault();

        return last?.Role == "tool"
            ? interactionId
            : null;
    }

    private static string BuildInitialInput(
        string systemPrompt,
        IReadOnlyList<ProviderTurn> turns)
    {
        var builder = new StringBuilder();

        builder.AppendLine("SYSTEM INSTRUCTIONS:");
        builder.AppendLine(systemPrompt);
        builder.AppendLine();
        builder.AppendLine("CONVERSATION:");

        foreach (var turn in turns)
        {
            if (turn.Role == "tool")
                continue;

            builder.AppendLine(
                turn.Role.ToUpperInvariant() +
                ": " +
                turn.Content);
        }

        return builder.ToString();
    }

    private static object[] BuildFunctionResults(
        IReadOnlyList<ProviderTurn> turns)
    {
        var lastAssistantIndex = -1;

        for (var i = turns.Count - 1; i >= 0; i--)
        {
            if (turns[i].Role == "assistant" &&
                turns[i].ToolCalls is { Count: > 0 })
            {
                lastAssistantIndex = i;
                break;
            }
        }

        if (lastAssistantIndex < 0)
            return [];

        var calls = turns[lastAssistantIndex].ToolCalls!;
        var results = new List<object>();

        foreach (var call in calls)
        {
            var result = turns.LastOrDefault(
                turn => turn.Role == "tool" &&
                        turn.ToolCallId == call.Id);

            if (result is null)
                continue;

            results.Add(new
            {
                type = "function_result",
                name = call.Name,
                call_id = call.Id,
                result = new[]
                {
                    new
                    {
                        type = "text",
                        text = result.Content
                    }
                }
            });
        }

        return results.ToArray();
    }
}
