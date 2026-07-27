namespace SQLGuardian.Reporting;

internal static class ReportPathHelper
{
    public static string NormalizePath(string path, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return path.Replace('\\', '/');
        }

        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullBase = Path.GetFullPath(baseDirectory);
            var relative = Path.GetRelativePath(fullBase, fullPath);
            return relative.Replace('\\', '/');
        }
        catch
        {
            return path.Replace('\\', '/');
        }
    }

    public static string ToSarifUri(string path, string? baseDirectory)
    {
        var normalized = NormalizePath(path, baseDirectory);
        if (normalized.Contains(':', StringComparison.Ordinal) && !normalized.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            // Absolute Windows path without base → file URI
            if (Path.IsPathRooted(path))
            {
                return new Uri(Path.GetFullPath(path)).AbsoluteUri;
            }
        }

        return normalized.Replace('\\', '/');
    }
}
