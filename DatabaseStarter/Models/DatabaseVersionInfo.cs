namespace DatabaseStarter.Models;

public class DatabaseVersionInfo
{
    public string Version { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string ExtractFolder { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    public override string ToString() => DisplayName;
}

