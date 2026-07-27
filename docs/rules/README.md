# Rule specifications

Rule specs are the heart of the product. Implementations can change; high-quality specifications are hard to copy.

## Directory layout

```
docs/rules/
  README.md
  SQLG0001.md … SQLG0010.md   ← first pack (Task 3)
  SQLG0011.md … SQLG0012.md   ← schema-aware (catalog)
  SQLG0013.md … SQLG0022.md   ← suggested-order pack (perf / security / schema)
```

## Required template

See any `SQLGxxxx.md` file. Every shipping rule must have a matching spec.

## Process

1. Write the spec first.
2. Implement `ISqlRule` with the same `RuleId`.
3. Add fixture tests that match the examples.
4. Only then expose the rule in CLI / LSP.

## First pack (Task 3)

| ID | Title | Implementation |
|----|-------|----------------|
| SQLG0001 | Avoid SELECT * | `SelectStarRule` |
| SQLG0002 | UPDATE/DELETE without WHERE | `MissingWhereRule` |
| SQLG0003 | Avoid NOLOCK table hint | `NoLockRule` |
| SQLG0004 | TOP without ORDER BY | `TopWithoutOrderRule` |
| SQLG0005 | LIKE with leading wildcard | `LikeLeadingWildcardRule` |
| SQLG0006 | CROSS JOIN usage | `CrossJoinRule` |
| SQLG0007 | Avoid cursors | `CursorRule` |
| SQLG0008 | UNION instead of UNION ALL | `UnionRule` |
| SQLG0009 | DISTINCT usage | `DistinctRule` |
| SQLG0010 | Avoid WAITFOR DELAY | `WaitForDelayRule` |

## Schema-aware

| ID | Title | Implementation |
|----|-------|----------------|
| SQLG0011 | Foreign key without supporting index | `MissingForeignKeyIndexRule` |
| SQLG0012 | Unindexed join column | `UnindexedJoinColumnRule` |
| SQLG0018 | Implicit conversion risk | `ImplicitConversionRiskRule` |
| SQLG0022 | Filter on non-leading index key | `NonLeadingIndexKeyFilterRule` |

## Suggested-order pack

| ID | Title | Implementation |
|----|-------|----------------|
| SQLG0013 | Non-SARGable function on column | `NonSargableFunctionOnColumnRule` |
| SQLG0014 | Dynamic SQL string concatenation | `DynamicSqlConcatenationRule` |
| SQLG0015 | Missing SET NOCOUNT ON in module | `MissingSetNocountOnRule` |
| SQLG0016 | Prefer NOT EXISTS over NOT IN (subquery) | `NotInSubqueryRule` |
| SQLG0017 | Prefer EXISTS over IN (subquery) | `InSubqueryPreferExistsRule` |
| SQLG0019 | Avoid xp_cmdshell | `XpCmdshellRule` |
| SQLG0020 | Avoid OPENROWSET / OPENDATASOURCE | `OpenRowsetRule` |
| SQLG0021 | TRUNCATE TABLE usage | `TruncateTableRule` |

See [schema-aware analysis](../schema-aware.md).

Configuration sample: `rules/sqlguardian.sample.json` (keys may be rule IDs or class names; values are severities or `Disabled`).
