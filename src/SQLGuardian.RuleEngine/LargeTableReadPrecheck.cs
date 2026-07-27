using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine;

/// <summary>
/// Detects SELECT * or unbounded SELECT against tables whose catalog approximate row count
/// meets or exceeds a threshold. Used for cancelable pre-execute warnings (not a RuleEngine rule).
/// </summary>
public static class LargeTableReadPrecheck
{
    public const long DefaultRowThreshold = 1_000_000;

    public static IReadOnlyList<LargeTableReadWarning> Evaluate(
        string sourceText,
        SchemaSnapshot schema,
        long rowThreshold = DefaultRowThreshold,
        ISqlParser? parser = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(schema);

        if (rowThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rowThreshold), "Threshold must be at least 1.");
        }

        parser ??= new ScriptDomSqlParser();
        var parseResult = parser.Parse(sourceText);
        if (parseResult.SyntaxTree is not TSqlFragment fragment || parseResult.Errors.Count > 0)
        {
            return [];
        }

        var visitor = new Visitor(schema, rowThreshold);
        fragment.Accept(visitor);
        return visitor.Warnings;
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly SchemaSnapshot _schema;
        private readonly long _rowThreshold;
        private readonly Dictionary<string, LargeTableReadWarning> _byKey =
            new(StringComparer.OrdinalIgnoreCase);

        public Visitor(SchemaSnapshot schema, long rowThreshold)
        {
            _schema = schema;
            _rowThreshold = rowThreshold;
        }

        public IReadOnlyList<LargeTableReadWarning> Warnings =>
            _byKey.Values.OrderByDescending(w => w.ApproximateRowCount).ToList();

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var hasSelectStar = node.SelectElements.Any(e => e is SelectStarExpression);
            var isUnbounded = node.WhereClause is null
                && node.TopRowFilter is null
                && node.OffsetClause is null;

            if (!hasSelectStar && !isUnbounded)
            {
                base.ExplicitVisit(node);
                return;
            }

            var kind = hasSelectStar
                ? LargeTableReadKind.SelectStar
                : LargeTableReadKind.UnboundedSelect;

            var tables = new TableVisitor();
            node.FromClause.Accept(tables);

            foreach (var occurrence in tables.Tables)
            {
                if (string.IsNullOrWhiteSpace(occurrence.Name))
                {
                    continue;
                }

                var table = _schema.FindTable(occurrence.Schema, occurrence.Name);
                if (table is null || table.ApproximateRowCount < _rowThreshold)
                {
                    continue;
                }

                var key = SchemaSnapshot.TableKey(table.Schema, table.Name);
                if (_byKey.TryGetValue(key, out var existing)
                    && Prefer(existing.Kind) >= Prefer(kind))
                {
                    continue;
                }

                _byKey[key] = new LargeTableReadWarning
                {
                    Schema = table.Schema,
                    Table = table.Name,
                    ApproximateRowCount = table.ApproximateRowCount,
                    Kind = kind,
                    StartLine = occurrence.Location.StartLine,
                    StartColumn = occurrence.Location.StartColumn,
                    Message = BuildMessage(table, kind)
                };
            }

            base.ExplicitVisit(node);
        }

        private static int Prefer(LargeTableReadKind kind) =>
            kind == LargeTableReadKind.SelectStar ? 2 : 1;

        private static string BuildMessage(TableSchema table, LargeTableReadKind kind)
        {
            var qualified = $"{table.Schema}.{table.Name}";
            var rows = table.ApproximateRowCount.ToString("N0");
            return kind == LargeTableReadKind.SelectStar
                ? $"SELECT * from {qualified} (~{rows} rows) may return a very large result set."
                : $"SELECT from {qualified} with no WHERE/TOP (~{rows} rows) may return a very large result set.";
        }
    }
}

public enum LargeTableReadKind
{
    SelectStar,
    UnboundedSelect
}

public sealed class LargeTableReadWarning
{
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public long ApproximateRowCount { get; init; }
    public LargeTableReadKind Kind { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public required string Message { get; init; }

    public string QualifiedName => $"{Schema}.{Table}";
}
