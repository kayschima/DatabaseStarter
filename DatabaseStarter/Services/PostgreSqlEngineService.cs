using System.IO;
using System.IO.Compression;
using DatabaseStarter.Models;
using DatabaseStarter.Resources;

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

            // Extract
            Directory.CreateDirectory(info.InstallPath);
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipPath, info.InstallPath, overwriteFiles: true);

                // PostgreSQL usually extracts to a 'pgsql' subfolder, but we make it robust
                var subFolder = FindExtractedSubFolder(info.InstallPath, versionInfo.ExtractFolder);

                if (subFolder != null && Directory.Exists(subFolder))
                {
                    MoveContentsUp(subFolder, info.InstallPath);
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
            throw new FileNotFoundException(Strings.ErrorInitdbPgctlNotFound);

        Directory.CreateDirectory(info.DataDir);

        // Use initdb directly for better control
        var executable = File.Exists(initdb) ? initdb : pgCtl;
        var args = File.Exists(initdb)
            ? $"-D \"{info.DataDir}\" -U postgres -E UTF8 --no-locale"
            : $"initdb -D \"{info.DataDir}\" -o \"-U postgres -E UTF8 --no-locale\"";

        var result = await _processService.RunProcessAsync(executable, args, info.InstallPath);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                string.Format(Strings.ErrorPostgresInitFailed, result.ExitCode, result.Error, result.Output));

        info.IsInitialized = true;
    }

    public async Task<int> StartAsync(DatabaseInstanceInfo info)
    {
        var pgCtl = Path.Combine(info.InstallPath, "bin", "pg_ctl.exe");
        if (!File.Exists(pgCtl))
            throw new FileNotFoundException(Strings.ErrorPgCtlNotFound, pgCtl);

        // pg_ctl start forks a child postgres process. We must NOT redirect
        // stdout/stderr because the child inherits the pipe handles, which
        // would cause ReadToEndAsync to block forever.
        var exitCode = await _processService.RunProcessNoRedirectAsync(
            pgCtl,
            $"start -D \"{info.DataDir}\" -o \"-p {info.Port}\" -w",
            info.InstallPath);

        if (exitCode != 0)
            throw new InvalidOperationException(
                string.Format(Strings.ErrorPostgresStartFailed, exitCode));

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

    /// <summary>
    /// Finds the extracted subfolder inside <paramref name="installPath"/>.
    /// First tries the explicit <paramref name="expectedFolder"/>, then scans for any
    /// directory containing a "bin" folder.
    /// </summary>
    private static string? FindExtractedSubFolder(string installPath, string expectedFolder)
    {
        // 1. Try the explicitly configured folder name
        var explicit1 = Path.Combine(installPath, expectedFolder);
        if (Directory.Exists(explicit1))
            return explicit1;

        // 2. Scan for any subfolder that looks like an extracted archive (contains bin)
        var candidates = Directory.GetDirectories(installPath);
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(Path.Combine(candidate, "bin")))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Moves all files and directories from <paramref name="source"/> into <paramref name="target"/>.
    /// </summary>
    private static void MoveContentsUp(string source, string target)
    {
        foreach (var dir in Directory.GetDirectories(source))
        {
            var dest = Path.Combine(target, Path.GetFileName(dir));
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.Move(dir, dest);
        }

        foreach (var file in Directory.GetFiles(source))
        {
            var dest = Path.Combine(target, Path.GetFileName(file));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(file, dest);
        }
    }
}