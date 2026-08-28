# Implementation Notes

Baseline: `com.nowsprinting.gameplay-mcp` `0.3.2`, upstream commit
`df6d235bc3911266be325255434ea272197757cb`.

## Existing reusable pieces

- `Runtime/McpServer.cs` owns MCP transport, tool discovery, dependency injection, and tool filtering.
- `Runtime/McpConfig.cs` exposes the existing UI Test Helper finder, operators, reachability strategy, namespace, and disabled-tool policy.
- `Runtime/Tools/` already provides live screenshot, loaded-scene, UI action, and GameObject inspection tools.
- `Tests/` covers the existing server and built-in tools.

## Star Man mapping

| Handoff requirement | Current mapping |
|---|---|
| Semantic live UI testing | `game_session`, `game_observe`, `game_act`, and `game_diagnostics`; `game_act` delegates to upstream `InvokeActionTool` and `UguiClickOperator`. |
| Screenshot observation | Upstream `take_screenshot`. |
| Scene observation | Upstream `list_scenes`. |
| Existing editor verification | Star Man `Assets/Editor/Tools/StarManVerificationMcpTool.cs` remains authoritative for test-job presets. |
| Runtime opt-in/build safety | Star Man adapter checks `UNITY_EDITOR || DEVELOPMENT_BUILD` and `-enable-gameplay-agent`. |
| Local-only network | Package default is `http://127.0.0.1:8010/`; Star Man sets it explicitly. |
| Deterministic reset | Generic active-scene reload fallback plus Star Man adapter using `AppStatesController.RestartGameStateFromProfile` with fresh in-memory profile. |
| Input System backend | Not implemented in this baseline; Star Man's existing device-level Input System tests remain separate evidence. |
| OS black-box runner | Not implemented in this baseline; use a standalone build and the documented manual procedure. |
| Unified `game_*` tools | Implemented as semantic P0 tools layered over existing server/discovery/DI. |

## Deliberate scope

This integration is a reusable live semantic P0 bridge. It does not replace
the upstream MCP server or the existing Star Man editor verification MCP. Input
System actions, OS black-box injection, artifact bundles, and mobile/WebGL
backends remain deliberately deferred follow-up phases.
