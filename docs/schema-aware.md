# Schema-aware analysis

SQLGuardian can attach a **catalog snapshot** so rules recommend indexes using real structure — without reading table data.

## Desktop app (recommended)

Most people should use the WPF companion — not CLI flags.

```bat
scripts\ssms\launch-companion.cmd
```

Or build and run:

```bat
dotnet run --project src\SQLGuardian.Ssms
```

### In the UI

1. Enter **Server** and **Database** (Windows auth is the default).
2. Choose an action:
   - **Scan database** — fast FK-index path (FKs + row counts + indexes on FK parents only; no columns). Cached ~5 minutes.
   - **Analyze SQL file(s)…** / **Analyze script folder…** — full schema when connected (parallel catalog queries).
3. Check **Force refresh schema** if you changed indexes and need a fresh load.
4. Review findings; **Copy** / **Export .sql…** from Recommended SQL.
5. **Export report…** writes Markdown.

### Performance notes

| Mode | What loads | Parallel? |
|------|------------|-----------|
| Catalog scan | FKs → then row counts + indexes only for FK parent tables | Yes (separate connections) |
| Script analyze | Columns + indexes + FKs + row counts | Yes (4 queries) |
| Cache | In-process, 5 minutes, shared by companion host | — |
SSMS External Tools can still launch the same app against the active script — see [ssms-integration.md](ssms-integration.md).

### What “recommended SQL” means

Fixes are **deterministic** (never LLM):

| Finding | Returned SQL |
|---------|----------------|
| SQLG0001 SELECT * + schema | Explicit column list from catalog |
| SQLG0011 / SQLG0012 | `IF NOT EXISTS` + `CREATE NONCLUSTERED INDEX` |
| SQLG0002 / 0006 / 0008 | Safe rewrite templates |

Sample bad query: `samples/recommendations/bad_join_select_star.sql`
## What is loaded

| Metadata | Source | Notes |
|----------|--------|-------|
| Tables / schemas | `sys.tables`, `sys.schemas` | User tables only |
| Columns | `sys.columns`, `sys.types` | Names + types |
| Indexes | `sys.indexes`, `sys.index_columns` | Leading keys + includes |
| Foreign keys | `sys.foreign_keys` | Parent (child table) columns |
| Approximate row counts | `sys.partitions` (`index_id` 0/1) | Never table row payloads |

## Rules

| Rule | When |
|------|------|
| [SQLG0011](rules/SQLG0011.md) | FK without leading-key index |
| [SQLG0012](rules/SQLG0012.md) | Equality join/filter column without leading-key index |
| [SQLG0018](rules/SQLG0018.md) | Implicit conversion (column type vs literal) |
| [SQLG0022](rules/SQLG0022.md) | Equality filter on non-leading index key only |

Without a database connection, SQLG0011/0012/0018/0022 no-op. Text-only rules still run on scripts.

## CLI (CI / automation only)

```bash
sqlguardian catalog --connection "..."
sqlguardian analyze .\scripts --connection "..."
sqlguardian precheck .\script.sql [--connection "..."] --row-threshold 1000000
```

`precheck` reports execute-guard warnings as JSON:
- `missingWhere` — UPDATE/DELETE without WHERE (no connection required)
- `largeReads` — SELECT * / unbounded SELECT on large tables
- `largeJoins` — JOINs involving large tables
- `suggestedNolockSql` — optional rewrite with `WITH (NOLOCK)` on large joined tables

Used by the SSMS Execute guard.

Env fallback: `SQLGUARDIAN_CONNECTION`.

## Architecture

```
SQLGuardian.Abstractions.Schema   SchemaSnapshot, ISchemaProvider
SQLGuardian.Schema               SqlServerSchemaProvider (Microsoft.Data.SqlClient)
SQLGuardian.Ssms                 Desktop UI (connection form + scan / analyze)
SqlAnalysisContext.Schema        Optional snapshot for rules
```

Detection stays deterministic. No LLM. No table data export.
