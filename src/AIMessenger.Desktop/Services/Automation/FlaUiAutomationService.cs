using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace AIMessenger.Desktop.Services.Automation;

public sealed class FlaUiAutomationService : IDisposable
{
    private readonly UIA3Automation _automation = new();

    private AutomationElement[] DesktopWindows()
    {
        return _automation
            .GetDesktop()
            .FindAllChildren()
            .Where(element => element.ControlType == ControlType.Window)
            .ToArray();
    }

    public IReadOnlyList<(int ProcessId, string Title, string ProcessName)> ListWindows()
    {
        return DesktopWindows()
            .Select(w =>
            {
                try
                {
                    var pid = w.Properties.ProcessId.ValueOrDefault;
                    return (pid, w.Name ?? string.Empty, TryProcessName(pid));
                }
                catch
                {
                    return (0, string.Empty, string.Empty);
                }
            })
            .Where(x => x.Item1 != 0)
            .OrderBy(x => x.Item1)
            .ToArray();
    }

    public AutomationElement? FindWindow(string titleContains) =>
        DesktopWindows().FirstOrDefault(w =>
            (w.Name ?? string.Empty).Contains(
                titleContains,
                StringComparison.OrdinalIgnoreCase));

    public string DescribeTree(string titleContains, int maxDepth = 2)
    {
        var window = FindWindow(titleContains);

        if (window is null)
            return "Window not found: " + titleContains;

        var sb = new StringBuilder();
        Walk(window, sb, 0, Math.Max(0, maxDepth));
        return sb.ToString();
    }

    private static void Walk(
        AutomationElement element,
        StringBuilder sb,
        int depth,
        int maxDepth)
    {
        var indent = new string(' ', depth * 2);

        string Safe(Func<string?> getter)
        {
            try
            {
                return getter() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        sb.AppendLine(
            indent + "- " +
            Safe(() => element.ControlType.ToString()) +
            " name=[" + Safe(() => element.Name) +
            "] automationId=[" + Safe(() => element.AutomationId) + "]");

        if (depth >= maxDepth)
            return;

        AutomationElement[] children;
        try
        {
            children = element.FindAllChildren();
        }
        catch
        {
            return;
        }

        foreach (var child in children.Take(100))
            Walk(child, sb, depth + 1, maxDepth);
    }

    private static string TryProcessName(int pid)
    {
        try
        {
            return System.Diagnostics.Process.GetProcessById(pid).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose() => _automation.Dispose();
}
