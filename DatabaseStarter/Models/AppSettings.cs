namespace DatabaseStarter.Models;

public class AppSettings
{
    public string BasePath { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public List<DatabaseInstanceInfo> Instances { get; set; } = new();
}