using SQLGuardian.ScriptDom.Visitors;

namespace SQLGuardian.ScriptDom.Tests;

public class VisitorPipelineTests
{
    [Fact]
    public void TableVisitor_FindsSchemaQualifiedTablesAndAliases()
    {
        var root = SampleSql.ParseRequired(SampleSql.ReadVisitorSample("tables_and_joins.sql"));
        var tables = TableVisitor.Collect(root).Tables;

        Assert.Equal(3, tables.Count);
        Assert.Contains(tables, t => t.Schema == "dbo" && t.Name == "Users" && t.Alias == "u");
        Assert.Contains(tables, t => t.Name == "Orders" && t.Alias == "o");
        Assert.Contains(tables, t => t.Name == "Regions" && t.Alias == "r");
        Assert.All(tables, t => Assert.True(t.Location.StartLine >= 1));
    }

    [Fact]
    public void JoinVisitor_DistinguishesInnerAndCrossJoins()
    {
        var root = SampleSql.ParseRequired(SampleSql.ReadVisitorSample("tables_and_joins.sql"));
        var joins = JoinVisitor.Collect(root).Joins;

        Assert.Equal(2, joins.Count);
        Assert.Contains(joins, j => j.Kind == SqlJoinKind.Inner && j.HasSearchCondition);
        Assert.Contains(joins, j => j.Kind == SqlJoinKind.CrossJoin && !j.HasSearchCondition);
    }

    [Fact]
    public void ColumnVisitor_DetectsSelectStarAndQualifiedColumns()
    {
        var root = SampleSql.ParseRequired(SampleSql.ReadVisitorSample("columns_and_predicates.sql"));
        var columns = ColumnVisitor.Collect(root).Columns;

        Assert.Contains(columns, c => c.IsWildcard);
        Assert.Contains(columns, c => c is { Qualifier: "p", ColumnName: "Name", IsWildcard: false });
        Assert.Contains(columns, c => c.ColumnName == "ProductId");
    }

    [Fact]
    public void PredicateVisitor_CapturesWherePredicatesIncludingLikePattern()
    {
        var root = SampleSql.ParseRequired(SampleSql.ReadVisitorSample("columns_and_predicates.sql"));
        var visitor = PredicateVisitor.Collect(root);
        var where = visitor.WherePredicates.ToList();

        Assert.Contains(where, p => p.Kind == PredicateKind.Like && p.PatternLiteral == "%widget%");
        Assert.Contains(where, p => p.Kind == PredicateKind.In);
        Assert.Contains(where, p => p.Kind == PredicateKind.IsNull);
        Assert.Contains(where, p => p.Kind == PredicateKind.Exists);
        Assert.Contains(where, p => p.Kind == PredicateKind.And);
        Assert.Contains(visitor.Predicates, p => p.IsInWhereClause);
    }

    [Fact]
    public void FunctionVisitor_FindsScalarAndTableValuedCalls()
    {
        var root = SampleSql.ParseRequired(SampleSql.ReadVisitorSample("functions_and_indexes.sql"));
        var functions = FunctionVisitor.Collect(root).Functions;

        Assert.Contains(functions, f => f.Name == "GetTax" && !f.IsTableValued);
        Assert.Contains(functions, f => f.Name == "COUNT" && f.ArgumentCount == 1);
        Assert.Contains(functions, f => f.Name == "SplitTags" && f.IsTableValued);
    }

    [Fact]
    public void IndexVisitor_FindsCreateAlterAndDrop()
    {
        var root = SampleSql.ParseRequired(SampleSql.ReadVisitorSample("functions_and_indexes.sql"));
        var indexes = IndexVisitor.Collect(root).Indexes;

        Assert.Contains(indexes, i =>
            i.Operation == IndexOperationKind.Create
            && i.IndexName == "IX_Users_Email"
            && i.TableName == "dbo.Users"
            && i.IsUnique == true
            && i.IsClustered == true);

        Assert.Contains(indexes, i => i.Operation == IndexOperationKind.Alter && i.IndexName == "IX_Users_Email");
        Assert.Contains(indexes, i => i.Operation == IndexOperationKind.Drop && i.IndexName == "IX_Users_Email");
    }

    [Fact]
    public void SqlSyntaxFacts_CollectsAllVisitorResults()
    {
        var root = SampleSql.ParseRequired(SampleSql.ReadVisitorSample("tables_and_joins.sql"));
        var facts = SqlSyntaxFacts.Collect(root);

        Assert.NotEmpty(facts.Tables);
        Assert.NotEmpty(facts.Joins);
        Assert.NotEmpty(facts.Columns);
        Assert.NotEmpty(facts.Predicates);
        Assert.Empty(facts.Indexes);
    }

    [Fact]
    public void Visitors_HaveNoRuleKnowledge_TypesLiveInScriptDomOnly()
    {
        // Smoke: occurrence types are constructible without referencing RuleEngine.
        var location = new Abstractions.SourceLocation(1, 1, 1, 5);
        _ = new TableOccurrence(null, null, "dbo", "Users", "u", false, location);
        _ = new ColumnOccurrence("u", "Id", false, location);
        _ = new JoinOccurrence(SqlJoinKind.Inner, true, location);
        _ = new PredicateOccurrence(PredicateKind.Comparison, false, true, null, location);
        _ = new FunctionOccurrence("COUNT", null, 1, false, location);
        _ = new IndexOccurrence(IndexOperationKind.Create, "IX", "dbo.T", true, false, location);
    }
}
