# SQLGuardian VS Code extension

Language client for the SQLGuardian Language Server. Diagnostics come from the same `RuleEngine` as the CLI — not a second analyzer.

## Setup (development)

From the repository root:

```powershell
dotnet build src/SQLGuardian.LanguageServer/SQLGuardian.LanguageServer.csproj -c Release
cd extensions/vscode
npm install
npm run compile
```

Then in VS Code: **Extensions: Install from Location...** → select `extensions/vscode`, or press F5 from a launch config.

Open a `.sql` file. Issues appear as squiggles with rule IDs (`SQLG0001`, …).

## Commands

- **SQLGuardian: Analyze Active SQL Document** — re-runs the engine on the current file
- **SQLGuardian: Restart Language Server**

## Settings

| Setting | Purpose |
|---------|---------|
| `sqlguardian.server.path` | Absolute path to `SQLGuardian.LanguageServer.dll` |
| `sqlguardian.dotnet.path` | `dotnet` executable |
| `sqlguardian.configPath` | Optional `sqlguardian.json` severity overrides |
| `sqlguardian.trace.server` | LSP trace level |

## Architecture

```
VS Code extension (TypeScript)
        │  LSP stdio
        ▼
SQLGuardian.LanguageServer (C#)
        │
        ▼
SQLGuardian.RuleEngine + ScriptDom
```
