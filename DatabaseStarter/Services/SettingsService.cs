using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DatabaseStarter.Models;

namespace DatabaseStarter.Services;

public class SettingsService
{
    private static readonly Regex VersionRegex = new(@"\d+(?:\.\d+){1,3}", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var baseDir = AppContext.BaseDirectory;
        _settingsPath = Path.Combine(baseDir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            var settings = CreateSettingsWithAutoDetection();
            Save(settings);
            return settings;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ??
                           CreateSettingsWithAutoDetection();

            if (NormalizeInitializationState(settings))
            {
                Save(settings);
            }

            return settings;
        }
        catch
        {
            return CreateSettingsWithAutoDetection();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static AppSettings CreateSettingsWithAutoDetection()
    {
        var detectedInstances = new Dictionary<DatabaseEngine, DatabaseInstanceInfo>();
        string? detectedBasePath = null;
        var detectedCount = -1;

        foreach (var candidateBasePath in GetCandidateBasePaths())
        {
            var instances = DetectInstances(candidateBasePath);
            if (instances.Count <= detectedCount)
            {
                continue;
            }

            detectedCount = instances.Count;
            detectedBasePath = candidateBasePath;
            detectedInstances = instances;
        }

        var basePath = detectedBasePath ?? GetPreferredDefaultBasePath();
        var defaults = CreateDefaults(basePath);

        foreach (var instance in defaults.Instances)
        {
            if (detectedInstances.TryGetValue(instance.Engine, out var detected))
            {
                instance.Version = detected.Version;
                instance.InstallPath = detected.InstallPath;
                instance.DataDir = detected.DataDir;
                instance.Port = detected.Port;
                instance.ProcessId = detected.ProcessId;
                instance.IsInitialized = detected.IsInitialized;
            }
        }

        return defaults;
    }

    private static AppSettings CreateDefaults(string basePath)
    {
        basePath = Path.GetFullPath(basePath);

        return new AppSettings
        {
            BasePath = basePath,
            Instances = new List<DatabaseInstanceInfo>
            {
                new()
                {
                    Engine = DatabaseEngine.MySQL,
                    Version = DatabaseDefaults.GetDefaultVersion(DatabaseEngine.MySQL).Version,
                    InstallPath = Path.Combine(basePath, "MySQL"),
                    DataDir = Path.Combine(basePath, "MySQL", "data"),
                    Port = DatabaseDefaults.MySqlDefaultPort
                },
                new()
                {
                    Engine = DatabaseEngine.MariaDB,
                    Version = DatabaseDefaults.GetDefaultVersion(DatabaseEngine.MariaDB).Version,
                    InstallPath = Path.Combine(basePath, "MariaDB"),
                    DataDir = Path.Combine(basePath, "MariaDB", "data"),
                    Port = DatabaseDefaults.MariaDbDefaultPort
                },
                new()
                {
                    Engine = DatabaseEngine.PostgreSQL,
                    Version = DatabaseDefaults.GetDefaultVersion(DatabaseEngine.PostgreSQL).Version,
                    InstallPath = Path.Combine(basePath, "PostgreSQL"),
                    DataDir = Path.Combine(basePath, "PostgreSQL", "data"),
                    Port = DatabaseDefaults.PostgreSqlDefaultPort
                }
            }
        };
    }

    private static Dictionary<DatabaseEngine, DatabaseInstanceInfo> DetectInstances(string basePath)
    {
        var instances = new Dictionary<DatabaseEngine, DatabaseInstanceInfo>();

        if (!Directory.Exists(basePath))
        {
            return instances;
        }

        foreach (var engine in Enum.GetValues<DatabaseEngine>())
        {
            var installPath = FindInstallPath(basePath, engine);
            if (installPath is null)
            {
                continue;
            }

            var dataDir = Path.Combine(installPath, "data");

            instances[engine] = new DatabaseInstanceInfo
            {
                Engine = engine,
                Version = DetectVersion(engine, installPath),
                InstallPath = installPath,
                DataDir = dataDir,
                Port = DatabaseDefaults.GetDefaultPort(engine),
                IsInitialized = IsInitializedDataDirectory(dataDir)
            };
        }

        return instances;
    }

    private static IEnumerable<string> GetCandidateBasePaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appBaseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return new[]
            {
                Path.Combine(localAppData, "DatabaseStarter", "Database"),
                Path.Combine(localAppData, "DatabaseStarter"),
                Path.Combine(localAppData, "DatabaseStarter", "DB"),
                Path.Combine(appBaseDir, "Database"),
                Path.Combine(appBaseDir, "DB")
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetPreferredDefaultBasePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DatabaseStarter");
    }

    private static string? FindInstallPath(string basePath, DatabaseEngine engine)
    {
        foreach (var directoryName in GetDirectoryNames(engine))
        {
            var candidate = Path.Combine(basePath, directoryName);
            if (HasEngineBinary(candidate, engine))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetDirectoryNames(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.MySQL => new[] { "MySQL", "mysql" },
        DatabaseEngine.MariaDB => new[] { "MariaDB", "mariadb" },
        DatabaseEngine.PostgreSQL => new[] { "PostgreSQL", "postgresql", "pgsql" },
        _ => Array.Empty<string>()
    };

    private static bool HasEngineBinary(string installPath, DatabaseEngine engine)
    {
        if (!Directory.Exists(installPath))
        {
            return false;
        }

        return GetVersionBinaryCandidates(engine, installPath).Any(File.Exists);
    }

    private static IEnumerable<string> GetVersionBinaryCandidates(DatabaseEngine engine, string installPath) =>
        engine switch
        {
            DatabaseEngine.MySQL =>
            [
                Path.Combine(installPath, "bin", "mysqld.exe")
            ],
            DatabaseEngine.MariaDB =>
            [
                Path.Combine(installPath, "bin", "mariadbd.exe"),
                Path.Combine(installPath, "bin", "mysqld.exe")
            ],
            DatabaseEngine.PostgreSQL =>
            [
                Path.Combine(installPath, "bin", "postgres.exe"),
                Path.Combine(installPath, "bin", "pg_ctl.exe")
            ],
            _ => []
        };

    private static string DetectVersion(DatabaseEngine engine, string installPath)
    {
        foreach (var binaryPath in GetVersionBinaryCandidates(engine, installPath))
        {
            if (!File.Exists(binaryPath))
            {
                continue;
            }

            var version = TryGetFileVersion(binaryPath);
            if (!string.IsNullOrWhiteSpace(version))
            {
                var knownVersion = DatabaseDefaults.FindVersion(engine, version);
                return knownVersion?.Version ?? version;
            }
        }

        return DatabaseDefaults.GetDefaultVersion(engine).Version;
    }

    private static string? TryGetFileVersion(string filePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(filePath);
            var rawVersion = string.IsNullOrWhiteSpace(info.ProductVersion)
                ? info.FileVersion
                : info.ProductVersion;

            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return null;
            }

            var match = VersionRegex.Match(rawVersion);
            return match.Success
                ? NormalizeVersionNumber(match.Value)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeVersionNumber(string version)
    {
        var normalized = version.Trim();

        while (normalized.EndsWith(".0", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
        }

        return normalized;
    }

    private static bool IsInitializedDataDirectory(string dataDir)
    {
        if (string.IsNullOrWhiteSpace(dataDir) || !Directory.Exists(dataDir))
        {
            return false;
        }

        try
        {
            using var entries = Directory.EnumerateFileSystemEntries(dataDir).GetEnumerator();
            return entries.MoveNext();
        }
        catch
        {
            return false;
        }
    }

    private static bool NormalizeInitializationState(AppSettings settings)
    {
        var hasChanges = false;

        foreach (var instance in settings.Instances)
        {
            if (instance.IsInitialized || string.IsNullOrWhiteSpace(instance.DataDir))
            {
                continue;
            }

            try
            {
                if (!Directory.Exists(instance.DataDir))
                {
                    continue;
                }

                if (IsInitializedDataDirectory(instance.DataDir))
                {
                    instance.IsInitialized = true;
                    hasChanges = true;
                }
            }
            catch
            {
                // Ignore inaccessible or invalid data directories and keep the persisted value.
            }
        }

        return hasChanges;
    }
}