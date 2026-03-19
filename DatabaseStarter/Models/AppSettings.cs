namespace DatabaseStarter.Models;

public class AppSettings
{
    public string BasePath { get; set; } = string.Empty;
    public List<DatabaseInstanceInfo> Instances { get; set; } = new();
}

