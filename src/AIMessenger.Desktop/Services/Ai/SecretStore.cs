using System.IO;
using System.Text.RegularExpressions;

namespace AIMessenger.Desktop.Services.Ai;

public sealed class SecretStore
{
    public string? Get(string provider)
    {
        var envName = provider.ToUpperInvariant() switch
        {
            "OPENAI" => "OPENAI_API_KEY",
            "DEEPSEEK" => "DEEPSEEK_API_KEY",
            "MISTRAL" => "MISTRAL_API_KEY",
            "GEMINI" => "GEMINI_API_KEY",
            "ANTHROPIC" => "ANTHROPIC_API_KEY",
            _ => "AI_MESSENGER_API_KEY"
        };

        var fromEnv = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var path = FindLocalSecretFile();
        if (path is null || !File.Exists(path))
            return null;

        var text = File.ReadAllText(path);
        var providerRegex = new Regex(
            $"(?is)\\b{Regex.Escape(provider)}\\b.*?(?:=|:)\\s*([^\\s]+)");
        var match = providerRegex.Match(text);

        if (match.Success)
            return Clean(match.Groups[1].Value);

        if (provider.Equals("GEMINI", StringComparison.OrdinalIgnoreCase))
        {
            match = Regex.Match(text, @"AIza[0-9A-Za-z_-]{20,}");
            if (match.Success)
                return match.Value;
        }

        if (provider is "OPENAI" or "DEEPSEEK")
        {
            match = Regex.Match(text, @"\bsk-[A-Za-z0-9_-]{20,}\b");
            if (match.Success)
                return match.Value;
        }

        return null;
    }

    public static string? FindLocalSecretFile()
    {
        var desktop = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);

        var candidates = new[]
        {
            Path.Combine(desktop, "AI-Messenger-API-Keys.txt"),
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "Desktop",
                "AI-Messenger-API-Keys.txt"),
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Desktop",
                "AI-Messenger-API-Keys.txt")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string Clean(string value) =>
        value.Trim().Trim('"', ',', ';');
}
