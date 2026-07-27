namespace SQLGuardian.Abstractions.Schema;

/// <summary>Shared index-coverage checks for schema-aware rules.</summary>
public static class SchemaIndexHelpers
{
    /// <summary>
    /// True when any index has <paramref name="columns"/> as its leading key columns (order-sensitive).
    /// </summary>
    public static bool HasLeadingKeyIndex(TableSchema table, IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Count == 0)
        {
            return true;
        }

        foreach (var index in table.Indexes)
        {
            if (index.KeyColumns.Count < columns.Count)
            {
                continue;
            }

            var match = true;
            for (var i = 0; i < columns.Count; i++)
            {
                if (!string.Equals(index.KeyColumns[i], columns[i], StringComparison.OrdinalIgnoreCase))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsLeadingKeyOfPrimaryKey(TableSchema table, IReadOnlyList<string> columns)
    {
        var pk = table.Indexes.FirstOrDefault(i => i.IsPrimaryKey);
        if (pk is null || pk.KeyColumns.Count < columns.Count)
        {
            return false;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            if (!string.Equals(pk.KeyColumns[i], columns[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when some index key list contains <paramref name="column"/> but no index leads with it.
    /// </summary>
    public static bool IsOnlyNonLeadingKey(TableSchema table, string column)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrWhiteSpace(column))
        {
            return false;
        }

        if (HasLeadingKeyIndex(table, [column]))
        {
            return false;
        }

        foreach (var index in table.Indexes)
        {
            for (var i = 1; i < index.KeyColumns.Count; i++)
            {
                if (string.Equals(index.KeyColumns[i], column, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
