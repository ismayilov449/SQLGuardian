using Microsoft.SqlServer.TransactSql.ScriptDom;
using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.ScriptDom;
using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.RuleEngine;

/// <summary>
/// Pre-execute guards: UPDATE/DELETE without WHERE, large unbounded reads, and large joins.
/// Not a RuleEngine rule — used by CLI <c>precheck</c> and the SSMS BeforeExecute hook.
/// </summary>
public static class ExecuteGuardPrecheck
{
    public const long DefaultRowThreshold = LargeTableReadPrecheck.DefaultRowThreshold;

    public static ExecuteGuardPrecheckResult Evaluate(
        string sourceText,
        SchemaSnapshot? schema = null,
        long rowThreshold = DefaultRowThreshold,
        ISqlParser? parser = null)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        if (rowThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rowThreshold), "Threshold must be at least 1.");
        }

        parser ??= new ScriptDomSqlParser();
        var parseResult = parser.Parse(sourceText);
        // ScriptDom often still returns a usable tree when it reports non-fatal parse issues.
        if (parseResult.SyntaxTree is not TSqlFragment fragment)
        {
            return ExecuteGuardPrecheckResult.Empty;
        }

        var missingWhere = CollectMissingWhere(fragment);
        var largeReads = schema is null
            ? Array.Empty<LargeTableReadWarning>()
            : LargeTableReadPrecheck.Evaluate(sourceText, schema, rowThreshold, parser);
        var largeJoins = schema is null
            ? Array.Empty<LargeJoinWarning>()
            : CollectLargeJoins(fragment, schema, rowThreshold);

        return new ExecuteGuardPrecheckResult
        {
            MissingWhere = missingWhere,
            LargeReads = largeReads,
            LargeJoins = largeJoins
        };
    }

    /// <summary>
    /// Inserts <c>WITH (NOLOCK)</c> after matching joined table references that do not already have it.
    /// Returns the original text if nothing changes.
    /// </summary>
    public static string ApplyNolockHints(string sourceText, IReadOnlyList<LargeJoinWarning> joins)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(joins);

        if (joins.Count == 0)
        {
            return sourceText;
        }

        var parser = new ScriptDomSqlParser();
        var parseResult = parser.Parse(sourceText);
        if (parseResult.SyntaxTree is not TSqlFragment fragment || parseResult.Errors.Count > 0)
        {
            return sourceText;
        }

        var targets = new HashSet<string>(
            joins.Select(j => SchemaSnapshot.TableKey(j.Schema, j.Table)),
            StringComparer.OrdinalIgnoreCase);

        var inserts = new List<int>();
        var visitor = new NolockInsertVisitor(targets, inserts);
        fragment.Accept(visitor);

        if (inserts.Count == 0)
        {
            return sourceText;
        }

        var result = sourceText;
        foreach (var offset in inserts.Distinct().OrderByDescending(o => o))
        {
            if (offset < 0 || offset > result.Length)
            {
                continue;
            }

            result = result.Insert(offset, " WITH (NOLOCK)");
        }

        return result;
    }

    private static IReadOnlyList<MissingWhereWarning> CollectMissingWhere(TSqlFragment fragment)
    {
        var visitor = new MissingWhereVisitor();
        fragment.Accept(visitor);
        return visitor.Warnings;
    }

    private static IReadOnlyList<LargeJoinWarning> CollectLargeJoins(
        TSqlFragment fragment,
        SchemaSnapshot schema,
        long rowThreshold)
    {
        var visitor = new LargeJoinVisitor(schema, rowThreshold);
        fragment.Accept(visitor);
        return visitor.Warnings;
    }

    private sealed class MissingWhereVisitor : TSqlConcreteFragmentVisitor
    {
        private readonly List<MissingWhereWarning> _warnings = [];

        public IReadOnlyList<MissingWhereWarning> Warnings => _warnings;

        public override void ExplicitVisit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.WhereClause is null)
            {
                var target = FormatTarget(node.UpdateSpecification?.Target);
                _warnings.Add(new MissingWhereWarning
                {
                    StatementKind = "UPDATE",
                    Target = target,
                    StartLine = node.StartLine,
                    StartColumn = node.StartColumn,
                    Message = string.IsNullOrWhiteSpace(target)
                        ? "UPDATE without a WHERE clause affects all rows in the target."
                        : $"UPDATE on {target} without a WHERE clause affects all rows."
                });
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.WhereClause is null)
            {
                var target = FormatTarget(node.DeleteSpecification?.Target);
                _warnings.Add(new MissingWhereWarning
                {
                    StatementKind = "DELETE",
                    Target = target,
                    StartLine = node.StartLine,
                    StartColumn = node.StartColumn,
                    Message = string.IsNullOrWhiteSpace(target)
                        ? "DELETE without a WHERE clause removes all rows from the target."
                        : $"DELETE FROM {target} without a WHERE clause removes all rows."
                });
            }

            base.ExplicitVisit(node);
        }

        private static string? FormatTarget(TableReference? target)
        {
            if (target is NamedTableReference named)
            {
                var schema = ScriptDomNaming.IdentifierValue(named.SchemaObject?.SchemaIdentifier);
                var name = ScriptDomNaming.IdentifierValue(named.SchemaObject?.BaseIdentifier);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }

                return string.IsNullOrWhiteSpace(schema) ? name : $"{schema}.{name}";
            }

            return null;
        }
    }

    private sealed class LargeJoinVisitor : TSqlConcreteFragmentVisitor
    {
        private readonly SchemaSnapshot _schema;
        private readonly long _rowThreshold;
        private readonly Dictionary<string, LargeJoinWarning> _byKey =
            new(StringComparer.OrdinalIgnoreCase);

        public LargeJoinVisitor(SchemaSnapshot schema, long rowThreshold)
        {
            _schema = schema;
            _rowThreshold = rowThreshold;
        }

        public IReadOnlyList<LargeJoinWarning> Warnings =>
            _byKey.Values.OrderByDescending(w => w.ApproximateRowCount).ToList();

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var joinCount = CountJoins(node.FromClause);
            if (joinCount == 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            var tables = new JoinTableCollector();
            node.FromClause.Accept(tables);

            foreach (var item in tables.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Name) || item.HasNoLock)
                {
                    continue;
                }

                var table = _schema.FindTable(item.Schema, item.Name);
                if (table is null || table.ApproximateRowCount < _rowThreshold)
                {
                    continue;
                }

                var key = SchemaSnapshot.TableKey(table.Schema, table.Name);
                if (_byKey.ContainsKey(key))
                {
                    continue;
                }

                _byKey[key] = new LargeJoinWarning
                {
                    Schema = table.Schema,
                    Table = table.Name,
                    Alias = item.Alias,
                    ApproximateRowCount = table.ApproximateRowCount,
                    StartLine = item.StartLine,
                    StartColumn = item.StartColumn,
                    Message =
                        $"Join involves {table.Schema}.{table.Name} (~{table.ApproximateRowCount:N0} rows). " +
                        "This may block under load."
                };
            }

            base.ExplicitVisit(node);
        }

        private static int CountJoins(FromClause fromClause)
        {
            var counter = new JoinCounter();
            fromClause.Accept(counter);
            return counter.Count;
        }
    }

    private sealed class JoinCounter : TSqlConcreteFragmentVisitor
    {
        public int Count { get; private set; }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            Count++;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UnqualifiedJoin node)
        {
            Count++;
            base.ExplicitVisit(node);
        }
    }

    private sealed class JoinTableCollector : TSqlConcreteFragmentVisitor
    {
        public List<JoinTableItem> Items { get; } = [];

        public override void ExplicitVisit(NamedTableReference node)
        {
            var schemaObject = node.SchemaObject;
            var hasNoLock = false;
            foreach (var hint in node.TableHints ?? [])
            {
                if (hint.HintKind is TableHintKind.NoLock or TableHintKind.ReadUncommitted)
                {
                    hasNoLock = true;
                    break;
                }
            }

            Items.Add(new JoinTableItem(
                ScriptDomNaming.IdentifierValue(schemaObject?.SchemaIdentifier),
                ScriptDomNaming.IdentifierValue(schemaObject?.BaseIdentifier) ?? string.Empty,
                ScriptDomNaming.IdentifierValue(node.Alias),
                hasNoLock,
                node.StartLine,
                node.StartColumn,
                node.StartOffset,
                node.FragmentLength));

            base.ExplicitVisit(node);
        }
    }

    private sealed class NolockInsertVisitor : TSqlConcreteFragmentVisitor
    {
        private readonly HashSet<string> _targets;
        private readonly List<int> _inserts;

        public NolockInsertVisitor(HashSet<string> targets, List<int> inserts)
        {
            _targets = targets;
            _inserts = inserts;
        }

        public override void ExplicitVisit(NamedTableReference node)
        {
            foreach (var hint in node.TableHints ?? [])
            {
                if (hint.HintKind is TableHintKind.NoLock or TableHintKind.ReadUncommitted)
                {
                    base.ExplicitVisit(node);
                    return;
                }
            }

            var schema = ScriptDomNaming.IdentifierValue(node.SchemaObject?.SchemaIdentifier);
            var name = ScriptDomNaming.IdentifierValue(node.SchemaObject?.BaseIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || !IsTarget(schema, name))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Insert after name/alias, before any existing hints.
            var insertAt = node.StartOffset + node.FragmentLength;
            if (node.TableHints is { Count: > 0 })
            {
                insertAt = node.TableHints[0].StartOffset;
            }
            else if (node.Alias is not null)
            {
                insertAt = node.Alias.StartOffset + node.Alias.FragmentLength;
            }
            else if (node.SchemaObject is not null)
            {
                insertAt = node.SchemaObject.StartOffset + node.SchemaObject.FragmentLength;
            }

            _inserts.Add(insertAt);
            base.ExplicitVisit(node);
        }

        private bool IsTarget(string? schema, string name)
        {
            if (_targets.Contains(SchemaSnapshot.TableKey(schema ?? "dbo", name)))
            {
                return true;
            }

            return _targets.Any(t =>
                t.EndsWith("." + name, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed record JoinTableItem(
        string? Schema,
        string Name,
        string? Alias,
        bool HasNoLock,
        int StartLine,
        int StartColumn,
        int StartOffset,
        int FragmentLength);
}

public sealed class ExecuteGuardPrecheckResult
{
    public static ExecuteGuardPrecheckResult Empty { get; } = new()
    {
        MissingWhere = [],
        LargeReads = [],
        LargeJoins = []
    };

    public required IReadOnlyList<MissingWhereWarning> MissingWhere { get; init; }
    public required IReadOnlyList<LargeTableReadWarning> LargeReads { get; init; }
    public required IReadOnlyList<LargeJoinWarning> LargeJoins { get; init; }

    public int WarningCount => MissingWhere.Count + LargeReads.Count + LargeJoins.Count;
}

public sealed class MissingWhereWarning
{
    public required string StatementKind { get; init; }
    public string? Target { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public required string Message { get; init; }
}

public sealed class LargeJoinWarning
{
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public string? Alias { get; init; }
    public long ApproximateRowCount { get; init; }
    public int StartLine { get; init; }
    public int StartColumn { get; init; }
    public required string Message { get; init; }

    public string QualifiedName => $"{Schema}.{Table}";
}
