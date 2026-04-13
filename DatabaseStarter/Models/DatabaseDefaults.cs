using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace DatabaseStarter.Models;

public static class DatabaseDefaults
{
    public const int MySqlDefaultPort = 3306;
    public const int MariaDbDefaultPort = 3307;
    public const int PostgreSqlDefaultPort = 5432;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Lazy<IReadOnlyDictionary<DatabaseEngine, IReadOnlyList<DatabaseVersionInfo>>>
        AvailableVersionsByEngine =
            new(LoadAvailableVersions);

    // ── Available versions per engine ──────────────────────────────────

    public static IReadOnlyList<DatabaseVersionInfo> MySqlVersions => GetAvailableVersions(DatabaseEngine.MySQL);

    public static IReadOnlyList<DatabaseVersionInfo> MariaDbVersions => GetAvailableVersions(DatabaseEngine.MariaDB);

    public static IReadOnlyList<DatabaseVersionInfo> PostgreSqlVersions =>
        GetAvailableVersions(DatabaseEngine.PostgreSQL);

    // ── Helper methods ─────────────────────────────────────────────────

    public static IReadOnlyList<DatabaseVersionInfo> GetAvailableVersions(DatabaseEngine engine) =>
        AvailableVersionsByEngine.Value.TryGetValue(engine, out var versions)
            ? versions
            : throw new ArgumentOutOfRangeException(nameof(engine));

    public static void EnsureAvailableVersionsLoaded() => _ = AvailableVersionsByEngine.Value;

    public static DatabaseVersionInfo GetDefaultVersion(DatabaseEngine engine)
    {
        var versions = GetAvailableVersions(engine);
        return versions[0]; // first entry = newest / recommended
    }

    public static DatabaseVersionInfo? FindVersion(DatabaseEngine engine, string version)
    {
        var normalizedVersion = NormalizeVersionNumber(version);
        var versions = GetAvailableVersions(engine);
        return versions.FirstOrDefault(v => NormalizeVersionNumber(v.Version) == normalizedVersion);
    }

    public static string NormalizeVersionNumber(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var normalized = version.Trim();

        while (normalized.EndsWith(".0", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
        }

        return normalized;
    }

    /// <summary>
    /// Resolves the <see cref="DatabaseVersionInfo"/> for a given instance,
    /// falling back to the engine's default version if no version is stored.
    /// </summary>
    public static DatabaseVersionInfo ResolveVersion(DatabaseInstanceInfo info)
    {
        if (!string.IsNullOrEmpty(info.Version))
        {
            var found = FindVersion(info.Engine, info.Version);
            if (found is not null) return found;

            var normalizedVersion = NormalizeVersionNumber(info.Version);
            if (!string.IsNullOrWhiteSpace(normalizedVersion))
            {
                return new DatabaseVersionInfo
                {
                    Version = normalizedVersion,
                    DisplayName = $"{GetEngineDisplayName(info.Engine)} {normalizedVersion}"
                };
            }
        }

        return GetDefaultVersion(info.Engine);
    }

    private static string GetEngineDisplayName(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.MySQL => "MySQL",
        DatabaseEngine.MariaDB => "MariaDB",
        DatabaseEngine.PostgreSQL => "PostgreSQL",
        _ => engine.ToString()
    };

    public static int GetDefaultPort(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.MySQL => MySqlDefaultPort,
        DatabaseEngine.MariaDB => MariaDbDefaultPort,
        DatabaseEngine.PostgreSQL => PostgreSqlDefaultPort,
        _ => throw new ArgumentOutOfRangeException(nameof(engine))
    };

    private static IReadOnlyDictionary<DatabaseEngine, IReadOnlyList<DatabaseVersionInfo>> LoadAvailableVersions()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "database-versions.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Die Versionskonfiguration wurde nicht gefunden: '{filePath}'.",
                filePath);
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<Dictionary<string, List<DatabaseVersionInfo>?>>(json, JsonOptions);

            if (config is null || config.Count == 0)
            {
                throw new InvalidOperationException("Die Versionskonfiguration ist leer oder ungültig.");
            }

            var versionsByEngine = new Dictionary<DatabaseEngine, IReadOnlyList<DatabaseVersionInfo>>();

            foreach (var engine in Enum.GetValues<DatabaseEngine>())
            {
                var engineKey = engine.ToString();
                if (!config.TryGetValue(engineKey, out List<DatabaseVersionInfo>? versions) || versions is null ||
                    versions.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Für die Datenbank-Engine '{engineKey}' wurden keine Versionen konfiguriert.");
                }

                ValidateVersions(engine, versions);
                versionsByEngine[engine] = new ReadOnlyCollection<DatabaseVersionInfo>(versions);
            }

            return new ReadOnlyDictionary<DatabaseEngine, IReadOnlyList<DatabaseVersionInfo>>(versionsByEngine);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Die Versionskonfiguration konnte nicht gelesen werden.", ex);
        }
    }

    private static void ValidateVersions(DatabaseEngine engine, IEnumerable<DatabaseVersionInfo> versions)
    {
        var seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var version in versions)
        {
            if (string.IsNullOrWhiteSpace(version.Version))
            {
                throw new InvalidOperationException(
                    $"Eine konfigurierte Version für '{engine}' enthält keine Versionsnummer.");
            }

            if (string.IsNullOrWhiteSpace(version.DisplayName))
            {
                throw new InvalidOperationException(
                    $"Die konfigurierte Version '{version.Version}' für '{engine}' enthält keinen Anzeigenamen.");
            }

            if (string.IsNullOrWhiteSpace(version.DownloadUrl) ||
                !Uri.TryCreate(version.DownloadUrl, UriKind.Absolute, out var downloadUri) ||
                (downloadUri.Scheme != Uri.UriSchemeHttp && downloadUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Die konfigurierte Version '{version.Version}' für '{engine}' enthält keine gültige Download-URL.");
            }

            if (string.IsNullOrWhiteSpace(version.ExtractFolder))
            {
                throw new InvalidOperationException(
                    $"Die konfigurierte Version '{version.Version}' für '{engine}' enthält keinen ExtractFolder-Wert.");
            }

            var normalizedVersion = NormalizeVersionNumber(version.Version);
            if (!seenVersions.Add(normalizedVersion))
            {
                throw new InvalidOperationException(
                    $"Die Versionskonfiguration für '{engine}' enthält die Version '{version.Version}' mehrfach.");
            }
        }
    }
}