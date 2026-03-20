using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DatabaseStarter.Models;
using DatabaseStarter.Services;

namespace DatabaseStarter.ViewModels;

public class DatabaseViewModel : ViewModelBase
{
    private readonly AppSettings _appSettings;
    private readonly IDatabaseEngineService _engineService;
    private readonly SettingsService _settingsService;
    private double _downloadProgress;
    private bool _isBusy;
    private string _logOutput = string.Empty;
    private DatabaseVersionInfo _selectedVersion;
    private DatabaseStatus _status;
    private string _statusMessage = string.Empty;

    public DatabaseViewModel(
        DatabaseInstanceInfo instanceInfo,
        IDatabaseEngineService engineService,
        SettingsService settingsService,
        AppSettings appSettings)
    {
        InstanceInfo = instanceInfo;
        _engineService = engineService;
        _settingsService = settingsService;
        _appSettings = appSettings;

        // Populate available versions
        AvailableVersions = new ObservableCollection<DatabaseVersionInfo>(
            DatabaseDefaults.GetAvailableVersions(instanceInfo.Engine));

        // Resolve current version
        _selectedVersion = DatabaseDefaults.ResolveVersion(instanceInfo);

        InstallCommand = new AsyncRelayCommand(InstallAsync,
            () => !IsBusy && Status == DatabaseStatus.NotInstalled);
        StartCommand = new AsyncRelayCommand(StartAsync,
            () => !IsBusy && Status == DatabaseStatus.Installed);
        StopCommand = new AsyncRelayCommand(StopAsync,
            () => !IsBusy && Status == DatabaseStatus.Running);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync,
            () => !IsBusy && Status == DatabaseStatus.Installed);

        // Initial status
        RefreshStatus();
    }

    public DatabaseInstanceInfo InstanceInfo { get; }

    public string EngineName => InstanceInfo.Engine switch
    {
        DatabaseEngine.MySQL => "MySQL",
        DatabaseEngine.MariaDB => "MariaDB",
        DatabaseEngine.PostgreSQL => "PostgreSQL",
        _ => InstanceInfo.Engine.ToString()
    };

    public string EngineIcon => InstanceInfo.Engine switch
    {
        DatabaseEngine.MySQL => "🐬",
        DatabaseEngine.MariaDB => "🦭",
        DatabaseEngine.PostgreSQL => "🐘",
        _ => "🗄️"
    };

    public ObservableCollection<DatabaseVersionInfo> AvailableVersions { get; }

    public DatabaseVersionInfo SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (_selectedVersion != value && value is not null)
            {
                _selectedVersion = value;
                InstanceInfo.Version = value.Version;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VersionDisplayName));
                SaveSettings();
            }
        }
    }

    public string VersionDisplayName => _selectedVersion.DisplayName;

    /// <summary>Version can only be changed when not yet installed.</summary>
    public bool CanChangeVersion => Status == DatabaseStatus.NotInstalled && !IsBusy;

    public DatabaseStatus Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(CanChangeVersion));
            }
        }
    }

    public string StatusText => Status switch
    {
        DatabaseStatus.NotInstalled => "Nicht installiert",
        DatabaseStatus.Installed => "Installiert (gestoppt)",
        DatabaseStatus.Running => $"Läuft auf Port {InstanceInfo.Port}",
        _ => "Unbekannt"
    };

    public bool IsInstalled => Status != DatabaseStatus.NotInstalled;
    public bool IsRunning => Status == DatabaseStatus.Running;

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetField(ref _downloadProgress, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanChangeVersion));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string LogOutput
    {
        get => _logOutput;
        set => SetField(ref _logOutput, value);
    }

    public int Port
    {
        get => InstanceInfo.Port;
        set
        {
            if (InstanceInfo.Port != value)
            {
                InstanceInfo.Port = value;
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    public string InstallPath => InstanceInfo.InstallPath;

    public ICommand InstallCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand UninstallCommand { get; }

    private async Task InstallAsync()
    {
        IsBusy = true;
        StatusMessage = "Wird heruntergeladen...";
        DownloadProgress = 0;

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
                StatusMessage = p < 100
                    ? $"Herunterladen... {p:F1}%"
                    : "Wird entpackt...";
            });

            await _engineService.InstallAsync(InstanceInfo, progress, CancellationToken.None);

            StatusMessage = "Wird initialisiert...";
            if (!InstanceInfo.IsInitialized)
            {
                await _engineService.InitializeAsync(InstanceInfo);
            }

            StatusMessage = "Installation abgeschlossen!";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {EngineName} erfolgreich installiert.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] FEHLER bei Installation: {ex.Message}");
            MessageBox.Show($"Installation fehlgeschlagen:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            DownloadProgress = 0;
            RefreshStatus();
            SaveSettings();
        }
    }

    private async Task StartAsync()
    {
        IsBusy = true;
        StatusMessage = "Wird gestartet...";

        try
        {
            if (!InstanceInfo.IsInitialized)
            {
                StatusMessage = "Wird initialisiert...";
                await _engineService.InitializeAsync(InstanceInfo);
            }

            var pid = await _engineService.StartAsync(InstanceInfo);

            // Give the server time to start up
            await Task.Delay(2000);

            StatusMessage = $"Läuft (PID: {pid})";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {EngineName} gestartet (PID: {pid}, Port: {InstanceInfo.Port}).");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] FEHLER beim Start: {ex.Message}");
            MessageBox.Show($"Start fehlgeschlagen:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            RefreshStatus();
            SaveSettings();
        }
    }

    private async Task StopAsync()
    {
        IsBusy = true;
        StatusMessage = "Wird gestoppt...";

        try
        {
            await _engineService.StopAsync(InstanceInfo);
            StatusMessage = "Gestoppt.";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {EngineName} gestoppt.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] FEHLER beim Stoppen: {ex.Message}");
            MessageBox.Show($"Stoppen fehlgeschlagen:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            RefreshStatus();
            SaveSettings();
        }
    }

    private async Task UninstallAsync()
    {
        var result = MessageBox.Show(
            $"Möchten Sie {EngineName} wirklich deinstallieren?\nAlle Daten werden gelöscht!",
            "Deinstallation bestätigen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusMessage = "Wird deinstalliert...";

        try
        {
            await _engineService.UninstallAsync(InstanceInfo);
            StatusMessage = "Deinstalliert.";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {EngineName} deinstalliert.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] FEHLER bei Deinstallation: {ex.Message}");
            MessageBox.Show($"Deinstallation fehlgeschlagen:\n{ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            RefreshStatus();
            SaveSettings();
        }
    }

    public void RefreshStatus()
    {
        Status = _engineService.GetStatus(InstanceInfo);
    }

    private void SaveSettings()
    {
        try
        {
            _settingsService.Save(_appSettings);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private void AppendLog(string message)
    {
        LogOutput = string.IsNullOrEmpty(LogOutput) ? message : $"{LogOutput}\n{message}";
    }
}