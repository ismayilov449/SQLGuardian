using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;

namespace SQLGuardian.ScriptDom.Visitors;

/// <summary>
/// Convenience entry point: parse once, collect all visitor results.
/// Visitors remain independent; this only orchestrates them.
/// </summary>
public sealed class SqlSyntaxFacts
{
    public required IReadOnlyList<TableOccurrence> Tables { get; init; }

    public required IReadOnlyList<ColumnOccurrence> Columns { get; init; }

    public required IReadOnlyList<JoinOccurrence> Joins { get; init; }

    public required IReadOnlyList<PredicateOccurrence> Predicates { get; init; }

    public required IReadOnlyList<FunctionOccurrence> Functions { get; init; }

    public required IReadOnlyList<IndexOccurrence> Indexes { get; init; }

    public static SqlSyntaxFacts Collect(TSqlFragment root)
    {
        ArgumentNullException.ThrowIfNull(root);

        return new SqlSyntaxFacts
        {
            Tables = TableVisitor.Collect(root).Tables,
            Columns = ColumnVisitor.Collect(root).Columns,
            Joins = JoinVisitor.Collect(root).Joins,
            Predicates = PredicateVisitor.Collect(root).Predicates,
            Functions = FunctionVisitor.Collect(root).Functions,
            Indexes = IndexVisitor.Collect(root).Indexes
        };
    }

    public static SqlSyntaxFacts? TryCollect(SqlAnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var fragment = ScriptDomSyntax.GetFragment(context);
        return fragment is null ? null : Collect(fragment);
    }
}
