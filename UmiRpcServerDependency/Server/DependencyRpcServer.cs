using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umi.Rpc.Server;
using Umi.Rpc.Server.Client;
using Umi.Rpc.Server.DependencyInjections.Configurations;

namespace UmiRpcServerDependency.Server;

public sealed class DependencyRpcServer(
    ILogger<DependencyRpcServer> logger,
    IOptionsSnapshot<ServerConfiguration> options,
    IServiceProvider provider)
    : UmiRpcServerBase(IPAddress.Parse(options.Value.ListenAddress), options.Value.ListenPort, options.Value.Backport)
{
    protected override UmiRpcClientProcessor CreateClientProcessor(Socket socket)
    {
        logger.LogDebug("starting create client processor");
        return ActivatorUtilities.CreateInstance<DependencyRpcServerClientProcessor>(provider, socket);
    }
}