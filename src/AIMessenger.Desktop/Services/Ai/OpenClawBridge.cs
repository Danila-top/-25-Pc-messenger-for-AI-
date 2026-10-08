using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AIMessenger.Desktop.Services.Ai;

public sealed class OpenClawBridge
{
    public async Task<(bool Success, string Output)> RunAgentAsync(
        string message,
        string agentId = "main",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return (false, "OpenClaw message is empty.");

        var executable = FindExecutable();
        if (executable is null)
            return (false, "OpenClaw CLI was not found on PATH or the standard npm locations.");

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("agent");
        psi.ArgumentList.Add("--agent");
        psi.ArgumentList.Add(string.IsNullOrWhiteSpace(agentId) ? "main" : agentId);
        psi.ArgumentList.Add("--message");
        psi.ArgumentList.Add(message);
        psi.ArgumentList.Add("--json");
        psi.ArgumentList.Add("--timeout");
        psi.ArgumentList.Add("120");

        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
                return (false, "OpenClaw process could not be started.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(stderr)
                    ? stdout
                    : stderr;

                if (error.Contains(
                        "requires credentials",
                        StringComparison.OrdinalIgnoreCase) ||
                    error.Contains(
                        "before opening a websocket",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return await RunEmbeddedFallbackAsync(
                        message,
                        cancellationToken);
                }

                return (
                    false,
                    Truncate(error, 12000));
            }

            return (
                true,
                ExtractAnswer(stdout));
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cancellation cleanup.
            }

            throw;
        }
        catch (Exception ex)
        {
            return (false, $"OpenClaw execution error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Output)> RunEmbeddedFallbackAsync(
        string message,
        CancellationToken cancellationToken)
    {
        var executable = FindExecutable();

        if (executable is null)
            return (false, "OpenClaw CLI was not found.");

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add("agent");
        psi.ArgumentList.Add("exec");
        psi.ArgumentList.Add(message);
        psi.ArgumentList.Add("--json");
        psi.ArgumentList.Add("--timeout");
        psi.ArgumentList.Add("60");
        psi.ArgumentList.Add("--cwd");
        psi.ArgumentList.Add(@"C:\AI\PcMessenger");

        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
                return (false, "OpenClaw embedded process could not be started.");

            var stdoutTask =
                process.StandardOutput.ReadToEndAsync(cancellationToken);

            var stderrTask =
                process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(stderr)
                    ? stdout
                    : stderr;

                return (
                    false,
                    "Gateway credentials are not configured and the " +
                    "embedded OpenClaw fallback also failed: " +
                    Truncate(error, 10000));
            }

            return (
                true,
                ExtractAnswer(stdout));
        }
        catch (Exception ex)
        {
            return (false, $"OpenClaw embedded execution error: {ex.Message}");
        }
    }

    public bool IsAvailable() => FindExecutable() is not null;

    private static string? FindExecutable()
    {
        var candidates = new[]
        {
            "openclaw.cmd",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm",
                "openclaw.cmd"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "npm",
                "openclaw.cmd"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "nodejs",
                "openclaw.cmd")
        };

        foreach (var candidate in candidates)
        {
            if (candidate == "openclaw.cmd")
                return IsOnPath(candidate) ? candidate : null;

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsOnPath(string executable)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process is null)
                return false;

            process.WaitForExit(1500);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractAnswer(string stdout)
    {
        var trimmed = stdout.Trim();

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            foreach (var property in new[] { "reply", "text", "message", "summary" })
            {
                if (doc.RootElement.TryGetProperty(property, out var value) &&
                    value.ValueKind == JsonValueKind.String)
                {
                    return Truncate(value.GetString() ?? string.Empty, 20000);
                }
            }

            if (doc.RootElement.TryGetProperty("result", out var result))
                return Truncate(result.ToString(), 20000);
        }
        catch
        {
            // Human-readable fallback.
        }

        return Truncate(trimmed, 20000);
    }

    private static string Truncate(string value, int max)
    {
        if (value.Length <= max)
            return value;

        return value[..max] + Environment.NewLine + "[truncated]";
    }
}
