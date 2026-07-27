# Architecture

## Goal

Ship the world’s best SQL static analysis **platform**. Clients (VS Code, CLI, API, dashboard) share one rule engine.

## Layers

```
Presentation   Cli / (future) Api / LSP / Dashboard
      ↓
Application    (future orchestration, scanning jobs)
      ↓
Domain         RuleMetadata, RuleConfiguration, AnalysisReport
      ↓
Abstractions    ISqlRule, ISqlParser, Issue, Severity
      ↑
Infrastructure ScriptDom parser, (future) DB / LLM clients
```

### Current projects

```
SQLGuardian.Abstractions     ← contracts only (incl. SchemaSnapshot)
SQLGuardian.Domain           ← domain models (references Abstractions)
SQLGuardian.ScriptDom        ← ISqlParser via Microsoft ScriptDom
SQLGuardian.Schema           ← SQL Server catalog loader (metadata + row counts)
SQLGuardian.RuleEngine       ← executes ISqlRule (references Domain + ScriptDom)
SQLGuardian.Reporting        ← JSON / SARIF / Markdown / text writers
SQLGuardian.LanguageServer   ← LSP host over RuleEngine
SQLGuardian.Ssms             ← SSMS companion (External Tools + Error List UI)
SQLGuardian.Cli              ← thin host over RuleEngine + Reporting
extensions/vscode            ← VS Code language client (TypeScript)
```

### Dependency rules

| Allowed | Forbidden |
|---------|-----------|
| RuleEngine → Abstractions, Domain, ScriptDom | Rules → ASP.NET, OpenAI, VS Code APIs |
| Cli / Ssms → Schema (load catalog) | Domain → ScriptDom package types |
| ScriptDom → Abstractions | Regex-based SQL “parsing” anywhere |
| Schema → Abstractions + SqlClient | LLM calls inside `ISqlRule.Analyze` |
| Tests → RuleEngine, Domain, Abstractions | Schema providers reading table row data |

`SqlAnalysisContext.SyntaxTree` is typed as `object?` in Abstractions so Domain/Abstractions stay free of ScriptDom package references. ScriptDom and future visitors cast to `TSqlFragment`.

## Analysis pipeline

```
Source (.sql)
    → ISqlParser (ScriptDomSqlParser)
    → SqlParseResult / SqlAnalysisContext
    → Visitors (Table / Column / Join / Predicate / Function / Index)
    → ISqlRule.Analyze (× N)
    → Issue[]
    → AnalysisReport
```

Visitors know nothing about rules. Rules consume visitor results (or `SqlSyntaxFacts.Collect`) and/or dedicated `TSqlConcreteFragmentVisitor`s.

Built-in rules are discovered by `RuleCatalog.CreateDefault()` (SQLG0001–SQLG0022). Severity overrides load from JSON via `RuleConfigurationLoader` (rule ID or class name keys). Schema-aware rules (SQLG0011/0012/0018/0022) no-op unless `SqlAnalysisContext.Schema` is set.

| Visitor | Collects |
|---------|----------|
| `TableVisitor` | `NamedTableReference` → schema, name, alias, hints |
| `ColumnVisitor` | column refs + `SELECT *` wildcards |
| `JoinVisitor` | qualified + unqualified joins (incl. CROSS JOIN / APPLY) |
| `PredicateVisitor` | comparisons, LIKE, IN, IS NULL, EXISTS, AND/OR (+ WHERE nesting) |
| `FunctionVisitor` | scalar `FunctionCall` + TVF table references |
| `IndexVisitor` | CREATE / ALTER / DROP INDEX |

Helpers: `FragmentExtensions.GetSourceLocation`, `ScriptDomNaming`, `ScriptDomSyntax`.

## Rule identity

- IDs: `SQLG0001`, `SQLG0002`, …
- Specs live in `/docs/rules/` (product IP)
- Implementations live in `SQLGuardian.RuleEngine` (Task 3+)
- User severity overrides: `RuleConfiguration` (JSON file format in Task 4)

## Multi-database future

Do not hardcode “SQL Server” into Domain contracts. Parser and rule packs will be database-specific; shared infrastructure (issues, config, reporting) stays common. Task 1 only implements the SQL Server / ScriptDom path.

## Out of scope until later tasks

- Application / Infrastructure persistence projects
- AI explanation service
- REST API, enterprise dashboard
- VS Code / Visual Studio / JetBrains clients
