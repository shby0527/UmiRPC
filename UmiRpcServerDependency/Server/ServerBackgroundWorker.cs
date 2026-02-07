using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UmiRpcServerDependency.Server;

public sealed class ServerBackgroundWorker(ILogger<ServerBackgroundWorker> logger, DependencyRpcServer server)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Server started");
        server.Start();
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Server stopped");
        server.Stop();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        base.Dispose();
        server.Dispose();
    }
}