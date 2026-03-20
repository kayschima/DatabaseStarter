using System.Diagnostics;

namespace DatabaseStarter.Services;

public class ProcessService
{
    /// <summary>
    /// Starts a process and returns the Process object (for background servers).
    /// </summary>
    public Process StartProcess(string fileName, string arguments, string workingDirectory,
        bool createNoWindow = true, bool redirectOutput = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = createNoWindow,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput
        };

        var process = Process.Start(psi)
                      ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        return process;
    }

    /// <summary>
    /// Runs a process and waits for it to complete, returning stdout.
    /// </summary>
    public async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return (process.ExitCode, output, error);
    }

    /// <summary>
    /// Runs a process WITHOUT redirecting stdout/stderr and waits for it to exit.
    /// Use this when the process forks a long-running child (e.g. pg_ctl start)
    /// to avoid deadlocking on inherited pipe handles.
    /// </summary>
    public async Task<int> RunProcessNoRedirectAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }

    /// <summary>
    /// Checks whether a process with a given PID is still running.
    /// </summary>
    public bool IsProcessRunning(int processId)
    {
        if (processId <= 0) return false;
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kills a process by PID.
    /// </summary>
    public void KillProcess(int processId)
    {
        if (processId <= 0) return;
        try
        {
            var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // Process may have already exited
        }
    }
}