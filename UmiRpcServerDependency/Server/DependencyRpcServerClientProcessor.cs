using System.Collections.Immutable;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umi.Rpc.Server;
using Umi.Rpc.Server.Client;
using Umi.Rpc.Server.Executors;

namespace UmiRpcServerDependency.Server;

public sealed class DependencyRpcServerClientProcessor(
    Socket socket,
    IServiceScope scope,
    ILogger<DependencyRpcServerClientProcessor> logger)
    : UmiRpcClientProcessor(socket)
{
    private readonly IServiceProvider _provider = scope.ServiceProvider;

    protected override IServiceFactory ServiceFactory { get; } =
        scope.ServiceProvider.GetRequiredService<IServiceFactory>();

    protected override IReadOnlyDictionary<uint, IServerExecutor> RegisterExtensionsExecutors()
    {
        return ImmutableDictionary<uint, IServerExecutor>.Empty;
    }


    public override void Dispose()
    {
        base.Dispose();
        scope.Dispose();
    }
}