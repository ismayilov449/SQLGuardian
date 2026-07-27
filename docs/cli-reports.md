# CLI report formats

`sqlguardian analyze` supports structured outputs for CI and humans.

## Formats

| `--format` | Description |
|------------|-------------|
| `text` | Human-readable console output (default) |
| `json` | Machine-readable analysis document |
| `sarif` | SARIF 2.1.0 (GitHub code scanning) |
| `markdown` / `md` | Markdown summary tables |

## Options

```bash
dotnet run --project src/SQLGuardian.Cli -- analyze samples \
  --format sarif \
  --output sqlguardian.sarif \
  --base-dir . \
  --fail-on high \
  --config rules/sqlguardian.sample.json \
  --quiet
```

| Option | Meaning |
|--------|---------|
| `--output` / `-o` | Write report to a file |
| `--fail-on` | Exit `1` when findings reach this severity (`critical`, `high`, `medium`, `low`, `info`, `never`) |
| `--base-dir` | Root for relative paths in JSON/SARIF/Markdown |
| `--config` | Rule severity overrides |
| `--quiet` | Suppress banner |

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success (or `--fail-on never`) |
| 1 | Issues at/above `--fail-on`, or parse errors |
| 2 | Usage / configuration error |

## GitHub Actions

See [.github/workflows/analyze-sarif.yml](../.github/workflows/analyze-sarif.yml) for SARIF upload.
