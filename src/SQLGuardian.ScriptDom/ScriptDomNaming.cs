using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SQLGuardian.ScriptDom;

/// <summary>
/// Formats ScriptDom name nodes without touching raw SQL text for detection.
/// </summary>
public static class ScriptDomNaming
{
    public static string? IdentifierValue(Identifier? identifier) =>
        string.IsNullOrEmpty(identifier?.Value) ? null : identifier.Value;

    public static string Format(SchemaObjectName? name)
    {
        if (name is null)
        {
            return string.Empty;
        }

        return FormatParts(
            IdentifierValue(name.ServerIdentifier),
            IdentifierValue(name.DatabaseIdentifier),
            IdentifierValue(name.SchemaIdentifier),
            IdentifierValue(name.BaseIdentifier));
    }

    public static string Format(MultiPartIdentifier? name)
    {
        if (name is null || name.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < name.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('.');
            }

            builder.Append(IdentifierValue(name[i]) ?? string.Empty);
        }

        return builder.ToString();
    }

    public static string FormatParts(params string?[] parts)
    {
        var builder = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('.');
            }

            builder.Append(part);
        }

        return builder.ToString();
    }
}
