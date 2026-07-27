> **SSMS 21+ VSIX (Error List)** — preferred for day-to-day script analysis.  
> See [ssms-extension.md](ssms-extension.md).  
> The WPF companion below remains for catalog scan and batch folders.

```
SSMS Tools → SQLGuardian — Analyze Active Script (Error List)
        ↓
Error List  (double-click → jump to line)
        ↓
RuleEngine (via bundled CLI)
```

---

## Companion app (catalog / folders)

SQL Server developers live in **SQL Server Management Studio**, not VS Code.
This track also includes a WPF companion launched from **Tools → External Tools**.

```
SSMS (External Tools / Object Explorer scripts)
        ↓
SQLGuardian.Ssms.exe  (Error List UI)
        ↓
RuleEngine + ScriptDom
```

---

## Task S1 — External Tools (CLI / companion)

### One-time setup in SSMS 21

1. Build once:

```bat
dotnet build src\SQLGuardian.Ssms\SQLGuardian.Ssms.csproj -c Release
```

2. SSMS → **Tools** → **External Tools…** → **Add**

#### Tool A — Analyze Active Script

| Field | Value |
|-------|-------|
| Title | `SQLGuardian: Analyze Active Script` |
| Command | `C:\Users\...\SQLGuardian\scripts\ssms\analyze-active-script.cmd` |
| Arguments | `$(ItemPath)` |
| Initial directory | `$(ItemDir)` |

Open a `.sql` query window, save it, then run the tool.

#### Tool B — Analyze Script Folder

| Field | Value |
|-------|-------|
| Title | `SQLGuardian: Analyze Script Folder` |
| Command | `C:\Users\...\SQLGuardian\scripts\ssms\analyze-folder.cmd` |
| Arguments | `$(ItemDir)` (or leave empty and pick in UI) |

Scripts live in `scripts/ssms/`.

### CLI-only variant (no UI)

```bat
dotnet run --project src/SQLGuardian.Cli -c Release -- analyze "$(ItemPath)" --format text
```

---

## Task S2 — Analyze Active Script

The companion opens with the active file path from External Tools and runs analysis immediately.

Standalone:

```bat
scripts\ssms\launch-companion.cmd --file samples\visitors\columns_and_predicates.sql
```

UI actions:

- **Analyze File…** — multi-select `.sql`
- **Re-analyze** — run again on the current set

---

## Task S3 — Error List

Findings appear in a grid (Severity, Rule, File, Line, Col, Message).

- Select a row → suggestion shown in the footer
- Double-click → opens the `.sql` file (often in SSMS)
- **Export Markdown** → shareable report

Same rule IDs as CLI (`SQLG0001`, …).

---

## Task S4 — Configuration (`sqlguardian.json`)

Severity overrides use the same JSON as the CLI:

```json
{
  "SelectStarRule": "Warning",
  "SQLG0003": "Error",
  "SQLG0009": "Disabled"
}
```

Resolution order:

1. `--config <path>` argument
2. Nearest `sqlguardian.json` walking up from the analyzed file/folder
3. Built-in defaults

Sample: `rules/sqlguardian.sample.json`

---

## Task S5 — Object Explorer / multi-script analyze

SSMS does not expose a stable public API for “analyze checked Object Explorer nodes” across versions. The supported workflow:

1. Object Explorer → right-click database / folder  
2. **Tasks → Generate Scripts…** (or Script As → CREATE to file)  
3. Save scripts into a folder, e.g. `C:\SqlExport\MyDb\`  
4. Run **SQLGuardian: Analyze Script Folder** (or **Analyze Folder…** in the companion)

The companion recursively analyzes all `*.sql` files.

```bat
scripts\ssms\launch-companion.cmd --folder C:\SqlExport\MyDb
```

---

## Architecture notes

| Project | Role |
|---------|------|
| `SQLGuardian.Ssms` | WPF companion (SSMS client) |
| `SQLGuardian.Cli` | Headless / CI / External Tools fallback |
| `SQLGuardian.RuleEngine` | Shared detection (unchanged) |

Rules still never call an LLM. Detection stays deterministic.

---

## Schema-aware recommendations

Use the desktop companion (not CLI) for day-to-day work:

1. Launch `scripts\ssms\launch-companion.cmd` (or External Tools).
2. Fill in **Server** + **Database**.
3. Click **Scan database** (catalog) or **Analyze SQL file(s)…**.

Details: [schema-aware analysis](schema-aware.md).
