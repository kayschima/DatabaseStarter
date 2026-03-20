using System.Collections.ObjectModel;
using System.Reflection;
using DatabaseStarter.Models;
using DatabaseStarter.Services;

namespace DatabaseStarter.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly SettingsService _settingsService;

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _appSettings = _settingsService.Load();

        var downloadService = new DownloadService();
        var processService = new ProcessService();

        Databases = new ObservableCollection<DatabaseViewModel>();

        foreach (var instance in _appSettings.Instances)
        {
            IDatabaseEngineService engineService = instance.Engine switch
            {
                DatabaseEngine.MySQL => new MySqlEngineService(downloadService, processService),
                DatabaseEngine.MariaDB => new MariaDbEngineService(downloadService, processService),
                DatabaseEngine.PostgreSQL => new PostgreSqlEngineService(downloadService, processService),
                _ => throw new ArgumentOutOfRangeException()
            };

            Databases.Add(new DatabaseViewModel(instance, engineService, _settingsService, _appSettings));
        }
    }

    public ObservableCollection<DatabaseViewModel> Databases { get; }

    public string Title => "Database Starter – Portable DB Manager";

    public string AppVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                              ?.InformationalVersion
                          ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                          ?? "0.0.1";
            // Strip build metadata (e.g. "+sha..." suffix added by SDK)
            var plusIndex = version.IndexOf('+');
            if (plusIndex >= 0)
                version = version[..plusIndex];
            return version;
        }
    }

    /// <summary>
    /// Stop all running databases (called on application exit).
    /// </summary>
    public async Task StopAllAsync()
    {
        foreach (var db in Databases)
        {
            if (db.Status == DatabaseStatus.Running)
            {
                try
                {
                    db.StopCommand.Execute(null);
                    // Wait a bit for graceful shutdown
                    await Task.Delay(3000);
                }
                catch
                {
                    // Best effort
                }
            }
        }
    }
}