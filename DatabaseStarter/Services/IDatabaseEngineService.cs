using DatabaseStarter.Models;

namespace DatabaseStarter.Services;

public interface IDatabaseEngineService
{
    DatabaseEngine Engine { get; }

    Task InstallAsync(DatabaseInstanceInfo info, IProgress<double> progress, CancellationToken ct);

    Task InitializeAsync(DatabaseInstanceInfo info);

    Task<int> StartAsync(DatabaseInstanceInfo info);

    Task StopAsync(DatabaseInstanceInfo info);

    Task UninstallAsync(DatabaseInstanceInfo info);

    DatabaseStatus GetStatus(DatabaseInstanceInfo info);
}

