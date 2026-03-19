using System.IO;
using System.IO.Compression;
using DatabaseStarter.Models;

namespace DatabaseStarter.Services;

public class MySqlEngineService : IDatabaseEngineService
{
    private readonly DownloadService _downloadService;
    private readonly ProcessService _processService;

    public MySqlEngineService(DownloadService downloadService, ProcessService processService)
    {
        _downloadService = downloadService;
        _processService = processService;
    }

    public DatabaseEngine Engine => DatabaseEngine.MySQL;

    public async Task InstallAsync(DatabaseInstanceInfo info, IProgress<double> progress, CancellationToken ct)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), "mysql.zip");
        var versionInfo = DatabaseDefaults.ResolveVersion(info);

        try
        {
            // Download
            await _downloadService.DownloadFileAsync(
                versionInfo.DownloadUrl, zipPath, progress, ct);

            // Extract
            Directory.CreateDirectory(info.InstallPath);
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipPath, info.InstallPath, overwriteFiles: true);

                // Move contents from subfolder to install path
                var subFolder = Path.Combine(info.InstallPath, versionInfo.ExtractFolder);
                if (Directory.Exists(subFolder))
                {
                    foreach (var dir in Directory.GetDirectories(subFolder))
                    {
                        var dest = Path.Combine(info.InstallPath, Path.GetFileName(dir));
                        if (Directory.Exists(dest)) Directory.Delete(dest, true);
                        Directory.Move(dir, dest);
                    }

                    foreach (var file in Directory.GetFiles(subFolder))
                    {
                        var dest = Path.Combine(info.InstallPath, Path.GetFileName(file));
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(file, dest);
                    }

                    Directory.Delete(subFolder, true);
                }
            }, ct);
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    public async Task InitializeAsync(DatabaseInstanceInfo info)
    {
        var mysqld = Path.Combine(info.InstallPath, "bin", "mysqld.exe");
        if (!File.Exists(mysqld))
            throw new FileNotFoundException("mysqld.exe nicht gefunden.", mysqld);

        Directory.CreateDirectory(info.DataDir);

        var result = await _processService.RunProcessAsync(
            mysqld,
            $"--initialize-insecure --basedir=\"{info.InstallPath}\" --datadir=\"{info.DataDir}\"",
            info.InstallPath);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"MySQL-Initialisierung fehlgeschlagen (Exit {result.ExitCode}):\n{result.Error}");

        info.IsInitialized = true;
    }

    public Task<int> StartAsync(DatabaseInstanceInfo info)
    {
        var mysqld = Path.Combine(info.InstallPath, "bin", "mysqld.exe");
        if (!File.Exists(mysqld))
            throw new FileNotFoundException("mysqld.exe nicht gefunden.", mysqld);

        var process = _processService.StartProcess(
            mysqld,
            $"--console --basedir=\"{info.InstallPath}\" --datadir=\"{info.DataDir}\" --port={info.Port}",
            info.InstallPath);

        info.ProcessId = process.Id;
        return Task.FromResult(process.Id);
    }

    public async Task StopAsync(DatabaseInstanceInfo info)
    {
        // Try graceful shutdown with mysqladmin first
        var mysqladmin = Path.Combine(info.InstallPath, "bin", "mysqladmin.exe");
        if (File.Exists(mysqladmin))
        {
            try
            {
                var result = await _processService.RunProcessAsync(
                    mysqladmin,
                    $"-u root --port={info.Port} shutdown",
                    info.InstallPath);

                if (result.ExitCode == 0)
                {
                    // Wait a moment for process to fully exit
                    await Task.Delay(2000);
                    info.ProcessId = 0;
                    return;
                }
            }
            catch
            {
                // Fall through to force kill
            }
        }

        // Force kill
        _processService.KillProcess(info.ProcessId);
        info.ProcessId = 0;
    }

    public async Task UninstallAsync(DatabaseInstanceInfo info)
    {
        if (GetStatus(info) == DatabaseStatus.Running)
            await StopAsync(info);

        if (Directory.Exists(info.InstallPath))
            Directory.Delete(info.InstallPath, true);

        info.IsInitialized = false;
        info.ProcessId = 0;
    }

    public DatabaseStatus GetStatus(DatabaseInstanceInfo info)
    {
        if (!Directory.Exists(info.InstallPath) ||
            !File.Exists(Path.Combine(info.InstallPath, "bin", "mysqld.exe")))
            return DatabaseStatus.NotInstalled;

        if (info.ProcessId > 0 && _processService.IsProcessRunning(info.ProcessId))
            return DatabaseStatus.Running;

        return DatabaseStatus.Installed;
    }
}

