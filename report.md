# SQLGuardian Report

- **Version:** 0.4.0
- **Generated:** 2026-07-21 11:57:02 UTC
- **Files:** 1
- **Issues:** 2
- **Parse errors:** 0

## `columns_and_predicates.sql`

| Severity | Rule | Line | Message | Suggestion |
|----------|------|------|---------|------------|
| Medium | SQLG0001 | 3:8 | SELECT * expands to all columns and increases I/O and coupling to table shape. | List only the columns you need. |
| Medium | SQLG0005 | 5:7 | LIKE pattern '%widget%' starts with a wildcard and may prevent index seeks. | Avoid a leading wildcard, or use full-text search for contains matching. |

