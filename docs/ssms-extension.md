# SQLGuardian for SSMS

**Catch risky or expensive T-SQL before it runs** — then review the rest of your script in the familiar **Error List**.

SQLGuardian works inside **SQL Server Management Studio 21+**. It uses the same deterministic rules as the `sqlguardian` CLI: no AI guesses, same findings every time.

| Who you are | What you get |
|-------------|--------------|
| **Developer** | Static analysis in the Error List, double-click to jump to the line, keyboard shortcut |
| **Data analyst** | Warnings about `SELECT *`, large tables, and costly patterns before a query hammers production |
| **Business analyst** | A safety net before Execute: missing `WHERE` on updates/deletes, and large-table / large-join prompts |

---

## Install (about 2 minutes)

1. [Download the latest `.vsix`](https://www.vsixgallery.com/extension/SQLGuardian.Ssms.Extension.7c2e9a1b-4d5f-4a8e-9b3c-1f0e6d8a2c44).
2. **Close SSMS completely.**
3. Install with the **SSMS** VSIX installer (not the Visual Studio one):

```bat
"%ProgramFiles%\Microsoft SQL Server Management Studio 21\Release\Common7\IDE\VSIXInstaller.exe" SQLGuardian.Ssms.Extension.vsix
```

4. Open SSMS again.

> If double-clicking the `.vsix` opens Visual Studio and fails, use the command above instead.

---

## How to use it

### Analyze the script you are editing

1. Open a query window (`.sql`).
2. Press **Ctrl+Shift+G**, or choose **Tools → SQLGuardian — Analyze Active Script (Error List)**.
3. Open **View → Error List** if it is not visible.
4. **Double-click** any finding to jump to that line.

Clear previous results anytime with **Tools → SQLGuardian — Clear Findings**.

| Severity in Error List | Typical meaning |
|------------------------|-----------------|
| **Error** | High / critical issue — fix or confirm before relying on the script |
| **Warning** | Performance, style, or risk pattern worth reviewing |

### Analyze automatically after Execute

By default, after **F5** / **Query → Execute**, SQLGuardian re-analyzes the script and refreshes the Error List.

Turn this on or off under **Tools → Options → SQLGuardian → Analysis**.

### Stop dangerous or expensive Execute (guards)

Before Execute runs, SQLGuardian can interrupt risky actions. You choose **Cancel** or continue.

| Guard | When it appears | Why it matters |
|-------|-----------------|----------------|
| **Missing WHERE** | `UPDATE` or `DELETE` with no `WHERE` | Can change or wipe an entire table |
| **Large SELECT** | Unbounded `SELECT` / `SELECT *` on a large table | Can lock, flood the network, or freeze SSMS |
| **Large JOIN** | Join involving a large table | Can explode row counts and run for a long time |

**Selection tip:** If you highlight a statement and press F5, SSMS (and SQLGuardian) only look at the **selection**. With nothing selected, the **whole window** is checked — so a leftover `SELECT *` above your `UPDATE` can still trigger a large-read warning.

Schema-aware guards (row counts) use your **active SSMS connection**. If that is unavailable, set a fallback under **Tools → Options → SQLGuardian → Connection**.

Large-join dialogs can optionally offer **Apply NOLOCK** (off by default). That can allow dirty reads; treat it as an advanced escape hatch, not a best practice.

---

## What kinds of issues it finds

Examples (not a full list):

- `SELECT *`
- `UPDATE` / `DELETE` without `WHERE`
- Leading wildcards in `LIKE` (`'%text'`)
- `CROSS JOIN`, cursors, `TOP` without `ORDER BY`
- Other performance and safety rules documented in the [rules catalog](https://github.com/ismayilov449/SQLGuardian/blob/main/docs/rules/README.md)

Findings always point at a **file + line** so you can fix or discuss the exact statement.

---

## Settings at a glance

| Path | What it controls |
|------|------------------|
| **Tools → Options → SQLGuardian → Analysis** | Analyze after Execute; execute-guard toggles and thresholds |
| **Tools → Options → SQLGuardian → Connection** | Fallback server/database when the active connection is missing |
| **Tools → Options → Environment → Keyboard** | Remap **Ctrl+Shift+G** (`SQLGuardian.AnalyzeActiveScript`) |

---

## Extension vs companion app

| Capability | This SSMS extension | WPF companion |
|------------|---------------------|---------------|
| Analyze active script | Yes — Error List | Yes — own results grid |
| Jump to line in SSMS | Yes | Opens the file externally |
| Folder / catalog scan | Coming later | Yes today |
| Install | VSIX (this page) | Separate exe / External Tools |

---

## Build from source (contributors)

```bat
scripts\ssms\pack-vsix.cmd
```

Output: `artifacts\SQLGuardian.Ssms.Extension.vsix`  
Requires Visual Studio 2022 (MSBuild) and the .NET 9 SDK (bundled CLI).

---

## Feedback

- **Source:** [github.com/ismayilov449/SQLGuardian](https://github.com/ismayilov449/SQLGuardian)
- **Issues:** [github.com/ismayilov449/SQLGuardian/issues](https://github.com/ismayilov449/SQLGuardian/issues)
