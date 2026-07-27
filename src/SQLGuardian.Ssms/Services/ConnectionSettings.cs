using System.IO;
using System.Text.Json;

namespace SQLGuardian.Ssms.Services;

/// <summary>
/// Builds a SQL Server connection string from form fields (no raw CS required).
/// </summary>
public sealed class SqlConnectionForm
{
    public string Server { get; set; } = ".";

    public string Database { get; set; } = string.Empty;

    public bool UseWindowsAuthentication { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool TrustServerCertificate { get; set; } = true;

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            error = "Enter a server name (e.g. . or localhost\\SQLEXPRESS).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            error = "Enter a database name.";
            return false;
        }

        if (!UseWindowsAuthentication && string.IsNullOrWhiteSpace(UserName))
        {
            error = "Enter a SQL login user name, or switch to Windows authentication.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public string ToConnectionString()
    {
        // Built manually so the UI project does not depend on SqlClient types.
        var server = Escape(Server.Trim());
        var database = Escape(Database.Trim());
        var trust = TrustServerCertificate ? "True" : "False";

        if (UseWindowsAuthentication)
        {
            return
                $"Server={server};Database={database};Trusted_Connection=True;" +
                $"TrustServerCertificate={trust};Encrypt=True";
        }

        return
            $"Server={server};Database={database};User ID={Escape(UserName.Trim())};" +
            $"Password={Escape(Password)};TrustServerCertificate={trust};Encrypt=True";
    }

    private static string Escape(string value) =>
        value.Contains(';', StringComparison.Ordinal) || value.Contains('=', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    public static bool TryParse(string connectionString, out SqlConnectionForm form)
    {
        form = new SqlConnectionForm();
        try
        {
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string? server = null;
            string? database = null;
            string? user = null;
            string? password = null;
            var windows = false;
            var trust = true;

            foreach (var part in parts)
            {
                var eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                var key = part[..eq].Trim();
                var value = part[(eq + 1)..].Trim().Trim('"');

                if (key.Equals("Server", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("Data Source", StringComparison.OrdinalIgnoreCase))
                {
                    server = value;
                }
                else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase)
                         || key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase))
                {
                    database = value;
                }
                else if (key.Equals("User ID", StringComparison.OrdinalIgnoreCase)
                         || key.Equals("UID", StringComparison.OrdinalIgnoreCase))
                {
                    user = value;
                }
                else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase)
                         || key.Equals("PWD", StringComparison.OrdinalIgnoreCase))
                {
                    password = value;
                }
                else if (key.Equals("Trusted_Connection", StringComparison.OrdinalIgnoreCase)
                         || key.Equals("Integrated Security", StringComparison.OrdinalIgnoreCase))
                {
                    windows = value.Equals("True", StringComparison.OrdinalIgnoreCase)
                              || value.Equals("SSPI", StringComparison.OrdinalIgnoreCase)
                              || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                }
                else if (key.Equals("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
                {
                    trust = value.Equals("True", StringComparison.OrdinalIgnoreCase)
                            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            {
                return false;
            }

            form = new SqlConnectionForm
            {
                Server = server,
                Database = database,
                UseWindowsAuthentication = windows || string.IsNullOrWhiteSpace(user),
                UserName = user ?? string.Empty,
                Password = password ?? string.Empty,
                TrustServerCertificate = trust
            };
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Persists last-used UI settings (not passwords by default).</summary>
public sealed class CompanionSettings
{
    public string Server { get; set; } = ".";

    public string Database { get; set; } = string.Empty;

    public bool UseWindowsAuthentication { get; set; } = true;

    public string UserName { get; set; } = string.Empty;

    public bool TrustServerCertificate { get; set; } = true;

    public bool RememberPassword { get; set; }

    public string? Password { get; set; }

    public string? LastScriptPath { get; set; }
}

public static class CompanionSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SQLGuardian",
            "companion-settings.json");

    public static CompanionSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new CompanionSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<CompanionSettings>(json, JsonOptions)
                   ?? new CompanionSettings();
        }
        catch
        {
            return new CompanionSettings();
        }
    }

    public static void Save(CompanionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var toSave = new CompanionSettings
        {
            Server = settings.Server,
            Database = settings.Database,
            UseWindowsAuthentication = settings.UseWindowsAuthentication,
            UserName = settings.UserName,
            TrustServerCertificate = settings.TrustServerCertificate,
            RememberPassword = settings.RememberPassword,
            Password = settings.RememberPassword ? settings.Password : null,
            LastScriptPath = settings.LastScriptPath
        };

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(toSave, JsonOptions));
    }
}
