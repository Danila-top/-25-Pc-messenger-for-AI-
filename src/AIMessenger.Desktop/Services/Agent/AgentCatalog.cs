using AIMessenger.Desktop.Models;

namespace AIMessenger.Desktop.Services.Agent;

public static class AgentCatalog
{
    public static IReadOnlyList<AgentDefinition> Defaults =>
    [
        new(
            "luna",
            "Luna",
            "OPENAI",
            "https://api.openai.com/v1",
            Environment.GetEnvironmentVariable("AI_OPENAI_MODEL")
                ?? "gpt-6-luna",
            "You are Luna, the coordinator agent of AI Messenger. Plan carefully, use tools when useful, verify results, and be explicit when an action requires confirmation."),

        new(
            "deepseek",
            "DeepSeek",
            "DEEPSEEK",
            "https://api.deepseek.com/v1",
            Environment.GetEnvironmentVariable("AI_DEEPSEEK_MODEL")
                ?? "deepseek-flash",
            "You are a specialist reasoning and coding agent. Give precise technical analysis and use tools when they materially improve correctness."),

        new(
            "gemini",
            "Gemini",
            "GEMINI",
            "https://generativelanguage.googleapis.com/v1beta",
            Environment.GetEnvironmentVariable("AI_GEMINI_MODEL")
                ?? "gemini-3.8-flash",
            "You are Gemini, a multimodal and agentic specialist. Use structured tools when useful, verify actions, and provide concise technical results.")
    ];
}
