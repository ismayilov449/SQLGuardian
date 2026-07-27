# VS Code extension + Language Server

Task 5 delivers editor diagnostics from the **same** RuleEngine as the CLI.

## Components

| Piece | Path |
|-------|------|
| Language Server (C#, LSP) | `src/SQLGuardian.LanguageServer` |
| VS Code extension (TypeScript) | `extensions/vscode` |
| Publish script | `scripts/publish-vscode-server.cmd` (or `.ps1`) |

```
.sql buffer
   │ textDocument/didOpen|didChange|didSave
   ▼
SQLGuardian.LanguageServer
   │ SqlAnalysisService
   ▼
SQLGuardian.RuleEngine  (+ ScriptDom)
   │
   ▼
textDocument/publishDiagnostics  (rule IDs SQLGxxxx)
```

## Develop

```powershell
dotnet build SQLGuardian.sln -c Release
dotnet test SQLGuardian.sln -c Release

# Optional: copy server into the extension
.\scripts\publish-vscode-server.cmd
# or from PowerShell:
# powershell -ExecutionPolicy Bypass -File .\scripts\publish-vscode-server.ps1

cd extensions/vscode
npm.cmd install
npm.cmd run compile
```

> **Windows tips**
> - Typing `publish-vscode-server.ps1` in cmd may open Notepad — use `publish-vscode-server.cmd` instead.
> - If F5 fails with `npm.ps1 is not digitally signed`, use `npm.cmd` (the launch task already does). Do not lower execution policy globally unless you want to.

Install the extension folder in VS Code (**Install from Location...**), open a `.sql` file.

## Commands

- `SQLGuardian: Analyze Active SQL Document`
- `SQLGuardian: Restart Language Server`

## Settings

See `extensions/vscode/README.md`.
