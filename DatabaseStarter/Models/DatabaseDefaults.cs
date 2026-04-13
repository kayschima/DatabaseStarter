namespace DatabaseStarter.Models;

public static class DatabaseDefaults
{
    public const int MySqlDefaultPort = 3306;
    public const int MariaDbDefaultPort = 3307;
    public const int PostgreSqlDefaultPort = 5432;

    // ── Available versions per engine ──────────────────────────────────

    public static readonly IReadOnlyList<DatabaseVersionInfo> MySqlVersions = new List<DatabaseVersionInfo>
    {
        new()
        {
            Version = "9.5.0", DisplayName = "MySQL 9.5.0",
            DownloadUrl = "https://cdn.mysql.com/archives/mysql-9.5/mysql-9.5.0-winx64.zip", ExtractFolder = "mysql"
        },
        new()
        {
            Version = "8.4.7", DisplayName = "MySQL 8.4.7",
            DownloadUrl = "https://cdn.mysql.com/archives/mysql-8.4/mysql-8.4.7-winx64.zip", ExtractFolder = "mysql"
        },
        new()
        {
            Version = "8.0.44", DisplayName = "MySQL 8.0.44",
            DownloadUrl = "https://cdn.mysql.com/archives/mysql-8.0/mysql-8.0.44-winx64.zip", ExtractFolder = "mysql"
        },
    };

    public static readonly IReadOnlyList<DatabaseVersionInfo> MariaDbVersions = new List<DatabaseVersionInfo>
    {
        new()
        {
            Version = "12.2.2", DisplayName = "MariaDB 12.2.2",
            DownloadUrl = "https://archive.mariadb.org/mariadb-12.2.2/winx64-packages/mariadb-12.2.2-winx64.zip",
            ExtractFolder = "mariadb"
        },
        new()
        {
            Version = "11.8.6", DisplayName = "MariaDB 11.8.6",
            DownloadUrl = "https://archive.mariadb.org/mariadb-11.8.6/winx64-packages/mariadb-11.8.6-winx64.zip",
            ExtractFolder = "mariadb"
        },
        new()
        {
            Version = "11.4.10", DisplayName = "MariaDB 11.4.10",
            DownloadUrl = "https://archive.mariadb.org/mariadb-11.4.10/winx64-packages/mariadb-11.4.10-winx64.zip",
            ExtractFolder = "mariadb"
        },
        new()
        {
            Version = "10.11.16", DisplayName = "MariaDB 10.11.16",
            DownloadUrl = "https://archive.mariadb.org/mariadb-10.11.16/winx64-packages/mariadb-10.11.16-winx64.zip",
            ExtractFolder = "mariadb"
        },
    };

    public static readonly IReadOnlyList<DatabaseVersionInfo> PostgreSqlVersions = new List<DatabaseVersionInfo>
    {
        new()
        {
            Version = "18.3", DisplayName = "PostgreSQL 18.3",
            DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-18.3-1-windows-x64-binaries.zip",
            ExtractFolder = "pgsql"
        },
        new()
        {
            Version = "17.9", DisplayName = "PostgreSQL 17.9",
            DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-17.9-1-windows-x64-binaries.zip",
            ExtractFolder = "pgsql"
        },
        new()
        {
            Version = "16.13", DisplayName = "PostgreSQL 16.13",
            DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-16.13-1-windows-x64-binaries.zip",
            ExtractFolder = "pgsql"
        },
        new()
        {
            Version = "16.3", DisplayName = "PostgreSQL 16.3",
            DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-16.3-1-windows-x64-binaries.zip",
            ExtractFolder = "pgsql"
        },
        new()
        {
            Version = "15.17", DisplayName = "PostgreSQL 15.17",
            DownloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-15.17-1-windows-x64-binaries.zip",
            ExtractFolder = "pgsql"
        },
    };

    // ── Helper methods ─────────────────────────────────────────────────

    public static IReadOnlyList<DatabaseVersionInfo> GetAvailableVersions(DatabaseEngine engine) => engine switch
    {
        DatabaseEngine.MySQL => MySqlVersions,
        DatabaseEngine.MariaDB => MariaDbVersions,
        DatabaseEngine.PostgreSQL => PostgreSqlVersions,
        _ => throw new ArgumentOutOfRangeException(nameof(engine))
    };

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
}