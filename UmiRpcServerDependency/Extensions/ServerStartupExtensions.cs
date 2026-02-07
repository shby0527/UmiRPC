using Microsoft.Extensions.DependencyInjection;
using Umi.Rpc.Server.DependencyInjections.Injections;
using UmiRpcServerDependency.Server;

namespace Umi.Rpc.Server.DependencyInjections.Extensions;

public static class ServerStartupExtensions
{
    public static IRpcServerConfigure AddRpcServer(this IServiceCollection collection)
    {
        collection.AddHostedService<ServerBackgroundWorker>();
        collection.AddSingleton<DependencyRpcServer>();
        var configure = new InternalServerConfigure(collection);
        collection.AddSingleton<IRpcServerMetadata>(_ => configure.Build());
        return configure;
    }
}