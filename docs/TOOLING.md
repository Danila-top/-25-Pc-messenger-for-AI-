# Windows tooling manifest

## Downloaded for this project

- OpenClaw Windows Hub x64 installer
- FlaUInspect 3.1.0
- Microsoft Accessibility Insights for Windows 1.1.2924.01 installer
- FlaUI 5.0.0 NuGet packages: Core, UIA2, UIA3

Local tool cache:

- C:\AI\tools\downloads
- C:\AI\tools\FlaUInspect

## Installed baseline

- .NET SDK 10.0.401
- Node.js 24.21.0
- Git 2.45.1
- Python 3.14.x
- Hermes Agent 2026.9.24
- OpenClaw CLI 2026.9.8
- OpenClaw Windows Hub / Tray
- WSL services
- WebView2 runtime/processes
- Visual Studio Build Tools launcher
- sqlite3

## Hermes strategy

Hermes provides a very large on-demand Skills Hub. The messenger does not copy the entire catalog into Git.

Installed for this environment:

- official/mcp/mcporter

The official qmd skill was inspected but not installed because its scanner flagged privileged/persistence operations that are unnecessary for this Windows messenger.

Built-in Hermes computer-use, Codex and OpenCode capabilities remain available locally and can be used as agent capabilities rather than vendored into the application.

## UI automation stack

Primary runtime:

- FlaUI UIA3

Inspection:

- FlaUInspect

Independent Microsoft inspector:

- Accessibility Insights installer is cached, but the MSI requires elevated administrative installation on this machine and therefore remains uninstalled.

The application's automation layer prefers semantic UI Automation properties such as Name, ControlType and AutomationId over pixel coordinates.
