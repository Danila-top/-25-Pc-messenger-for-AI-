# AI Messenger PC — #25

Windows-first AI Messenger / agent operating environment.

This is the main PC implementation of the AI-first messenger concept. Windows is the execution surface for desktop applications, filesystem access, Git, terminal workflows and semantic UI automation.

## Current foundation

- .NET 10 + WPF desktop client
- MVVM via CommunityToolkit.Mvvm
- SQLite workspace persistence
- OpenAI-compatible AI provider abstraction (OpenAI / DeepSeek / Mistral)
- Gemini Interactions API client
- Native Anthropic Messages API client (Claude Sonnet 5.5)
- Native structured tool-calling loop
- SAFE / CONFIRM tool policy
- Persistent approval center
- Workspace file read/write tools with repository boundary
- Windows process/window inspection
- FlaUI UI Automation bridge
- OpenClaw CLI/Hub bridge with embedded fallback
- Hermes Agent integration point
- Local Desktop API-key file discovery plus environment-variable support
- Activity/audit logging

## Desktop execution stack

Messenger UI
  -> Agent Runtime
  -> Tool Registry / approval policy
  -> FlaUI / OpenClaw
  -> Windows applications

## Security

Real API keys stay outside Git. AI-Messenger-API-Keys.txt is excluded by .gitignore.

The messenger does not bypass OpenClaw Gateway authentication. It uses the Gateway when credentials are available and otherwise exposes OpenClaw's documented embedded agent exec fallback.

## Roadmap

- MCP client + registry
- native Anthropic provider
- durable agent profiles
- agent teams/delegation
- richer UI Automation actions
- terminal/process tool with command policy
- memory/RAG
- attachments
- scheduler/autonomous tasks
- agent-to-agent protocol
- Android #21 synchronization
- GitHub / CI agent workflows
- packaging and installer
