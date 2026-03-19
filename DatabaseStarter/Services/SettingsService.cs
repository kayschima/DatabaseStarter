using System.IO;
using System.Text.Json;
using DatabaseStarter.Models;

namespace DatabaseStarter.Services;

public class SettingsService
{
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
            var defaults = CreateDefaults();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefaults();
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static AppSettings CreateDefaults()
    {
        var basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DatabaseStarter");

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
}

