# Coding standards

## Non-negotiable

1. **Never parse or detect SQL with regular expressions.** Use `Microsoft.SqlServer.TransactSql.ScriptDom` via `SQLGuardian.ScriptDom`.
2. **Never put detection logic in an LLM.** AI may explain issues; it may not find them.
3. **Every rule implements `ISqlRule`.** Same shape, always.
4. **Respect dependency direction.** See [architecture.md](architecture.md).

## C# / .NET

- Target: `net9.0` (upgrade to `net10.0` when SDK available)
- `Nullable` enabled; treat warnings as errors
- Prefer `sealed` types for leaf implementations
- Prefer `record` for immutable value-like types (`SourceLocation`)
- No `#pragma warning disable` without a linked issue/comment
- Public APIs documented with XML comments where intent is not obvious from names

## Naming

| Kind | Pattern | Example |
|------|---------|---------|
| Rule ID | `SQLG` + 4 digits | `SQLG0001` |
| Rule class | `{Name}Rule` | `SelectStarRule` |
| Visitor | `{Name}Visitor` | `JoinVisitor` |
| Tests | `{Subject}Tests` | `SelectStarRuleTests` |

## Visitors

- Prefer `TSqlConcreteFragmentVisitor` + `ExplicitVisit` so child nodes walk correctly (required for WHERE-depth tracking and similar context).
- Visitors must not reference `ISqlRule`, severities, or issue messages.
- Rules consume visitor outputs (`TableOccurrence`, `SqlSyntaxFacts`, etc.).

## Style

- File-scoped namespaces
- One primary type per file
- Keep rules isolated — no shared mutable statics between rules

## What not to build yet

Do not scaffold Dashboard, AI, multi-DB parsers, or editor extensions until their task begins. Empty shells create false progress.
