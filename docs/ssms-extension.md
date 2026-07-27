# SQLGuardian SSMS Extension (Error List)

Native SSMS 21+ integration: analyze the active query window and push findings into the **Error List**. Double-click jumps to the line in the script.

This is Phase 1 of the SSMS-native track. The WPF companion remains available for catalog scan / batch folders.

## Build the VSIX

```bat
scripts\ssms\pack-vsix.cmd
```

Output: `artifacts\SQLGuardian.Ssms.Extension.vsix`

Requires: Visual Studio 2022 (MSBuild), .NET 9 SDK (for bundled CLI).

## Install into SSMS 21

Close SSMS first, then install with the **SSMS** installer (not the Visual Studio one from Explorer double-click):

```bat
"C:\Program Files\Microsoft SQL Server Management Studio 21\Release\Common7\IDE\VSIXInstaller.exe" artifacts\SQLGuardian.Ssms.Extension.vsix
```

If double-click opens the VS installer and fails with “not a valid VSIX package”, rebuild with `scripts\ssms\pack-vsix.cmd` and use the command above.

Restart SSMS.

## Use it

After install + SSMS restart, open **Tools** and look for these **flat** items (not a nested submenu):

- **SQLGuardian — Analyze Active Script (Error List)**
- **SQLGuardian — Clear Findings**

They sit on the Tools menu itself (near External Tools). Names include **(Error List)** so they are distinct from any older companion External Tools entries named `SQLGuardian: Analyze Active Script`.

1. Open a `.sql` query window.
2. Press **Ctrl+Shift+G**, or **Tools → SQLGuardian — Analyze Active Script (Error List)**
3. Open **View → Error List** if needed (findings appear under **Warnings**, Errors for High/Critical).
4. Double-click a finding to jump to that line.

Shortcut: **Ctrl+Shift+G** (`SQLGuardian.AnalyzeActiveScript`). Remap under **Tools → Options → Environment → Keyboard** if needed.

**Analyze after Execute (on by default):** after **Query → Execute** / **F5**, SQLGuardian analyzes the active script and updates the Error List. Toggle under **Tools → Options → SQLGuardian → Analysis**.

**Execute guards (on by default):** before **Query → Execute** / **F5**, SQLGuardian can cancel dangerous or expensive scripts:

- **Missing WHERE (SQLG0002):** `UPDATE`/`DELETE` without `WHERE` → critical warning. **Cancel** stops Execute.
- **Large SELECT:** `SELECT *` or unbounded `SELECT` against a table at/above the row threshold → warning dialog.
- **Large JOIN:** joins involving a table at/above the row threshold → warning dialog (**Cancel** / **Execute anyway**). Optional advanced setting **Allow NOLOCK quick-fix on large joins** adds an **Apply NOLOCK** button (off by default; dirty reads possible; SQLG0003 still warns in Error List).

Schema-aware guards prefer the **active SSMS connection**. If that is unavailable, SQLGuardian can fall back to a saved profile under **Tools → Options → SQLGuardian → Connection**.

**Execute guards note:** the guard inspects the **selected text** when you highlight a statement before F5 (same as SSMS Execute selection). With no selection, it inspects the **whole query window**, so a leftover `SELECT *` above an `UPDATE`/`DELETE` can still trigger the large-read dialog.

## How it works

```
SSMS (active document)
    → Extension command
    → bundled sqlguardian CLI (--format json)
    → Error List (ErrorListProvider)
```

Same RuleEngine as CLI / companion. Detection stays deterministic.

## Next phases

- Apply fix / insert suggested SQL into the editor (Quick Fix)
- Catalog scan from the extension
- Lightbulb suggested actions

## Companion vs Extension

| | Extension | Companion (WPF) |
|--|-----------|-----------------|
| Analyze active script | Yes (Error List) | Yes (own grid) |
| Jump to line in SSMS | Yes | Opens file externally |
| Catalog scan | Later | Yes |
| Install | VSIX | External Tools / exe |
