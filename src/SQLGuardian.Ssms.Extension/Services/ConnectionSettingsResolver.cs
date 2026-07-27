using System;
using System.Collections;
using System.IO;
using System.Reflection;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace SQLGuardian.Ssms.Extension;

internal static class ConnectionSettingsResolver
{
    public static bool TryResolveConnectionString(
        SQLGuardianPackage package,
        GeneralOptionPage options,
        Document? document,
        out string? connectionString,
        out string? source,
        out string? error)
    {
        if (package is null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.UseActiveDocumentConnection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
        }

        if (options.UseActiveDocumentConnection
            && TryGetActiveDocumentConnectionString(package, out connectionString, out error))
        {
            source = "active SSMS connection";
            return true;
        }

        var configured = BuildConfiguredConnectionString(options);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            connectionString = configured;
            source = "saved SQLGuardian settings";
            error = null;
            return true;
        }

        connectionString = null;
        source = null;
        error = options.UseActiveDocumentConnection
            ? "SQLGuardian could not read the active SSMS connection and no saved fallback profile is configured."
            : "No SQLGuardian connection profile is configured.";
        return false;
    }

    public static string? BuildConfiguredConnectionString(GeneralOptionPage options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (TryBuildFromProfile(options, out var profileConnection))
        {
            return profileConnection;
        }

        return string.IsNullOrWhiteSpace(options.ConnectionString)
            ? null
            : options.ConnectionString.Trim();
    }

    private static bool TryBuildFromProfile(GeneralOptionPage options, out string? connectionString)
    {
        connectionString = null;
        if (string.IsNullOrWhiteSpace(options.ServerName) || string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            return false;
        }

        if (options.UseWindowsAuthentication)
        {
            connectionString =
                $"Server={Escape(options.ServerName.Trim())};Database={Escape(options.DatabaseName.Trim())};" +
                $"Trusted_Connection=True;TrustServerCertificate={BoolText(options.TrustServerCertificate)};Encrypt=True";
            return true;
        }

        if (string.IsNullOrWhiteSpace(options.SqlUserName))
        {
            return false;
        }

        connectionString =
            $"Server={Escape(options.ServerName.Trim())};Database={Escape(options.DatabaseName.Trim())};" +
            $"User ID={Escape(options.SqlUserName.Trim())};Password={Escape(options.SqlPassword)};" +
            $"TrustServerCertificate={BoolText(options.TrustServerCertificate)};Encrypt=True";
        return true;
    }

    private static bool TryGetActiveDocumentConnectionString(
        SQLGuardianPackage package,
        out string? connectionString,
        out string? error)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        connectionString = null;
        error = null;

        try
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte?.ActiveDocument is null)
            {
                error = "No active SSMS document.";
                return false;
            }

            var serviceCacheType = FindLoadedType("Microsoft.SqlServer.Management.UI.VSIntegration.ServiceCache")
                ?? LoadTypeFromSqlPackageBase("Microsoft.SqlServer.Management.UI.VSIntegration.ServiceCache");
            if (serviceCacheType is null)
            {
                error = "SSMS connection services are not available.";
                return false;
            }

            var scriptFactory = serviceCacheType.GetProperty("ScriptFactory", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            if (scriptFactory is null)
            {
                error = "SSMS ScriptFactory is unavailable.";
                return false;
            }

            var activeInfo = scriptFactory.GetType().GetProperty("CurrentlyActiveWndConnectionInfo")
                ?.GetValue(scriptFactory);
            var uiConnectionInfo = activeInfo?.GetType().GetProperty("UIConnectionInfo")?.GetValue(activeInfo);
            if (uiConnectionInfo is null)
            {
                error = "The active window has no SQL connection.";
                return false;
            }

            var server = ReadString(uiConnectionInfo, "ServerName");
            var database = ReadAdvancedOption(uiConnectionInfo, "DATABASE");
            if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
            {
                error = "The active SSMS connection does not expose a server/database pair.";
                return false;
            }

            var userName = ReadString(uiConnectionInfo, "UserName");
            var password = ReadString(uiConnectionInfo, "Password");
            var integrated = ReadBool(uiConnectionInfo, "UseIntegratedSecurity")
                ?? GuessIntegratedSecurity(uiConnectionInfo, userName, password);
            var trustServerCertificate = ReadAdvancedOption(uiConnectionInfo, "TRUSTSERVERCERTIFICATE");
            var encrypt = ReadAdvancedOption(uiConnectionInfo, "ENCRYPT");

            connectionString = integrated
                ? $"Server={Escape(server ?? string.Empty)};Database={Escape(database ?? string.Empty)};Trusted_Connection=True;" +
                  $"TrustServerCertificate={NormalizeBool(trustServerCertificate, true)};" +
                  $"Encrypt={NormalizeBool(encrypt, true)}"
                : $"Server={Escape(server ?? string.Empty)};Database={Escape(database ?? string.Empty)};User ID={Escape(userName ?? string.Empty)};" +
                  $"Password={Escape(password ?? string.Empty)};" +
                  $"TrustServerCertificate={NormalizeBool(trustServerCertificate, true)};" +
                  $"Encrypt={NormalizeBool(encrypt, true)}";

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static Type? LoadTypeFromSqlPackageBase(string fullName)
    {
        var sqlPackageBase = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Microsoft SQL Server Management Studio 21",
            "Release",
            "Common7",
            "IDE",
            "SqlPackageBase.dll");

        if (!File.Exists(sqlPackageBase))
        {
            return null;
        }

        try
        {
            var asm = Assembly.LoadFrom(sqlPackageBase);
            return asm.GetType(fullName, throwOnError: false);
        }
        catch
        {
            return null;
        }
    }

    private static Type? FindLoadedType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static string? ReadAdvancedOption(object uiConnectionInfo, string key)
    {
        var advancedOptions = uiConnectionInfo.GetType().GetProperty("AdvancedOptions")?.GetValue(uiConnectionInfo);
        if (advancedOptions is null)
        {
            return null;
        }

        try
        {
            var indexer = advancedOptions.GetType().GetProperty("Item", [typeof(string)]);
            var value = indexer?.GetValue(advancedOptions, [key]);
            return Convert.ToString(value);
        }
        catch
        {
            if (advancedOptions is IDictionary dictionary && dictionary.Contains(key))
            {
                return Convert.ToString(dictionary[key]);
            }

            return null;
        }
    }

    private static string? ReadString(object target, string propertyName) =>
        Convert.ToString(target.GetType().GetProperty(propertyName)?.GetValue(target));

    private static bool? ReadBool(object target, string propertyName)
    {
        var value = target.GetType().GetProperty(propertyName)?.GetValue(target);
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool GuessIntegratedSecurity(object uiConnectionInfo, string? userName, string? password)
    {
        var authentication = uiConnectionInfo.GetType().GetProperty("AuthenticationType")?.GetValue(uiConnectionInfo);
        if (authentication is not null && int.TryParse(authentication.ToString(), out var authType))
        {
            return authType != 1;
        }

        return string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(password);
    }

    private static string NormalizeBool(string? value, bool fallback)
    {
        if (bool.TryParse(value, out var parsed))
        {
            return BoolText(parsed);
        }

        return BoolText(fallback);
    }

    private static string BoolText(bool value) => value ? "True" : "False";

    private static string Escape(string value) =>
        value.Contains(";") || value.Contains("=")
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
