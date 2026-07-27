# SQLGuardian

**Analyze once, everywhere.**

SQLGuardian is a deterministic SQL static analysis platform. The VS Code extension is a client — not the product.

```
                        SQLGuardian

                Analyze once, everywhere.

                       Rule Engine
                            │
     ┌──────────────┬──────────────┬──────────────┐
     │              │              │              │
 VS Code          CLI           REST API      Enterprise
 Extension        Tool          Server        Dashboard
```

One engine. Many clients.

## Principles

1. **Detection is deterministic** — rules analyze a ScriptDom AST with visitors. Never regex. Never “ask an LLM if this SQL is bad.”
2. **Explanation is optional AI** — LLMs receive structured issues and explain them. They do not find issues.
3. **Clean Architecture** — Presentation → Application → Domain → Infrastructure. Rules live in `SQLGuardian.RuleEngine`, not buried in Infrastructure.
4. **Roslyn model** — Query → Parser → AST → Visitors → Rules → Issues.

## Solution (Task 1)

| Project | Role |
|---------|------|
| `SQLGuardian.Abstractions` | `ISqlRule`, `Issue`, `Severity`, `ISqlParser` |
| `SQLGuardian.Domain` | Metadata, configuration, analysis reports |
| `SQLGuardian.ScriptDom` | Microsoft ScriptDom parser wrapper |
| `SQLGuardian.Schema` | SQL Server catalog loader (schema + row counts only) |
| `SQLGuardian.RuleEngine` | Runs rules against parsed SQL |
| `SQLGuardian.Reporting` | JSON / SARIF / Markdown / text writers |
| `SQLGuardian.LanguageServer` | LSP host (same RuleEngine as CLI) |
| `SQLGuardian.Ssms` | SSMS companion (catalog scan / batch folders) |
| `SQLGuardian.Ssms.Extension` | SSMS 21+ VSIX (Tools menu → Error List) |
| `SQLGuardian.Cli` | `sqlguardian` CLI |
| `extensions/vscode` | VS Code language client |
| `*.Tests` | Unit tests |

Target framework: **net9.0** (local SDK). Upgrade to **net10.0** when the .NET 10 SDK is installed.

## Build

```bash
dotnet build SQLGuardian.sln
dotnet test SQLGuardian.sln
dotnet run --project src/SQLGuardian.Cli -- analyze samples --format text
dotnet run --project src/SQLGuardian.Cli -- analyze samples --format sarif -o sqlguardian.sarif --fail-on high

# Schema-aware (metadata + row counts only — never table data)
dotnet run --project src/SQLGuardian.Cli -- catalog --connection "Server=.;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project src/SQLGuardian.Cli -- analyze samples --connection "Server=.;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True"
```

### SSMS 21+ VSIX (Error List)

```bat
scripts\ssms\pack-vsix.cmd
```

Install with SSMS `VSIXInstaller.exe`, then **Tools → SQLGuardian — Analyze Active Script (Error List)**.  
See [SSMS extension](docs/ssms-extension.md).

### Desktop companion (catalog / folders)

```bat
dotnet run --project src\SQLGuardian.Ssms
scripts\ssms\launch-companion.cmd
```

Enter Server + Database in the UI, then **Scan database** or **Analyze SQL file(s)…**.  
See [schema-aware analysis](docs/schema-aware.md). CLI `--connection` flags remain for CI only.

## Roadmap

| Task | Focus |
|------|--------|
| **1** | Solution, contracts, CI |
| **2** | AST visitors + parse pipeline tests |
| **3** | First 10 rules + `/docs/rules` specs |
| **4** | CLI outputs: JSON, SARIF, Markdown |
| **5** | VS Code LSP client |
| **S1–S5** | SSMS External Tools + companion |
| **S6** | SSMS 21 VSIX → Error List (Phase 1) — *this checkpoint* |
| **Schema** | Catalog snapshot + SQLG0011/0012 |
| **6** | AI explanations (sidecar) |
| **7** | Enterprise scanner / dashboard |

## Docs

- [Architecture](docs/architecture.md)
- [Coding standards](docs/coding-standards.md)
- [Schema-aware analysis](docs/schema-aware.md)
- [Rule specification format](docs/rules/README.md)
- [CLI report formats](docs/cli-reports.md)
- [VS Code extension / LSP](docs/vscode-extension.md)
- [SSMS extension (VSIX)](docs/ssms-extension.md)
- [SSMS companion](docs/ssms-integration.md)

## License

Proprietary / TBD.
