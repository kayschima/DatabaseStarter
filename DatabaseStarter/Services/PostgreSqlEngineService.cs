using System.IO;
using System.IO.Compression;
using DatabaseStarter.Models;

namespace DatabaseStarter.Services;

public class PostgreSqlEngineService : IDatabaseEngineService
{
    private readonly DownloadService _downloadService;
    private readonly ProcessService _processService;

    public PostgreSqlEngineService(DownloadService downloadService, ProcessService processService)
    {
        _downloadService = downloadService;
        _processService = processService;
    }

    public DatabaseEngine Engine => DatabaseEngine.PostgreSQL;

    public async Task InstallAsync(DatabaseInstanceInfo info, IProgress<double> progress, CancellationToken ct)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), "postgresql.zip");
        var versionInfo = DatabaseDefaults.ResolveVersion(info);

        try
        {
            await _downloadService.DownloadFileAsync(
                versionInfo.DownloadUrl, zipPath, progress, ct);

            Directory.CreateDirectory(info.InstallPath);
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipPath, info.InstallPath, overwriteFiles: true);

                // PostgreSQL extracts to a 'pgsql' subfolder
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
        var pgCtl = Path.Combine(info.InstallPath, "bin", "pg_ctl.exe");
        var initdb = Path.Combine(info.InstallPath, "bin", "initdb.exe");

        if (!File.Exists(initdb) && !File.Exists(pgCtl))
            throw new FileNotFoundException("initdb.exe / pg_ctl.exe nicht gefunden.");

        Directory.CreateDirectory(info.DataDir);

        // Use initdb directly for better control
        var executable = File.Exists(initdb) ? initdb : pgCtl;
        var args = File.Exists(initdb)
            ? $"-D \"{info.DataDir}\" -U postgres -E UTF8 --no-locale"
            : $"initdb -D \"{info.DataDir}\" -o \"-U postgres -E UTF8 --no-locale\"";

        var result = await _processService.RunProcessAsync(executable, args, info.InstallPath);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"PostgreSQL-Initialisierung fehlgeschlagen (Exit {result.ExitCode}):\n{result.Error}\n{result.Output}");

        info.IsInitialized = true;
    }

    public async Task<int> StartAsync(DatabaseInstanceInfo info)
    {
        var pgCtl = Path.Combine(info.InstallPath, "bin", "pg_ctl.exe");
        if (!File.Exists(pgCtl))
            throw new FileNotFoundException("pg_ctl.exe nicht gefunden.", pgCtl);

        var result = await _processService.RunProcessAsync(
            pgCtl,
            $"start -D \"{info.DataDir}\" -o \"-p {info.Port}\" -w",
            info.InstallPath);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"PostgreSQL-Start fehlgeschlagen (Exit {result.ExitCode}):\n{result.Error}\n{result.Output}");

        // Find the postgres process - pg_ctl starts it as a child
        // Read the PID from the postmaster.pid file
        var pidFile = Path.Combine(info.DataDir, "postmaster.pid");
        if (File.Exists(pidFile))
        {
            var lines = await File.ReadAllLinesAsync(pidFile);
            if (lines.Length > 0 && int.TryParse(lines[0].Trim(), out var pid))
            {
                info.ProcessId = pid;
                return pid;
            }
        }

        return 0;
    }

    public async Task StopAsync(DatabaseInstanceInfo info)
    {
        var pgCtl = Path.Combine(info.InstallPath, "bin", "pg_ctl.exe");
        if (File.Exists(pgCtl))
        {
            try
            {
                var result = await _processService.RunProcessAsync(
                    pgCtl,
                    $"stop -D \"{info.DataDir}\" -m fast -w",
                    info.InstallPath);

                if (result.ExitCode == 0)
                {
                    info.ProcessId = 0;
                    return;
                }
            }
            catch
            {
                // Fall through to force kill
            }
        }

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
            !File.Exists(Path.Combine(info.InstallPath, "bin", "pg_ctl.exe")))
            return DatabaseStatus.NotInstalled;

        if (info.ProcessId > 0 && _processService.IsProcessRunning(info.ProcessId))
            return DatabaseStatus.Running;

        // Also check postmaster.pid as PG might have been started externally
        var pidFile = Path.Combine(info.DataDir, "postmaster.pid");
        if (File.Exists(pidFile))
        {
            try
            {
                var lines = File.ReadAllLines(pidFile);
                if (lines.Length > 0 && int.TryParse(lines[0].Trim(), out var pid))
                {
                    if (_processService.IsProcessRunning(pid))
                    {
                        info.ProcessId = pid;
                        return DatabaseStatus.Running;
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        return DatabaseStatus.Installed;
    }
}

