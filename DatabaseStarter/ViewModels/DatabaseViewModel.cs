using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DatabaseStarter.Models;
using DatabaseStarter.Resources;
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
        InstanceInfo.Version = _selectedVersion.Version;

        if (AvailableVersions.All(v => v.Version != _selectedVersion.Version))
        {
            AvailableVersions.Insert(0, _selectedVersion);
        }

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
            if (_selectedVersion != value)
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

    public bool IsInstalledAndInitialized =>
        (Status is DatabaseStatus.Installed or DatabaseStatus.Running) && InstanceInfo.IsInitialized;

    public bool ShowVersionSelector => !IsInstalledAndInitialized;

    public bool ShowInstalledVersionText => IsInstalledAndInitialized;

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
                OnPropertyChanged(nameof(IsInstalledAndInitialized));
                OnPropertyChanged(nameof(ShowVersionSelector));
                OnPropertyChanged(nameof(ShowInstalledVersionText));
                OnPropertyChanged(nameof(CanChangeVersion));
            }
        }
    }

    public string StatusText => Status switch
    {
        DatabaseStatus.NotInstalled => Strings.StatusNotInstalled,
        DatabaseStatus.Installing => DownloadProgress > 0 && DownloadProgress < 100
            ? string.Format(Strings.StatusInstallingProgress, DownloadProgress)
            : Strings.StatusInstalling,
        DatabaseStatus.Installed => Strings.StatusInstalled,
        DatabaseStatus.Running => string.Format(Strings.StatusRunning, InstanceInfo.Port),
        _ => Strings.StatusUnknown
    };

    public bool IsInstalled => Status is DatabaseStatus.Installed or DatabaseStatus.Running;
    public bool IsRunning => Status == DatabaseStatus.Running;

    public double DownloadProgress
    {
        get => _downloadProgress;
        set
        {
            if (SetField(ref _downloadProgress, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(ShowVersionSelector));
                OnPropertyChanged(nameof(ShowInstalledVersionText));
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
        Status = DatabaseStatus.Installing;
        StatusMessage = Strings.MessageDownloading;
        DownloadProgress = 0;

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
                StatusMessage = p < 100
                    ? string.Format(Strings.MessageDownloadingProgress, p)
                    : Strings.MessageExtracting;
            });

            await _engineService.InstallAsync(InstanceInfo, progress, CancellationToken.None);

            StatusMessage = Strings.MessageInitializing;
            if (!InstanceInfo.IsInitialized)
            {
                await _engineService.InitializeAsync(InstanceInfo);
            }

            StatusMessage = Strings.MessageInstallComplete;
            AppendLog(string.Format(Strings.LogInstallSuccess, DateTime.Now, EngineName));
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Strings.TitleError}: {ex.Message}";
            AppendLog(string.Format(Strings.LogInstallError, DateTime.Now, ex.Message));
            MessageBox.Show(string.Format(Strings.MessageInstallFailed, ex.Message),
                Strings.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
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
        StatusMessage = Strings.MessageStarting;

        try
        {
            if (!InstanceInfo.IsInitialized)
            {
                StatusMessage = Strings.MessageInitializing;
                await _engineService.InitializeAsync(InstanceInfo);
            }

            var pid = await _engineService.StartAsync(InstanceInfo);

            // Give the server time to start up
            await Task.Delay(2000);

            StatusMessage = string.Format(Strings.MessageRunningPid, pid);
            AppendLog(string.Format(Strings.LogStartSuccess, DateTime.Now, EngineName, pid, InstanceInfo.Port));
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Strings.TitleError}: {ex.Message}";
            AppendLog(string.Format(Strings.LogStartError, DateTime.Now, ex.Message));
            MessageBox.Show(string.Format(Strings.MessageStartFailed, ex.Message),
                Strings.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
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
        StatusMessage = Strings.MessageStopping;

        try
        {
            await _engineService.StopAsync(InstanceInfo);
            StatusMessage = Strings.MessageStopped;
            AppendLog(string.Format(Strings.LogStopSuccess, DateTime.Now, EngineName));
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Strings.TitleError}: {ex.Message}";
            AppendLog(string.Format(Strings.LogStopError, DateTime.Now, ex.Message));
            MessageBox.Show(string.Format(Strings.MessageStopFailed, ex.Message),
                Strings.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
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
            string.Format(Strings.ConfirmUninstallMessage, EngineName),
            Strings.ConfirmUninstallTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusMessage = Strings.MessageUninstalling;

        try
        {
            await _engineService.UninstallAsync(InstanceInfo);
            StatusMessage = Strings.MessageUninstalled;
            AppendLog(string.Format(Strings.LogUninstallSuccess, DateTime.Now, EngineName));
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Strings.TitleError}: {ex.Message}";
            AppendLog(string.Format(Strings.LogUninstallError, DateTime.Now, ex.Message));
            MessageBox.Show(string.Format(Strings.MessageUninstallFailed, ex.Message),
                Strings.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
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
        OnPropertyChanged(nameof(IsInstalledAndInitialized));
        OnPropertyChanged(nameof(ShowVersionSelector));
        OnPropertyChanged(nameof(ShowInstalledVersionText));
        OnPropertyChanged(nameof(CanChangeVersion));
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