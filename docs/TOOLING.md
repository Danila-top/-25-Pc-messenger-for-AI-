# Windows tooling manifest

## Downloaded for this project

- OpenClaw Windows Hub x64 installer
- FlaUInspect 3.1.0
- Microsoft Accessibility Insights for Windows 1.1.2924.01
- FlaUI 5.0.0 NuGet packages: Core, UIA2, UIA3

Local tool cache:

- C:\AI\tools\downloads
- C:\AI\tools\FlaUInspect

## Baseline already present

- .NET SDK 10.0.401
- Node.js 24.21.0
- Git 2.45.1
- Python 3.14.x
- Hermes Agent 2026.9.24
- OpenClaw CLI 2026.9.8
- WSL services
- WebView2 runtime/processes
- Visual Studio Build Tools launcher

## Hermes strategy

Hermes already provides on-demand skills and registries. We will use its skill catalog as a capability source and install only skills that materially add value to the messenger instead of copying an enormous skill corpus into Git.
