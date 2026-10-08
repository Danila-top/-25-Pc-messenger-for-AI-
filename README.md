# AI Messenger PC — #25

Windows-first AI Messenger / agent operating environment.

This is the main PC implementation of the AI-first messenger concept. Windows is the execution surface for desktop applications, filesystem access, Git, terminal workflows and semantic UI automation.

## Current foundation

- .NET 10 + WPF desktop client
- MVVM via CommunityToolkit.Mvvm
- SQLite workspace persistence
- Provider abstraction for OpenAI-compatible APIs
- Native structured tool-calling loop
- Tool risk classification
- FlaUI 5 UI Automation bridge (UIA2 + UIA3)
- Hermes Agent integration point
- OpenClaw / Windows Hub integration point
- Local API-key discovery from a Desktop-only secret file or environment variables
- Activity logging

## Security

Real API keys stay outside Git. AI-Messenger-API-Keys.txt is excluded by .gitignore.

## Roadmap

Provider-native Gemini/Anthropic clients, approval center, MCP registry, richer UI Automation actions, OpenClaw bridge, memory/RAG, attachments, scheduler, agent-to-agent protocol, Android #21 sync, GitHub/CI workflows, packaging.
