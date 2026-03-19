using System.IO;
using System.IO.Compression;
using DatabaseStarter.Models;

namespace DatabaseStarter.Services;

public class MariaDbEngineService : IDatabaseEngineService
{
    private readonly DownloadService _downloadService;
    private readonly ProcessService _processService;

    public MariaDbEngineService(DownloadService downloadService, ProcessService processService)
    {
        _downloadService = downloadService;
        _processService = processService;
    }

    public DatabaseEngine Engine => DatabaseEngine.MariaDB;

    public async Task InstallAsync(DatabaseInstanceInfo info, IProgress<double> progress, CancellationToken ct)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), "mariadb.zip");
        var versionInfo = DatabaseDefaults.ResolveVersion(info);

        try
        {
            await _downloadService.DownloadFileAsync(
                versionInfo.DownloadUrl, zipPath, progress, ct);

            Directory.CreateDirectory(info.InstallPath);
            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(zipPath, info.InstallPath, overwriteFiles: true);

                // Dynamically find the version-specific subfolder (e.g. "mariadb-11.8.6-winx64")
                // so that binaries always end up directly in InstallPath (e.g. ...\MariaDB\bin\...)
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

    /// <summary>
    /// Finds the extracted subfolder inside <paramref name="installPath"/>.
    /// First tries the explicit <paramref name="expectedFolder"/>, then scans for any
    /// directory starting with "mariadb-" that contains a "bin" folder.
    /// </summary>
    private static string? FindExtractedSubFolder(string installPath, string expectedFolder)
    {
        // 1. Try the explicitly configured folder name
        var explicit1 = Path.Combine(installPath, expectedFolder);
        if (Directory.Exists(explicit1))
            return explicit1;

        // 2. Scan for any mariadb-* subfolder that looks like an extracted archive
        var candidates = Directory.GetDirectories(installPath, "mariadb-*");
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

    public async Task InitializeAsync(DatabaseInstanceInfo info)
    {
        // MariaDB uses mysql_install_db.exe for initialization
        var installDb = Path.Combine(info.InstallPath, "bin", "mysql_install_db.exe");
        if (!File.Exists(installDb))
        {
            // Fallback: try mariadb-install-db.exe
            installDb = Path.Combine(info.InstallPath, "bin", "mariadb-install-db.exe");
        }

        if (!File.Exists(installDb))
            throw new FileNotFoundException("mysql_install_db.exe nicht gefunden.", installDb);

        Directory.CreateDirectory(info.DataDir);

        var result = await _processService.RunProcessAsync(
            installDb,
            $"--datadir=\"{info.DataDir}\" --password=\"\"",
            info.InstallPath);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"MariaDB-Initialisierung fehlgeschlagen (Exit {result.ExitCode}):\n{result.Error}\n{result.Output}");

        info.IsInitialized = true;
    }

    public Task<int> StartAsync(DatabaseInstanceInfo info)
    {
        var mysqld = Path.Combine(info.InstallPath, "bin", "mysqld.exe");
        if (!File.Exists(mysqld))
        {
            mysqld = Path.Combine(info.InstallPath, "bin", "mariadbd.exe");
        }

        if (!File.Exists(mysqld))
            throw new FileNotFoundException("mysqld.exe / mariadbd.exe nicht gefunden.", mysqld);

        var process = _processService.StartProcess(
            mysqld,
            $"--console --basedir=\"{info.InstallPath}\" --datadir=\"{info.DataDir}\" --port={info.Port}",
            info.InstallPath);

        info.ProcessId = process.Id;
        return Task.FromResult(process.Id);
    }

    public async Task StopAsync(DatabaseInstanceInfo info)
    {
        var mysqladmin = Path.Combine(info.InstallPath, "bin", "mysqladmin.exe");
        if (!File.Exists(mysqladmin))
        {
            mysqladmin = Path.Combine(info.InstallPath, "bin", "mariadb-admin.exe");
        }

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
        if (!Directory.Exists(info.InstallPath))
            return DatabaseStatus.NotInstalled;

        var hasBinary = File.Exists(Path.Combine(info.InstallPath, "bin", "mysqld.exe")) ||
                        File.Exists(Path.Combine(info.InstallPath, "bin", "mariadbd.exe"));

        if (!hasBinary) return DatabaseStatus.NotInstalled;

        if (info.ProcessId > 0 && _processService.IsProcessRunning(info.ProcessId))
            return DatabaseStatus.Running;

        return DatabaseStatus.Installed;
    }
}

