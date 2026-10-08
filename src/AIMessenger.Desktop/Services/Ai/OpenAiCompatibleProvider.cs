using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Ai;

public sealed class OpenAiCompatibleProvider(HttpClient http, SecretStore secrets) : IAiProvider
{
    public async Task<ProviderResponse> SendAsync(
        AgentDefinition agent,
        IReadOnlyList<ProviderTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(agent.Model))
            return new ProviderResponse("No model is configured for this agent. Set its model in the environment before use.", []);

        var key = secrets.Get(agent.Provider);
        if (string.IsNullOrWhiteSpace(key))
            return new ProviderResponse($"No API key was found for provider '{agent.Provider}'.", []);

        var messages = new List<object>
        {
            new { role = "system", content = agent.SystemPrompt }
        };
        messages.AddRange(turns.Select(t => t.ToOpenAiMessage()));

        var body = new Dictionary<string, object?>
        {
            ["model"] = agent.Model,
            ["messages"] = messages,
            ["temperature"] = 0.2,
            ["stream"] = false
        };

        if (tools.Count > 0)
        {
            body["tools"] = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            }).ToArray();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildEndpoint(agent.BaseUrl));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new ProviderResponse(
                $"Provider error {(int)response.StatusCode}: {raw}",
                []);

        using var doc = JsonDocument.Parse(raw);
        var message = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");

        var text = message.TryGetProperty("content", out var content) &&
                   content.ValueKind != JsonValueKind.Null
            ? content.GetString() ?? string.Empty
            : string.Empty;

        var calls = new List<ToolCall>();
        if (message.TryGetProperty("tool_calls", out var toolCalls) &&
            toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                var id = call.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                var fn = call.GetProperty("function");
                calls.Add(new ToolCall(
                    id,
                    fn.GetProperty("name").GetString() ?? string.Empty,
                    fn.GetProperty("arguments").GetString() ?? "{}"));
            }
        }

        return new ProviderResponse(text, calls);
    }

    private static string BuildEndpoint(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            return trimmed + "/chat/completions";
        return trimmed + "/v1/chat/completions";
    }
}
