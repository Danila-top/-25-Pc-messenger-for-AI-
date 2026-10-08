# AI Messenger PC architecture

## Core path

Human -> Messenger UI -> Agent Runtime -> Tools / Providers -> Windows / Internet.

## Runtime

The runtime owns a bounded execution loop. A provider can return structured tool calls. Safe tools can execute automatically; confirmation-required tools are recorded and surfaced instead of being silently executed.

## Providers

IAiProvider is independent of the WPF UI. The first implementation is OpenAI-compatible Chat Completions, which supports OpenAI, DeepSeek and other compatible endpoints. Native Gemini and Anthropic clients will be added without changing the runtime contract.

## Windows automation

FlaUI UIA3 is the semantic automation layer. The application should prefer AutomationId, Name, ControlType and other UI Automation properties over screen coordinates. FlaUInspect and Accessibility Insights are development/inspection tools and are not shipped in the application.

## Persistence

SQLite stores messages and activity under %LOCALAPPDATA%\AI-Messenger\workspace.db.

## Secrets

SecretStore checks provider environment variables first, then a local Desktop file named AI-Messenger-API-Keys.txt. The secret file is explicitly excluded from Git.

## Execution stack

Messenger UI
  -> Agent Runtime
  -> Tool Registry / policy
  -> FlaUI / OpenClaw bridges
  -> Windows applications

## Next layers

1. Persistent approvals and approval UI.
2. Native Gemini and Anthropic providers.
3. MCP registry and client.
4. OpenClaw node/session bridge.
5. File, process and browser tools with granular policies.
6. Agent profiles, teams and delegation.
7. Scheduler, checkpoints and autonomous tasks.
8. Android #21 synchronization.
9. GitHub and CI workflows.
10. Packaging and installer.
