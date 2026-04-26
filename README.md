# Claude Code for Visual Studio

A Visual Studio 2022 extension that implements the Claude Code IDE protocol — the same one the official *Claude Code for VS Code* extension uses — so the `claude` CLI can connect to Visual Studio and drive inline diffs, selection sharing, diagnostics lookup, and file navigation.

## What it does

- Starts a local MCP server on `127.0.0.1:<random port>` (WebSocket, JSON-RPC 2.0, MCP `2024-11-05`).
- Writes a discovery lock file at `%USERPROFILE%\.claude\ide\<port>.lock` so `claude /ide` discovers "Visual Studio 2022" automatically.
- Exposes 11 MCP tools: `get_workspace_folders`, `get_open_editors`, `get_current_selection`, `get_latest_selection`, `get_diagnostics`, `check_document_dirty`, `open_file`, `save_document`, `close_tab`, `close_all_diff_tabs`, and the blocking `open_diff` that shows a native VS diff and waits for Accept/Reject.
- Emits `selection_changed` and `at_mentioned` notifications so Claude sees editor selections in real time.
- Ships a tool window showing server status and a one-click "Start Claude in terminal" button that pre-wires the env vars.

## Requirements

- Visual Studio 2022 (17.0+) — Community, Professional, or Enterprise
- Visual Studio SDK workload (install via the VS Installer → "Visual Studio extension development")
- .NET Framework 4.7.2 targeting pack
- [Claude Code CLI](https://docs.claude.com/en/docs/claude-code) installed and on PATH (`npm i -g @anthropic-ai/claude-code`)

## Build

1. Open `ClaudeCodeVS.sln` in Visual Studio 2022.
2. First build restores NuGet packages: `Microsoft.VisualStudio.SDK`, `Microsoft.VSSDK.BuildTools`, `Newtonsoft.Json`.
3. Press **F5** — this launches the Visual Studio *Experimental Instance* with the extension loaded.
4. The packaged VSIX lands in `src\ClaudeCodeVS\bin\Debug\ClaudeCodeVS.vsix`. Double-click to install into your normal Visual Studio.

## Verify end-to-end

1. In the Experimental Instance, open any solution.
2. Open `View → Other Windows → Claude Code` (or `Tools → Open Claude Code Panel`). The panel shows `Running` + a port number.
3. Confirm `%USERPROFILE%\.claude\ide\<port>.lock` exists and contains JSON with `"ideName": "Visual Studio 2022"`, `"transport": "ws"`, and an `authToken`.
4. In an external terminal, run `claude`. Then inside the chat: `/ide`. It should list **Visual Studio 2022** and connect without an auth error.
5. Ask Claude: *"list the files I have open"* → it should invoke `get_open_editors` and return your tabs.
6. Select a block of code in the editor, press **Ctrl+Alt+K** → Claude receives `at_mentioned` with the selection.
7. Ask Claude to edit a file → a diff view opens. Press **Ctrl+Alt+Y** (Accept) to write the change, or **Ctrl+Alt+N** (Reject) to discard it.
8. Close Visual Studio → confirm the lock file is deleted.

## Architecture

```
VsixPackage (AsyncPackage)
 ├─ IdeServices ───── DTE2, IVsTextManager, IVsDifferenceService, SVsErrorList
 ├─ SelectionTracker  IVsTextManagerEvents → debounced selection_changed
 ├─ DiffCoordinator   IVsDifferenceService.OpenComparisonWindow2 + TaskCompletionSource
 ├─ LockFileManager   ~/.claude/ide/<port>.lock (atomic write, sweep stale)
 ├─ McpWebSocketServer HttpListener → AcceptWebSocketAsync on 127.0.0.1:<10000..65535>
 │                     x-claude-code-ide-authorization header → AuthToken.ConstantTimeEquals
 ├─ JsonRpcDispatcher initialize, tools/list, tools/call, ping
 └─ ToolRegistry → 11 MCP tools under Protocol/Tools/
```

## Security notes

- The server only binds to `127.0.0.1`, so it is unreachable from other machines.
- Each VS session generates a fresh 48-byte random `authToken` (base64url). Connections without the `x-claude-code-ide-authorization` header are rejected with HTTP 401.
- Token comparison uses a constant-time check to prevent timing attacks (see CVE-2025-52882).
- The lock file is readable only by the user's profile (NTFS default ACLs).

## Key files

- `src/ClaudeCodeVS/VsixPackage.cs` — entry point; starts server, writes lockfile, registers commands.
- `src/ClaudeCodeVS/Protocol/McpWebSocketServer.cs` — WebSocket server + auth.
- `src/ClaudeCodeVS/Protocol/JsonRpcDispatcher.cs` — MCP handshake + routing.
- `src/ClaudeCodeVS/Protocol/Tools/` — 11 MCP tool handlers.
- `src/ClaudeCodeVS/Ide/DiffCoordinator.cs` — blocking `open_diff` implementation.
- `src/ClaudeCodeVS/ClaudeCodePackage.vsct` — menus + key bindings.

## References

- Protocol reverse-engineering reference: <https://github.com/coder/claudecode.nvim>
- MCP specification: <https://modelcontextprotocol.io/specification/2024-11-05>
- Visual Studio SDK: <https://learn.microsoft.com/en-us/visualstudio/extensibility/>

## License

MIT — see `LICENSE.txt`.
