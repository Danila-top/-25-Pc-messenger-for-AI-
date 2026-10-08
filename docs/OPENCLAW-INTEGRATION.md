# OpenClaw integration

AI Messenger PC treats the installed OpenClaw Windows Hub/CLI as an execution backend, not as a second chat UI.

## Current bridge

OpenClawBridge invokes the installed openclaw.cmd CLI using structured argument lists.

Primary path:

- openclaw agent --agent <id> --message <text> --json
- Gateway-backed execution when the local Gateway has valid credentials.

Fallback:

- openclaw agent exec <message> --json
- embedded execution when Gateway credentials or pairing are not yet available.

The fallback is deliberately bounded by a timeout and uses the messenger repository as its working directory.

## Gateway state

The OpenClaw Windows Hub is installed and running locally. The local Gateway listens on the default loopback operator port.

The current machine does not have an explicitly configured Gateway auth mode, so a Gateway-backed openclaw agent call reports that device credentials are required. The messenger does not bypass or disable that trust boundary.

The next integration step is explicit device pairing/token configuration through OpenClaw's supported setup flow. Until then, the messenger keeps the embedded fallback available.

## Why this design

The bridge keeps OpenClaw's own agent/runtime and credential handling intact. The messenger does not copy provider secrets into its process or repository and does not invent a second authentication protocol.
