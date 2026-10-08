using System.Net.Http;
using System.Text;
using System.Text.Json;
using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Ai;

public sealed class AnthropicMessagesProvider(
    HttpClient http,
    SecretStore secrets) : IAiProvider
{
    public async Task<ProviderResponse> SendAsync(
        AgentDefinition agent,
        IReadOnlyList<ProviderTurn> turns,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        var key = secrets.Get("ANTHROPIC");

        if (string.IsNullOrWhiteSpace(key))
            return new ProviderResponse(
                "No Anthropic API key was found.",
                []);

        if (string.IsNullOrWhiteSpace(agent.Model))
            return new ProviderResponse(
                "No Claude model is configured.",
                []);

        var body = new Dictionary<string, object?>
        {
            ["model"] = agent.Model,
            ["max_tokens"] = 4096,
            ["system"] = agent.SystemPrompt,
            ["messages"] = BuildMessages(turns)
        };

        if (tools.Count > 0)
        {
            body["tools"] = tools.Select(tool => new
            {
                name = tool.Name,
                description = tool.Description,
                input_schema = tool.Parameters
            }).ToArray();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.anthropic.com/v1/messages");

        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", "2023-06-01");

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(
            request,
            cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ProviderResponse(
                $"Anthropic error {(int)response.StatusCode}: {raw}",
                []);
        }

        using var doc = JsonDocument.Parse(raw);

        var text = new StringBuilder();
        var calls = new List<ToolCall>();

        if (doc.RootElement.TryGetProperty(
                "content",
                out var content) &&
            content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                var type = block.TryGetProperty(
                    "type",
                    out var typeElement)
                    ? typeElement.GetString()
                    : null;

                if (type == "text")
                {
                    if (block.TryGetProperty(
                            "text",
                            out var textElement))
                    {
                        text.Append(textElement.GetString());
                    }
                }
                else if (type == "tool_use")
                {
                    var id = block.TryGetProperty(
                            "id",
                            out var idElement)
                        ? idElement.GetString()
                        : null;

                    var name = block.TryGetProperty(
                            "name",
                            out var nameElement)
                        ? nameElement.GetString()
                        : null;

                    var input = block.TryGetProperty(
                            "input",
                            out var inputElement)
                        ? inputElement.GetRawText()
                        : "{}";

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        calls.Add(new ToolCall(
                            id ?? Guid.NewGuid().ToString("N"),
                            name,
                            input));
                    }
                }
            }
        }

        return new ProviderResponse(
            text.ToString(),
            calls);
    }

    private static object[] BuildMessages(
        IReadOnlyList<ProviderTurn> turns)
    {
        var result = new List<object>();

        foreach (var turn in turns)
        {
            if (turn.Role == "system")
                continue;

            if (turn.Role == "tool")
            {
                result.Add(
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "tool_result",
                                tool_use_id = turn.ToolCallId,
                                content = turn.Content
                            }
                        }
                    });

                continue;
            }

            if (turn.Role == "assistant" &&
                turn.ToolCalls is { Count: > 0 })
            {
                var blocks = new List<object>();

                if (!string.IsNullOrWhiteSpace(turn.Content))
                {
                    blocks.Add(
                        new
                        {
                            type = "text",
                            text = turn.Content
                        });
                }

                foreach (var call in turn.ToolCalls)
                {
                    object input;

                    try
                    {
                        input = JsonSerializer.Deserialize<JsonElement>(
                            call.ArgumentsJson);
                    }
                    catch
                    {
                        input = new Dictionary<string, object?>();
                    }

                    blocks.Add(
                        new
                        {
                            type = "tool_use",
                            id = call.Id,
                            name = call.Name,
                            input
                        });
                }

                result.Add(
                    new
                    {
                        role = "assistant",
                        content = blocks
                    });

                continue;
            }

            result.Add(
                new
                {
                    role = turn.Role,
                    content = turn.Content
                });
        }

        return result.ToArray();
    }
}
