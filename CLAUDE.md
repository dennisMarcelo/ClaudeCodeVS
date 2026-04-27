# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Visual Studio 2022 VSIX extension (single `.csproj`) that implements the Claude Code IDE MCP protocol so the `claude` CLI can attach to Visual Studio just like it attaches to VS Code. It is **not** a regular .NET app — it runs inside `devenv.exe` as an `AsyncPackage`.

## Build / run

- Open `ClaudeCodeVS.sln` (the solution at the repo root) in Visual Studio 2022 with the "Visual Studio extension development" workload installed.
- Press **F5**. The csproj's `StartProgram`/`StartArguments` are wired to launch `devenv.exe /rootsuffix Exp` — a sandboxed Experimental Instance with the freshly built VSIX deployed.
- Built VSIX lands at `src\bin\Debug\ClaudeCodeVS.vsix`.
- There is **no test project, no CLI build, no linter**. Validation = launch Experimental Instance and exercise the protocol manually (see README "Verify end-to-end").
- `src\src.sln` is a stray Visual Studio scratch file — ignore it; the canonical solution is `ClaudeCodeVS.sln` at the repo root.

## Hard constraints on the toolchain

- **Target framework: .NET Framework 4.7.2** (`<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>`). Do not introduce APIs or `PackageReference`s that require .NET Core / .NET 5+ — the VS 2022 in-proc extension model still pins to desktop .NET Framework.
- **JSON: Newtonsoft.Json only** (`JObject`/`JArray`/`JsonConvert`). Do not switch to `System.Text.Json`; the protocol layer (`Protocol/Json.cs`, `JsonRpcDispatcher`, every tool) is built on `JToken`.
- **C# language version: `latest`**, but no nullable reference types are enabled — keep style consistent with existing files (defensive null checks, no `?` annotations).

## Architecture: how a `claude /ide` connection actually works

The flow that ties the codebase together:

1. **`VsixPackage.InitializeAsync`** (auto-loaded for both `SolutionExists` and `NoSolution` UI contexts) builds the object graph in this exact order: `IdeServices` → `SelectionTracker` → `DiffCoordinator` → `ToolRegistry` → `JsonRpcDispatcher` → fresh 48-byte `AuthToken` → `McpWebSocketServer.StartAsync` → `LockFileManager.WriteAsync`. Several callers (e.g. `OpenTerminalWithClaudeCommand.InitializeAsync`) need the bound port and token, so the order matters.
2. **Discovery contract.** `LockFileManager` writes `%USERPROFILE%\.claude\ide\<port>.lock` (override dir with `CLAUDE_CODE_CONFIG_DIR`) with `{ pid, workspaceFolders, ideName: "Visual Studio 2022", transport: "ws", authToken, runningInWindows: true }`. The `claude` CLI scans this directory; changing the filename pattern, the JSON shape, or the `ideName` breaks `/ide` discovery silently.
3. **Transport.** `McpWebSocketServer` uses `HttpListener` bound to a random port in `[10000, 65535]` on `127.0.0.1` only. Every incoming request must carry header `x-claude-code-ide-authorization` matching the session token; comparison goes through `AuthToken.ConstantTimeEquals` (CVE-2025-52882). Do not introduce shortcut equality checks.
4. **JSON-RPC layer.** `JsonRpcDispatcher` handles `initialize` (returns `protocolVersion: "2024-11-05"` — bumping this requires a coordinated CLI release), `notifications/initialized`, `tools/list`, `tools/call`, and `ping`. `tools/call` wraps every tool result in the MCP `{ content: [{ type: "text", text: ... }], isError: false }` envelope — individual tools should return raw JSON (`JObject`/`JArray`) and let the dispatcher serialize.
5. **Notifications.** `SelectionTracker` debounces `IVsTextManagerEvents` selection changes (100 ms timer in `Poke`/`Debounced`) and `VsixPackage.OnSelectionChanged` broadcasts them via `McpWebSocketServer.BroadcastNotification("selection_changed", ...)`. `AddSelectionToClaudeCommand` sends `at_mentioned` the same way. Both methods on `McpWebSocketServer` fan out to every connected client.
6. **Blocking diffs.** `OpenDiffTool` → `DiffCoordinator.OpenAsync` writes a temp file, opens a native VS diff via `IVsDifferenceService.OpenComparisonWindow2`, and `await`s a `TaskCompletionSource<DiffResult>` keyed by `tab_name`. The MCP request thread blocks until the user runs `AcceptDiffCommand` (Ctrl+Alt+Y) or `RejectDiffCommand` (Ctrl+Alt+N), which call `DiffCoordinator.AcceptTopmost` / `RejectTopmost` to resolve the TCS. If you add new diff entry points, register them through this coordinator — don't open `OpenComparisonWindow2` ad hoc, or accept/reject won't write the file.

## Threading rules (these will bite you)

- All `DTE2`, `IVsTextManager`, `IVsDifferenceService`, `IVsSolution`, `IErrorList`, and `IVsTextView` calls **must** run on the UI thread. Use `_ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct)` (preferred in async paths) or `_ide.RunOnMainAsync(...)`. Synchronous fallback: `ThreadHelper.JoinableTaskFactory.Run(async () => { await _jtf.SwitchToMainThreadAsync(); ... })` — used in `IdeServices.GetWorkspaceFolders` because it has to return synchronously to the lock file writer.
- Methods that assert with `ThreadHelper.ThrowIfNotOnUIThread()` (e.g. `IdeServices.GetActiveTextView`, `GetFilePathFromTextView`, `SelectionTracker.Capture`) are documenting "caller must already be on UI thread" — don't call them from `Task.Run`.
- The WebSocket accept loop and per-message handler in `McpWebSocketServer` run on the thread pool. A tool that touches VS interop must hop to the UI thread inside its `InvokeAsync`.

## Adding a new MCP tool

1. Implement `IMcpTool` (`Name`, `Description`, `InputSchema` as a `JObject`, `InvokeAsync(JObject, CancellationToken) → Task<JToken>`).
2. Add the `<Compile Include="Protocol\Tools\YourTool.cs" />` line to `src/ClaudeCodeVS.csproj` — this csproj is **not SDK-style**, so files are not auto-included.
3. Register it in `ToolRegistry.RegisterDefaults` so `tools/list` advertises it.
4. The dispatcher unwraps single-string results; return structured JSON (a `JObject` or `JArray`) and let the dispatcher convert via `ToString(Formatting.None)`.

## Adding a new VS command

Commands are split between two files that must stay in sync:

- `src/PackageIds.cs` — define a new `int` command id constant (current set: `0x0100`–`0x0104`, all under `CommandSet` GUID `8b1e8a27-...-1d03`).
- `src/ClaudeCodePackage.vsct` — add matching `<Button>` and (optionally) `<KeyBinding>` plus an `<IDSymbol>` under `guidClaudeCodeCmdSet`. The `.vsct` is compiled by `Microsoft.VSSDK.BuildTools` into `Menus.ctmenu` and surfaced via `[ProvideMenuResource]` on `VsixPackage`.
- Then create a `Commands/<Name>Command.cs` exposing `InitializeAsync(AsyncPackage)` and call it from `VsixPackage.InitializeAsync` (commands that need the port/token, like `OpenTerminalWithClaudeCommand`, take them as args).

Existing keybindings: Ctrl+Alt+K (add selection), Ctrl+Alt+Y (accept diff), Ctrl+Alt+N (reject diff) — all scoped to `guidVSStd97`.

## Things that look broken but aren't

- `try { ... } catch { }` blocks swallowing exceptions in `LockFileManager`, `Stop`, command handlers, and event raisers are intentional: VS extensions must never throw out of shutdown / event-callback / COM-event paths or they kill the host process.
- `OpenTerminalWithClaudeCommand` shells out to `cmd.exe /K claude` and pre-populates `CLAUDE_CODE_SSE_PORT` / `CLAUDECODE` / `ENABLE_IDE_INTEGRATION` env vars. The CLI uses these to bypass `/ide` discovery and connect directly — keep all three when editing.

## Reference

- Protocol reverse-engineered from <https://github.com/coder/claudecode.nvim>.
- MCP spec pinned to `2024-11-05`: <https://modelcontextprotocol.io/specification/2024-11-05>.
- The `.github/copilot-instructions.md` file only contains Azure-tool guidance unrelated to this project — ignore it for ClaudeCodeVS work.
