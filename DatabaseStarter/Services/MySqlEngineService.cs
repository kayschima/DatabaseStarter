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

                // Dynamically find the version-specific subfolder (e.g. "mysql-8.0.44-winx64")
                // so that binaries always end up directly in InstallPath (e.g. ...\mysql\bin\...)
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

    /// <summary>
    /// Finds the extracted subfolder inside <paramref name="installPath"/>.
    /// First tries the explicit <paramref name="expectedFolder"/>, then scans for any
    /// directory starting with "mysql-" that contains a "bin" folder.
    /// </summary>
    private static string? FindExtractedSubFolder(string installPath, string expectedFolder)
    {
        // 1. Try the explicitly configured folder name
        var explicit1 = Path.Combine(installPath, expectedFolder);
        if (Directory.Exists(explicit1))
            return explicit1;

        // 2. Scan for any mysql-* subfolder that looks like an extracted archive
        var candidates = Directory.GetDirectories(installPath, "mysql-*");
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(Path.Combine(candidate, "bin")))
                return candidate;
        }

        // 3. If there's exactly one subfolder (and no bin/ at top level yet), use it
        var allDirs = Directory.GetDirectories(installPath);
        if (allDirs.Length == 1 && !Directory.Exists(Path.Combine(installPath, "bin")))
            return allDirs[0];

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