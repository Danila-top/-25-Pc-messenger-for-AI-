using System.Diagnostics;
using System.Text.Json;
using AIMessenger.Desktop.Models;
using AIMessenger.Desktop.Services.Automation;

namespace AIMessenger.Desktop.Services.Tools;

public sealed class ToolRegistry(FlaUiAutomationService ui)
{
    private static readonly JsonElement EmptyObjectSchema =
        JsonSerializer.Deserialize<JsonElement>(
            """{"type":"object","properties":{},"additionalProperties":false}""");

    public IReadOnlyList<ToolDefinition> Definitions =>
    [
        new(
            "calculator",
            "Evaluate a basic arithmetic expression. No code execution.",
            JsonSerializer.Deserialize<JsonElement>(
                """{"type":"object","properties":{"expression":{"type":"string"}},"required":["expression"],"additionalProperties":false}""")),
        new(
            "open_url",
            "Open an http/https URL using the default Windows browser.",
            JsonSerializer.Deserialize<JsonElement>(
                """{"type":"object","properties":{"url":{"type":"string"}},"required":["url"],"additionalProperties":false}"""),
            ToolRisk.Confirm),
        new(
            "list_windows",
            "List visible top-level Windows windows with process IDs and titles.",
            EmptyObjectSchema),
        new(
            "inspect_window",
            "Inspect the semantic UI Automation tree of a Windows window by title substring.",
            JsonSerializer.Deserialize<JsonElement>(
                """{"type":"object","properties":{"titleContains":{"type":"string"},"depth":{"type":"integer","minimum":0,"maximum":4}},"required":["titleContains"],"additionalProperties":false}"""))
    ];

    public Task<string> ExecuteAsync(
        ToolCall call,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(call.ArgumentsJson);
            var root = doc.RootElement;

            return call.Name switch
            {
                "calculator" =>
                    Task.FromResult(
                        Calculate(root.GetProperty("expression").GetString() ?? string.Empty)),
                "open_url" =>
                    Task.FromResult(
                        OpenUrl(root.GetProperty("url").GetString() ?? string.Empty)),
                "list_windows" =>
                    Task.FromResult(ListWindows()),
                "inspect_window" =>
                    Task.FromResult(
                        ui.DescribeTree(
                            root.GetProperty("titleContains").GetString() ?? string.Empty,
                            root.TryGetProperty("depth", out var depth)
                                ? depth.GetInt32()
                                : 2)),
                _ => Task.FromResult($"Unknown tool: {call.Name}")
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Tool error: {ex.Message}");
        }
    }

    private static string Calculate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "Expression is empty.";

        if (expression.Any(ch =>
                !char.IsDigit(ch) &&
                !" +-*/().%".Contains(ch)))
            return "Only arithmetic characters are allowed.";

        try
        {
            var value = new System.Data.DataTable().Compute(expression, null);
            return Convert.ToDouble(
                    value,
                    System.Globalization.CultureInfo.InvariantCulture)
                .ToString(
                    "G17",
                    System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return "Could not evaluate expression.";
        }
    }

    private static string OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
            return "Only http/https URLs are allowed.";

        Process.Start(
            new ProcessStartInfo(uri.ToString())
            {
                UseShellExecute = true
            });

        return $"Opened {uri}";
    }

    private string ListWindows() =>
        string.Join(
            Environment.NewLine,
            ui.ListWindows().Select(
                x => $"{x.ProcessId}	{x.ProcessName}	{x.Title}"));
}
