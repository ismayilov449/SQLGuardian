using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine.Rules;

/// <summary>SQLG0018 — Implicit conversion risk (schema-aware).</summary>
public sealed class ImplicitConversionRiskRule : SqlRuleBase
{
    public override string RuleId => "SQLG0018";
    public override string Title => "Implicit conversion risk";
    public override string Description =>
        "Comparing a column to a literal of a different type family can force scans.";
    public override Severity Severity => Severity.Medium;
    public override RuleCategory Category => RuleCategory.Performance;
    public override IReadOnlyList<string> Tags { get; } = ["schema", "conversion", "sargable"];

    protected override void AnalyzeCore(
        TSqlFragment fragment,
        SqlAnalysisContext context,
        ICollection<Issue> issues)
    {
        if (context.Schema is null)
        {
            return;
        }

        var tables = TableVisitor.Collect(fragment).Tables;
        fragment.Accept(new Visitor(this, context, tables, issues));
    }

    private sealed class Visitor : TSqlConcreteFragmentVisitor
    {
        private readonly ImplicitConversionRiskRule _rule;
        private readonly SqlAnalysisContext _context;
        private readonly IReadOnlyList<TableOccurrence> _tables;
        private readonly ICollection<Issue> _issues;
        private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);

        public Visitor(
            ImplicitConversionRiskRule rule,
            SqlAnalysisContext context,
            IReadOnlyList<TableOccurrence> tables,
            ICollection<Issue> issues)
        {
            _rule = rule;
            _context = context;
            _tables = tables;
            _issues = issues;
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (node.ComparisonType == BooleanComparisonType.Equals)
            {
                Consider(node.FirstExpression, node.SecondExpression, node);
                Consider(node.SecondExpression, node.FirstExpression, node);
            }

            base.ExplicitVisit(node);
        }

        private void Consider(ScalarExpression? columnSide, ScalarExpression? literalSide, TSqlFragment location)
        {
            if (columnSide is not ColumnReferenceExpression columnRef
                || !TryInferLiteralFamily(literalSide, out var literalFamily))
            {
                return;
            }

            var col = ToRef(columnRef);
            if (string.IsNullOrWhiteSpace(col.ColumnName))
            {
                return;
            }

            var tableOccurrence = ResolveTable(col.Qualifier, _tables);
            if (tableOccurrence is null || _context.Schema is null)
            {
                return;
            }

            var table = _context.Schema.FindTable(tableOccurrence.Schema, tableOccurrence.Name);
            var column = table?.Columns.FirstOrDefault(c =>
                string.Equals(c.Name, col.ColumnName, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                return;
            }

            var columnFamily = ClassifySqlType(column.DataType);
            if (columnFamily == TypeFamily.Unknown || columnFamily == literalFamily)
            {
                return;
            }

            var key = $"{table!.QualifiedName}.{column.Name}:{literalFamily}";
            if (!_reported.Add(key))
            {
                return;
            }

            _issues.Add(_rule.CreateIssue(
                _context,
                location.GetSourceLocation(),
                $"Possible implicit conversion: column {table.QualifiedName}.{column.Name} is '{column.DataType}' " +
                $"but compared to a {literalFamily.ToString().ToLowerInvariant()} literal.",
                "Use a literal/parameter type that matches the column, or CAST the literal explicitly."));
        }

        private static ColumnRef ToRef(ColumnReferenceExpression column)
        {
            var identifiers = column.MultiPartIdentifier?.Identifiers;
            if (identifiers is null || identifiers.Count == 0)
            {
                return new ColumnRef(null, string.Empty);
            }

            var columnName = ScriptDomNaming.IdentifierValue(identifiers[^1]) ?? string.Empty;
            string? qualifier = identifiers.Count >= 2
                ? ScriptDomNaming.IdentifierValue(identifiers[^2])
                : null;
            return new ColumnRef(qualifier, columnName);
        }

        private static TableOccurrence? ResolveTable(string? qualifier, IReadOnlyList<TableOccurrence> tables)
        {
            if (string.IsNullOrWhiteSpace(qualifier))
            {
                return tables.Count == 1 ? tables[0] : null;
            }

            return tables.FirstOrDefault(t =>
                       string.Equals(t.Alias, qualifier, StringComparison.OrdinalIgnoreCase))
                   ?? tables.FirstOrDefault(t =>
                       string.Equals(t.Name, qualifier, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryInferLiteralFamily(ScalarExpression? expression, out TypeFamily family)
        {
            family = TypeFamily.Unknown;
            switch (expression)
            {
                case StringLiteral:
                    family = TypeFamily.String;
                    return true;
                case IntegerLiteral:
                case NumericLiteral:
                case MoneyLiteral:
                case RealLiteral:
                    family = TypeFamily.Numeric;
                    return true;
                case BinaryLiteral:
                    family = TypeFamily.Binary;
                    return true;
                case ParenthesisExpression paren:
                    return TryInferLiteralFamily(paren.Expression, out family);
                default:
                    return false;
            }
        }

        private static TypeFamily ClassifySqlType(string dataType)
        {
            var t = dataType.Trim().ToLowerInvariant();
            // Strip precision: varchar(50) -> varchar
            var paren = t.IndexOf('(');
            if (paren > 0)
            {
                t = t[..paren];
            }

            return t switch
            {
                "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "sysname" => TypeFamily.String,
                "int" or "bigint" or "smallint" or "tinyint" or "decimal" or "numeric"
                    or "money" or "smallmoney" or "float" or "real" or "bit" => TypeFamily.Numeric,
                "binary" or "varbinary" or "image" or "rowversion" or "timestamp" => TypeFamily.Binary,
                "date" or "datetime" or "datetime2" or "smalldatetime" or "datetimeoffset" or "time" => TypeFamily.DateTime,
                _ => TypeFamily.Unknown
            };
        }
    }

    private enum TypeFamily
    {
        Unknown,
        String,
        Numeric,
        Binary,
        DateTime
    }
}
