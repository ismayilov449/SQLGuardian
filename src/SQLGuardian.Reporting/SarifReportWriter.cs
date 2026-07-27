using System.Text.Json;
using System.Text.Json.Serialization;
using SQLGuardian.Abstractions;
using SQLGuardian.Domain;

namespace SQLGuardian.Reporting;

/// <summary>
/// Writes SARIF 2.1.0 suitable for GitHub code scanning upload.
/// </summary>
public sealed class SarifReportWriter : IReportWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ReportFormat Format => ReportFormat.Sarif;

    public string Write(AnalysisRun run, ReportWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(run);

        var rules = run.AllIssues
            .GroupBy(i => i.RuleId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(i => i.RuleId, StringComparer.OrdinalIgnoreCase)
            .Select(i => new SarifReportingDescriptor
            {
                Id = i.RuleId,
                Name = SanitizeRuleName(i.Title),
                ShortDescription = new SarifMessage { Text = i.Title },
                FullDescription = new SarifMessage { Text = i.Message },
                DefaultConfiguration = new SarifReportingConfiguration
                {
                    Level = ToSarifLevel(i.Severity)
                },
                HelpUri = $"https://github.com/sqlguardian/sqlguardian/blob/main/docs/rules/{i.RuleId}.md",
                Properties = new SarifPropertyBag
                {
                    Category = i.Category.ToString(),
                    Tags = i.Tags.Count == 0 ? null : i.Tags.ToList()
                }
            })
            .ToList();

        var results = new List<SarifResult>();

        foreach (var report in run.Reports)
        {
            var uri = ReportPathHelper.ToSarifUri(report.Target, options?.BaseDirectory);

            foreach (var error in report.ParseErrors)
            {
                results.Add(new SarifResult
                {
                    RuleId = "SQLG-PARSE",
                    Level = "error",
                    Message = new SarifMessage { Text = error },
                    Locations =
                    [
                        new SarifLocation
                        {
                            PhysicalLocation = new SarifPhysicalLocation
                            {
                                ArtifactLocation = new SarifArtifactLocation { Uri = uri },
                                Region = new SarifRegion { StartLine = 1, StartColumn = 1 }
                            }
                        }
                    ]
                });
            }

            foreach (var issue in report.Issues)
            {
                results.Add(new SarifResult
                {
                    RuleId = issue.RuleId,
                    Level = ToSarifLevel(issue.Severity),
                    Message = new SarifMessage
                    {
                        Text = string.IsNullOrWhiteSpace(issue.Suggestion)
                            ? issue.Message
                            : $"{issue.Message} Suggestion: {issue.Suggestion}"
                    },
                    Locations =
                    [
                        new SarifLocation
                        {
                            PhysicalLocation = new SarifPhysicalLocation
                            {
                                ArtifactLocation = new SarifArtifactLocation { Uri = uri },
                                Region = new SarifRegion
                                {
                                    StartLine = Math.Max(1, issue.Location.StartLine),
                                    StartColumn = Math.Max(1, issue.Location.StartColumn),
                                    EndLine = Math.Max(1, issue.Location.EndLine),
                                    EndColumn = Math.Max(1, issue.Location.EndColumn)
                                }
                            }
                        }
                    ],
                    Properties = new Dictionary<string, string>
                    {
                        ["category"] = issue.Category.ToString()
                    }
                });
            }
        }

        if (run.ParseErrorCount > 0
            && rules.All(r => !string.Equals(r.Id, "SQLG-PARSE", StringComparison.OrdinalIgnoreCase)))
        {
            rules.Insert(0, new SarifReportingDescriptor
            {
                Id = "SQLG-PARSE",
                Name = "SqlParseError",
                ShortDescription = new SarifMessage { Text = "SQL parse error" },
                FullDescription = new SarifMessage { Text = "The ScriptDom parser reported one or more errors." },
                DefaultConfiguration = new SarifReportingConfiguration { Level = "error" }
            });
        }

        var document = new SarifLog
        {
            Schema = "https://json.schemastore.org/sarif-2.1.0.json",
            Version = "2.1.0",
            Runs =
            [
                new SarifRun
                {
                    Tool = new SarifTool
                    {
                        Driver = new SarifToolComponent
                        {
                            Name = run.ToolName,
                            Version = run.ToolVersion,
                            InformationUri = "https://github.com/sqlguardian/sqlguardian",
                            Rules = rules
                        }
                    },
                    Results = results,
                    ColumnKind = "utf16CodeUnits"
                }
            ]
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static string ToSarifLevel(Severity severity) => severity switch
    {
        Severity.Critical or Severity.High => "error",
        Severity.Medium => "warning",
        _ => "note"
    };

    private static string SanitizeRuleName(string title)
    {
        var chars = title
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var name = new string(chars).Trim('_');
        return string.IsNullOrEmpty(name) ? "Rule" : name;
    }

    private sealed class SarifLog
    {
        [JsonPropertyName("$schema")]
        public required string Schema { get; init; }

        public required string Version { get; init; }

        public required List<SarifRun> Runs { get; init; }
    }

    private sealed class SarifRun
    {
        public required SarifTool Tool { get; init; }

        public required List<SarifResult> Results { get; init; }

        public string? ColumnKind { get; init; }
    }

    private sealed class SarifTool
    {
        public required SarifToolComponent Driver { get; init; }
    }

    private sealed class SarifToolComponent
    {
        public required string Name { get; init; }

        public required string Version { get; init; }

        public string? InformationUri { get; init; }

        public required List<SarifReportingDescriptor> Rules { get; init; }
    }

    private sealed class SarifReportingDescriptor
    {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public SarifMessage? ShortDescription { get; init; }

        public SarifMessage? FullDescription { get; init; }

        public SarifReportingConfiguration? DefaultConfiguration { get; init; }

        public string? HelpUri { get; init; }

        public SarifPropertyBag? Properties { get; init; }
    }

    private sealed class SarifPropertyBag
    {
        public string? Category { get; init; }

        public List<string>? Tags { get; init; }
    }

    private sealed class SarifReportingConfiguration
    {
        public required string Level { get; init; }
    }

    private sealed class SarifResult
    {
        public required string RuleId { get; init; }

        public required string Level { get; init; }

        public required SarifMessage Message { get; init; }

        public required List<SarifLocation> Locations { get; init; }

        public Dictionary<string, string>? Properties { get; init; }
    }

    private sealed class SarifMessage
    {
        public required string Text { get; init; }
    }

    private sealed class SarifLocation
    {
        public required SarifPhysicalLocation PhysicalLocation { get; init; }
    }

    private sealed class SarifPhysicalLocation
    {
        public required SarifArtifactLocation ArtifactLocation { get; init; }

        public required SarifRegion Region { get; init; }
    }

    private sealed class SarifArtifactLocation
    {
        public required string Uri { get; init; }
    }

    private sealed class SarifRegion
    {
        public int StartLine { get; init; }

        public int StartColumn { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int EndLine { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int EndColumn { get; init; }
    }
}
