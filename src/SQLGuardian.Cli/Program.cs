using SQLGuardian.Abstractions;
using SQLGuardian.Abstractions.Schema;
using SQLGuardian.Domain;
using SQLGuardian.Reporting;
using SQLGuardian.RuleEngine;
using SQLGuardian.Schema;

namespace SQLGuardian.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintBanner();
            PrintUsage();
            return 0;
        }

        var command = args[0];
        if (string.Equals(command, "catalog", StringComparison.OrdinalIgnoreCase))
        {
            return RunCatalog(args.Skip(1).ToArray());
        }

        if (string.Equals(command, "precheck", StringComparison.OrdinalIgnoreCase))
        {
            return RunPrecheck(args.Skip(1).ToArray());
        }

        if (!string.Equals(command, "analyze", StringComparison.OrdinalIgnoreCase))
        {
            PrintBanner();
            Console.Error.WriteLine($"Unknown command: {command}");
            PrintUsage();
            return 2;
        }

        return RunAnalyze(args.Skip(1).ToArray());
    }

    private static int RunPrecheck(string[] args)
    {
        var path = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            PrintBanner();
            Console.Error.WriteLine(string.IsNullOrWhiteSpace(path) ? "Missing path." : $"Path not found: {path}");
            PrintUsage();
            return 2;
        }

        var connection = ResolveConnection(args);
        var thresholdText = GetOptionValue(args, "--row-threshold") ?? GetOptionValue(args, "--threshold");
        var threshold = ExecuteGuardPrecheck.DefaultRowThreshold;
        if (!string.IsNullOrWhiteSpace(thresholdText)
            && (!long.TryParse(thresholdText, out threshold) || threshold < 1))
        {
            Console.Error.WriteLine("Invalid --row-threshold (must be a positive integer).");
            return 2;
        }

        var quiet = HasFlag(args, "--quiet");
        SchemaSnapshot? schema = null;
        if (!string.IsNullOrWhiteSpace(connection))
        {
            try
            {
                schema = new SqlServerSchemaProvider(connection)
                    .LoadAsync(SchemaLoadOptions.RowCountsOnly)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load row counts: {ex.Message}");
                return 2;
            }
        }

        var source = File.ReadAllText(path);
        var result = ExecuteGuardPrecheck.Evaluate(source, schema, threshold);
        var suggestedNolockSql = result.LargeJoins.Count == 0
            ? null
            : ExecuteGuardPrecheck.ApplyNolockHints(source, result.LargeJoins);

        var payload = new
        {
            database = schema?.DatabaseName,
            rowThreshold = threshold,
            warningCount = result.WarningCount,
            missingWhere = result.MissingWhere.Select(w => new
            {
                statementKind = w.StatementKind,
                target = w.Target,
                message = w.Message,
                startLine = w.StartLine,
                startColumn = w.StartColumn
            }).ToList(),
            largeReads = result.LargeReads.Select(w => new
            {
                schema = w.Schema,
                table = w.Table,
                approximateRowCount = w.ApproximateRowCount,
                kind = w.Kind.ToString(),
                message = w.Message,
                startLine = w.StartLine,
                startColumn = w.StartColumn
            }).ToList(),
            largeJoins = result.LargeJoins.Select(w => new
            {
                schema = w.Schema,
                table = w.Table,
                alias = w.Alias,
                approximateRowCount = w.ApproximateRowCount,
                message = w.Message,
                startLine = w.StartLine,
                startColumn = w.StartColumn
            }).ToList(),
            suggestedNolockSql =
                suggestedNolockSql is not null
                && !string.Equals(suggestedNolockSql, source, StringComparison.Ordinal)
                    ? suggestedNolockSql
                    : null,
            // Backward-compatible alias used by older extension builds.
            warnings = result.LargeReads.Select(w => new
            {
                schema = w.Schema,
                table = w.Table,
                approximateRowCount = w.ApproximateRowCount,
                kind = w.Kind.ToString(),
                message = w.Message,
                startLine = w.StartLine,
                startColumn = w.StartColumn
            }).ToList()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(
            payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = !quiet });
        Console.WriteLine(json);

        if (!quiet && result.WarningCount > 0)
        {
            Console.Error.WriteLine($"Precheck: {result.WarningCount} execute-guard warning(s).");
        }

        return 0;
    }

    private static int RunCatalog(string[] args)
    {
        var connection = ResolveConnection(args);
        if (string.IsNullOrWhiteSpace(connection))
        {
            PrintBanner();
            Console.Error.WriteLine("Missing --connection (or SQLGUARDIAN_CONNECTION).");
            PrintUsage();
            return 2;
        }

        if (!TryParseReportOptions(args, out var format, out var failOn, out var outputPath, out var baseDirectory, out var quiet))
        {
            return 2;
        }

        var rules = RuleCatalog.CreateDefault();
        var configPath = GetOptionValue(args, "--config");
        var configuration = RuleConfigurationLoader.TryLoadFile(configPath ?? string.Empty, out var loaded, rules)
            ? loaded
            : new RuleConfiguration();

        if (!quiet)
        {
            PrintBanner();
            Console.WriteLine("Loading catalog metadata (schema + row counts only)…");
        }

        SchemaSnapshot schema;
        try
        {
            schema = new SqlServerSchemaProvider(connection)
                .LoadAsync(SchemaLoadOptions.CatalogScan)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load schema: {ex.Message}");
            return 2;
        }

        if (!quiet)
        {
            Console.WriteLine(
                $"Loaded {schema.Tables.Count} FK parent table(s) from '{schema.DatabaseName}' " +
                $"(captured {schema.CapturedAtUtc:u}).");
        }

        var engine = new SqlRuleEngine(rules, configuration: configuration);
        var report = engine.AnalyzeCatalog(schema);
        var run = new AnalysisRun
        {
            Reports = [report],
            ToolVersion = "0.6.0"
        };

        return WriteAndExit(run, format, outputPath, baseDirectory, failOn, quiet, rules.Count);
    }

    private static int RunAnalyze(string[] args)
    {
        var path = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(path))
        {
            PrintBanner();
            Console.Error.WriteLine("Missing path.");
            PrintUsage();
            return 2;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            Console.Error.WriteLine($"Path not found: {path}");
            return 2;
        }

        if (!TryParseReportOptions(args, out var format, out var failOn, out var outputPath, out var baseDirectory, out var quiet))
        {
            return 2;
        }

        baseDirectory ??= Directory.Exists(path)
            ? Path.GetFullPath(path)
            : Path.GetDirectoryName(Path.GetFullPath(path));

        var rules = RuleCatalog.CreateDefault();
        var configPath = GetOptionValue(args, "--config") ?? FindDefaultConfig(path);
        var configuration = RuleConfigurationLoader.TryLoadFile(configPath ?? string.Empty, out var loaded, rules)
            ? loaded
            : new RuleConfiguration();

        SchemaSnapshot? schema = null;
        var connection = ResolveConnection(args);
        if (!string.IsNullOrWhiteSpace(connection))
        {
            if (!quiet)
            {
                PrintBanner();
                Console.WriteLine("Loading catalog metadata (schema + row counts only)…");
            }

            try
            {
                schema = new SqlServerSchemaProvider(connection)
                    .LoadAsync(SchemaLoadOptions.Full)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load schema: {ex.Message}");
                return 2;
            }

            if (!quiet)
            {
                Console.WriteLine(
                    $"Schema attached: {schema.Tables.Count} table(s) from '{schema.DatabaseName}'.");
            }
        }
        else if (!quiet)
        {
            PrintBanner();
            if (configPath is not null && File.Exists(configPath))
            {
                Console.WriteLine($"Using config: {configPath}");
            }
        }

        if (!quiet && configPath is not null && File.Exists(configPath) && schema is not null)
        {
            Console.WriteLine($"Using config: {configPath}");
        }

        var engine = new SqlRuleEngine(rules, configuration: configuration);
        var files = EnumerateSqlFiles(path).ToList();

        if (files.Count == 0)
        {
            if (!quiet)
            {
                Console.WriteLine("No .sql files found.");
            }

            return 0;
        }

        var reports = new List<AnalysisReport>(files.Count);
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            reports.Add(engine.Analyze(source, file, schema));
        }

        var run = new AnalysisRun
        {
            Reports = reports,
            ToolVersion = "0.6.0"
        };

        return WriteAndExit(run, format, outputPath, baseDirectory, failOn, quiet, rules.Count);
    }

    private static int WriteAndExit(
        AnalysisRun run,
        ReportFormat format,
        string? outputPath,
        string? baseDirectory,
        Severity? failOn,
        bool quiet,
        int ruleCount)
    {
        var writer = ReportWriterFactory.Create(format);
        var content = writer.Write(run, new ReportWriteOptions { BaseDirectory = baseDirectory });

        if (outputPath is not null)
        {
            var fullOutput = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullOutput);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullOutput, content);
            if (!quiet)
            {
                Console.WriteLine($"Wrote {format} report: {fullOutput}");
                Console.WriteLine($"Analyzed {run.FileCount} target(s) with {ruleCount} rule(s). Issues: {run.IssueCount}.");
            }
        }
        else
        {
            Console.Write(content);
            if (format is ReportFormat.Text && !content.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                Console.WriteLine();
            }
        }

        if (failOn is null)
        {
            return 0;
        }

        return run.MeetsFailureThreshold(failOn.Value) ? 1 : 0;
    }

    private static bool TryParseReportOptions(
        string[] args,
        out ReportFormat format,
        out Severity? failOn,
        out string? outputPath,
        out string? baseDirectory,
        out bool quiet)
    {
        format = ReportFormat.Text;
        failOn = null;
        outputPath = null;
        baseDirectory = null;
        quiet = false;

        var formatValue = GetOptionValue(args, "--format") ?? "text";
        if (!ReportWriterFactory.TryParseFormat(formatValue, out format))
        {
            Console.Error.WriteLine($"Unknown --format '{formatValue}'. Use text, json, sarif, or markdown.");
            return false;
        }

        try
        {
            failOn = FailureThresholdParser.Parse(GetOptionValue(args, "--fail-on") ?? "high");
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }

        outputPath = GetOptionValue(args, "--output") ?? GetOptionValue(args, "-o");
        baseDirectory = GetOptionValue(args, "--base-dir");
        quiet = HasFlag(args, "--quiet") || format is not ReportFormat.Text || outputPath is not null;
        return true;
    }

    private static string? ResolveConnection(string[] args) =>
        GetOptionValue(args, "--connection")
        ?? Environment.GetEnvironmentVariable("SQLGUARDIAN_CONNECTION");

    private static void PrintBanner()
    {
        Console.WriteLine("SQLGuardian — Analyze once, everywhere.");
        Console.WriteLine();
    }

    private static string? GetOptionValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? FindDefaultConfig(string analyzePath)
    {
        var start = File.Exists(analyzePath)
            ? Path.GetDirectoryName(Path.GetFullPath(analyzePath))
            : Path.GetFullPath(analyzePath);

        for (var dir = start; dir is not null; dir = Directory.GetParent(dir)?.FullName)
        {
            var candidate = Path.Combine(dir, "sqlguardian.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsHelp(string value) =>
        value is "-h" or "--help" or "/?" or "help";

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  sqlguardian analyze <file-or-directory> [options]");
        Console.WriteLine("  sqlguardian catalog --connection <cs> [options]");
        Console.WriteLine("  sqlguardian precheck <file> [--connection <cs>] [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --connection <cs>     SQL Server connection string (schema + row counts only)");
        Console.WriteLine("  --row-threshold <n>   precheck: warn when approximate rows >= n (default: 1000000)");
        Console.WriteLine("                        Also used for large-join warnings. Missing WHERE needs no connection.");
        Console.WriteLine("  --config <file>       Rule severity overrides (JSON)");
        Console.WriteLine("  --format <format>     text | json | sarif | markdown (default: text)");
        Console.WriteLine("  --output, -o <file>   Write report to file instead of stdout");
        Console.WriteLine("  --fail-on <level>     critical|high|medium|low|info|never (default: high)");
        Console.WriteLine("  --base-dir <dir>      Root for relative paths in JSON/SARIF/Markdown");
        Console.WriteLine("  --quiet               Suppress banner / non-report messages");
        Console.WriteLine();
        Console.WriteLine("Env: SQLGUARDIAN_CONNECTION — default connection string");
        Console.WriteLine();
        Console.WriteLine($"Built-in rules: {RuleCatalog.CreateDefault().Count}");
        Console.WriteLine();
        Console.WriteLine("Exit codes: 0 = ok, 1 = issues at/above --fail-on, 2 = usage error");
    }

    private static IEnumerable<string> EnumerateSqlFiles(string path)
    {
        if (File.Exists(path))
        {
            yield return Path.GetFullPath(path);
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.sql", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.GetFullPath(file);
        }
    }
}
