namespace DatabaseStarter.Models;

public class DatabaseInstanceInfo
{
    public DatabaseEngine Engine { get; set; }
    public string Version { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public int Port { get; set; }
    public string DataDir { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public bool IsInitialized { get; set; }
}

